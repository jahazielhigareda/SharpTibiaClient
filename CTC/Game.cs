using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Main game class. Phase 3: uses a Raylib-based window and game loop.
    /// Rendering (Phase 5) and input (Phase 6) still use stub implementations.
    /// </summary>
    public class Game : IDisposable
    {
        private const int DefaultWidth  = 1280;
        private const int DefaultHeight = 800;

        private GameDesktop? Desktop;

        // ------------------------------------------------------------------ //
        // Entry point                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the Raylib window and runs the game loop until the window is
        /// closed. Validates the Phase 3 checkpoint: an empty black window
        /// opens via Raylib and closes cleanly.
        /// </summary>
        public void Run()
        {
            // Phase 3: Raylib window initialization.
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(DefaultWidth, DefaultHeight, "SharpTibiaClient");
            Raylib.SetTargetFPS(60);   // Phase 3: 60 FPS cap (roadmap default); set to 0 for uncapped

            Initialize();
            LoadContent();

            while (!Raylib.WindowShouldClose())
            {
                // Build a GameTime from Raylib's high-resolution timer.
                // ElapsedGameTime and TotalGameTime drive movie playback and
                // sprite animation until Phase 6 replaces the remaining consumers.
                GameTime time = new GameTime
                {
                    ElapsedGameTime = TimeSpan.FromSeconds(Raylib.GetFrameTime()),
                    TotalGameTime   = TimeSpan.FromSeconds(Raylib.GetTime()),
                };

                // Keep UIContext window dimensions in sync with the live Raylib window.
                UIContext.SyncWindowSize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

                // Propagate resize events so GameDesktop.OnResize fires correctly.
                if (Raylib.IsWindowResized())
                    UIContext.Window.RaiseClientSizeChanged();

                Update(time);

                // Phase 3: Raylib draw block replaces GraphicsDevice.Clear + base.Draw().
                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.Black);
                Draw(time);
                Raylib.EndDrawing();
            }

            UnloadContent();
            Raylib.CloseWindow();
        }

        // ------------------------------------------------------------------ //
        // Lifecycle methods                                                    //
        // ------------------------------------------------------------------ //

        private void Initialize()
        {
            // UIContext is initialised against the now-live Raylib window so that
            // UIContext.SyncWindowSize picks up the correct initial dimensions.
            UIContext.Initialize(
                new GameWindow(),
                new GraphicsDeviceManager(null),
                new ContentManager(null) { RootDirectory = "Content" }
            );
        }

        private void LoadContent()
        {
            // Phase 4 will replace Content.Load<T> stubs with real Raylib asset loading.
            UIContext.Load();

            Desktop = new GameDesktop();
            Desktop.Load();
            Desktop.CreatePanels();
            Desktop.LayoutSubviews();
            Desktop.NeedsLayout = true;

            // The TMV / Tibia.dat / Tibia.spr files are optional for this phase —
            // the window opens cleanly even when they are absent.
            try
            {
                LoadMovieState();
            }
            catch (Exception ex)
            {
                Log.Warning($"Movie state not loaded (files may be missing): {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the debug TMV movie replay. Extracted so try/catch doesn't hide
        /// bugs in the rest of LoadContent.
        /// </summary>
        private void LoadMovieState()
        {
            FileInfo file = new FileInfo("./Test.tmv");
            Stream virtualStream;
            FileStream fileStream = file.OpenRead();
            if (file.Extension == ".tmv")
                virtualStream = new System.IO.Compression.GZipStream(
                    fileStream, System.IO.Compression.CompressionMode.Decompress);
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
                    Desktop?.AddClient(State);
                };
            }
            else
            {
                Desktop?.AddClient(State);
            }

            State.Update(new GameTime());
        }

        private void UnloadContent()
        {
            // Phase 5 will add Raylib texture/font unloading here.
        }

        /// <summary>
        /// Per-frame update. Phase 6 will replace Desktop.Update(time) with
        /// direct Raylib input reads (IsMouseButtonPressed, GetMousePosition).
        /// </summary>
        private void Update(GameTime time)
        {
            Desktop?.Update(time);
        }

        /// <summary>
        /// Per-frame draw. Phase 5 will replace the SpriteBatch stub draw calls
        /// with direct Raylib draw calls; draw is already wrapped inside
        /// BeginDrawing/EndDrawing by Run().
        /// </summary>
        private void Draw(GameTime time)
        {
            // SpriteBatch is a stub in this phase; GameDesktop.Draw uses its own batch.
            Desktop?.Draw(null!, UIContext.Window.ClientBounds);
        }

        public void Dispose()
        {
            // Run() calls UnloadContent() and CloseWindow() when the loop exits;
            // Dispose is a safety net for early termination.
        }
    }
}
