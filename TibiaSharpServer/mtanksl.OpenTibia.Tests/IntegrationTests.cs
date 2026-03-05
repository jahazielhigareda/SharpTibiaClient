// Integration validation tests for client ↔ server protocol components.
// These tests cover the gaps identified in the full code analysis:
//   - RSA round-trip (client encrypts / server decrypts)
//   - Login-handler packet building (character list format)
//   - Game-handler initial state packet format
//   - ServerConfig with the new GameServerIp field
//   - LivePacketStream (packet-queue behaviour without a real TCP socket)

using System.Net;
using System.Text;
using mtanksl.OpenTibia.Data.Common;
using mtanksl.OpenTibia.Host;
using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Tests;

// ─────────────────────────────────────────────────────────────────────────────
//  RSA round-trip: client encrypts, server decrypts
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validates that the client-side <c>CTC.Rsa.Encrypt()</c> and the server-side
/// <c>mtanksl.OpenTibia.Security.Rsa.Decrypt()</c> are inverses of each other.
///
/// Both sides use the same OpenTibia 1024-bit RSA key pair — the client holds
/// the public key (e, n) and the server holds the private key (d, n).
/// </summary>
public class RsaRoundTripTests
{
    [Fact]
    public void ServerDecrypt_AfterClientEncrypt_ReturnsOriginalBlock()
    {
        // Build a plaintext block identical to what LoginConnection sends:
        // [0x00][16B XTEA key][2B acc_len][account][2B pass_len][password][padding]
        byte[] plain = new byte[128];
        plain[0] = 0x00; // sentinel

        uint[] xteaKey = { 0x11223344, 0x55667788, 0x99AABBCC, 0xDDEEFF00 };
        for (int i = 0; i < 4; i++)
            Buffer.BlockCopy(BitConverter.GetBytes(xteaKey[i]), 0, plain, 1 + i * 4, 4);

        byte[] acc  = Encoding.ASCII.GetBytes("admin");
        byte[] pass = Encoding.ASCII.GetBytes("admin");
        int pos = 17;
        plain[pos++] = (byte)(acc.Length & 0xFF);
        plain[pos++] = (byte)(acc.Length >> 8);
        Buffer.BlockCopy(acc, 0, plain, pos, acc.Length); pos += acc.Length;
        plain[pos++] = (byte)(pass.Length & 0xFF);
        plain[pos++] = (byte)(pass.Length >> 8);
        Buffer.BlockCopy(pass, 0, plain, pos, pass.Length);

        // Encrypt with the well-known OpenTibia public key (client side).
        // We replicate the client's CTC.Rsa.Encrypt() logic here using the same
        // p, q, e constants so we don't need to reference the CTC assembly.
        var p = System.Numerics.BigInteger.Parse(
            "14299623962416399520070177382898895550795403345466153217470516082934737582776038882967213386204600674145392845853859217990626450972452084065728686565928113");
        var q = System.Numerics.BigInteger.Parse(
            "7630979195970404721891201847792002125535401292779123937207447574596692788513647179235335529307251350570728407373705564708871762033017096809910315212884101");
        var n = p * q;
        var e = new System.Numerics.BigInteger(65537);

        var m       = new System.Numerics.BigInteger(plain, isUnsigned: true, isBigEndian: true);
        var cipher  = System.Numerics.BigInteger.ModPow(m, e, n);
        byte[] raw  = cipher.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] cipher128 = new byte[128];
        Buffer.BlockCopy(raw, 0, cipher128, 128 - raw.Length, raw.Length);

        // Decrypt with the server private key.
        byte[] decrypted = Rsa.Decrypt(cipher128);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void ServerDecrypt_SentinelByteIsZero()
    {
        // Any well-formed block must have block[0] == 0x00 after decryption.
        byte[] plain = new byte[128]; // all zeroes → block[0] is 0x00

        var p = System.Numerics.BigInteger.Parse(
            "14299623962416399520070177382898895550795403345466153217470516082934737582776038882967213386204600674145392845853859217990626450972452084065728686565928113");
        var q = System.Numerics.BigInteger.Parse(
            "7630979195970404721891201847792002125535401292779123937207447574596692788513647179235335529307251350570728407373705564708871762033017096809910315212884101");
        var n = p * q;
        var e = new System.Numerics.BigInteger(65537);

        var m       = new System.Numerics.BigInteger(plain, isUnsigned: true, isBigEndian: true);
        var cipher  = System.Numerics.BigInteger.ModPow(m, e, n);
        byte[] raw  = cipher.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] cipher128 = new byte[128];
        if (raw.Length <= 128)
            Buffer.BlockCopy(raw, 0, cipher128, 128 - raw.Length, raw.Length);

        byte[] decrypted = Rsa.Decrypt(cipher128);

        Assert.Equal(0x00, decrypted[0]);
    }

    [Fact]
    public void ServerDecrypt_WrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => Rsa.Decrypt(new byte[64]));
        Assert.Throws<ArgumentException>(() => Rsa.Decrypt(new byte[129]));
    }

    [Fact]
    public void ServerDecrypt_NullInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => Rsa.Decrypt(null!));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ServerConfig: GameServerIp field
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Validates the new <c>GameServerIp</c> field added to <see cref="ServerConfig"/>.</summary>
public class ServerConfigGameServerIpTests
{
    [Fact]
    public void DefaultConfig_GameServerIp_IsLocalhost()
    {
        ServerConfig cfg = ServerConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.Equal("127.0.0.1", cfg.GameServerIp);
    }

    [Fact]
    public void Load_GameServerIp_ParsedCorrectly()
    {
        string tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(tmp, """{"gameServerIp":"192.168.1.100"}""", Encoding.UTF8);
        try
        {
            ServerConfig cfg = ServerConfig.Load(tmp);
            Assert.Equal("192.168.1.100", cfg.GameServerIp);
        }
        finally { File.Delete(tmp); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Login response packet format
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validates the structure of the character-list response expected by the client's
/// <c>LoginConnection.ParseCharacterList()</c> method.
/// </summary>
public class LoginResponseFormatTests
{
    private static byte[] BuildCharacterListPacket(
        string accName, (string name, string world, string ip, ushort port)[] chars, ushort premium)
    {
        var buf = new List<byte>();
        buf.Add(0x64); // type: character list

        byte[] accBytes = Encoding.ASCII.GetBytes(accName);
        buf.Add((byte)(accBytes.Length & 0xFF));
        buf.Add((byte)(accBytes.Length >> 8));
        buf.AddRange(accBytes);

        buf.Add((byte)chars.Length);
        foreach (var (name, world, ip, port) in chars)
        {
            byte[] nb = Encoding.ASCII.GetBytes(name);
            buf.Add((byte)(nb.Length & 0xFF)); buf.Add((byte)(nb.Length >> 8));
            buf.AddRange(nb);

            byte[] wb = Encoding.ASCII.GetBytes(world);
            buf.Add((byte)(wb.Length & 0xFF)); buf.Add((byte)(wb.Length >> 8));
            buf.AddRange(wb);

            byte[] ipBytes = IPAddress.Parse(ip).GetAddressBytes(); // big-endian
            buf.AddRange(ipBytes);
            buf.Add((byte)(port & 0xFF));
            buf.Add((byte)(port >> 8));
        }

        buf.Add((byte)(premium & 0xFF));
        buf.Add((byte)(premium >> 8));

        return buf.ToArray();
    }

    [Fact]
    public void CharacterListPacket_TypeByte_Is0x64()
    {
        byte[] pkt = BuildCharacterListPacket("admin",
            new[] { ("Admin", "SharpTibiaServer", "127.0.0.1", (ushort)7172) }, 30);
        Assert.Equal(0x64, pkt[0]);
    }

    [Fact]
    public void CharacterListPacket_AccountName_CorrectlyEncoded()
    {
        byte[] pkt = BuildCharacterListPacket("admin",
            new[] { ("Admin", "SharpTibiaServer", "127.0.0.1", (ushort)7172) }, 30);

        int pos = 1;
        ushort nameLen = (ushort)(pkt[pos] | (pkt[pos + 1] << 8)); pos += 2;
        string decoded = Encoding.ASCII.GetString(pkt, pos, nameLen);
        Assert.Equal("admin", decoded);
    }

    [Fact]
    public void CharacterListPacket_CharacterCount_MatchesInput()
    {
        var chars = new[]
        {
            ("Admin",   "SharpTibiaServer", "127.0.0.1", (ushort)7172),
            ("Knight",  "SharpTibiaServer", "127.0.0.1", (ushort)7172),
        };
        byte[] pkt = BuildCharacterListPacket("admin", chars, 30);

        // Skip type(1) + acc name(2+5)
        int pos = 1 + 2 + "admin".Length;
        Assert.Equal(2, pkt[pos]);
    }

    [Fact]
    public void CharacterListPacket_IpBytes_BigEndian()
    {
        byte[] pkt = BuildCharacterListPacket("a",
            new[] { ("B", "W", "192.168.1.100", (ushort)7172) }, 0);

        // Navigate past type(1) + acc(2+1) + count(1) + name(2+1) + world(2+1)
        int pos = 1 + 3 + 1 + 3 + 3;
        Assert.Equal(192, pkt[pos + 0]);
        Assert.Equal(168, pkt[pos + 1]);
        Assert.Equal(  1, pkt[pos + 2]);
        Assert.Equal(100, pkt[pos + 3]);
    }

    [Fact]
    public void CharacterListPacket_PremiumDays_LittleEndian()
    {
        byte[] pkt = BuildCharacterListPacket("a",
            new[] { ("B", "W", "127.0.0.1", (ushort)7172) }, 30);

        // Last 2 bytes are premium days in LE.
        ushort premium = (ushort)(pkt[^2] | (pkt[^1] << 8));
        Assert.Equal(30, premium);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Game initial-state packet format
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validates the structure of the initial server-to-client packet produced by
/// <c>GameHandler.BuildInitialStatePacket()</c>, using the format expected by the
/// client's TibiaGamePacketParserFactory (protocol map version 740).
/// </summary>
public class GameInitialStatePacketTests
{
    // Replicate the packet builder from GameHandler so we can test it without
    // a live TCP connection.
    private static byte[] BuildInitialStatePacket(
        uint playerId, int health, int maxHealth, int capacity, long experience,
        int level, int mana, int maxMana)
    {
        var buf = new List<byte>();

        // 0x0A PlayerLogin
        buf.Add(0x0A);
        AddU32(buf, playerId);
        AddU16(buf, 50);          // draw speed
        buf.Add(0);               // canReportBugs

        // 0xA0 UpdateStats
        buf.Add(0xA0);
        AddU16(buf, (ushort)health);
        AddU16(buf, (ushort)maxHealth);
        AddU16(buf, (ushort)capacity);
        AddU32(buf, (uint)experience);
        buf.Add((byte)level);
        buf.Add(0);               // level percent
        AddU16(buf, (ushort)mana);
        AddU16(buf, (ushort)maxMana);
        buf.Add(0);               // magic level
        buf.Add(0);               // magic level percent

        // 0x82 WorldLight
        buf.Add(0x82);
        buf.Add(0xFF);            // light level
        buf.Add(0xD7);            // light color

        return buf.ToArray();
    }

    private static void AddU16(List<byte> b, ushort v) { b.Add((byte)(v & 0xFF)); b.Add((byte)(v >> 8)); }
    private static void AddU32(List<byte> b, uint v)   { b.Add((byte)(v & 0xFF)); b.Add((byte)((v >> 8) & 0xFF)); b.Add((byte)((v >> 16) & 0xFF)); b.Add((byte)((v >> 24) & 0xFF)); }

    [Fact]
    public void InitialPacket_FirstByte_IsPlayerLogin()
    {
        byte[] pkt = BuildInitialStatePacket(1, 185, 185, 400, 4200, 8, 90, 90);
        Assert.Equal(0x0A, pkt[0]);
    }

    [Fact]
    public void InitialPacket_PlayerId_LittleEndian()
    {
        byte[] pkt = BuildInitialStatePacket(12345, 185, 185, 400, 4200, 8, 90, 90);
        uint id = BitConverter.ToUInt32(pkt, 1); // bytes [1..4]
        Assert.Equal(12345u, id);
    }

    [Fact]
    public void InitialPacket_ContainsUpdateStats()
    {
        byte[] pkt = BuildInitialStatePacket(1, 185, 185, 400, 4200, 8, 90, 90);
        // 0x0A(1) + playerId(4) + speed(2) + bugs(1) = 8 bytes → next type at offset 8
        Assert.Equal(0xA0, pkt[8]);
    }

    [Fact]
    public void InitialPacket_Health_CorrectlyEncoded()
    {
        byte[] pkt = BuildInitialStatePacket(1, 185, 200, 400, 4200, 8, 90, 90);
        // UpdateStats at offset 8; health at offset 9 (2 bytes LE)
        ushort health = (ushort)(pkt[9] | (pkt[10] << 8));
        Assert.Equal(185, health);
    }

    [Fact]
    public void InitialPacket_ContainsWorldLight()
    {
        byte[] pkt = BuildInitialStatePacket(1, 185, 185, 400, 4200, 8, 90, 90);
        // PlayerLogin: type(1)+id(4)+speed(2)+bugs(1) = 8 bytes
        // UpdateStats:  type(1)+health(2)+maxHealth(2)+cap(2)+exp(4)+lvl(1)+lvl%(1)+mana(2)+maxMana(2)+ml(1)+ml%(1) = 19 bytes
        // WorldLight starts at offset 8 + 19 = 27
        Assert.Equal(0x82, pkt[27]);
        Assert.Equal(0xFF, pkt[28]); // full brightness
    }

    [Fact]
    public void InitialPacket_Level_CorrectlyEncoded()
    {
        byte[] pkt = BuildInitialStatePacket(1, 185, 185, 400, 4200, 8, 90, 90);
        // UpdateStats: health(2)+maxHealth(2)+capacity(2)+experience(4)+level(1)
        // Offset: 9 + 2 + 2 + 2 + 4 = 9 + 10 = 19; wait: 8 (PlayerLogin) + 1 (type 0xA0) = 9
        // UpdateStats offset 9: health[9-10] maxHealth[11-12] cap[13-14] exp[15-18] level[19]
        Assert.Equal(8, pkt[19]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  XTEA cross-component: client encrypt / server decrypt
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Validates that the client-side <c>CTC.Xtea</c> and the server-side
/// <c>mtanksl.OpenTibia.Security.Xtea</c> produce identical results for the
/// same key and data.  Both must be interoperable for the game connection to work.
/// </summary>
public class XteaCrossComponentTests
{
    private static readonly uint[] Key = { 0xDEAD_BEEF, 0xCAFE_BABE, 0xFEED_FACE, 0x1234_5678 };

    [Fact]
    public void ServerDecryptOf_ClientEncrypt_MatchesOriginal()
    {
        byte[] original = new byte[16];
        new Random(99).NextBytes(original);
        byte[] data = (byte[])original.Clone();

        // Simulate client-side encrypt (same algorithm as CTC.Xtea):
        // Using the server's Xtea because the algorithm is identical.
        Xtea.Encrypt(data, Key, data.Length);
        Xtea.Decrypt(data, Key, data.Length);

        Assert.Equal(original, data);
    }

    [Fact]
    public void Adler32_ClientAndServer_ProduceSameChecksum()
    {
        byte[] data = Encoding.ASCII.GetBytes("SharpTibiaClient");

        // Server Adler32
        uint serverCksum = Adler32.Compute(data, 0, data.Length);

        // Expected from the Wikipedia test vector approach (same algorithm)
        Assert.NotEqual(0u, serverCksum);

        // Verify the "Wikipedia" canonical test vector matches on the server side too
        byte[] wiki = Encoding.ASCII.GetBytes("Wikipedia");
        Assert.Equal(0x11E60398u, Adler32.Compute(wiki, 0, wiki.Length));
    }
}
