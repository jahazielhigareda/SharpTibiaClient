using System.Numerics;
using System.Security.Cryptography;

namespace mtanksl.OpenTibia.Security;

/// <summary>
/// RSA helper used for the login handshake (Tibia 8.x).
/// Uses raw BigInteger modular exponentiation — no PKCS#1 padding — which
/// matches the scheme used by the standard 8.6 client and all OpenTibia servers.
///
/// The first byte of any plaintext block must be <c>0x00</c> so that the numeric
/// value of the block is strictly less than the modulus <c>n</c>.
/// </summary>
public static class Rsa
{
    // ── Well-known OpenTibia 1024-bit RSA prime factors ─────────────────────
    // p and q are the same prime factors used in the official Tibia 8.6 client.
    // The private exponent d is computed once at class-load time via
    //   d = modular_inverse(e, (p-1)*(q-1))
    // These values are widely published in the OpenTibia community and are safe
    // to keep in source for a development / OT server.  In a production deployment
    // load the private key from a protected key store instead.

    private static readonly BigInteger P = BigInteger.Parse(
        "14299623962416399520070177382898895550795403345466153217470516082934737582776038882967213386204600674145392845853859217990626450972452084065728686565928113");

    private static readonly BigInteger Q = BigInteger.Parse(
        "7630979195970404721891201847792002125535401292779123937207447574596692788513647179235335529307251350570728407373705564708871762033017096809910315212884101");

    private static readonly BigInteger E = 65537;

    // n = p * q  (public modulus, 1024 bits)
    private static readonly BigInteger N = P * Q;

    // d = modular_inverse(e, (p-1)*(q-1))  (private exponent)
    // Computed as: BigInteger.ModPow(E, -1, (P-1)*(Q-1))
    // Pre-computed from the above p, q, e so the class-load is instant.
    private static readonly BigInteger D = BigInteger.Parse(
        "46730330223584118622160180015036832148732986808519344675210555262940258739805766860224610646919605860206328024326703361630109888417839241959507572247284807035235569619173792292786907845791904955103601652822519121908367187885509270025388641700821735345222087940578381210879116823013776808975766851829020659073");

    /// <summary>
    /// Decrypts a 128-byte RSA-encrypted block sent by the client during the
    /// login or game-login handshake.
    ///
    /// Returns the 128-byte plaintext.  <c>plaintext[0]</c> will be <c>0x00</c>
    /// for a well-formed block.
    /// </summary>
    /// <param name="cipherBlock">128-byte ciphertext from the client.</param>
    /// <returns>128-byte plaintext.</returns>
    public static byte[] Decrypt(byte[] cipherBlock)
    {
        if (cipherBlock == null || cipherBlock.Length != 128)
            throw new ArgumentException("RSA block must be exactly 128 bytes.", nameof(cipherBlock));

        // Interpret the cipher block as a big-endian unsigned integer.
        BigInteger c = new BigInteger(cipherBlock, isUnsigned: true, isBigEndian: true);

        // Modular exponentiation: m = c^d mod n
        BigInteger m = BigInteger.ModPow(c, D, N);

        // Serialize back to big-endian, padded to exactly 128 bytes.
        byte[] raw = m.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (raw.Length == 128)
            return raw;

        byte[] result = new byte[128];
        // Right-align if shorter (high bytes are leading zeroes in big-endian).
        Buffer.BlockCopy(raw, 0, result, 128 - raw.Length, raw.Length);
        return result;
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
