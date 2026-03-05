namespace mtanksl.OpenTibia.IO;

/// <summary>
/// Reads primitive Tibia protocol types from a byte buffer.
/// All multi-byte integers are little-endian (Tibia convention).
/// </summary>
public sealed class BinaryStream
{
    private readonly byte[] _buffer;
    private int _position;

    public BinaryStream(byte[] buffer)
    {
        _buffer   = buffer;
        _position = 0;
    }

    public int Position  => _position;
    public int Remaining => _buffer.Length - _position;
    public bool IsEmpty  => Remaining == 0;

    public byte   ReadByte()   => _buffer[_position++];
    public ushort ReadUInt16() { var v = BitConverter.ToUInt16(_buffer, _position); _position += 2; return v; }
    public uint   ReadUInt32() { var v = BitConverter.ToUInt32(_buffer, _position); _position += 4; return v; }

    /// <summary>Reads a Pascal-style string: 2-byte length prefix followed by UTF-8 bytes.</summary>
    public string ReadString()
    {
        ushort len = ReadUInt16();
        string s   = System.Text.Encoding.UTF8.GetString(_buffer, _position, len);
        _position += len;
        return s;
    }

    public byte[] ReadBytes(int count)
    {
        var slice = new byte[count];
        Buffer.BlockCopy(_buffer, _position, slice, 0, count);
        _position += count;
        return slice;
    }

    public void Skip(int count) => _position += count;
}

/// <summary>
/// Writes primitive Tibia protocol types into a growable buffer.
/// </summary>
public sealed class BinaryWriter
{
    private readonly List<byte> _buffer = new();

    public void WriteByte(byte value)    => _buffer.Add(value);
    public void WriteUInt16(ushort value) => _buffer.AddRange(BitConverter.GetBytes(value));
    public void WriteUInt32(uint value)   => _buffer.AddRange(BitConverter.GetBytes(value));

    /// <summary>Writes a Pascal-style string: 2-byte length prefix followed by UTF-8 bytes.</summary>
    public void WriteString(string value)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteUInt16((ushort)bytes.Length);
        _buffer.AddRange(bytes);
    }

    public void WriteBytes(byte[] bytes) => _buffer.AddRange(bytes);

    public byte[] ToArray() => _buffer.ToArray();
    public int    Length    => _buffer.Count;
}
