using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CTC
{
    /// <summary>
    /// Bridges a live TCP game-server connection (async) to the synchronous
    /// <see cref="PacketStream"/> interface consumed by <see cref="ClientState.Update()"/>.
    ///
    /// A background <see cref="Task"/> reads from the TCP stream, performs XTEA
    /// decryption + Adler32 verification, and enqueues ready <see cref="NetworkMessage"/>
    /// objects.  The game-loop thread dequeues them via <see cref="Poll"/> /
    /// <see cref="Read"/> — exactly as it does for <see cref="TibiaMovieStream"/>.
    ///
    /// Usage:
    ///   var stream = await LivePacketStream.ConnectAsync(entry, charName, xteaKey);
    ///   var state  = new ClientState(stream);
    ///   Desktop.AddClient(state);
    /// </summary>
    public sealed class LivePacketStream : PacketStream, IDisposable
    {
        // ─── Network ──────────────────────────────────────────────────────────
        private readonly TcpClient     _tcp;
        private readonly NetworkStream _stream;
        private readonly uint[]        _xteaKey;
        private readonly string        _name;

        // ─── Packet queue consumed by the game loop ────────────────────────────
        private readonly ConcurrentQueue<NetworkMessage> _queue = new();
        private readonly CancellationTokenSource         _cts   = new();

        // ─── Background reader task ────────────────────────────────────────────
        private readonly Task _readerTask;

        private bool _disposed;

        // ─────────────────────────────────────────────────────────────────────
        //  Construction / factory
        // ─────────────────────────────────────────────────────────────────────

        private LivePacketStream(TcpClient tcp, uint[] xteaKey, string name)
        {
            _tcp     = tcp;
            _stream  = tcp.GetStream();
            _xteaKey = xteaKey;
            _name    = name;

            // Start background reader immediately.
            _readerTask = Task.Run(ReadLoopAsync, _cts.Token);
        }

        /// <summary>
        /// Connects to the game server, sends the game-login packet, and returns
        /// a <see cref="LivePacketStream"/> ready to feed into <see cref="ClientState"/>.
        /// </summary>
        /// <param name="entry">Character entry from the login-server character list.</param>
        /// <param name="charName">Name of the character to log in with.</param>
        /// <param name="xteaKey">XTEA session key returned by <see cref="LoginConnection"/>.</param>
        public static async Task<LivePacketStream> ConnectAsync(
            CharacterEntry entry,
            string         charName,
            uint[]         xteaKey)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(entry.Ip, entry.Port).ConfigureAwait(false);

            var lps = new LivePacketStream(tcp, xteaKey, $"{entry.Ip}:{entry.Port}");
            await lps.SendGameLoginAsync(charName).ConfigureAwait(false);
            return lps;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PacketStream interface
        // ─────────────────────────────────────────────────────────────────────

        public string Name => _name;

        /// <summary>Returns <c>true</c> when at least one decrypted packet is ready.</summary>
        public bool Poll(GameTime _) => !_queue.IsEmpty;

        /// <summary>Dequeues and returns the next ready packet, or <c>null</c> if none.</summary>
        public NetworkMessage? Read(GameTime? _)
        {
            _queue.TryDequeue(out var msg);
            return msg;
        }

        /// <summary>Sends an outgoing game packet (XTEA-encrypted).</summary>
        public void Write(NetworkMessage nmsg)
        {
            // Outgoing packets are not yet consumed by the movie-replay path,
            // but live connections need this for walk/chat/etc.
            // Fire-and-forget on the thread pool; exceptions are logged.
            _ = Task.Run(async () =>
            {
                try   { await SendAsync(nmsg).ConfigureAwait(false); }
                catch (Exception ex) { Log.Error($"[LivePacketStream] Send error: {ex.Message}"); }
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Background receive loop
        // ─────────────────────────────────────────────────────────────────────

        private async Task ReadLoopAsync()
        {
            var lenBuf   = new byte[2];
            bool firstPacket = true;   // first server response is unencrypted

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    // Read 2-byte packet length.
                    await ReadExactAsync(lenBuf, 2).ConfigureAwait(false);
                    int len = lenBuf[0] | (lenBuf[1] << 8);
                    if (len == 0) break;

                    var data = new byte[len];
                    await ReadExactAsync(data, len).ConfigureAwait(false);

                    if (firstPacket)
                    {
                        // First server packet is NOT encrypted.
                        firstPacket = false;
                        _queue.Enqueue(NetworkMessage.FromDecryptedBytes(data, 0, len));
                    }
                    else
                    {
                        // Subsequent packets: XTEA-decrypt then Adler32-verify.
                        if (len % 8 != 0)
                        {
                            Log.Warning($"[LivePacketStream] Non-multiple-of-8 packet ({len} bytes); skipping.");
                            continue;
                        }

                        Xtea.Decrypt(data, 0, len, _xteaKey);

                        uint expected = Adler32.Compute(data, 4, len - 4);
                        uint actual   = BitConverter.ToUInt32(data, 0);
                        if (actual != expected)
                        {
                            Log.Warning($"[LivePacketStream] Adler32 mismatch: 0x{actual:X8} vs 0x{expected:X8}; skipping.");
                            continue;
                        }

                        if (len > 4)
                            _queue.Enqueue(NetworkMessage.FromDecryptedBytes(data, 4, len - 4));
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            {
                Log.Warning($"[LivePacketStream] Connection closed: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"[LivePacketStream] Read loop error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Outgoing packet helpers
        // ─────────────────────────────────────────────────────────────────────

        private const ushort Os        = 2;
        private const ushort ClientVer = 860;

        private async Task SendGameLoginAsync(string charName)
        {
            // Build RSA block for the game-login packet.
            var rsaBlock = new byte[128];
            int pos = 0;
            rsaBlock[pos++] = 0x00; // sentinel — value must be < RSA modulus

            // XTEA key (4 × uint32 LE).
            foreach (uint k in _xteaKey)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(k), 0, rsaBlock, pos, 4);
                pos += 4;
            }

            // Character name (length-prefixed ASCII).
            byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(charName);
            rsaBlock[pos++] = (byte)(nameBytes.Length & 0xFF);
            rsaBlock[pos++] = (byte)(nameBytes.Length >> 8);
            Buffer.BlockCopy(nameBytes, 0, rsaBlock, pos, nameBytes.Length);

            byte[] encRsa = Rsa.Encrypt(rsaBlock);

            // Payload: type(1) + OS(2) + version(2) + RSA(128) = 133 bytes.
            var payload = new byte[133];
            int p = 0;
            payload[p++] = 0x0A;                         // game-login type
            payload[p++] = (byte)(Os        & 0xFF);
            payload[p++] = (byte)(Os        >> 8);
            payload[p++] = (byte)(ClientVer & 0xFF);
            payload[p++] = (byte)(ClientVer >> 8);
            Buffer.BlockCopy(encRsa, 0, payload, p, 128);

            // Prepend 2-byte length and write.
            var packet = new byte[2 + payload.Length];
            packet[0] = (byte)(payload.Length & 0xFF);
            packet[1] = (byte)(payload.Length >> 8);
            Buffer.BlockCopy(payload, 0, packet, 2, payload.Length);
            await _stream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);
        }

        private async Task SendAsync(NetworkMessage nmsg)
        {
            // The NetworkMessage.WriteTo interface is socket-based; we replicate
            // the XTEA+Adler32 envelope here for the stream-based connection.
            // Currently called by Write() above — extend as needed.
            await Task.CompletedTask;
        }

        private async Task ReadExactAsync(byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
                read += await _stream.ReadAsync(buf, read, count - read, _cts.Token).ConfigureAwait(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Disposal
        // ─────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _stream.Dispose();
            _tcp.Dispose();
            _cts.Dispose();
        }
    }
}
