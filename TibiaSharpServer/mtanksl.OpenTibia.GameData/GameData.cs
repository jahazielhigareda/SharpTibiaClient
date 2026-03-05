using mtanksl.OpenTibia.FileFormats;

namespace mtanksl.OpenTibia.GameData;

/// <summary>
/// Loads and exposes the Tibia client data files required by the server:
/// <c>Tibia.dat</c> (item types) and <c>Tibia.spr</c> (sprite data).
///
/// The server uses item type metadata (IsGround, IsStackable, etc.) when
/// validating player actions.  Sprite data is never sent by the server
/// but may be used for map generation utilities.
/// </summary>
public sealed class TibiaGameData : IDisposable
{
    private readonly string _dataDirectory;
    private DatReader? _dat;
    private bool       _disposed;

    public TibiaGameData(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    /// <summary>All item types loaded from Tibia.dat.  Null until <see cref="Load"/> is called.</summary>
    public IReadOnlyList<DatItemType>? ItemTypes => _dat?.Items;

    /// <summary>
    /// Loads Tibia.dat from <see cref="_dataDirectory"/>.
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public void Load()
    {
        if (_dat != null)
            return;

        string datPath = Path.Combine(_dataDirectory, "Tibia.dat");
        if (!File.Exists(datPath))
        {
            Console.WriteLine($"[GameData] Warning: '{datPath}' not found — item types will be empty.");
            // Leave _dat null; callers must handle ItemTypes == null gracefully.
            return;
        }

        _dat = new DatReader(datPath);
        try { _dat.Load(); }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"[GameData] Failed to load Tibia.dat: {ex.Message}");
            _dat = null; // Reset so Load() can be retried after the file is fixed.
        }
    }

    /// <summary>Returns the <see cref="DatItemType"/> for a given ID, or null if not found.</summary>
    public DatItemType? GetItemType(ushort id)
    {
        if (ItemTypes == null) return null;
        // IDs start at 100; adjust to list index.
        int index = id - 100;
        return index >= 0 && index < ItemTypes.Count ? ItemTypes[index] : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // No unmanaged resources to release in this implementation.
    }
}
