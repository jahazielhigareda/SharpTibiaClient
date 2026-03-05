using System;

namespace CTC
{
    /// <summary>
    /// Phase 8: XTEA symmetric block cipher used to encrypt/decrypt Tibia 8.6 network packets.
    /// Operates on 8-byte (two uint32) blocks with a 128-bit (four uint32) key.
    /// 32 rounds, constant delta = 0x9E3779B9.
    /// </summary>
    public static class Xtea
    {
        private const uint Delta = 0x9E3779B9u;
        private const int Rounds = 32;

        /// <summary>
        /// Encrypts an 8-byte block in-place.
        /// </summary>
        /// <param name="v">Two-element array [v0, v1] representing the 8-byte block.</param>
        /// <param name="key">Four-element array representing the 128-bit key.</param>
        public static void EncryptBlock(uint[] v, uint[] key)
        {
            uint v0 = v[0], v1 = v[1];
            uint sum = 0;
            for (int i = 0; i < Rounds; ++i)
            {
                v0 += ((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + key[sum & 3]);
                sum += Delta;
                v1 += ((v0 << 4 ^ v0 >> 5) + v0) ^ (sum + key[sum >> 11 & 3]);
            }
            v[0] = v0;
            v[1] = v1;
        }

        /// <summary>
        /// Decrypts an 8-byte block in-place.
        /// </summary>
        /// <param name="v">Two-element array [v0, v1] representing the 8-byte block.</param>
        /// <param name="key">Four-element array representing the 128-bit key.</param>
        public static void DecryptBlock(uint[] v, uint[] key)
        {
            uint v0 = v[0], v1 = v[1];
            uint sum = unchecked(Delta * (uint)Rounds);
            for (int i = 0; i < Rounds; ++i)
            {
                v1 -= ((v0 << 4 ^ v0 >> 5) + v0) ^ (sum + key[sum >> 11 & 3]);
                sum -= Delta;
                v0 -= ((v1 << 4 ^ v1 >> 5) + v1) ^ (sum + key[sum & 3]);
            }
            v[0] = v0;
            v[1] = v1;
        }

        /// <summary>
        /// Decrypts a byte buffer in-place.  The buffer length must be a multiple of 8.
        /// </summary>
        /// <param name="data">Buffer to decrypt.</param>
        /// <param name="offset">Start offset within <paramref name="data"/>.</param>
        /// <param name="length">Number of bytes to decrypt (must be a multiple of 8).</param>
        /// <param name="key">Four-element uint32 XTEA key.</param>
        public static void Decrypt(byte[] data, int offset, int length, uint[] key)
        {
            if (length % 8 != 0)
                throw new ArgumentException("XTEA data length must be a multiple of 8.", nameof(length));

            uint[] block = new uint[2];
            for (int i = offset; i < offset + length; i += 8)
            {
                block[0] = BitConverter.ToUInt32(data, i);
                block[1] = BitConverter.ToUInt32(data, i + 4);
                DecryptBlock(block, key);
                Buffer.BlockCopy(BitConverter.GetBytes(block[0]), 0, data, i,     4);
                Buffer.BlockCopy(BitConverter.GetBytes(block[1]), 0, data, i + 4, 4);
            }
        }

        /// <summary>
        /// Encrypts a byte buffer in-place.  The buffer length must be a multiple of 8.
        /// </summary>
        /// <param name="data">Buffer to encrypt.</param>
        /// <param name="offset">Start offset within <paramref name="data"/>.</param>
        /// <param name="length">Number of bytes to encrypt (must be a multiple of 8).</param>
        /// <param name="key">Four-element uint32 XTEA key.</param>
        public static void Encrypt(byte[] data, int offset, int length, uint[] key)
        {
            if (length % 8 != 0)
                throw new ArgumentException("XTEA data length must be a multiple of 8.", nameof(length));

            uint[] block = new uint[2];
            for (int i = offset; i < offset + length; i += 8)
            {
                block[0] = BitConverter.ToUInt32(data, i);
                block[1] = BitConverter.ToUInt32(data, i + 4);
                EncryptBlock(block, key);
                Buffer.BlockCopy(BitConverter.GetBytes(block[0]), 0, data, i,     4);
                Buffer.BlockCopy(BitConverter.GetBytes(block[1]), 0, data, i + 4, 4);
            }
        }
    }
}
