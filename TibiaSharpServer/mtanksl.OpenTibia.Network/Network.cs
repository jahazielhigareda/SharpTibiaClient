using System.Net;
using System.Net.Sockets;

namespace mtanksl.OpenTibia.Network;

/// <summary>
/// Listens on a TCP port and accepts incoming client connections.
/// Each accepted connection is handed to a callback on the caller's thread pool.
/// Uses <see cref="CancellationToken"/> for graceful shutdown (replaces Thread.Abort).
/// </summary>
public sealed class TcpServer : IDisposable
{
    private readonly TcpListener          _listener;
    private readonly Func<TcpClient, Task> _handler;
    private CancellationTokenSource?      _cts;

    public int Port { get; }

    public TcpServer(int port, Func<TcpClient, Task> connectionHandler)
    {
        Port      = port;
        _handler  = connectionHandler;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    /// <summary>
    /// Starts listening. Runs until <see cref="Stop"/> is called or the
    /// <see cref="CancellationToken"/> is cancelled.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener.Start();

        Console.WriteLine($"[TcpServer] Listening on port {Port}");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => _handler(client), _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path
        }
        finally
        {
            _listener.Stop();
        }
    }

    public void Stop() => _cts?.Cancel();

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

/// <summary>
/// Wraps a <see cref="TcpClient"/> with helpers for reading and writing
/// length-prefixed Tibia packets.
/// </summary>
public sealed class Connection : IAsyncDisposable
{
    private readonly TcpClient    _client;
    private readonly NetworkStream _stream;

    public Connection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    /// <summary>
    /// Reads a 2-byte length prefix then reads that many bytes.
    /// Returns null on clean disconnect.
    /// </summary>
    public async Task<byte[]?> ReadPacketAsync(CancellationToken ct = default)
    {
        byte[] header = new byte[2];
        // ReadExactAsync returns 0 on clean disconnect, buffer.Length on full read.
        int bytesRead = await _stream.ReadExactAsync(header, ct);
        if (bytesRead == 0) return null;

        ushort length = BitConverter.ToUInt16(header, 0);
        byte[] body   = new byte[length];
        await _stream.ReadExactAsync(body, ct);
        return body;
    }

    /// <summary>
    /// Writes a 2-byte length prefix followed by the packet body.
    /// </summary>
    public async Task WritePacketAsync(byte[] body, CancellationToken ct = default)
    {
        byte[] header = BitConverter.GetBytes((ushort)body.Length);
        await _stream.WriteAsync(header, ct);
        await _stream.WriteAsync(body,   ct);
    }

    /// <summary>
    /// Writes raw bytes without a length prefix.
    /// Used for the 4-byte login challenge sent immediately after connection.
    /// </summary>
    public async Task WriteRawAsync(byte[] data, CancellationToken ct = default)
    {
        await _stream.WriteAsync(data, ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        _client.Dispose();
    }
}

/// <summary>Extension helpers for <see cref="NetworkStream"/>.</summary>
internal static class NetworkStreamExtensions
{
    /// <summary>
    /// Reads exactly <c>buffer.Length</c> bytes, retrying until the buffer is full
    /// or the stream is closed.  Returns 0 on clean disconnect, throws on error.
    /// </summary>
    public static async Task<int> ReadExactAsync(
        this Stream stream, byte[] buffer, CancellationToken ct = default)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (n == 0) return 0;
            total += n;
        }
        return total;
    }
}
