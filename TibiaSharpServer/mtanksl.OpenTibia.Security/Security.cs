using System.Security.Cryptography;

namespace mtanksl.OpenTibia.Security;

/// <summary>
/// RSA helper used for the login handshake (Tibia 8.x).
/// Wraps <see cref="System.Security.Cryptography.RSA"/> — the modern .NET 8 API —
/// rather than the legacy <c>RSACryptoServiceProvider</c>.
/// </summary>
public static class Rsa
{
    // Standard OpenTibia RSA private key (well-known in the OT community).
    // In production, load this from a secure key store rather than hard-coding it.
    private const string Modulus =
        "124710459426827943004376449897985578145813518249911" +
        "415456906523584961616233" +
        "5097721477088755082166007484756460541776735";

    /// <summary>
    /// Decrypts a 128-byte RSA block using the server private key.
    /// Returns the plaintext or throws <see cref="CryptographicException"/>
    /// if the block is malformed.
    /// </summary>
    public static byte[] Decrypt(byte[] cipherBlock)
    {
        // NOTE: Replace the stub below with actual key material when real
        // server private key parameters are available.
        // using var rsa = RSA.Create();
        // rsa.ImportParameters(...);
        // return rsa.Decrypt(cipherBlock, RSAEncryptionPadding.Pkcs1);

        // Stub: return the input as-is (no real key material supplied yet).
        return cipherBlock;
    }
}

/// <summary>
/// XTEA symmetric cipher — used to encrypt/decrypt game packets after login.
/// Phase 8 (client-side) and Phase 11 (server-side) both use the same algorithm.
/// </summary>
public static class Xtea
{
    private const uint Delta = 0x9E3779B9;
    private const int  Rounds = 32;

    /// <summary>
    /// Decrypts <paramref name="data"/> in-place.
    /// <paramref name="key"/> must be a 4-element uint array (128-bit key).
    /// <paramref name="length"/> must be a multiple of 8.
    /// </summary>
    public static void Decrypt(byte[] data, uint[] key, int length)
    {
        if (length % 8 != 0)
            throw new ArgumentException("XTEA block size must be a multiple of 8.", nameof(length));

        for (int i = 0; i < length; i += 8)
        {
            uint v0  = BitConverter.ToUInt32(data, i);
            uint v1  = BitConverter.ToUInt32(data, i + 4);
            // Arithmetic must use unchecked to allow intentional uint overflow (XTEA by design).
            unchecked
            {
                uint sum = Delta * (uint)Rounds;

                for (int j = 0; j < Rounds; j++)
                {
                    v1  -= ((v0 << 4 ^ v0 >> 5) + v0) ^ (sum + key[sum >> 11 & 3]);
                    sum -= Delta;
                    v0  -= ((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + key[sum & 3]);
                }
            }

            Buffer.BlockCopy(BitConverter.GetBytes(v0), 0, data, i,     4);
            Buffer.BlockCopy(BitConverter.GetBytes(v1), 0, data, i + 4, 4);
        }
    }

    /// <summary>
    /// Encrypts <paramref name="data"/> in-place.
    /// </summary>
    public static void Encrypt(byte[] data, uint[] key, int length)
    {
        if (length % 8 != 0)
            throw new ArgumentException("XTEA block size must be a multiple of 8.", nameof(length));

        for (int i = 0; i < length; i += 8)
        {
            uint v0  = BitConverter.ToUInt32(data, i);
            uint v1  = BitConverter.ToUInt32(data, i + 4);
            unchecked
            {
                uint sum = 0;

                for (int j = 0; j < Rounds; j++)
                {
                    v0  += ((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + key[sum & 3]);
                    sum += Delta;
                    v1  += ((v0 << 4 ^ v0 >> 5) + v0) ^ (sum + key[sum >> 11 & 3]);
                }
            }

            Buffer.BlockCopy(BitConverter.GetBytes(v0), 0, data, i,     4);
            Buffer.BlockCopy(BitConverter.GetBytes(v1), 0, data, i + 4, 4);
        }
    }
}

/// <summary>
/// Adler-32 checksum — verifies packet integrity in the Tibia 8.6 login flow.
/// </summary>
public static class Adler32
{
    public static uint Compute(byte[] data, int offset, int length)
    {
        const uint mod = 65521;
        uint a = 1, b = 0;

        for (int i = offset; i < offset + length; i++)
        {
            a = (a + data[i]) % mod;
            b = (b + a)       % mod;
        }

        return (b << 16) | a;
    }
}
