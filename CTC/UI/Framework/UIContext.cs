using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;
using RaylibFont = Raylib_cs.Font;

namespace CTC
{
    public static class UIContext
    {
        /// <summary>
        /// The OS window data carrier. ClientBounds is kept in sync with Raylib
        /// each frame by Game.Run() via SyncWindowSize().
        /// </summary>
        public static GameWindow Window = null!; // assigned in Initialize()

        /// <summary>
        /// The size of the OS window the game is contained in.
        /// Updated each frame from Raylib.GetScreenWidth()/GetScreenHeight()
        /// via SyncWindowSize().
        /// </summary>
        public static Rectangle GameWindowSize;

        /// <summary>
        /// The time elapsed in the game. Updated each frame from Raylib timing
        /// by Game.Run() before calling Update().
        /// </summary>
        public static GameTime GameTime = null!; // assigned each frame in Update()

        public static UIView? MouseFocusedPanel = null; // null until a panel captures mouse
        public static Boolean SkinChanged;
        public static UISkin Skin = null!; // assigned in Load()

        /// <summary>
        /// Phase 4: real Raylib font replacing the XNA SpriteFont.
        /// Loaded from Content/StandardFont.ttf in Load(); falls back to
        /// Raylib.GetFontDefault() if the file is missing.
        /// </summary>
        public static RaylibFont StandardFont; // default(Font) until Load() runs

        /// <summary>
        /// Point size passed to Raylib.LoadFontEx() and Raylib.MeasureTextEx().
        /// Approximates the original Tahoma Bold 8 pt at 96 DPI.
        /// </summary>
        public const int StandardFontSize = 12;

        public static Stack<Rectangle> ScissorStack = new Stack<Rectangle>();

        public static void Initialize(GameWindow Window)
        {
            UIContext.Window = Window;

            // Sync window size immediately from Raylib if the window is already open.
            int w = Raylib.GetScreenWidth();
            int h = Raylib.GetScreenHeight();
            if (w > 0 && h > 0)
                SyncWindowSize(w, h);
        }

        public static void Load()
        {
            // Phase 4: load the TTF font via Raylib instead of XNA Content.Load<SpriteFont>.
            // Phase 14: use Path.Combine + AppContext.BaseDirectory for cross-platform safety.
            string fontPath = Path.Combine(AppContext.BaseDirectory, "Content", "StandardFont.ttf");
            StandardFont = Raylib.LoadFontEx(fontPath, StandardFontSize, null, 0);
            if (!Raylib.IsFontReady(StandardFont))
            {
                Log.Warning($"StandardFont not found at '{fontPath}'; using Raylib default font.");
                StandardFont = Raylib.GetFontDefault();
            }

            Skin = new UISkin();
            Skin.Load(); // Phase 4: skin loaded via Raylib — no Stream parameter needed
            SkinChanged = true;
        }

        public static void Update(GameTime Time)
        {
            GameTime = Time;
        }

        /// <summary>
        /// Synchronizes UIContext.Window.ClientBounds and UIContext.GameWindowSize
        /// with the live Raylib screen dimensions. Called once per frame by Game.Run().
        /// </summary>
        public static void SyncWindowSize(int width, int height)
        {
            var bounds = new Rectangle(0, 0, width, height);
            Window.ClientBounds = bounds;
            GameWindowSize = bounds;
        }
    }
}
