using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using Raylib_cs;
using Color = Raylib_cs.Color;
using PixelFormat = Raylib_cs.PixelFormat;

namespace CTC
{
    /// <summary>
    /// Phase 9: implements IDisposable so every GPU texture created via
    /// <see cref="GetTexture"/> is released with <c>Raylib.UnloadTexture()</c>
    /// when the image is no longer needed.
    /// </summary>
    public class GameImage : IDisposable
    {
        public int ID;
        private TibiaGameData GameData;
        private Byte[]? Dump;

        // Phase 5: Raylib texture handle, lazily loaded from RGBA bytes.
        private Raylib_cs.Texture2D _handle;
        private bool _textureLoaded = false;
        private bool _disposed = false;

        public GameImage(TibiaGameData GameData, int ID)
        {
            this.GameData = GameData;
            this.ID = ID;
        }

        /// <summary>
        /// Phase 9: Releases the GPU texture if one was loaded.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_textureLoaded)
            {
                Raylib.UnloadTexture(_handle);
                _handle = default;
                _textureLoaded = false;
            }
        }

        /// <summary>
        /// Returns the Raylib texture handle for this sprite image,
        /// loading it from raw RGBA bytes on first call (lazy init).
        /// Requires AllowUnsafeBlocks because the Raylib Image struct
        /// holds a void* to pixel data.
        /// </summary>
        public Raylib_cs.Texture2D GetTexture()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameImage), $"Sprite #{ID} has already been disposed.");
            if (_textureLoaded)
                return _handle;

            byte[] rgba = LoadRGBA();

            // Pin the managed byte array so the GC won't move it while Raylib reads it.
            var pin = GCHandle.Alloc(rgba, GCHandleType.Pinned);
            try
            {
                unsafe
                {
                    Raylib_cs.Image img = new Raylib_cs.Image
                    {
                        Data    = (void*)pin.AddrOfPinnedObject(),
                        Width   = 32,
                        Height  = 32,
                        Mipmaps = 1,
                        Format  = PixelFormat.UncompressedR8G8B8A8
                    };
                    _handle = Raylib.LoadTextureFromImage(img);
                }
            }
            finally
            {
                pin.Free();
            }

            _textureLoaded = true;
            return _handle;
        }

        public Byte[] LoadRGBA()
        {
            if (Dump == null)
                Dump = GameData.LoadSpriteDump(ID);

            Byte[] rgba32x32 = new Byte[32 * 32 * 4];
            /* SPR dump format
             *  The spr format contains chunks, a chunk can either be transparent or contain pixel data.
             *  First 2 bytes (unsigned short) are read, which tells us how long the chunk is. One
             * chunk can stretch several rows in the outputted image, for example, if the chunk is 400
             * pixels long. We will have to wrap it over 14 rows in the image.
             *  If the chunk is transparent. Set that many pixels to be transparent.
             *  If the chunk is pixel data, read from the cursor that many pixels. One pixel is 3 bytes in
             * in RGB aligned data (eg. char R, char B, char G) so if the unsigned short says 20, we
             * read 20*3 = 60 bytes.
             *  Once we read one chunk, we switch to the other type of chunk (if we've just read a transparent
             * chunk, we read a pixel chunk and vice versa). And then start over again.
             *  All sprites start with a transparent chunk.
             */

            int bytes = 0;
            int x = 0;
            int y = 0;
            int chunk_size;

            while (bytes < Dump.Length && y < 32)
            {
                chunk_size = Dump[bytes] | Dump[bytes + 1] << 8;
                bytes += 2;

                for (int i = 0; i < chunk_size; ++i)
                {
                    rgba32x32[128 * y + x * 4 + 3] = 0x00; // Transparent pixel
                    x++;
                    if (x >= 32) {
                        x = 0;
                        y++;
                        if (y >= 32) break;
                    }
                }

                if (bytes >= Dump.Length || y >= 32)
                    break; // We're done
                // Now comes a pixel chunk, read it!
                chunk_size = Dump[bytes] | Dump[bytes + 1] << 8;
                bytes += 2;
                for (int i = 0; i < chunk_size; ++i)
                {
                    rgba32x32[128 * y + x * 4 + 0] = Dump[bytes + 0]; // Red
                    rgba32x32[128 * y + x * 4 + 1] = Dump[bytes + 1]; // Green
                    rgba32x32[128 * y + x * 4 + 2] = Dump[bytes + 2]; // Blue
                    rgba32x32[128 * y + x * 4 + 3] = 0xFF;             // Opaque pixel

                    bytes += 3;

                    x++;
                    if (x >= 32) {
                        x = 0;
                        y++;
                        if (y >= 32) break;
                    }
                }
            }

            // Fill up any trailing pixels
            while (y < 32 && x < 32) {
                rgba32x32[128 * y + x * 4 + 3] = 0x00; // Transparent pixel
                x++;
                if (x >= 32) {
                    x = 0;
                    y++;
                }
            }

            return rgba32x32;
        }
    }
}
