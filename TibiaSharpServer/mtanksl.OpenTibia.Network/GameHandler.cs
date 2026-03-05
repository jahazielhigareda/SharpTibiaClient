using System.Text;
using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data.Common;
using mtanksl.OpenTibia.Game;
using mtanksl.OpenTibia.Game.Common;
using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Network;

/// <summary>
/// Implements the Tibia 8.6 game-server connection on a single accepted TCP connection.
///
/// Protocol flow:
///   1. Client sends game-login packet (unencrypted):
///        [2B len] [1B type=0x0A] [2B OS] [2B version=860] [128B RSA-encrypted block]
///      RSA plaintext: [0x00] [16B XTEA key] [2B name_len] [char_name]
///   2. Server decrypts, loads the player, and sends the initial state packet
///      (unencrypted for the very first message; all subsequent are XTEA-encrypted).
///   3. Server enters a receive loop, decrypting each incoming packet with XTEA
///      and dispatching to the game engine.
/// </summary>
public static class GameHandler
{
    private const ushort ClientVersion = 860;

    /// <summary>
    /// Handles a single game-server connection end-to-end.
    /// </summary>
    public static async Task HandleAsync(
        Connection        conn,
        IPlayerRepository players,
        GameEngine        engine,
        CancellationToken ct = default)
    {
        uint[]? xteaKey = null;
        Player? player  = null;

        try
        {
            // ── Step 1: Read game-login packet (unencrypted) ──────────────────
            byte[]? body = await conn.ReadPacketAsync(ct);
            if (body == null || body.Length < 5)
            {
                Logger.Warning("[Game] Client disconnected before sending game-login.");
                return;
            }

            int pos = 0;
            byte packetType = body[pos++];
            if (packetType != 0x0A)
            {
                Logger.Warning($"[Game] Unexpected game-login type 0x{packetType:X2}.");
                return;
            }

            ushort os      = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;
            ushort version = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;

            if (version != ClientVersion)
            {
                Logger.Warning($"[Game] Client version {version} != expected {ClientVersion}.");
                return;
            }

            if (body.Length - pos < 128)
            {
                Logger.Warning("[Game] Game-login packet too short for RSA block.");
                return;
            }

            byte[] rsaCipher = new byte[128];
            Buffer.BlockCopy(body, pos, rsaCipher, 0, 128);

            // ── Step 2: RSA-decrypt to extract XTEA key + character name ──────
            byte[] plain;
            try   { plain = Rsa.Decrypt(rsaCipher); }
            catch (Exception ex)
            {
                Logger.Warning($"[Game] RSA decrypt failed: {ex.Message}");
                return;
            }

            int rpos = 0;
            if (plain[rpos++] != 0x00)
            {
                Logger.Warning("[Game] RSA plaintext sentinel is not 0x00.");
                return;
            }

            xteaKey = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                xteaKey[i] = BitConverter.ToUInt32(plain, rpos);
                rpos += 4;
            }

            ushort nameLen = (ushort)(plain[rpos] | (plain[rpos + 1] << 8)); rpos += 2;
            string charName = Encoding.ASCII.GetString(plain, rpos, nameLen);

            Logger.Debug($"[Game] Game-login: char='{charName}' os={os} ver={version}");

            // ── Step 3: Load player from game engine ──────────────────────────
            player = await engine.LoginPlayerAsync(charName);
            if (player == null)
            {
                // Character not found — send disconnect (no data, just close)
                Logger.Warning($"[Game] Character '{charName}' not found.");
                return;
            }

            // ── Step 4: Send initial state (unencrypted first packet) ─────────
            byte[] initPacket = BuildInitialStatePacket(player);
            await conn.WritePacketAsync(initPacket, ct);

            Logger.Info($"[Game] Player '{player.Name}' entered the game.");

            // ── Step 5: XTEA-encrypted receive loop ───────────────────────────
            await ReceiveLoopAsync(conn, engine, player, xteaKey, ct);
        }
        catch (OperationCanceledException) { /* server shutting down */ }
        catch (Exception ex)
        {
            Logger.Error($"[Game] Unhandled error for player '{player?.Name ?? "?"}': {ex.Message}");
        }
        finally
        {
            // Ensure the player is removed from the live game state on disconnect.
            if (player != null)
                await engine.LogoutPlayerAsync(player.Id);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Initial state packet builder
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the first (unencrypted) response packet sent after game-login.
    /// Contains PlayerLogin (0x0A), UpdateStats (0xA0) and WorldLight (0x82).
    ///
    /// The client's TibiaGameProtocol (protocol map version 740) expects:
    ///   0x0A  PlayerLogin : u32 playerId, u16 drawSpeed, u8 canReportBugs
    ///   0xA0  UpdateStats : u16 health, u16 maxHealth, u16 capacity,
    ///                       u32 experience, u8 level, u8 levelPercent,
    ///                       u16 mana, u16 maxMana, u8 magicLevel, u8 magicLevelPercent
    ///   0x82  WorldLight  : u8 lightLevel, u8 lightColor
    /// </summary>
    private static byte[] BuildInitialStatePacket(Player player)
    {
        var buf = new List<byte>(64);

        // ── PlayerLogin (0x0A) ────────────────────────────────────────────────
        buf.Add(0x0A);                          // packet type
        AddU32(buf, player.Id);                 // player creature ID
        AddU16(buf, 50);                        // draw speed (beat period ms, e.g. 50)
        buf.Add(0);                             // canReportBugs = false

        // ── UpdateStats (0xA0) ────────────────────────────────────────────────
        buf.Add(0xA0);
        AddU16(buf, (ushort)Math.Clamp(player.Health,    0, ushort.MaxValue));
        AddU16(buf, (ushort)Math.Clamp(player.MaxHealth, 0, ushort.MaxValue));
        AddU16(buf, (ushort)Math.Clamp(player.Capacity,  0, ushort.MaxValue));
        AddU32(buf, (uint)Math.Clamp(player.Experience,  0, uint.MaxValue));
        buf.Add((byte)Math.Clamp(player.Level, 0, 255));
        buf.Add(0);                             // level percent
        AddU16(buf, (ushort)Math.Clamp(player.Mana,    0, ushort.MaxValue));
        AddU16(buf, (ushort)Math.Clamp(player.MaxMana, 0, ushort.MaxValue));
        buf.Add(0);                             // magic level
        buf.Add(0);                             // magic level percent

        // ── WorldLight (0x82) ─────────────────────────────────────────────────
        buf.Add(0x82);
        buf.Add(0xFF);                          // light level (full brightness)
        buf.Add(0xD7);                          // light color (warm white)

        return buf.ToArray();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Receive loop
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task ReceiveLoopAsync(
        Connection        conn,
        GameEngine        engine,
        Player            player,
        uint[]            xteaKey,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            byte[]? data = await conn.ReadPacketAsync(ct);
            if (data == null)
                break; // client disconnected

            // All game packets after the first are XTEA-encrypted.
            if (data.Length % 8 != 0)
            {
                Logger.Warning($"[Game] Received packet with non-multiple-of-8 length {data.Length}; skipping.");
                continue;
            }

            Xtea.Decrypt(data, xteaKey, data.Length);

            // Bytes [0..3]: Adler32 checksum covering bytes [4..].
            if (data.Length < 5)
                continue;

            uint expectedAdler = Adler32.Compute(data, 4, data.Length - 4);
            uint actualAdler   = BitConverter.ToUInt32(data, 0);
            if (actualAdler != expectedAdler)
            {
                Logger.Warning("[Game] Adler32 mismatch on incoming game packet; skipping.");
                continue;
            }

            // Byte [4]: outgoing packet type from client.
            byte cmd = data[4];
            int  dpos = 5;

            await DispatchAsync(conn, engine, player, xteaKey, cmd, data, dpos, ct);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Command dispatch
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task DispatchAsync(
        Connection        conn,
        GameEngine        engine,
        Player            player,
        uint[]            xteaKey,
        byte              cmd,
        byte[]            data,
        int               pos,
        CancellationToken ct)
    {
        switch (cmd)
        {
            // ── Walk commands (0x65–0x68) ─────────────────────────────────────
            // Movement is routed through GameEngine.MovePlayerAsync() so future
            // collision detection and movement script dispatch can be added there
            // without touching this handler.
            case 0x65: // MoveNorth
                await engine.MovePlayerAsync(player,  0, -1);
                break;

            case 0x66: // MoveEast
                await engine.MovePlayerAsync(player, +1,  0);
                break;

            case 0x67: // MoveSouth
                await engine.MovePlayerAsync(player,  0, +1);
                break;

            case 0x68: // MoveWest
                await engine.MovePlayerAsync(player, -1,  0);
                break;

            // ── Say (0x96) ────────────────────────────────────────────────────
            case 0x96:
                if (pos + 2 >= data.Length) break;
                byte msgType = data[pos++];
                // Skip channel ID if it's a channel message type
                if (msgType == 4 || msgType == 5 || msgType == 6 || msgType == 7)
                    pos += 2; // channel ID
                ushort textLen = (ushort)(data[pos] | (data[pos + 1] << 8)); pos += 2;
                if (pos + textLen > data.Length) break;
                string text = System.Text.Encoding.ASCII.GetString(data, pos, textLen);
                Logger.Info($"[Game] {player.Name} says: \"{text}\"");
                await engine.DispatchSpellAsync(player, text);
                break;

            // ── Logout (0x14) ─────────────────────────────────────────────────
            case 0x14:
                Logger.Info($"[Game] {player.Name} logged out gracefully.");
                await engine.LogoutPlayerAsync(player.Id);
                break;

            default:
                Logger.Debug($"[Game] Unhandled command 0x{cmd:X2} from '{player.Name}'.");
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Packet write helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="payload"/> in the Tibia 8.6 game-packet envelope:
    /// [4B Adler32][payload] padded to a multiple of 8, then XTEA-encrypted,
    /// then prepended with a 2-byte length prefix.
    /// </summary>
    public static async Task SendEncryptedAsync(
        Connection        conn,
        byte[]            payload,
        int               length,
        uint[]            xteaKey,
        CancellationToken ct = default)
    {
        // Pad to next multiple of 8 (XTEA block size), accounting for the 4-byte checksum.
        int padded = ((length + 4 + 7) / 8) * 8;
        var block  = new byte[padded];

        uint cksum = Adler32.Compute(payload, 0, length);
        block[0] = (byte)(cksum         & 0xFF);
        block[1] = (byte)((cksum >>  8) & 0xFF);
        block[2] = (byte)((cksum >> 16) & 0xFF);
        block[3] = (byte)((cksum >> 24) & 0xFF);
        Buffer.BlockCopy(payload, 0, block, 4, length);

        Xtea.Encrypt(block, xteaKey, block.Length);
        await conn.WritePacketAsync(block, ct);
    }

    // ── Primitive serialization helpers ──────────────────────────────────────

    private static void AddU16(List<byte> buf, ushort value)
    {
        buf.Add((byte)(value & 0xFF));
        buf.Add((byte)(value >> 8));
    }

    private static void AddU32(List<byte> buf, uint value)
    {
        buf.Add((byte)(value         & 0xFF));
        buf.Add((byte)((value >>  8) & 0xFF));
        buf.Add((byte)((value >> 16) & 0xFF));
        buf.Add((byte)((value >> 24) & 0xFF));
    }
}
