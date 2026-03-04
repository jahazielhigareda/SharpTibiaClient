using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

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

        /// <summary>Stub graphics manager — replaced in Phase 5 (Rendering).</summary>
        public static GraphicsDeviceManager Graphics = null!; // assigned in Initialize()

        /// <summary>Stub content manager — replaced in Phase 4 (Asset Loading).</summary>
        public static ContentManager Content = null!; // assigned in Initialize()

        public static RasterizerState Rasterizer = null!; // assigned in Initialize()

        /// <summary>
        /// The time elapsed in the game. Updated each frame from Raylib timing
        /// by Game.Run() before calling Update().
        /// </summary>
        public static GameTime GameTime = null!; // assigned each frame in Update()

        public static UIView MouseFocusedPanel = null!; // null until a panel captures mouse
        public static Boolean SkinChanged;
        public static UISkin Skin = null!;         // assigned in Load()
        public static SpriteFont StandardFont = null!; // assigned in Load()

        public static Stack<Rectangle> ScissorStack = new Stack<Rectangle>();

        public static void Initialize(GameWindow Window, GraphicsDeviceManager Graphics, ContentManager Content)
        {
            UIContext.Window = Window;
            UIContext.Graphics = Graphics;
            UIContext.Content = Content;

            Rasterizer = new RasterizerState()
            {
                ScissorTestEnable = true
            };

            // Sync window size immediately from Raylib if the window is already open.
            int w = Raylib.GetScreenWidth();
            int h = Raylib.GetScreenHeight();
            if (w > 0 && h > 0)
                SyncWindowSize(w, h);
        }

        public static void Load()
        {
            StandardFont = Content.Load<SpriteFont>("StandardFont")!; // Phase 4 provides real font
            Skin = new UISkin();
            Skin.Load(null!); // Phase 4 provides real skin stream
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
