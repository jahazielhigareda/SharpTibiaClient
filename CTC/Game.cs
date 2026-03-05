using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Main game class. Phase 3: uses a Raylib-based window and game loop.
    /// Phase 6: mouse/keyboard input read directly from Raylib each frame.
    /// </summary>
    public class Game : IDisposable
    {
        private const int DefaultWidth  = 1280;
        private const int DefaultHeight = 800;

        private GameDesktop? Desktop;

        // Phase 6: previous frame mouse state, used to detect button transitions.
        private MouseState _prevMouse = new MouseState(0, 0);

        // ------------------------------------------------------------------ //
        // Entry point                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens the Raylib window and runs the game loop until the window is closed.
        /// </summary>
        public void Run()
        {
            Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
            Raylib.InitWindow(DefaultWidth, DefaultHeight, "SharpTibiaClient");
            Raylib.SetTargetFPS(60);

            Initialize();
            LoadContent();

            while (!Raylib.WindowShouldClose())
            {
                GameTime time = new GameTime
                {
                    ElapsedGameTime = TimeSpan.FromSeconds(Raylib.GetFrameTime()),
                    TotalGameTime   = TimeSpan.FromSeconds(Raylib.GetTime()),
                };

                // Keep UIContext window dimensions in sync with the live Raylib window.
                UIContext.SyncWindowSize(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

                if (Raylib.IsWindowResized())
                    UIContext.Window.RaiseClientSizeChanged();

                // Phase 6: read Raylib input and dispatch to the UI hierarchy.
                ProcessInput();

                Update(time);

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
            UIContext.Initialize(new GameWindow());
        }

        private void LoadContent()
        {
            UIContext.Load();

            Desktop = new GameDesktop();
            Desktop.Load();
            Desktop.CreatePanels();
            Desktop.LayoutSubviews();
            Desktop.NeedsLayout = true;

            // Show the login panel so the player can connect to a live server.
            // If a local Test.tmv replay file exists it is also loaded as a fallback
            // debug view (added to the desktop alongside the login screen).
            ShowLoginPanel();

            try
            {
                LoadMovieState();
            }
            catch
            {
                // Movie file absent or corrupt — normal in a live-server deployment.
                // The login panel is already visible; silently swallow this.
            }
        }

        /// <summary>
        /// Creates a <see cref="LoginPanel"/> centred in the game window and wires up
        /// the <see cref="LoginPanel.CharacterSelected"/> event to connect to the
        /// game server via <see cref="LivePacketStream"/>.
        /// </summary>
        private void ShowLoginPanel()
        {
            var panel = new LoginPanel();
            panel.LayoutSubviews();
            Desktop?.AddLoginPanel(panel);

            panel.CharacterSelected += async (entry, xteaKey) =>
            {
                try
                {
                    Log.Debug($"[Game] Connecting to game server {entry.Ip}:{entry.Port} as '{entry.Name}'…");
                    LivePacketStream lps = await LivePacketStream.ConnectAsync(entry, entry.Name, xteaKey)
                                                                 .ConfigureAwait(false);
                    ClientState state = new ClientState(lps);

                    // The server sends PlayerLogin immediately; hook Login event
                    // to add the client to the desktop once we know the player is live.
                    state.Viewport.Login += vp =>
                    {
                        // Marshalled to the game thread — safe to call Desktop.AddClient here.
                        Desktop?.AddClient(state);
                        Desktop?.RemoveLoginPanel(panel);
                    };

                    // Kick off the first Update so the PlayerLogin packet is processed
                    // on the very next frame without waiting a full second.
                    state.Update(new GameTime());
                }
                catch (Exception ex)
                {
                    Log.Error($"[Game] Failed to connect to game server: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// Loads the debug TMV movie replay. Extracted so try/catch doesn't hide
        /// bugs in the rest of LoadContent.
        /// </summary>
        private void LoadMovieState()
        {
            // Phase 14: resolve relative to the executable directory so the path
            // works for both `dotnet run` and self-contained platform publishes.
            FileInfo file = new FileInfo(Path.Combine(AppContext.BaseDirectory, "Test.tmv"));
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
            // Phase 9: dispose the desktop, which disposes all ClientState objects,
            // which dispose their TibiaGameData, releasing all GPU textures.
            Desktop?.Dispose();
            Desktop = null;
        }

        // ------------------------------------------------------------------ //
        // Phase 6 — Input                                                     //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Phase 6: reads Raylib mouse/keyboard state and dispatches UI events.
        /// Called once per frame before Update().
        /// </summary>
        private void ProcessInput()
        {
            MouseState curr = BuildMouseState();

            // Always propagate mouse position so drag tracking works.
            Desktop?.MouseMove(curr);

            // Dispatch on left-button state transitions (both press and release).
            if (curr.LeftButton != _prevMouse.LeftButton)
                Desktop?.MouseLeftClick(curr);

            // Scroll wheel: dispatch to the UI hierarchy.
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0f)
                Desktop?.MouseScroll(curr, (int)wheel);

            _prevMouse = curr;
        }

        /// <summary>
        /// Phase 6: builds a MouseState by querying Raylib for the current
        /// mouse position and button states.
        /// </summary>
        private static MouseState BuildMouseState()
        {
            return new MouseState(
                Raylib.GetMouseX(),
                Raylib.GetMouseY(),
                Raylib.IsMouseButtonDown(MouseButton.Left) ? ButtonState.Pressed : ButtonState.Released,
                Raylib.IsMouseButtonDown(MouseButton.Right) ? ButtonState.Pressed : ButtonState.Released,
                Raylib.IsMouseButtonDown(MouseButton.Middle) ? ButtonState.Pressed : ButtonState.Released
            );
        }

        // ------------------------------------------------------------------ //
        // Update / Draw                                                        //
        // ------------------------------------------------------------------ //

        private void Update(GameTime time)
        {
            Desktop?.Update(time);
        }

        private void Draw(GameTime time)
        {
            Desktop?.Draw(UIContext.Window.ClientBounds);
        }

        public void Dispose()
        {
            // Phase 9: ensure GPU textures are released if Run() was not called
            // (or if an exception prevented UnloadContent from running).
            Desktop?.Dispose();
            Desktop = null;
        }
    }
}
