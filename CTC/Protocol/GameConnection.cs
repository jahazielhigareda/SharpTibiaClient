using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CTC
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Outgoing packet type constants (client → server), Tibia 8.6
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Phase 8: Outgoing packet type byte IDs for Tibia 8.6.</summary>
    public static class OutgoingPacketType
    {
        public const byte Logout      = 0x14;
        // Walk direction bytes (standard OT 8.6 client assignments).
        // Note: the roadmap listed 0x65–0x68 as N/E/S/W; standard 8.6 is N=0x65 E=0x66 S=0x67 W=0x68.
        public const byte MoveNorth   = 0x65;
        public const byte MoveEast    = 0x66;
        public const byte MoveSouth   = 0x67;
        public const byte MoveWest    = 0x68;
        public const byte TurnNorth   = 0x6F;
        public const byte TurnEast    = 0x70;
        public const byte TurnSouth   = 0x71;
        public const byte TurnWest    = 0x72;
        public const byte MoveItem    = 0x78;
        public const byte LookAt      = 0x8C;
        public const byte UseItem     = 0x82;
        public const byte OpenContainer  = 0x86;
        public const byte CloseContainer = 0x87;
        public const byte Attack      = 0xA0;
        public const byte Say         = 0x96;
        // Game login (0x0A) is shared between client and server — see IncomingPacketType.GameLogin.
        public const byte GameLogin   = 0x0A;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Incoming packet type constants (server → client), Tibia 8.6
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Phase 8: Incoming packet type byte IDs for Tibia 8.6.</summary>
    public static class IncomingPacketType
    {
        public const byte GameLogin        = 0x0A;
        public const byte MapDescription   = 0x14;
        public const byte MoveNorth        = 0x15;
        public const byte MoveEast         = 0x16;
        public const byte MoveSouth        = 0x17;
        public const byte MoveWest         = 0x18;
        public const byte UpdateTile       = 0x1E;
        public const byte AddTileThing     = 0x1F;
        public const byte RemoveTileThing  = 0x20;
        public const byte TransformTileThing = 0x21;
        public const byte OpenContainer    = 0x6B;
        public const byte CloseContainer   = 0x6C;
        public const byte UpdateContainer  = 0x6D;
        public const byte Inventory        = 0x78;
        public const byte WorldLight       = 0x82;
        public const byte MagicEffect      = 0x83;
        public const byte AnimatedText     = 0x85;
        public const byte DistanceEffect   = 0x86;
        public const byte CreatureOutfits  = 0x8E;
        public const byte ChannelList      = 0x96;
        public const byte OpenChannel      = 0x97;
        public const byte PlayerStats      = 0xA1;
        public const byte PlayerSkills     = 0xA2;
        public const byte CooldownGroup    = 0xA3;
        public const byte Cooldown         = 0xA4;
        public const byte TextMessage      = 0xB4;
        public const byte QuestLogList     = 0xCA;
        public const byte QuestLogDetail   = 0xCB;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GameConnection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Phase 8: Manages the live TCP connection to the Tibia 8.6 game server (port 7172).
    ///
    /// Flow:
    ///   1. Connect to <c>ip:port</c> (from the character list entry).
    ///   2. Send the game-login packet (type 0x0A) encrypted with XTEA.
    ///   3. After the server's game-login response is received, all subsequent
    ///      packets are XTEA-decrypted and routed to <see cref="TibiaGameProtocol"/>.
    ///
    /// Outgoing packet builders are exposed as public methods (8e requirements).
    /// </summary>
    public class GameConnection : IDisposable
    {
        private const ushort Os        = 2;      // Linux / generic
        private const ushort ClientVer = 860;

        private readonly TcpClient      _tcp;
        private readonly NetworkStream  _stream;
        private readonly uint[]         _xteaKey;
        private readonly TibiaGameProtocol _protocol;

        private bool _xteaEnabled = false;

        private GameConnection(TcpClient tcp, uint[] xteaKey, TibiaGameProtocol protocol)
        {
            _tcp      = tcp;
            _stream   = tcp.GetStream();
            _xteaKey  = xteaKey;
            _protocol = protocol;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Factory / handshake
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Connects to the game server and performs the login handshake.
        /// </summary>
        /// <param name="entry">Character list entry (provides IP and port).</param>
        /// <param name="charName">Name of the character to log in.</param>
        /// <param name="xteaKey">XTEA session key obtained from <see cref="LoginConnection"/>.</param>
        /// <param name="protocol">Protocol object that will dispatch incoming packets.</param>
        public static async Task<GameConnection> ConnectAsync(
            CharacterEntry entry,
            string         charName,
            uint[]         xteaKey,
            TibiaGameProtocol protocol)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(entry.Ip, entry.Port).ConfigureAwait(false);

            var conn = new GameConnection(tcp, xteaKey, protocol);
            await conn.SendGameLoginAsync(charName).ConfigureAwait(false);
            return conn;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Receive loop
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads packets from the game server in a loop until the connection is closed.
        /// Decrypts with XTEA (after the server login response enables it) and dispatches
        /// each packet through <see cref="TibiaGameProtocol"/>.
        /// </summary>
        public async Task ReceiveLoopAsync()
        {
            var lenBuf = new byte[2];
            while (true)
            {
                await ReadExactAsync(lenBuf, 2).ConfigureAwait(false);
                int len = lenBuf[0] | (lenBuf[1] << 8);
                if (len == 0) break;

                var data = new byte[len];
                await ReadExactAsync(data, len).ConfigureAwait(false);

                if (_xteaEnabled)
                {
                    // Decrypt in-place; len must be a multiple of 8.
                    Xtea.Decrypt(data, 0, len, _xteaKey);

                    // The first 4 bytes after decryption are the Adler32 checksum
                    // covering the payload (bytes 4..len).
                    uint expected = Adler32.Compute(data, 4, len - 4);
                    uint actual   = BitConverter.ToUInt32(data, 0);
                    if (actual != expected)
                    {
                        Log.Warning($"[GameConnection] Adler32 mismatch: expected 0x{expected:X8}, got 0x{actual:X8}");
                        continue;
                    }

                    // Wrap bytes [4..] as a NetworkMessage and dispatch.
                    var nmsg = NetworkMessage.FromDecryptedBytes(data, 4, len - 4);
                    _protocol.parsePacket(nmsg);

                    // If the first packet was the game login response, enable XTEA for all
                    // subsequent packets (the server switches after sending 0x0A response).
                }
                else
                {
                    // First packet (game login response) is NOT encrypted.
                    var nmsg = NetworkMessage.FromDecryptedBytes(data, 0, len);
                    _protocol.parsePacket(nmsg);
                    _xteaEnabled = true;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Outgoing packet builders (8e)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Sends a "walk north" packet (0x65).</summary>
        public Task WalkNorthAsync()   => SendRawByteAsync(OutgoingPacketType.MoveNorth);
        /// <summary>Sends a "walk east" packet (0x66).</summary>
        public Task WalkEastAsync()    => SendRawByteAsync(OutgoingPacketType.MoveEast);
        /// <summary>Sends a "walk south" packet (0x67).</summary>
        public Task WalkSouthAsync()   => SendRawByteAsync(OutgoingPacketType.MoveSouth);
        /// <summary>Sends a "walk west" packet (0x68).</summary>
        public Task WalkWestAsync()    => SendRawByteAsync(OutgoingPacketType.MoveWest);

        /// <summary>Sends a "turn north" packet (0x6F).</summary>
        public Task TurnNorthAsync()   => SendRawByteAsync(OutgoingPacketType.TurnNorth);
        /// <summary>Sends a "turn east" packet (0x70).</summary>
        public Task TurnEastAsync()    => SendRawByteAsync(OutgoingPacketType.TurnEast);
        /// <summary>Sends a "turn south" packet (0x71).</summary>
        public Task TurnSouthAsync()   => SendRawByteAsync(OutgoingPacketType.TurnSouth);
        /// <summary>Sends a "turn west" packet (0x72).</summary>
        public Task TurnWestAsync()    => SendRawByteAsync(OutgoingPacketType.TurnWest);

        /// <summary>Sends a "logout" packet (0x14).</summary>
        public Task LogoutAsync()      => SendRawByteAsync(OutgoingPacketType.Logout);

        /// <summary>
        /// Sends a "say" packet (0x96).
        /// </summary>
        /// <param name="type">Message type (e.g. <c>MessageType.Say</c>).</param>
        /// <param name="text">Text to send.</param>
        /// <param name="channelId">Channel ID (used when type is a channel message; otherwise 0).</param>
        public Task SayAsync(MessageType type, string text, ushort channelId = 0)
        {
            var buf = new byte[512];
            int p = 0;
            buf[p++] = OutgoingPacketType.Say;
            buf[p++] = (byte)type;
            if (type == MessageType.ChannelYellow || type == MessageType.ChannelRed ||
                type == MessageType.ChannelOrange || type == MessageType.ChannelAnonymousRed)
            {
                buf[p++] = (byte)(channelId & 0xFF);
                buf[p++] = (byte)(channelId >> 8);
            }
            byte[] textBytes = Encoding.ASCII.GetBytes(text);
            buf[p++] = (byte)(textBytes.Length & 0xFF);
            buf[p++] = (byte)(textBytes.Length >> 8);
            Buffer.BlockCopy(textBytes, 0, buf, p, textBytes.Length);
            p += textBytes.Length;
            return SendPayloadAsync(buf, p);
        }

        /// <summary>
        /// Sends an "attack creature" packet (0xA0).
        /// </summary>
        /// <param name="creatureId">Target creature ID; 0 to cancel.</param>
        public Task AttackAsync(uint creatureId)
        {
            var buf = new byte[5];
            buf[0] = OutgoingPacketType.Attack;
            Buffer.BlockCopy(BitConverter.GetBytes(creatureId), 0, buf, 1, 4);
            return SendPayloadAsync(buf, 5);
        }

        /// <summary>
        /// Sends a "look at" packet (0x8C).
        /// </summary>
        public Task LookAtAsync(MapPosition pos, ushort stackIndex)
        {
            var buf = new byte[8];
            buf[0] = OutgoingPacketType.LookAt;
            WritePosition(buf, 1, pos);
            buf[7] = (byte)(stackIndex & 0xFF);
            return SendPayloadAsync(buf, 8);
        }

        /// <summary>
        /// Sends a "use item" packet (0x82).
        /// </summary>
        public Task UseItemAsync(MapPosition pos, ushort itemId, byte stackIndex, byte containerId)
        {
            var buf = new byte[12];
            int p = 0;
            buf[p++] = OutgoingPacketType.UseItem;
            WritePosition(buf, p, pos); p += 6;
            buf[p++] = (byte)(itemId & 0xFF);
            buf[p++] = (byte)(itemId >> 8);
            buf[p++] = stackIndex;
            buf[p++] = containerId;
            buf[p++] = 0; // no target
            return SendPayloadAsync(buf, p);
        }

        /// <summary>
        /// Sends a "move item" packet (0x78).
        /// </summary>
        public Task MoveItemAsync(
            MapPosition fromPos, ushort fromItem, byte fromStack,
            MapPosition toPos,   ushort toItem,   byte count)
        {
            var buf = new byte[18];
            int p = 0;
            buf[p++] = OutgoingPacketType.MoveItem;
            WritePosition(buf, p, fromPos); p += 6;
            buf[p++] = (byte)(fromItem & 0xFF);
            buf[p++] = (byte)(fromItem >> 8);
            buf[p++] = fromStack;
            WritePosition(buf, p, toPos); p += 6;
            buf[p++] = count;
            return SendPayloadAsync(buf, p);
        }

        /// <summary>Sends an "open container" packet (0x86).</summary>
        public Task OpenContainerAsync(MapPosition pos, ushort itemId, byte stackIndex)
        {
            var buf = new byte[10];
            int p = 0;
            buf[p++] = OutgoingPacketType.OpenContainer;
            WritePosition(buf, p, pos); p += 6;
            buf[p++] = (byte)(itemId & 0xFF);
            buf[p++] = (byte)(itemId >> 8);
            buf[p++] = stackIndex;
            return SendPayloadAsync(buf, p);
        }

        /// <summary>Sends a "close container" packet (0x87).</summary>
        public Task CloseContainerAsync(byte containerId)
        {
            var buf = new byte[2];
            buf[0] = OutgoingPacketType.CloseContainer;
            buf[1] = containerId;
            return SendPayloadAsync(buf, 2);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Private helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task SendGameLoginAsync(string charName)
        {
            // Build RSA block for game login (type 0x0A).
            var rsaBlock = new byte[128];
            int pos = 0;
            rsaBlock[pos++] = 0x00; // sentinel

            // XTEA key (4 × uint32 LE).
            foreach (uint k in _xteaKey)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(k), 0, rsaBlock, pos, 4);
                pos += 4;
            }

            // Character name (length-prefixed string).
            byte[] nameBytes = Encoding.ASCII.GetBytes(charName);
            rsaBlock[pos++] = (byte)(nameBytes.Length & 0xFF);
            rsaBlock[pos++] = (byte)(nameBytes.Length >> 8);
            Buffer.BlockCopy(nameBytes, 0, rsaBlock, pos, nameBytes.Length);

            byte[] encRsa = Rsa.Encrypt(rsaBlock);

            // Payload: type(1) + OS(2) + version(2) + rsaBlock(128) = 133 bytes.
            var payload = new byte[133];
            int p = 0;
            payload[p++] = OutgoingPacketType.GameLogin; // 0x0A — shared type byte for both directions
            payload[p++] = (byte)(Os & 0xFF);
            payload[p++] = (byte)(Os >> 8);
            payload[p++] = (byte)(ClientVer & 0xFF);
            payload[p++] = (byte)(ClientVer >> 8);
            Buffer.BlockCopy(encRsa, 0, payload, p, 128);

            // Prepend 2-byte little-endian packet length and send.
            var packet = new byte[2 + payload.Length];
            packet[0] = (byte)(payload.Length & 0xFF);
            packet[1] = (byte)(payload.Length >> 8);
            Buffer.BlockCopy(payload, 0, packet, 2, payload.Length);
            await _stream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a single-byte outgoing packet, XTEA-encrypted.
        /// </summary>
        private Task SendRawByteAsync(byte type)
        {
            return SendPayloadAsync(new[] { type }, 1);
        }

        /// <summary>
        /// Wraps <paramref name="payload"/> in the Tibia 8.6 packet envelope
        /// (2-byte length + 4-byte Adler32 + XTEA-encrypted payload) and sends it.
        /// </summary>
        private async Task SendPayloadAsync(byte[] payload, int length)
        {
            // Pad to next multiple of 8 (XTEA block size).
            int padded = ((length + 4 + 7) / 8) * 8; // +4 for Adler32
            var block  = new byte[padded];

            // First 4 bytes: Adler32 of unencrypted payload.
            uint cksum = Adler32.Compute(payload, 0, length);
            block[0] = (byte)(cksum & 0xFF);
            block[1] = (byte)((cksum >>  8) & 0xFF);
            block[2] = (byte)((cksum >> 16) & 0xFF);
            block[3] = (byte)((cksum >> 24) & 0xFF);
            Buffer.BlockCopy(payload, 0, block, 4, length);

            // Encrypt in-place with XTEA.
            Xtea.Encrypt(block, 0, block.Length, _xteaKey);

            // Prepend 2-byte packet length and send.
            var packet = new byte[2 + block.Length];
            packet[0] = (byte)(block.Length & 0xFF);
            packet[1] = (byte)(block.Length >> 8);
            Buffer.BlockCopy(block, 0, packet, 2, block.Length);
            await _stream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);
        }

        private static void WritePosition(byte[] buf, int offset, MapPosition pos)
        {
            buf[offset + 0] = (byte)(pos.X & 0xFF);
            buf[offset + 1] = (byte)(pos.X >> 8);
            buf[offset + 2] = (byte)(pos.Y & 0xFF);
            buf[offset + 3] = (byte)(pos.Y >> 8);
            buf[offset + 4] = (byte)(pos.Z & 0xFF);
            buf[offset + 5] = (byte)(pos.Z >> 8);
        }

        private async Task ReadExactAsync(byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
                read += await _stream.ReadAsync(buf, read, count - read).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcp?.Dispose();
        }
    }
}
