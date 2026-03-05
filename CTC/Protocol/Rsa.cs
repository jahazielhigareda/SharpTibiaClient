using System;
using System.Numerics;

namespace CTC
{
    /// <summary>
    /// Phase 8: Client-side RSA encryption using the well-known OpenTibia 1024-bit public key.
    /// Implements raw modular exponentiation (no PKCS#1 wrapper) to match the server-side
    /// (mtanksl.OpenTibia.Security) decrypt that also uses raw BigInteger RSA.
    /// The first byte of any plaintext block passed to <see cref="Encrypt"/> must be 0x00 so
    /// the numeric value of the block is guaranteed to be less than the modulus n.
    /// </summary>
    public static class Rsa
    {
        // Well-known OpenTibia RSA 1024-bit public key.
        // n = p * q where p and q are the prime factors used by OT servers and the official
        // 8.6 reference client.  The modulus is computed once at class-load time.
        private static readonly BigInteger P = BigInteger.Parse(
            "14299623962416399520070177382898895550795403345466153217470516082934737582776038882967213386204600674145392845853859217990626450972452084065728686565928113");
        private static readonly BigInteger Q = BigInteger.Parse(
            "7630979195970404721891201847792002125535401292779123937207447574596692788513647179235335529307251350570728407373705564708871762033017096809910315212884101");

        private static readonly BigInteger N = P * Q;

        // Standard RSA public exponent used in Tibia 8.6 / OpenTibia.
        private static readonly BigInteger E = 65537;

        /// <summary>
        /// Encrypts a 128-byte plaintext block using the OpenTibia RSA public key.
        /// <paramref name="block"/>[0] must be <c>0x00</c> to ensure the numeric value is less than n.
        /// </summary>
        /// <param name="block">128-byte plaintext block to encrypt.</param>
        /// <returns>128-byte ciphertext.</returns>
        public static byte[] Encrypt(byte[] block)
        {
            if (block == null || block.Length != 128)
                throw new ArgumentException("RSA block must be exactly 128 bytes.", nameof(block));

            // Interpret the block as a big-endian unsigned integer.
            BigInteger m = new BigInteger(block, isUnsigned: true, isBigEndian: true);
            BigInteger c = BigInteger.ModPow(m, E, N);

            // Serialize back to big-endian, padded to exactly 128 bytes.
            byte[] raw = c.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (raw.Length == 128)
                return raw;

            byte[] result = new byte[128];
            // If raw is shorter, copy right-aligned (high bytes are zeroes).
            Buffer.BlockCopy(raw, 0, result, 128 - raw.Length, raw.Length);
            return result;
        }
    }
}
