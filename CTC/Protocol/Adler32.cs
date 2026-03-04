using System;

namespace CTC
{
    /// <summary>
    /// Phase 8: Adler-32 checksum algorithm used for Tibia 8.6 packet integrity verification.
    /// The server prepends a 4-byte Adler-32 checksum to every encrypted game packet;
    /// the client verifies it after XTEA decryption.
    /// </summary>
    public static class Adler32
    {
        private const uint Modulus = 65521u;

        /// <summary>
        /// Computes the Adler-32 checksum over <paramref name="length"/> bytes of
        /// <paramref name="data"/> starting at <paramref name="offset"/>.
        /// </summary>
        public static uint Compute(byte[] data, int offset, int length)
        {
            uint a = 1, b = 0;
            for (int i = offset; i < offset + length; ++i)
            {
                a = (a + data[i]) % Modulus;
                b = (b + a) % Modulus;
            }
            return (b << 16) | a;
        }

        /// <summary>
        /// Convenience overload: computes Adler-32 over the entire array.
        /// </summary>
        public static uint Compute(byte[] data) => Compute(data, 0, data.Length);
    }
}
