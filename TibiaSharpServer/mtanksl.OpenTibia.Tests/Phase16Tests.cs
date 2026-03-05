// Phase 16: Login protocol outer-Adler32 checksum fix
//
// Root cause: Tibia 8.60 clients place a 4-byte Adler32 checksum BEFORE the
// packet-type byte in both the login-server (0x01) and game-server (0x0A)
// handshake packets.  Prior to this fix the server parsed byte [0] as the
// packet type and got the first byte of the checksum (e.g. 0xFB) instead of
// the expected 0x01, causing:
//
//   [WRN] [Login] Unexpected packet type 0xFB (expected 0x01).
//
// Correct wire format for both login and game-login packets:
//   [2B]   packet length (LE)
//   [4B]   outer Adler32  ← covers bytes 4.. (type + OS + version + RSA block)
//   [1B]   packet type    ← byte [4] of the body
//   [2B]   OS
//   [2B]   client version
//   [128B] RSA-encrypted block
//
// These tests validate the packet layout and Adler32 position in isolation,
// without requiring a live TCP connection.

using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Tests that describe and validate the corrected Tibia 8.60 login/game-login
/// packet format (outer Adler32 checksum before the packet-type byte).
/// </summary>
public class LoginPacketFormatTests
{
    private const byte LoginType    = 0x01;
    private const byte GameType     = 0x0A;
    private const int  PayloadSize  = 137; // 4 (adler) + 1 (type) + 2 (OS) + 2 (ver) + 128 (RSA)
    private const int  PacketSize   = 2 + PayloadSize; // 2-byte length prefix + payload

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal login-style body (payload only, no 2-byte length prefix)
    /// in the corrected Tibia 8.60 format:
    ///   [4B outer_adler32][1B type][2B OS][2B version][128B RSA]
    /// The outer Adler32 covers bytes 4..136.
    /// </summary>
    private static byte[] BuildBody(byte type, ushort os = 860, ushort version = 860)
    {
        var body = new byte[PayloadSize];
        int p = 4; // skip Adler32 slot

        body[p++] = type;
        body[p++] = (byte)(os      & 0xFF);
        body[p++] = (byte)(os      >> 8);
        body[p++] = (byte)(version & 0xFF);
        body[p++] = (byte)(version >> 8);
        // RSA block stays zero-filled (bytes 9..136)

        // Compute and store outer Adler32 over bytes 4..136 (133 bytes).
        uint adler = Adler32.Compute(body, 4, 133);
        body[0] = (byte)(adler         & 0xFF);
        body[1] = (byte)((adler >>  8) & 0xFF);
        body[2] = (byte)((adler >> 16) & 0xFF);
        body[3] = (byte)((adler >> 24) & 0xFF);
        return body;
    }

    // ── packet-size tests ─────────────────────────────────────────────────────

    [Fact]
    public void Payload_HasExpectedSize_137Bytes()
    {
        byte[] body = BuildBody(LoginType);
        Assert.Equal(137, body.Length);
    }

    [Fact]
    public void FullPacket_HasExpectedSize_139Bytes()
    {
        // Full wire packet = 2-byte length prefix + 137-byte payload.
        Assert.Equal(139, PacketSize);
    }

    // ── packet-type position tests ────────────────────────────────────────────

    [Fact]
    public void LoginPacket_TypeByte_IsAtOffset4()
    {
        byte[] body = BuildBody(LoginType);

        // Server reads: adler[0..3] → pos 4 → type byte.
        byte type = body[4];

        Assert.Equal(LoginType, type);
    }

    [Fact]
    public void GameLoginPacket_TypeByte_IsAtOffset4()
    {
        byte[] body = BuildBody(GameType);

        byte type = body[4];

        Assert.Equal(GameType, type);
    }

    [Fact]
    public void Byte0_IsNotPacketType_LoginPacket()
    {
        // Prior to the fix, the server mistakenly read body[0] as the type.
        // body[0] is the LSB of the Adler32 checksum and must NOT equal 0x01
        // for a well-formed packet (it's overwhelmingly unlikely to collide).
        // This documents the class of bug that was fixed.
        byte[] body = BuildBody(LoginType);

        // The correct type is at offset 4, NOT offset 0.
        Assert.Equal(LoginType, body[4]);
        // And byte 0 is part of the Adler32, so is generally != type
        // (we cannot assert it's never 0x01 in theory, but we can confirm
        //  the server must read from offset 4, not 0).
        Assert.NotEqual(0, body[0] | body[1] | body[2] | body[3]); // checksum is non-zero for non-trivial input
    }

    // ── Adler32 position and correctness tests ────────────────────────────────

    [Fact]
    public void OuterAdler32_IsAtBytes0Through3()
    {
        byte[] body = BuildBody(LoginType);

        uint storedAdler = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint computed    = Adler32.Compute(body, 4, 133);

        Assert.Equal(computed, storedAdler);
    }

    [Fact]
    public void OuterAdler32_CoversTypeOsVersionAndRsa()
    {
        // Verify that the Adler32 range is [4..136] (133 bytes = type+OS+version+RSA).
        byte[] body = BuildBody(LoginType);

        // Tamper with the type byte (offset 4) — checksum should no longer match.
        byte original = body[4];
        body[4] = (byte)(original ^ 0xFF); // flip bits

        uint storedAdler = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed  = Adler32.Compute(body, 4, 133);

        Assert.NotEqual(storedAdler, recomputed); // tampering is detected
    }

    [Fact]
    public void OuterAdler32_DoesNotCoverAdler32Itself()
    {
        // Modifying the Adler32 bytes (0..3) must not affect the verification,
        // since Adler32.Compute starts at offset 4.
        byte[] body = BuildBody(LoginType);

        uint adlerBeforeTamper = Adler32.Compute(body, 4, 133);

        body[0] = (byte)(body[0] ^ 0xFF); // tamper with checksum byte itself

        uint adlerAfterTamper = Adler32.Compute(body, 4, 133);

        Assert.Equal(adlerBeforeTamper, adlerAfterTamper); // range [4..] is unchanged
    }

    // ── OS / version field position tests ─────────────────────────────────────

    [Fact]
    public void OsField_IsAtBytes5And6()
    {
        const ushort expectedOs = 2; // Linux
        byte[] body = BuildBody(LoginType, os: expectedOs);

        ushort os = (ushort)(body[5] | (body[6] << 8));

        Assert.Equal(expectedOs, os);
    }

    [Fact]
    public void VersionField_IsAtBytes7And8()
    {
        const ushort expectedVersion = 860;
        byte[] body = BuildBody(LoginType, version: expectedVersion);

        ushort version = (ushort)(body[7] | (body[8] << 8));

        Assert.Equal(expectedVersion, version);
    }

    [Fact]
    public void RsaBlock_StartsAtByte9()
    {
        // RSA block occupies bytes 9..136 (128 bytes).
        byte[] rsaMarker = { 0xDE, 0xAD, 0xBE, 0xEF };
        byte[] body = BuildBody(LoginType);
        Buffer.BlockCopy(rsaMarker, 0, body, 9, rsaMarker.Length);

        Assert.Equal(0xDE, body[9]);
        Assert.Equal(0xAD, body[10]);
        Assert.Equal(0xBE, body[11]);
        Assert.Equal(0xEF, body[12]);
    }

    // ── Server-side parsing simulation ───────────────────────────────────────

    [Fact]
    public void ServerParsing_CorrectPacket_ExtractsTypeOsVersion()
    {
        byte[] body = BuildBody(LoginType, os: 2, version: 860);

        // Simulate what the fixed LoginHandler does:
        int pos = 0;
        uint adler = (uint)(body[pos] | (body[pos + 1] << 8) | (body[pos + 2] << 16) | (body[pos + 3] << 24));
        pos += 4;

        byte packetType = body[pos++];
        ushort os       = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;
        ushort version  = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;

        // Verify outer Adler32.
        uint expectedAdler = Adler32.Compute(body, 4, body.Length - 4);

        Assert.Equal(LoginType, packetType);
        Assert.Equal(2,         os);
        Assert.Equal(860,       version);
        Assert.Equal(expectedAdler, adler);
        Assert.Equal(9, pos); // RSA block starts here
    }

    [Fact]
    public void ServerParsing_GameLoginPacket_ExtractsType()
    {
        byte[] body = BuildBody(GameType, os: 2, version: 860);

        int pos = 0;
        pos += 4; // skip outer Adler32

        byte packetType = body[pos++];

        Assert.Equal(GameType, packetType);
    }

    [Fact]
    public void ServerParsing_TamperedChecksum_Detectable()
    {
        byte[] body = BuildBody(LoginType);

        // Tamper with a RSA byte to invalidate the Adler32.
        body[9] ^= 0xFF;

        uint stored    = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed = Adler32.Compute(body, 4, body.Length - 4);

        Assert.NotEqual(stored, recomputed);
    }
}
