using System;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Phase 10: 2D overhead minimap panel.
    /// Corresponds to the otclientv8 <c>modules/game_minimap</c> Lua module.
    ///
    /// Renders a colour-coded overhead map around the player's current position.
    /// Each map cell is drawn as a single pixel (scaled to fill the panel).
    /// A white dot marks the player's position.
    /// </summary>
    public class MinimapPanel : UIVirtualFrame
    {
        private ClientViewport? _viewport;

        // Radius (in tiles) rendered on each axis.
        private const int RadiusX = 48;
        private const int RadiusY = 32;

        public MinimapPanel()
        {
            Name = "Minimap";

            // Fixed size — big enough to be readable; can be resized by the user by dragging.
            Bounds.Width  = 178;
            Bounds.Height = 140;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Attach or replace the viewport whose map is rendered.</summary>
        public void SetViewport(ClientViewport? viewport)
        {
            _viewport = viewport;
        }

        // -------------------------------------------------------------------------
        // Drawing
        // -------------------------------------------------------------------------

        protected override void DrawContent()
        {
            if (_viewport == null || _viewport.Player == null)
                return;

            Rectangle cb = ScreenClientBounds;
            int panelW   = cb.Width;
            int panelH   = cb.Height;

            if (panelW <= 0 || panelH <= 0)
                return;

            MapPosition center = _viewport.ViewPosition;

            // Cell size in pixels — fill the panel evenly
            float cellW = (float)panelW / (RadiusX * 2 + 1);
            float cellH = (float)panelH / (RadiusY * 2 + 1);

            for (int dy = -RadiusY; dy <= RadiusY; dy++)
            {
                for (int dx = -RadiusX; dx <= RadiusX; dx++)
                {
                    MapPosition pos = new MapPosition(
                        center.X + dx,
                        center.Y + dy,
                        center.Z
                    );

                    ClientTile? tile = _viewport.Map[pos];
                    Color color = TileColor(tile);

                    float px = cb.X + (dx + RadiusX) * cellW;
                    float py = cb.Y + (dy + RadiusY) * cellH;

                    int iw = Math.Max(1, (int)cellW);
                    int ih = Math.Max(1, (int)cellH);

                    Raylib.DrawRectangle((int)px, (int)py, iw, ih, color);
                }
            }

            // Player dot — center of the panel
            int playerPx = cb.X + (int)(RadiusX * cellW);
            int playerPy = cb.Y + (int)(RadiusY * cellH);
            int dotR     = Math.Max(2, (int)(cellW * 1.5f));

            Raylib.DrawCircle(playerPx + dotR / 2,
                              playerPy + dotR / 2,
                              dotR, Color.White);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>Maps a tile's ground item to a representative minimap colour.</summary>
        private static Color TileColor(ClientTile? tile)
        {
            if (tile == null || tile.Ground == null)
                return new Color(20, 20, 20, 255); // void / unseen

            // Very coarse approximation: use the item type ID to pick a palette entry.
            // A real client would store minimap colours per-tile in a cache file.
            int id = tile.Ground.ID;

            // Water-ish (IDs typically 4608–4700 in 8.6)
            if (id >= 4608 && id <= 4700)
                return new Color(0, 80, 180, 255);

            // Default: grass green for walkable ground
            if (tile.Ground.Type != null && tile.Ground.Type.IsGround)
                return new Color(50, 120, 50, 255);

            // Fallback: dark grey (unknown)
            return new Color(80, 80, 80, 255);
        }

        // Override so we don't draw the scroll content inside the frame.
        // The minimap renders everything in DrawContent().
        protected override void DrawBackground() { }
    }
}
