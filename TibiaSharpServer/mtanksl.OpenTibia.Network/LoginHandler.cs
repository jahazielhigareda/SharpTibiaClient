using System.Net;
using System.Text;
using mtanksl.OpenTibia.Common;
using mtanksl.OpenTibia.Data.Common;
using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Network;

/// <summary>
/// Implements the Tibia 8.6 login-server handshake on a single accepted TCP connection.
///
/// Protocol flow (server → client, then client → server):
///   1. Server sends a 4-byte random challenge.
///   2. Client sends: [2B len] [1B type=0x01] [2B OS] [2B version=860]
///                    [4B Adler32 of RSA block] [128B RSA-encrypted block]
///      RSA plaintext: [0x00] [16B XTEA key] [2B acc_len] [account] [2B pass_len] [password]
///   3. Server validates account + password against the data layer.
///   4a. On success: sends character-list response (type 0x64).
///   4b. On failure: sends error message (type 0x0A).
/// </summary>
public static class LoginHandler
{
    private const ushort ClientVersion = 860;

    /// <summary>
    /// Handles a single login-server connection end-to-end.
    /// </summary>
    /// <param name="conn">Accepted connection (caller disposes).</param>
    /// <param name="accounts">Account repository for credential validation.</param>
    /// <param name="players">Player repository for building the character list.</param>
    /// <param name="serverName">World name to include in each character entry.</param>
    /// <param name="gameServerIp">IP address the client should connect to for the game.</param>
    /// <param name="gameServerPort">Port the client should connect to for the game.</param>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    public static async Task HandleAsync(
        Connection          conn,
        IAccountRepository  accounts,
        IPlayerRepository   players,
        string              serverName,
        string              gameServerIp,
        ushort              gameServerPort,
        CancellationToken   ct = default)
    {
        try
        {
            // ── Step 1: Send 4-byte random challenge ─────────────────────────
            byte[] challenge = new byte[4];
            Random.Shared.NextBytes(challenge);
            await conn.WriteRawAsync(challenge, ct);

            // ── Step 2: Read login packet ─────────────────────────────────────
            byte[]? body = await conn.ReadPacketAsync(ct);
            if (body == null || body.Length < 9)
            {
                Logger.Warning("[Login] Client disconnected before sending login packet.");
                return;
            }

            // Parse packet header (before the RSA block).
            int pos = 0;
            byte packetType = body[pos++];
            if (packetType != 0x01)
            {
                Logger.Warning($"[Login] Unexpected packet type 0x{packetType:X2} (expected 0x01).");
                return;
            }

            ushort os      = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;
            ushort version = (ushort)(body[pos] | (body[pos + 1] << 8)); pos += 2;

            if (version != ClientVersion)
            {
                Logger.Warning($"[Login] Client version {version} != expected {ClientVersion}.");
                await SendErrorAsync(conn, $"You need client version {ClientVersion}.", ct);
                return;
            }

            // Adler32 checksum of the RSA block (we verify it for integrity).
            uint adler = (uint)(body[pos] | (body[pos + 1] << 8) |
                                (body[pos + 2] << 16) | (body[pos + 3] << 24));
            pos += 4;

            if (body.Length - pos < 128)
            {
                Logger.Warning("[Login] Login packet too short for RSA block.");
                return;
            }

            byte[] rsaCipher = new byte[128];
            Buffer.BlockCopy(body, pos, rsaCipher, 0, 128);

            // ── Step 3: Verify Adler32 then RSA-decrypt ───────────────────────
            uint expectedAdler = Adler32.Compute(rsaCipher, 0, 128);
            if (adler != expectedAdler)
            {
                Logger.Warning($"[Login] Adler32 mismatch: got 0x{adler:X8}, expected 0x{expectedAdler:X8}.");
                await SendErrorAsync(conn, "Packet integrity check failed.", ct);
                return;
            }

            byte[] plain;
            try   { plain = Rsa.Decrypt(rsaCipher); }
            catch (Exception ex)
            {
                Logger.Warning($"[Login] RSA decrypt failed: {ex.Message}");
                await SendErrorAsync(conn, "Login failed.", ct);
                return;
            }

            // Parse RSA plaintext: [0x00][16B XTEA key][2B acc_len][account][2B pass_len][password]
            int rpos = 0;
            if (plain[rpos++] != 0x00)
            {
                Logger.Warning("[Login] RSA plaintext sentinel byte is not 0x00.");
                await SendErrorAsync(conn, "Login failed.", ct);
                return;
            }

            uint[] xteaKey = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                xteaKey[i] = BitConverter.ToUInt32(plain, rpos);
                rpos += 4;
            }

            ushort accLen = (ushort)(plain[rpos] | (plain[rpos + 1] << 8)); rpos += 2;
            string account  = Encoding.ASCII.GetString(plain, rpos, accLen); rpos += accLen;

            ushort passLen = (ushort)(plain[rpos] | (plain[rpos + 1] << 8)); rpos += 2;
            string password = Encoding.ASCII.GetString(plain, rpos, passLen);

            Logger.Debug($"[Login] Login attempt: account='{account}' os={os} ver={version}");

            // ── Step 4: Validate credentials ─────────────────────────────────
            Account? acc = accounts.FindByName(account);
            if (acc == null || acc.Password != password)
            {
                Logger.Info($"[Login] Invalid credentials for account '{account}'.");
                await SendErrorAsync(conn, "Invalid account name or password.", ct);
                return;
            }

            // ── Step 5: Build & send character list ───────────────────────────
            IReadOnlyList<PlayerRecord> chars = players.FindByAccount(acc.Id);
            await SendCharacterListAsync(conn, acc, chars, serverName,
                                         gameServerIp, gameServerPort, ct);

            Logger.Info($"[Login] Character list sent to '{account}' ({chars.Count} character(s)).");
        }
        catch (OperationCanceledException) { /* server shutting down */ }
        catch (Exception ex)
        {
            Logger.Error($"[Login] Unhandled error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Response builders
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task SendErrorAsync(Connection conn, string message, CancellationToken ct)
    {
        byte[] msg  = Encoding.ASCII.GetBytes(message);
        var    body = new byte[3 + msg.Length];
        int    p    = 0;

        body[p++] = 0x0A;                               // error message type
        body[p++] = (byte)(msg.Length & 0xFF);
        body[p++] = (byte)(msg.Length >> 8);
        Buffer.BlockCopy(msg, 0, body, p, msg.Length);

        await conn.WritePacketAsync(body, ct);
    }

    private static async Task SendCharacterListAsync(
        Connection                  conn,
        Account                     acc,
        IReadOnlyList<PlayerRecord> chars,
        string                      serverName,
        string                      gameServerIp,
        ushort                      gameServerPort,
        CancellationToken           ct)
    {
        var  buf = new List<byte>(256);

        // Packet type: character list (0x64)
        buf.Add(0x64);

        // Account name (length-prefixed string)
        byte[] accName = Encoding.ASCII.GetBytes(acc.Name);
        buf.Add((byte)(accName.Length & 0xFF));
        buf.Add((byte)(accName.Length >> 8));
        buf.AddRange(accName);

        // Number of characters (capped at 255 — protocol limit)
        int charCount = chars.Count;
        if (charCount > 255)
        {
            Logger.Warning($"[Login] Account '{acc.Name}' has {charCount} characters; only the first 255 will be sent (protocol limit).");
            charCount = 255;
        }
        buf.Add((byte)charCount);

        // 4-byte game-server IP in big-endian (for the character list entries)
        byte[] ipBytes = ParseIpBytes(gameServerIp);

        foreach (var ch in chars.Take(charCount))
        {
            // Character name
            byte[] charName = Encoding.ASCII.GetBytes(ch.Name);
            buf.Add((byte)(charName.Length & 0xFF));
            buf.Add((byte)(charName.Length >> 8));
            buf.AddRange(charName);

            // World name
            byte[] worldName = Encoding.ASCII.GetBytes(serverName);
            buf.Add((byte)(worldName.Length & 0xFF));
            buf.Add((byte)(worldName.Length >> 8));
            buf.AddRange(worldName);

            // IP (4 bytes big-endian) + port (2 bytes little-endian)
            buf.AddRange(ipBytes);
            buf.Add((byte)(gameServerPort & 0xFF));
            buf.Add((byte)(gameServerPort >> 8));
        }

        // Premium days
        ushort premium = acc.Premium ? (ushort)acc.PremiumDays : (ushort)0;
        buf.Add((byte)(premium & 0xFF));
        buf.Add((byte)(premium >> 8));

        await conn.WritePacketAsync(buf.ToArray(), ct);
    }

    /// <summary>Parses a dotted-quad IP string into 4 big-endian bytes.</summary>
    private static byte[] ParseIpBytes(string ip)
    {
        if (IPAddress.TryParse(ip, out IPAddress? addr))
            return addr.GetAddressBytes(); // already big-endian for IPv4

        // Fallback: localhost
        return new byte[] { 127, 0, 0, 1 };
    }
}
