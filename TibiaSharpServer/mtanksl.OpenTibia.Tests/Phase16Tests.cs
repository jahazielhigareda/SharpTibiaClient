// Phase 16: Login protocol packet-format validation
//
// Fix 1 (previous): The server was reading body[0] as the packet type, but the real
// Tibia 8.60 client prepends a 4-byte outer Adler32 checksum BEFORE the type byte.
// That caused: [WRN] [Login] Unexpected packet type 0xFB (expected 0x01).
//
// Fix 2 (this session): After fix 1, the server read body[9] as the start of the
// RSA block. But the real client also embeds a 4-byte inner Adler32 (checksum of
// the RSA-encrypted block) BETWEEN the version field and the RSA block, pushing the
// RSA start to body[13]. Decrypting body[9..136] (inner_adler32 bytes + first 124
// bytes of RSA) produced garbage — hence:
//   [WRN] [Login] RSA plaintext sentinel byte is not 0x00.
//
// Correct wire format — LOGIN packet (port 7171), body after the 2-byte length:
//   [2B]   packet length (LE)   — length prefix, not part of body
//   ─── body (141 bytes) ───────────────────────────────────────────────────────
//   [0..3]   outer Adler32 — covers body[4..140] (137 bytes)
//   [4]      packet type (0x01)
//   [5..6]   OS
//   [7..8]   client version
//   [9..12]  inner Adler32 — covers body[13..140] (128-byte RSA block only)
//   [13..140] RSA-encrypted block (128 bytes)
//
// Correct wire format — GAME-LOGIN packet (port 7172), body after the 2-byte length:
//   [2B]   packet length (LE)   — length prefix, not part of body
//   ─── body (137 bytes) ───────────────────────────────────────────────────────
//   [0..3]   outer Adler32 — covers body[4..136] (133 bytes)
//   [4]      packet type (0x0A)
//   [5..6]   OS
//   [7..8]   client version
//   [9..136] RSA-encrypted block (128 bytes)  ← NO inner Adler32 for game-login
//
// These tests validate the packet layout in isolation (no live TCP connection).

using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Tests that describe and validate the corrected Tibia 8.60 login and game-login
/// packet formats (outer + inner Adler32 for login; outer only for game-login).
/// </summary>
public class LoginPacketFormatTests
{
    private const byte LoginType         = 0x01;
    private const byte GameType          = 0x0A;

    // Login body = outer_adler32(4) + type(1) + OS(2) + version(2) + inner_adler32(4) + RSA(128)
    private const int  LoginBodySize     = 141;
    // Game-login body = outer_adler32(4) + type(1) + OS(2) + version(2) + RSA(128)
    private const int  GameLoginBodySize = 137;

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal LOGIN body (141 bytes, no 2-byte length prefix) in the
    /// corrected Tibia 8.60 format:
    ///   [outer_adler32(4)][type(1)][OS(2)][version(2)][inner_adler32(4)][RSA(128)]
    /// </summary>
    private static byte[] BuildLoginBody(byte type = LoginType, ushort os = 2, ushort version = 860,
                                         byte[]? rsaBlock = null)
    {
        var body = new byte[LoginBodySize];
        int p = 4; // skip outer Adler32 slot

        body[p++] = type;
        body[p++] = (byte)(os      & 0xFF);
        body[p++] = (byte)(os      >> 8);
        body[p++] = (byte)(version & 0xFF);
        body[p++] = (byte)(version >> 8);
        // p is now 9 — inner Adler32 slot (bytes [9..12])
        int innerAdlerOffset = p;
        p += 4; // skip inner Adler32 slot

        // RSA block occupies bytes [13..140]; copy caller-provided block if any.
        if (rsaBlock != null)
            Buffer.BlockCopy(rsaBlock, 0, body, p, Math.Min(rsaBlock.Length, 128));
        // (else stays zero-filled)

        // Inner Adler32 covers RSA block only (body[13..140] = 128 bytes).
        uint inner = Adler32.Compute(body, 13, 128);
        body[innerAdlerOffset    ] = (byte)(inner         & 0xFF);
        body[innerAdlerOffset + 1] = (byte)((inner >>  8) & 0xFF);
        body[innerAdlerOffset + 2] = (byte)((inner >> 16) & 0xFF);
        body[innerAdlerOffset + 3] = (byte)((inner >> 24) & 0xFF);

        // Outer Adler32 covers body[4..140] = 137 bytes (type+OS+version+inner_adler32+RSA).
        uint outer = Adler32.Compute(body, 4, 137);
        body[0] = (byte)(outer         & 0xFF);
        body[1] = (byte)((outer >>  8) & 0xFF);
        body[2] = (byte)((outer >> 16) & 0xFF);
        body[3] = (byte)((outer >> 24) & 0xFF);
        return body;
    }

    /// <summary>
    /// Builds a minimal GAME-LOGIN body (137 bytes, no 2-byte length prefix) in the
    /// corrected Tibia 8.60 format:
    ///   [outer_adler32(4)][type(1)][OS(2)][version(2)][RSA(128)]
    /// Game-login has no inner Adler32.
    /// </summary>
    private static byte[] BuildGameLoginBody(ushort os = 2, ushort version = 860)
    {
        var body = new byte[GameLoginBodySize];
        int p = 4; // skip outer Adler32 slot

        body[p++] = GameType;
        body[p++] = (byte)(os      & 0xFF);
        body[p++] = (byte)(os      >> 8);
        body[p++] = (byte)(version & 0xFF);
        body[p++] = (byte)(version >> 8);
        // RSA block occupies bytes [9..136] — stays zero-filled

        // Outer Adler32 covers body[4..136] = 133 bytes (type+OS+version+RSA).
        uint outer = Adler32.Compute(body, 4, 133);
        body[0] = (byte)(outer         & 0xFF);
        body[1] = (byte)((outer >>  8) & 0xFF);
        body[2] = (byte)((outer >> 16) & 0xFF);
        body[3] = (byte)((outer >> 24) & 0xFF);
        return body;
    }

    // ── Login body-size tests ─────────────────────────────────────────────────

    [Fact]
    public void LoginBody_HasExpectedSize_141Bytes()
    {
        byte[] body = BuildLoginBody();
        Assert.Equal(141, body.Length);
    }

    [Fact]
    public void LoginFullPacket_HasExpectedSize_143Bytes()
    {
        // Full wire packet = 2-byte length prefix + 141-byte body.
        Assert.Equal(143, 2 + LoginBodySize);
    }

    // ── Game-login body-size tests ────────────────────────────────────────────

    [Fact]
    public void GameLoginBody_HasExpectedSize_137Bytes()
    {
        byte[] body = BuildGameLoginBody();
        Assert.Equal(137, body.Length);
    }

    [Fact]
    public void GameLoginFullPacket_HasExpectedSize_139Bytes()
    {
        // Full wire packet = 2-byte length prefix + 137-byte body.
        Assert.Equal(139, 2 + GameLoginBodySize);
    }

    // ── Packet-type position tests ────────────────────────────────────────────

    [Fact]
    public void LoginPacket_TypeByte_IsAtOffset4()
    {
        byte[] body = BuildLoginBody();
        Assert.Equal(LoginType, body[4]);
    }

    [Fact]
    public void GameLoginPacket_TypeByte_IsAtOffset4()
    {
        byte[] body = BuildGameLoginBody();
        Assert.Equal(GameType, body[4]);
    }

    // ── OS / version field position tests ─────────────────────────────────────

    [Fact]
    public void LoginPacket_OsField_IsAtBytes5And6()
    {
        const ushort expectedOs = 2;
        byte[] body = BuildLoginBody(os: expectedOs);
        ushort os = (ushort)(body[5] | (body[6] << 8));
        Assert.Equal(expectedOs, os);
    }

    [Fact]
    public void LoginPacket_VersionField_IsAtBytes7And8()
    {
        const ushort expectedVersion = 860;
        byte[] body = BuildLoginBody(version: expectedVersion);
        ushort version = (ushort)(body[7] | (body[8] << 8));
        Assert.Equal(expectedVersion, version);
    }

    // ── Outer Adler32 tests (both login and game-login) ───────────────────────

    [Fact]
    public void LoginPacket_OuterAdler32_IsAtBytes0Through3()
    {
        byte[] body = BuildLoginBody();
        uint stored   = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint computed = Adler32.Compute(body, 4, 137); // covers 137 bytes = body[4..140]
        Assert.Equal(computed, stored);
    }

    [Fact]
    public void GameLoginPacket_OuterAdler32_IsAtBytes0Through3()
    {
        byte[] body = BuildGameLoginBody();
        uint stored   = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint computed = Adler32.Compute(body, 4, 133); // covers 133 bytes = body[4..136]
        Assert.Equal(computed, stored);
    }

    [Fact]
    public void LoginPacket_OuterAdler32_CoversInnerAdler32AndRsa()
    {
        // Tampering with the inner Adler32 bytes (body[9..12]) must invalidate
        // the outer Adler32, because the outer covers body[4..140] which includes
        // the inner Adler32 field.
        byte[] body = BuildLoginBody();

        body[9] ^= 0xFF; // tamper with first byte of inner Adler32

        uint stored     = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputed = Adler32.Compute(body, 4, 137);

        Assert.NotEqual(stored, recomputed);
    }

    // ── Inner Adler32 tests (login only) ─────────────────────────────────────

    [Fact]
    public void LoginPacket_InnerAdler32_IsAtBytes9Through12()
    {
        byte[] rsa = new byte[128]; rsa[0] = 0xAB; // non-trivial RSA block
        byte[] body = BuildLoginBody(rsaBlock: rsa);

        uint stored   = (uint)(body[9] | (body[10] << 8) | (body[11] << 16) | (body[12] << 24));
        uint computed = Adler32.Compute(body, 13, 128); // covers RSA block = body[13..140]

        Assert.Equal(computed, stored);
    }

    [Fact]
    public void LoginPacket_InnerAdler32_CoversOnlyRsaBlock()
    {
        byte[] body = BuildLoginBody();

        // Tamper with an RSA byte (body[13]) — inner Adler32 should no longer match.
        body[13] ^= 0xFF;

        uint stored     = (uint)(body[9] | (body[10] << 8) | (body[11] << 16) | (body[12] << 24));
        uint recomputed = Adler32.Compute(body, 13, 128);

        Assert.NotEqual(stored, recomputed);
    }

    [Fact]
    public void LoginPacket_InnerAdler32_DoesNotCoverTypeOsVersion()
    {
        // The inner checksum only covers body[13..140] (the RSA block).
        // Modifying the type/OS/version bytes (body[4..8]) must NOT change the
        // inner Adler32 verification result.
        byte[] body = BuildLoginBody();

        uint innerBefore = Adler32.Compute(body, 13, 128);
        body[4] ^= 0xFF; // tamper with type byte
        uint innerAfter  = Adler32.Compute(body, 13, 128);

        Assert.Equal(innerBefore, innerAfter); // inner covers only the RSA block
    }

    // ── RSA block position tests ──────────────────────────────────────────────

    [Fact]
    public void LoginPacket_RsaBlock_StartsAtByte13()
    {
        // In the login packet, RSA occupies body[13..140] (128 bytes).
        // body[9..12] is the inner Adler32, NOT part of the RSA block.
        byte[] rsa = new byte[128];
        rsa[0] = 0xDE; rsa[1] = 0xAD; rsa[2] = 0xBE; rsa[3] = 0xEF;
        byte[] body = BuildLoginBody(rsaBlock: rsa);

        Assert.Equal(0xDE, body[13]);
        Assert.Equal(0xAD, body[14]);
        Assert.Equal(0xBE, body[15]);
        Assert.Equal(0xEF, body[16]);
    }

    [Fact]
    public void GameLoginPacket_RsaBlock_StartsAtByte9()
    {
        // In the game-login packet there is no inner Adler32, so RSA is at body[9..136].
        byte[] body = BuildGameLoginBody();
        // (RSA block is zero-filled; confirm offsets by checking body length)
        Assert.Equal(9 + 128, body.Length); // 137
    }

    [Fact]
    public void LoginPacket_Bytes9Through12_AreInnerAdler32_NotRsa()
    {
        // Key regression test: body[9..12] must be the inner Adler32, not the RSA start.
        // If a server reads body[9] as RSA start it gets 4 wrong bytes + 124 RSA bytes
        // and RSA decryption produces garbage (the sentinel ≠ 0x00 bug).
        byte[] rsa = new byte[128];
        rsa[0] = 0x00; // sentinel
        byte[] body = BuildLoginBody(rsaBlock: rsa);

        // body[9..12] = inner Adler32 (non-RSA)
        uint inner = (uint)(body[9] | (body[10] << 8) | (body[11] << 16) | (body[12] << 24));
        // body[13] = first byte of RSA = 0x00 (sentinel)
        Assert.Equal((byte)0x00, body[13]);
        // The inner Adler32 at [9..12] must verify against RSA block at [13..140].
        Assert.Equal(inner, Adler32.Compute(body, 13, 128));
    }

    // ── Full server-side parsing simulation ───────────────────────────────────

    [Fact]
    public void ServerParsing_LoginPacket_ExtractsAllFields()
    {
        byte[] rsa = new byte[128]; rsa[0] = 0x00; // sentinel byte
        byte[] body = BuildLoginBody(os: 2, version: 860, rsaBlock: rsa);

        // Simulate the fixed LoginHandler parsing:
        int pos = 0;
        uint outerAdler = (uint)(body[pos] | (body[pos+1] << 8) | (body[pos+2] << 16) | (body[pos+3] << 24));
        pos += 4;

        byte packetType = body[pos++];
        ushort os       = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;
        ushort version  = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;

        uint innerAdler = (uint)(body[pos] | (body[pos+1] << 8) | (body[pos+2] << 16) | (body[pos+3] << 24));
        pos += 4;

        byte[] rsaCipher = new byte[128];
        Buffer.BlockCopy(body, pos, rsaCipher, 0, 128);

        // Verify outer Adler32 (covers body[4..140] = 137 bytes).
        uint expectedOuter = Adler32.Compute(body, 4, body.Length - 4);
        // Verify inner Adler32 (covers RSA block = 128 bytes).
        uint expectedInner = Adler32.Compute(rsaCipher, 0, 128);

        Assert.Equal(LoginType, packetType);
        Assert.Equal(2,         os);
        Assert.Equal(860,       version);
        Assert.Equal(expectedOuter, outerAdler);
        Assert.Equal(expectedInner, innerAdler);
        Assert.Equal(13, pos);          // RSA block starts at body[13]
        Assert.Equal(0x00, rsaCipher[0]); // sentinel byte is correctly extracted
    }

    [Fact]
    public void ServerParsing_GameLoginPacket_ExtractsAllFields()
    {
        byte[] body = BuildGameLoginBody(os: 2, version: 860);

        int pos = 0;
        uint outerAdler = (uint)(body[pos] | (body[pos+1] << 8) | (body[pos+2] << 16) | (body[pos+3] << 24));
        pos += 4;

        byte packetType = body[pos++];
        ushort os       = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;
        ushort version  = (ushort)(body[pos] | (body[pos+1] << 8)); pos += 2;

        // Game-login has NO inner Adler32; RSA block starts immediately.
        byte[] rsaCipher = new byte[128];
        Buffer.BlockCopy(body, pos, rsaCipher, 0, 128);

        uint expectedOuter = Adler32.Compute(body, 4, body.Length - 4);

        Assert.Equal(GameType,  packetType);
        Assert.Equal(2,         os);
        Assert.Equal(860,       version);
        Assert.Equal(expectedOuter, outerAdler);
        Assert.Equal(9, pos); // RSA block starts at body[9] for game-login
    }

    [Fact]
    public void ServerParsing_TamperedOuterAdler32_Detectable()
    {
        byte[] body = BuildLoginBody();
        body[13] ^= 0xFF; // tamper with RSA block — invalidates both checksums

        uint storedOuter    = (uint)(body[0] | (body[1] << 8) | (body[2] << 16) | (body[3] << 24));
        uint recomputedOuter = Adler32.Compute(body, 4, body.Length - 4);

        Assert.NotEqual(storedOuter, recomputedOuter);
    }

    [Fact]
    public void ServerParsing_TamperedInnerAdler32_Detectable()
    {
        byte[] body = BuildLoginBody();
        body[13] ^= 0xFF; // tamper with RSA block

        uint storedInner    = (uint)(body[9] | (body[10] << 8) | (body[11] << 16) | (body[12] << 24));
        uint recomputedInner = Adler32.Compute(body, 13, 128);

        Assert.NotEqual(storedInner, recomputedInner);
    }
}
