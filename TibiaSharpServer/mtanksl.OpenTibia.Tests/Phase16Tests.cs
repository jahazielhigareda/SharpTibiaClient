// Phase 16: Login protocol packet-format validation
//
// Fix 1: The server was reading body[0] as the packet type, but the real Tibia 8.60
// client prepends a 4-byte outer Adler32 checksum BEFORE the type byte.
// That caused: [WRN] [Login] Unexpected packet type 0xFB (expected 0x01).
//
// Fix 2 (erroneously added and now reverted): An "inner Adler32" was wrongly inserted
// between the version field and the RSA block. The real Tibia 8.6 client sends NO inner
// Adler32. Presence of that check caused:
//   [WRN] [Login] Inner Adler32 mismatch: got 0x4C2C7993, expected 0x07B93EA7.
//
// Correct wire format — both LOGIN (port 7171) and GAME-LOGIN (port 7172) packets,
// body after the 2-byte length prefix (137 bytes total):
//   [0..3]   outer Adler32 — covers body[4..136] (133 bytes)
//   [4]      packet type (0x01 for login, 0x0A for game-login)
//   [5..6]   OS
//   [7..8]   client version
//   [9..136] RSA-encrypted block (128 bytes)   ← no inner Adler32
//
// These tests validate the packet layout in isolation (no live TCP connection).

using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Tests that describe and validate the corrected Tibia 8.60 login and game-login
/// packet formats (single outer Adler32 only; no inner Adler32).
/// </summary>
public class LoginPacketFormatTests
{
    private const byte LoginType    = 0x01;
    private const byte GameType     = 0x0A;
    private const int  BodySize     = 137; // outer_adler32(4)+type(1)+OS(2)+version(2)+RSA(128)
    private const int  PacketSize   = 2 + BodySize; // 2-byte length prefix + body

    // ── helper ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a 137-byte body (no 2-byte length prefix) in the correct Tibia 8.60 format:
    ///   [outer_adler32(4)][type(1)][OS(2)][version(2)][RSA(128)]
    /// The outer Adler32 covers body[4..136] (133 bytes).
    /// </summary>
    private static byte[] BuildBody(byte type, ushort os = 2, ushort version = 860,
                                    byte[]? rsaBlock = null)
    {
        var body = new byte[BodySize];
        int p = 4; // skip outer Adler32 slot

        body[p++] = type;
        body[p++] = (byte)(os      & 0xFF);
        body[p++] = (byte)(os      >> 8);
        body[p++] = (byte)(version & 0xFF);
        body[p++] = (byte)(version >> 8);

        // RSA block occupies body[9..136].
        if (rsaBlock != null)
            Buffer.BlockCopy(rsaBlock, 0, body, 9, Math.Min(rsaBlock.Length, 128));

        // Outer Adler32 covers body[4..136] = 133 bytes.
        uint adler = Adler32.Compute(body, 4, 133);
        body[0] = (byte)(adler         & 0xFF);
        body[1] = (byte)((adler >>  8) & 0xFF);
        body[2] = (byte)((adler >> 16) & 0xFF);
        body[3] = (byte)((adler >> 24) & 0xFF);
        return body;
    }

    // ── size tests ────────────────────────────────────────────────────────────

    [Fact]
    public void Body_HasExpectedSize_137Bytes()
    {
        Assert.Equal(137, BuildBody(LoginType).Length);
    }

    [Fact]
    public void FullPacket_HasExpectedSize_139Bytes()
    {
        Assert.Equal(139, PacketSize);
    }

    // ── packet-type position tests ────────────────────────────────────────────

    [Fact]
    public void LoginPacket_TypeByte_IsAtOffset4()
    {
        Assert.Equal(LoginType, BuildBody(LoginType)[4]);
    }

    [Fact]
    public void GameLoginPacket_TypeByte_IsAtOffset4()
    {
        Assert.Equal(GameType, BuildBody(GameType)[4]);
    }

    // ── OS / version field tests ──────────────────────────────────────────────

    [Fact]
    public void OsField_IsAtBytes5And6()
    {
        byte[] body = BuildBody(LoginType, os: 2);
        Assert.Equal(2, (ushort)(body[5] | (body[6] << 8)));
    }

    [Fact]
    public void VersionField_IsAtBytes7And8()
    {
        byte[] body = BuildBody(LoginType, version: 860);
        Assert.Equal(860, (ushort)(body[7] | (body[8] << 8)));
    }

    // ── RSA block position tests ──────────────────────────────────────────────

    [Fact]
    public void RsaBlock_StartsAtByte9_NoInnerAdler32()
    {
        // body[9] must be the first byte of the RSA block — NOT an inner Adler32.
        byte[] rsa = new byte[128]; rsa[0] = 0xDE; rsa[1] = 0xAD;
        byte[] body = BuildBody(LoginType, rsaBlock: rsa);

        Assert.Equal(0xDE, body[9]);
        Assert.Equal(0xAD, body[10]);
    }

    [Fact]
    public void RsaBlock_OccupiesBytes9Through136()
    {
        Assert.Equal(9 + 128, BodySize); // 137 — confirms RSA ends at body[136]
    }

    // ── outer Adler32 tests ───────────────────────────────────────────────────

    [Fact]
    public void OuterAdler32_IsAtBytes0Through3()
    {
        byte[] body = BuildBody(LoginType);
        uint stored   = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint computed = Adler32.Compute(body, 4, 133);
        Assert.Equal(computed, stored);
    }

    [Fact]
    public void OuterAdler32_CoversTypeOsVersionAndRsa()
    {
        byte[] body = BuildBody(LoginType);
        body[4] ^= 0xFF; // tamper with type byte

        uint stored     = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed = Adler32.Compute(body, 4, 133);

        Assert.NotEqual(stored, recomputed);
    }

    [Fact]
    public void OuterAdler32_CoversRsaBlock()
    {
        byte[] body = BuildBody(LoginType);
        body[9] ^= 0xFF; // tamper with first RSA byte

        uint stored     = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed = Adler32.Compute(body, 4, 133);

        Assert.NotEqual(stored, recomputed);
    }

    [Fact]
    public void OuterAdler32_DoesNotCoverItself()
    {
        byte[] body = BuildBody(LoginType);
        uint before = Adler32.Compute(body, 4, 133);
        body[0] ^= 0xFF; // tamper with checksum bytes
        uint after  = Adler32.Compute(body, 4, 133);
        Assert.Equal(before, after);
    }

    // ── server-side parsing simulation ────────────────────────────────────────

    [Fact]
    public void ServerParsing_LoginPacket_ExtractsAllFields()
    {
        byte[] rsa = new byte[128]; rsa[0] = 0x00; // sentinel
        byte[] body = BuildBody(LoginType, os: 2, version: 860, rsaBlock: rsa);

        int pos = 0;
        uint outerAdler = (uint)(body[pos] | (body[pos+1] << 8) | (body[pos+2] << 16) | (body[pos+3] << 24));
        pos += 4;

        byte packetType = body[pos++];
        ushort os       = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;
        ushort version  = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;

        // RSA block starts immediately — NO inner Adler32.
        byte[] rsaCipher = new byte[128];
        Buffer.BlockCopy(body, pos, rsaCipher, 0, 128);

        uint expectedAdler = Adler32.Compute(body, 4, body.Length - 4);

        Assert.Equal(LoginType, packetType);
        Assert.Equal(2,         os);
        Assert.Equal(860,       version);
        Assert.Equal(expectedAdler, outerAdler);
        Assert.Equal(9, pos);              // RSA starts at body[9]
        Assert.Equal(0x00, rsaCipher[0]);  // sentinel correctly extracted
    }

    [Fact]
    public void ServerParsing_GameLoginPacket_ExtractsAllFields()
    {
        byte[] body = BuildBody(GameType, os: 2, version: 860);

        int pos = 0;
        uint outerAdler = (uint)(body[pos] | (body[pos+1] << 8) | (body[pos+2] << 16) | (body[pos+3] << 24));
        pos += 4;

        byte packetType = body[pos++];
        ushort os       = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;
        ushort version  = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;

        uint expectedAdler = Adler32.Compute(body, 4, body.Length - 4);

        Assert.Equal(GameType,      packetType);
        Assert.Equal(2,             os);
        Assert.Equal(860,           version);
        Assert.Equal(expectedAdler, outerAdler);
        Assert.Equal(9, pos); // RSA starts at body[9] for game-login too
    }

    [Fact]
    public void ServerParsing_TamperedChecksum_Detectable()
    {
        byte[] body = BuildBody(LoginType);
        body[9] ^= 0xFF; // tamper with RSA block

        uint stored     = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed = Adler32.Compute(body, 4, body.Length - 4);

        Assert.NotEqual(stored, recomputed);
    }
}
