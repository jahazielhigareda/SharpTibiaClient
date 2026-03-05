namespace mtanksl.OpenTibia.FileFormats;

/// <summary>
/// Represents one item type loaded from the Tibia <c>Tibia.dat</c> data file.
/// Phase 11: skeleton type — extend with actual 8.6 property flags as needed.
/// </summary>
public sealed class DatItemType
{
    public ushort Id          { get; init; }
    public bool   IsGround    { get; init; }
    public bool   IsStackable { get; init; }
    public bool   IsFluid     { get; init; }
    public bool   IsContainer { get; init; }
    public string Name        { get; init; } = "";
}

/// <summary>
/// Parses the Tibia 8.6 <c>Tibia.dat</c> client data file.
/// Returns the item, outfit, effect, and missile type lists.
/// </summary>
public sealed class DatReader
{
    /// <summary>Expected signature for Tibia 8.60 dat files (official 8.60 release).</summary>
    public const uint SignatureV860 = 0x4C2C7993;

    /// <summary>Alternate signature accepted for Tibia 8.6x dat files.</summary>
    public const uint SignatureV86  = 0x439C7B00;

    private readonly string _path;

    public DatReader(string path) { _path = path; }

    public IReadOnlyList<DatItemType> Items    { get; private set; } = Array.Empty<DatItemType>();
    public int OutfitCount  { get; private set; }
    public int EffectCount  { get; private set; }
    public int MissileCount { get; private set; }

    /// <summary>
    /// Reads the header and item list from the dat file.
    /// Throws <see cref="InvalidDataException"/> if the signature doesn't match any known 8.6x value.
    /// </summary>
    public void Load()
    {
        using var fs = File.OpenRead(_path);
        using var br = new BinaryReader(fs);

        uint signature = br.ReadUInt32();
        if (signature != SignatureV860 && signature != SignatureV86)
            throw new InvalidDataException(
                $"Unexpected dat signature 0x{signature:X8}. Expected 0x{SignatureV860:X8}.");

        ushort itemCount    = br.ReadUInt16();
        ushort outfitCount  = br.ReadUInt16();
        ushort effectCount  = br.ReadUInt16();
        ushort missileCount = br.ReadUInt16();

        OutfitCount  = outfitCount;
        EffectCount  = effectCount;
        MissileCount = missileCount;

        var items = new List<DatItemType>(itemCount);
        // Items start at ID 100 in Tibia 8.6.
        for (int i = 100; i < 100 + itemCount; i++)
        {
            items.Add(new DatItemType { Id = (ushort)i });
            // NOTE: Full property reading is stubbed; add attribute parsing here.
        }

        Items = items;
    }
}

/// <summary>
/// Lightweight reader for the Tibia <c>Tibia.spr</c> sprite archive.
/// Returns raw RGBA pixel data for a given sprite ID.
/// </summary>
public sealed class SprReader
{
    private readonly string _path;
    private uint   _spriteCount;
    private long   _dataStart;
    private uint[] _offsets = Array.Empty<uint>();

    public SprReader(string path) { _path = path; }

    public uint SpriteCount => _spriteCount;

    /// <summary>Reads the sprite index from the file header.</summary>
    public void Load()
    {
        using var fs = File.OpenRead(_path);
        using var br = new BinaryReader(fs);

        br.ReadUInt32(); // signature
        _spriteCount = br.ReadUInt16();

        _offsets = new uint[_spriteCount + 1];
        for (int i = 1; i <= _spriteCount; i++)
            _offsets[i] = br.ReadUInt32();

        _dataStart = fs.Position;
    }

    /// <summary>
    /// Returns the raw bytes of sprite <paramref name="id"/> (1-based),
    /// or an empty array if the sprite is blank.
    /// </summary>
    public byte[] GetSpriteBytes(uint id)
    {
        if (id == 0 || id > _spriteCount)
            return Array.Empty<byte>();

        uint offset = _offsets[id];
        if (offset == 0)
            return Array.Empty<byte>();

        using var fs = File.OpenRead(_path);
        fs.Seek(offset, SeekOrigin.Begin);
        using var br = new BinaryReader(fs);

        br.ReadUInt16(); // colour key (magenta R, G, B — ignored)
        br.ReadByte();
        ushort dataLength = br.ReadUInt16();
        return br.ReadBytes(dataLength);
    }
}
