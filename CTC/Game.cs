using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Main game class. Inherits from XNA Game in the legacy code — replaced in Phase 3
    /// with a Raylib-based game loop using Raylib.InitWindow / WindowShouldClose.
    /// </summary>
    public class Game : IDisposable
    {
        // Phase 3: replace with Raylib window state
        protected GraphicsDeviceManager Graphics;
        protected ContentManager Content;
        protected GameWindow Window;
        protected GraphicsDevice GraphicsDevice => Graphics.GraphicsDevice;
        protected bool IsFixedTimeStep { get; set; }
        protected bool IsMouseVisible { get; set; }

        private GameDesktop Desktop;
        private bool _lastMouseLeftPressed;

        public Game()
        {
            Window = new GameWindow();
            Content = new ContentManager(null) { RootDirectory = "Content" };
            Graphics = new GraphicsDeviceManager(this);

            // Phase 3: PrepareDevice event removed — Raylib does not require it.
            Graphics.PreferredBackBufferWidth = 1280;
            Graphics.PreferredBackBufferHeight = 800;
        }

        /// <summary>
        /// Initializes game settings before the main loop.
        /// Phase 3 will move window setup here via Raylib.InitWindow().
        /// </summary>
        protected virtual void Initialize()
        {
            // Phase 3: IsFixedTimeStep → Raylib.SetTargetFPS(0) (uncapped)
            IsFixedTimeStep = false;
            Graphics.SynchronizeWithVerticalRetrace = false;
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Graphics.ApplyChanges();
        }

        /// <summary>
        /// Loads all content. Phase 4 will replace Content.Load&lt;T&gt; with Raylib asset loading.
        /// </summary>
        protected virtual void LoadContent()
        {
            UIContext.Initialize(Window, Graphics, Content);
            UIContext.Load();

            Desktop = new GameDesktop();
            Desktop.Load();
            Desktop.CreatePanels();

            Desktop.LayoutSubviews();
            Desktop.NeedsLayout = true;

            // For debugging: read a TMV movie file as input.
            FileInfo file = new FileInfo("./Test.tmv");
            Stream virtualStream;
            FileStream fileStream = file.OpenRead();
            if (file.Extension == ".tmv")
                virtualStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
            else
                virtualStream = fileStream;

            TibiaMovieStream MovieStream = new TibiaMovieStream(virtualStream, file.Name);
            ClientState State = new ClientState(MovieStream);

            MovieStream.PlaybackSpeed = 50;
            State.ForwardTo(new TimeSpan(0, 30, 0));

            if (State.Viewport.Player == null)
            {
                State.Viewport.Login += delegate(ClientViewport Viewport)
                {
                    Desktop.AddClient(State);
                };
            }
            else
            {
                Desktop.AddClient(State);
            }

            State.Update(new GameTime());
        }

        /// <summary>
        /// Unloads content. Phase 5 will add Raylib texture/font unloading here.
        /// </summary>
        protected virtual void UnloadContent() { }

        /// <summary>
        /// Per-frame update. Phase 3 replaces GameTime with Raylib.GetFrameTime();
        /// Phase 6 replaces Mouse.GetState() with Raylib mouse API.
        /// </summary>
        protected virtual void Update(GameTime gameTime)
        {
            // Phase 6: replace with Raylib.IsMouseButtonPressed / GetMousePosition()
            Desktop?.Update(gameTime);
        }

        /// <summary>
        /// Per-frame draw. Phase 3 wraps this with Raylib.BeginDrawing/EndDrawing;
        /// Phase 5 replaces GraphicsDevice.Clear with Raylib.ClearBackground.
        /// </summary>
        protected virtual void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            Desktop?.Draw(null, Window.ClientBounds);
        }

        /// <summary>
        /// Runs the game loop. Phase 3 replaces this with a Raylib window loop.
        /// </summary>
        public void Run()
        {
            Initialize();
            LoadContent();
            // Phase 3: while (!Raylib.WindowShouldClose()) { Update(); Draw(); }
        }

        public void Dispose()
        {
            UnloadContent();
            Graphics.Dispose();
            Content.Dispose();
        }
    }
}
