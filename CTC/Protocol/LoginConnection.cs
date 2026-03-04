using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CTC
{
    /// <summary>
    /// Phase 8: Represents a single Tibia 8.6 login server character entry returned in the
    /// character list packet.
    /// </summary>
    public class CharacterEntry
    {
        public string Name  { get; }
        public string World { get; }
        public string Ip    { get; }
        public ushort Port  { get; }

        public CharacterEntry(string name, string world, string ip, ushort port)
        {
            Name  = name;
            World = world;
            Ip    = ip;
            Port  = port;
        }

        public override string ToString() => $"{Name} @ {World} ({Ip}:{Port})";
    }

    /// <summary>
    /// Phase 8: Handles the Tibia 8.6 login server handshake (port 7171).
    ///
    /// Flow:
    ///   1. Connect to <c>host:7171</c> via TCP.
    ///   2. Read the 4-byte server challenge (used as part of checksum in older protocols;
    ///      in 8.6 this is read and discarded — the checksum field is 0).
    ///   3. Build the 160-byte login packet:
    ///        [2 bytes] packet length (LE)
    ///        [4 bytes] Adler32 checksum (0 for login packet)
    ///        [1 byte ] packet type = 0x01
    ///        [2 bytes] OS (2 = Linux)
    ///        [2 bytes] client version = 860
    ///        [4 bytes] data checksum (Adler32 of the RSA block; 0 for simplicity)
    ///        [128 bytes] RSA-encrypted block (see below)
    ///   RSA block layout (128 bytes, block[0] = 0x00):
    ///        [1  byte ] 0x00 padding sentinel
    ///        [16 bytes] XTEA session key (four uint32 LE)
    ///        [2  bytes] account name length (LE)
    ///        [N  bytes] account name (ASCII)
    ///        [2  bytes] password length (LE)
    ///        [M  bytes] password (ASCII)
    ///        [rest    ] zero padding to fill 128 bytes
    ///   4. Parse the character list response.
    /// </summary>
    public class LoginConnection : IDisposable
    {
        private const int LoginPort    = 7171;
        private const ushort Os        = 2;      // Linux / generic
        private const ushort ClientVer = 860;

        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;

        // XTEA session key generated for this login attempt (4 × uint32).
        public uint[] XteaKey { get; }

        private LoginConnection(TcpClient tcp, uint[] xteaKey)
        {
            _tcp     = tcp;
            _stream  = tcp.GetStream();
            XteaKey  = xteaKey;
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Connects to the login server, sends the handshake, and returns the character list.
        /// </summary>
        /// <param name="host">Login server hostname or IP (default "127.0.0.1").</param>
        /// <param name="accountName">Account name (string, 8.6 protocol).</param>
        /// <param name="password">Account password.</param>
        /// <returns>Parsed character list.</returns>
        public static async Task<(LoginConnection conn, List<CharacterEntry> chars, ushort premiumDays)>
            ConnectAsync(string host, string accountName, string password)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(host, LoginPort).ConfigureAwait(false);
            var stream = tcp.GetStream();

            // Step 1: Read the 4-byte server challenge (discard — 8.6 doesn't use it for XOR).
            var challenge = new byte[4];
            await ReadExactAsync(stream, challenge, 4).ConfigureAwait(false);

            // Step 2: Generate a fresh XTEA session key.
            uint[] xteaKey = GenerateXteaKey();

            // Step 3: Build & encrypt the 128-byte RSA block.
            byte[] rsaBlock = BuildRsaBlock(xteaKey, accountName, password);
            byte[] encrypted = Rsa.Encrypt(rsaBlock);

            // Step 4: Build and send the full login packet.
            byte[] packet = BuildLoginPacket(encrypted);
            await stream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);

            // Step 5: Read and parse the character list response.
            var conn   = new LoginConnection(tcp, xteaKey);
            var result = await conn.ReadCharacterListAsync().ConfigureAwait(false);
            return (conn, result.chars, result.premiumDays);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private static uint[] GenerateXteaKey()
        {
            var rng = RandomNumberGenerator.Create();
            var buf = new byte[16];
            rng.GetBytes(buf);
            return new[]
            {
                BitConverter.ToUInt32(buf, 0),
                BitConverter.ToUInt32(buf, 4),
                BitConverter.ToUInt32(buf, 8),
                BitConverter.ToUInt32(buf, 12),
            };
        }

        /// <summary>
        /// Builds the 128-byte RSA plaintext block.  Block[0] == 0x00 is mandatory so the
        /// BigInteger value is strictly less than the RSA modulus n.
        /// </summary>
        private static byte[] BuildRsaBlock(uint[] key, string account, string password)
        {
            var block = new byte[128]; // zero-initialized
            int pos = 0;

            block[pos++] = 0x00; // sentinel — ensures value < n

            // XTEA session key (4 × uint32, little-endian).
            foreach (uint k in key)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(k), 0, block, pos, 4);
                pos += 4;
            }

            // Account name (length-prefixed string, 8.6 change from uint32 account ID).
            byte[] accBytes  = Encoding.ASCII.GetBytes(account);
            byte[] passBytes = Encoding.ASCII.GetBytes(password);

            block[pos++] = (byte)(accBytes.Length & 0xFF);
            block[pos++] = (byte)(accBytes.Length >> 8);
            Buffer.BlockCopy(accBytes, 0, block, pos, accBytes.Length);
            pos += accBytes.Length;

            block[pos++] = (byte)(passBytes.Length & 0xFF);
            block[pos++] = (byte)(passBytes.Length >> 8);
            Buffer.BlockCopy(passBytes, 0, block, pos, passBytes.Length);
            // Remaining bytes stay as zero-padding.

            return block;
        }

        /// <summary>
        /// Assembles the full login packet sent to port 7171.
        /// </summary>
        private static byte[] BuildLoginPacket(byte[] encryptedBlock)
        {
            // Payload: type(1) + OS(2) + version(2) + checksum(4) + rsaBlock(128) = 137 bytes.
            var payload = new byte[137];
            int p = 0;

            payload[p++] = 0x01;                              // packet type
            payload[p++] = (byte)(Os & 0xFF);                 // OS lo
            payload[p++] = (byte)(Os >> 8);                   // OS hi
            payload[p++] = (byte)(ClientVer & 0xFF);          // version lo
            payload[p++] = (byte)(ClientVer >> 8);            // version hi

            // 4-byte Adler32 of the RSA block (sent before encryption check).
            uint cksum = Adler32.Compute(encryptedBlock);
            payload[p++] = (byte)(cksum & 0xFF);
            payload[p++] = (byte)((cksum >>  8) & 0xFF);
            payload[p++] = (byte)((cksum >> 16) & 0xFF);
            payload[p++] = (byte)((cksum >> 24) & 0xFF);

            Buffer.BlockCopy(encryptedBlock, 0, payload, p, 128);

            // Prepend 2-byte little-endian packet length.
            var packet = new byte[2 + payload.Length];
            packet[0] = (byte)(payload.Length & 0xFF);
            packet[1] = (byte)(payload.Length >> 8);
            Buffer.BlockCopy(payload, 0, packet, 2, payload.Length);
            return packet;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Response parsing
        // ─────────────────────────────────────────────────────────────────────

        private async Task<(List<CharacterEntry> chars, ushort premiumDays)> ReadCharacterListAsync()
        {
            // Read 2-byte packet length.
            var lenBuf = new byte[2];
            await ReadExactAsync(_stream, lenBuf, 2).ConfigureAwait(false);
            int len = lenBuf[0] | (lenBuf[1] << 8);

            var data = new byte[len];
            await ReadExactAsync(_stream, data, len).ConfigureAwait(false);

            int pos = 0;
            byte type = data[pos++];

            if (type == 0x0A)
            {
                // Error message.
                ushort msgLen = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2;
                string msg = Encoding.ASCII.GetString(data, pos, msgLen);
                throw new Exception($"Login server error: {msg}");
            }

            if (type == 0x14)
            {
                // MOTD — skip it and expect character list next.
                ushort motdLen = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2 + motdLen;
                type = data[pos++];
            }

            if (type != 0x64)
                throw new Exception($"Unexpected login response packet type 0x{type:X2}");

            return ParseCharacterList(data, ref pos);
        }

        /// <summary>
        /// Parses the 8.6 character list packet payload starting at <paramref name="pos"/>.
        /// Format: account name (string), characters (count byte + entries), premium days (uint16).
        /// </summary>
        private static (List<CharacterEntry> chars, ushort premiumDays) ParseCharacterList(byte[] data, ref int pos)
        {
            // Account name string (8.6 format).
            ushort accLen    = (ushort)(data[pos] | (data[pos + 1] << 8));
            pos += 2;
            pos += accLen; // skip account name — we already know it

            // Character entries.
            byte count = data[pos++];
            var chars  = new List<CharacterEntry>(count);
            for (int i = 0; i < count; ++i)
            {
                ushort nameLen = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2;
                string name = Encoding.ASCII.GetString(data, pos, nameLen);
                pos += nameLen;

                ushort worldLen = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2;
                string world = Encoding.ASCII.GetString(data, pos, worldLen);
                pos += worldLen;

                // 4-byte IP (big-endian) + 2-byte port.
                string ip = $"{data[pos]}.{data[pos+1]}.{data[pos+2]}.{data[pos+3]}";
                pos += 4;
                ushort port = (ushort)(data[pos] | (data[pos + 1] << 8));
                pos += 2;

                chars.Add(new CharacterEntry(name, world, ip, port));
            }

            // Premium days.
            ushort premiumDays = (ushort)(data[pos] | (data[pos + 1] << 8));
            pos += 2;

            return (chars, premiumDays);
        }

        // ─────────────────────────────────────────────────────────────────────

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
                read += await stream.ReadAsync(buf, read, count - read).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcp?.Dispose();
        }
    }
}
