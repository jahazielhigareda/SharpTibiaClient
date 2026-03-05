// Phase 15: Tibia.dat multi-signature support
//
// These tests validate the fix for:
//   "[GameData] Failed to load Tibia.dat: Unexpected dat signature 0x4C2C7993.
//    Expected 0x439C7B00."
//
// DatReader must now accept both the official Tibia 8.60 signature (0x4C2C7993)
// and the legacy alternate value (0x439C7B00) that was previously the only
// accepted signature.  Any other value must still be rejected.

using System.Text;
using mtanksl.OpenTibia.FileFormats;
using mtanksl.OpenTibia.GameData;

namespace mtanksl.OpenTibia.Tests;

// ---------------------------------------------------------------------------
// DatReader signature-constant tests
// ---------------------------------------------------------------------------

/// <summary>
/// Validates the signature constants exposed by <see cref="DatReader"/>.
/// </summary>
public class DatReaderConstantTests
{
    [Fact]
    public void SignatureV860_HasExpectedValue()
    {
        Assert.Equal(0x4C2C7993u, DatReader.SignatureV860);
    }

    [Fact]
    public void SignatureV86_HasExpectedValue()
    {
        Assert.Equal(0x439C7B00u, DatReader.SignatureV86);
    }

    [Fact]
    public void SignatureV860_DifferentFromSignatureV86()
    {
        Assert.NotEqual(DatReader.SignatureV860, DatReader.SignatureV86);
    }
}

// ---------------------------------------------------------------------------
// DatReader.Load() signature-acceptance tests
// ---------------------------------------------------------------------------

/// <summary>
/// Validates that <see cref="DatReader.Load"/> accepts both known 8.6x
/// signatures and rejects unknown ones.
/// </summary>
public class DatReaderSignatureTests
{
    /// <summary>
    /// Writes a minimal syntactically-valid .dat header to a temp file and
    /// returns its path.
    /// </summary>
    private static string CreateMinimalDat(uint signature)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.dat");

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        bw.Write(signature);  // 4-byte signature
        bw.Write((ushort)0);  // itemCount = 0  → loop body never executes
        bw.Write((ushort)0);  // outfitCount
        bw.Write((ushort)0);  // effectCount
        bw.Write((ushort)0);  // missileCount

        return path;
    }

    [Fact]
    public void Load_SignatureV860_DoesNotThrow()
    {
        string path = CreateMinimalDat(DatReader.SignatureV860);
        try
        {
            var reader = new DatReader(path);
            var ex = Record.Exception(() => reader.Load());
            Assert.Null(ex);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_SignatureV86_DoesNotThrow()
    {
        string path = CreateMinimalDat(DatReader.SignatureV86);
        try
        {
            var reader = new DatReader(path);
            var ex = Record.Exception(() => reader.Load());
            Assert.Null(ex);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_UnknownSignature_ThrowsInvalidDataException()
    {
        string path = CreateMinimalDat(0xDEADBEEFu);
        try
        {
            var reader = new DatReader(path);
            Assert.Throws<InvalidDataException>(() => reader.Load());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ZeroSignature_ThrowsInvalidDataException()
    {
        string path = CreateMinimalDat(0x00000000u);
        try
        {
            var reader = new DatReader(path);
            Assert.Throws<InvalidDataException>(() => reader.Load());
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(0x4C2C7993u)] // SignatureV860
    [InlineData(0x439C7B00u)] // SignatureV86
    public void Load_KnownSignature_SetsCountsToZero(uint sig)
    {
        string path = CreateMinimalDat(sig);
        try
        {
            var reader = new DatReader(path);
            reader.Load();

            Assert.Empty(reader.Items);
            Assert.Equal(0, reader.OutfitCount);
            Assert.Equal(0, reader.EffectCount);
            Assert.Equal(0, reader.MissileCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_ErrorMessage_MentionsActualAndExpectedSignature()
    {
        uint badSig = 0xCAFEBABEu;
        string path = CreateMinimalDat(badSig);
        try
        {
            var reader = new DatReader(path);
            var ex = Assert.Throws<InvalidDataException>(() => reader.Load());

            // Error message must quote the bad signature and the primary expected value.
            Assert.Contains("4C2C7993", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("CAFEBABE",  ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }
}

// ---------------------------------------------------------------------------
// TibiaGameData (server-side) integration tests
// ---------------------------------------------------------------------------

/// <summary>
/// Validates that <see cref="TibiaGameData.Load"/> handles missing files and
/// dat signature mismatches gracefully (does not propagate exceptions to the caller).
/// </summary>
public class TibiaGameDataLoadTests
{
    [Fact]
    public void Load_MissingDatFile_DoesNotThrow()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        try
        {
            using var gd = new TibiaGameData(tmpDir);
            var ex = Record.Exception(() => gd.Load());
            Assert.Null(ex);
            Assert.Null(gd.ItemTypes); // dat was absent → null
        }
        finally { Directory.Delete(tmpDir, recursive: true); }
    }

    [Fact]
    public void Load_InvalidSignatureDat_DoesNotThrow()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        string datPath = Path.Combine(tmpDir, "Tibia.dat");

        {
            using var fs = File.Create(datPath);
            using var bw = new BinaryWriter(fs);
            bw.Write(0xDEADBEEFu); // bad signature
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
        }

        try
        {
            using var gd = new TibiaGameData(tmpDir);
            var ex = Record.Exception(() => gd.Load());
            Assert.Null(ex);          // must not propagate
            Assert.Null(gd.ItemTypes); // reset to null on error
        }
        finally { Directory.Delete(tmpDir, recursive: true); }
    }

    [Fact]
    public void Load_ValidSignatureV860Dat_ItemTypesNotNull()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        string datPath = Path.Combine(tmpDir, "Tibia.dat");

        {
            using var fs = File.Create(datPath);
            using var bw = new BinaryWriter(fs);
            bw.Write(DatReader.SignatureV860); // 0x4C2C7993
            bw.Write((ushort)0);  // itemCount = 0
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
        }

        try
        {
            using var gd = new TibiaGameData(tmpDir);
            gd.Load();

            Assert.NotNull(gd.ItemTypes);
            Assert.Empty(gd.ItemTypes!);
        }
        finally { Directory.Delete(tmpDir, recursive: true); }
    }

    [Fact]
    public void Load_CalledTwice_IsNoOp()
    {
        string tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tmpDir);
        string datPath = Path.Combine(tmpDir, "Tibia.dat");

        {
            using var fs = File.Create(datPath);
            using var bw = new BinaryWriter(fs);
            bw.Write(DatReader.SignatureV860);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
        }

        try
        {
            using var gd = new TibiaGameData(tmpDir);
            gd.Load();
            var items1 = gd.ItemTypes;
            gd.Load(); // second call must be a no-op
            var items2 = gd.ItemTypes;

            Assert.Same(items1, items2);
        }
        finally { Directory.Delete(tmpDir, recursive: true); }
    }
}
