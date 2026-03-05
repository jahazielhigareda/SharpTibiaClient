using mtanksl.OpenTibia.Security;

namespace mtanksl.OpenTibia.Tests;

/// <summary>
/// Unit tests for XTEA encryption/decryption round-trip.
/// Phase 11 validation checkpoint: these tests must pass after the net8.0 upgrade.
/// </summary>
public class XteaTests
{
    private static readonly uint[] TestKey = { 0x1234_5678, 0x9ABC_DEF0, 0x0FED_CBA9, 0x8765_4321 };

    [Fact]
    public void EncryptThenDecrypt_ProducesOriginalData()
    {
        // 8 bytes = one XTEA block
        byte[] original = { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0x00, 0x00 };
        byte[] data = (byte[])original.Clone();

        Xtea.Encrypt(data, TestKey, data.Length);
        Xtea.Decrypt(data, TestKey, data.Length);

        Assert.Equal(original, data);
    }

    [Fact]
    public void Encrypt_ChangesData()
    {
        byte[] original = { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x21, 0x00, 0x00 };
        byte[] data = (byte[])original.Clone();

        Xtea.Encrypt(data, TestKey, data.Length);

        Assert.NotEqual(original, data);
    }

    [Fact]
    public void EncryptDecrypt_MultipleBlocks_ProducesOriginalData()
    {
        byte[] original = new byte[32];
        new Random(42).NextBytes(original);
        byte[] data = (byte[])original.Clone();

        Xtea.Encrypt(data, TestKey, data.Length);
        Xtea.Decrypt(data, TestKey, data.Length);

        Assert.Equal(original, data);
    }

    [Fact]
    public void Encrypt_InvalidLength_Throws()
    {
        byte[] data = new byte[7]; // not a multiple of 8
        Assert.Throws<ArgumentException>(() => Xtea.Encrypt(data, TestKey, data.Length));
    }
}

/// <summary>
/// Unit tests for Adler-32 checksum computation.
/// </summary>
public class Adler32Tests
{
    [Fact]
    public void KnownVector_Wikipedia()
    {
        // "Wikipedia" → Adler-32 = 0x11E60398 (from the Wikipedia article on Adler-32)
        byte[] data = System.Text.Encoding.ASCII.GetBytes("Wikipedia");
        uint checksum = Adler32.Compute(data, 0, data.Length);
        Assert.Equal(0x11E60398u, checksum);
    }

    [Fact]
    public void EmptyInput_ReturnsOne()
    {
        // Empty input: a=1, b=0 → 0x00000001
        uint checksum = Adler32.Compute(Array.Empty<byte>(), 0, 0);
        Assert.Equal(1u, checksum);
    }
}
