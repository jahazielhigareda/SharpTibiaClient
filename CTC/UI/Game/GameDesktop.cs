using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    public delegate void ViewportChangedEventHandler(ClientViewport? NewViewport);

    public class GameDesktop : UIView, IDisposable
    {
        public GameDesktop()
        {
            // Store window size
            Bounds.X = 0;
            Bounds.Y = 0;
            Bounds.Width = UIContext.Window.ClientBounds.Width;
            Bounds.Height = UIContext.Window.ClientBounds.Height;

            UIContext.GameWindowSize = Bounds;

            // Listener when window changes size
            UIContext.Window.ClientSizeChanged += new EventHandler<EventArgs>(OnResize);
        }

        #region Data Members and Properties

        List<ClientState> Clients = new List<ClientState>();

        GameSidebar  Sidebar   = null!;
        ChatPanel    Chat      = null!;
        GameFrame    Frame     = null!;
        // Phase 10: hotbar at the bottom of the game area.
        HotbarPanel  Hotbar    = null!;

        protected ClientState ActiveClient
        {
            get
            {
                return _ActiveClient;
            }
            set
            {
                ActiveViewportChanged?.Invoke(value.Viewport);
                _ActiveClient = value;
            }
        }
        protected ClientState _ActiveClient = null!;

        public ClientViewport? ActiveViewport
        {
            get
            {
                if (ActiveClient != null)
                    return ActiveClient.Viewport;
                return null;
            }
        }

        Queue<long> LFPS = new Queue<long>();
        Queue<long> GFPS = new Queue<long>(); 

        #endregion

        #region Event Slots

        public event ViewportChangedEventHandler? ActiveViewportChanged;

        #endregion

        // Methods
        public void AddClient(ClientState State)
        {
            Clients.Add(State);
            ActiveClient = State;

            // Show game UI panels now that a client has entered the game.
            Frame.Visible   = true;
            Sidebar.Visible = true;
            Chat.Visible    = true;
            Hotbar.Visible  = true;

            // Read in some state (in case the game was fast-forwarded)
            foreach (ClientContainer Container in State.Viewport.Containers.Values)
                OnOpenContainer(State.Viewport, Container);

            // Hook up handlers for some events
            State.Viewport.OpenContainer += OnOpenContainer;
            State.Viewport.CloseContainer += OnCloseContainer;
            Frame.AddClient(State);
        }

        /// <summary>
        /// Adds a <see cref="LoginPanel"/> as a floating overlay on the desktop.
        /// </summary>
        public void AddLoginPanel(LoginPanel panel)
        {
            panel.ZOrder = 100; // Float above game panels
            AddSubview(panel);
            NeedsLayout = true;
        }

        /// <summary>
        /// Removes a previously added <see cref="LoginPanel"/> from the desktop.
        /// </summary>
        public void RemoveLoginPanel(LoginPanel panel)
        {
            panel.RemoveFromSuperview();
        }

        #region Event Handlers

        /// <summary>
        /// The game window was resized
        /// </summary>
        void OnResize(object? o, EventArgs args)
        {
            System.Console.WriteLine("Game Window was resized!");
            if (UIContext.Window.ClientBounds.Height > 0 && UIContext.Window.ClientBounds.Width > 0)
            {
                UIContext.GameWindowSize = UIContext.Window.ClientBounds;
                NeedsLayout = true;
            }
        }

        /// <summary>
        /// We override this to handle captured devices
        /// </summary>
        public override bool MouseLeftClick(MouseState mouse)
        {
            if (UIContext.MouseFocusedPanel != null)
                return UIContext.MouseFocusedPanel.MouseLeftClick(mouse);

            List<UIView> SubviewListCopy = new List<UIView>(Children);
            foreach (UIView subview in SubviewListCopy)
            {
                if (subview.AcceptsMouseEvent(mouse))
                    if (subview.MouseLeftClick(mouse))
                        return true;
            }

            return false;
        }

        public override bool MouseMove(MouseState mouse)
        {
            if (UIContext.MouseFocusedPanel != null)
                return UIContext.MouseFocusedPanel.MouseMove(mouse);
            return false;
        }

        /// <summary>
        /// Phase 6: dispatches scroll-wheel to the focused panel (if any) or
        /// to whichever child the mouse is currently over.
        /// </summary>
        public override bool MouseScroll(MouseState mouse, int delta)
        {
            if (UIContext.MouseFocusedPanel != null)
                return UIContext.MouseFocusedPanel.MouseScroll(mouse, delta);

            return base.MouseScroll(mouse, delta);
        }

        protected void OnOpenContainer(ClientViewport Viewport, ClientContainer Container)
        {
            ContainerPanel Panel = new ContainerPanel(Viewport, Container.ContainerID);
            Panel.Bounds.Height = 100;
            Sidebar.AddWindow(Panel);
        }

        protected void OnCloseContainer(ClientViewport Viewport, ClientContainer Container)
        {
            foreach (ContainerPanel CPanel in Sidebar.ContentView.SubviewsOfType<ContainerPanel>())
                if (CPanel.ContainerID == Container.ContainerID)
                    CPanel.RemoveFromSuperview();
        }

        #endregion

        public override void LayoutSubviews()
        {
            Bounds.Width = UIContext.GameWindowSize.Width;
            Bounds.Height = UIContext.GameWindowSize.Height;

            Sidebar.Bounds = new Rectangle
            {
                X = ClientBounds.Width - Sidebar.FullBounds.Width,
                Y = ClientBounds.Top,
                Height = ClientBounds.Height,
                Width = Sidebar.FullBounds.Width
            }.Subtract(Sidebar.Margin);

            // Phase 10: hotbar sits just above the chat panel.
            Hotbar.Bounds = new Rectangle
            {
                X = ClientBounds.Left,
                Y = ClientBounds.Height - Chat.FullBounds.Height - Hotbar.Bounds.Height - 2,
                Width = Hotbar.Bounds.Width,
                Height = Hotbar.Bounds.Height
            };

            Chat.Bounds = new Rectangle
            {
                X = ClientBounds.Top,
                Y = ClientBounds.Height - Chat.FullBounds.Height,
                Width = ClientBounds.Width - Sidebar.FullBounds.Width,
                Height = Chat.Bounds.Height
            }.Subtract(Chat.Margin);

            Frame.Bounds = new Rectangle
            {
                X = ClientBounds.Top,
                Y = ClientBounds.Left,
                Width = ClientBounds.Width - Sidebar.FullBounds.Width,
                Height = ClientBounds.Height - Chat.Bounds.Height - Hotbar.Bounds.Height - 2
            }.Subtract(Frame.Margin);

            base.LayoutSubviews();
        }

        public override void Update(GameTime Time)
        {
            UIContext.Update(Time);

            LFPS.Enqueue(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
            while (LFPS.Count > 0 && LFPS.First() < DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - 1000)
                LFPS.Dequeue();

            foreach (ClientState State in Clients)
                State.Update(Time);

            base.Update(Time);
        }

        #region Drawing Code

        /// <summary>
        /// Phase 5: Draw uses Raylib directly — no SpriteBatch or ForegroundBatch.
        /// </summary>
        public override void Draw(Rectangle BoundingBox)
        {
            // Count the FPS
            GFPS.Enqueue(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
            while (GFPS.Count > 0 && GFPS.First() < DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - 1000)
                GFPS.Dequeue();

            DrawFPS();

            // Draw UI children (each child applies its own scissor via Raylib.BeginScissorMode)
            DrawBackgroundChildren(Bounds);
            DrawForegroundChildren(Bounds);
        }

        protected void DrawFPS()
        {
            string o = "";
            o += " LFPS: " + LFPS.Count;
            o += " GFPS: " + GFPS.Count;
            o += " RCTC";

            // Measure text to right-align it
            Vector2 textSize = Raylib.MeasureTextEx(UIContext.StandardFont, o, UIContext.StandardFontSize, 1f);
            Vector2 pos = new Vector2(
                UIContext.Window.ClientBounds.Width - textSize.X - 6,
                UIContext.Window.ClientBounds.Height - textSize.Y - 4
            );

            Raylib.DrawTextEx(UIContext.StandardFont, o, pos, UIContext.StandardFontSize, 1f, Color.Lime);
        }

        #endregion


        #region Loading Code

        public void Load()
        {
            // Phase 5: ForegroundBatch removed; drawing goes directly through Raylib.
        }

        public void CreatePanels()
        {
            Frame = new GameFrame();
            Frame.Bounds.X = 10;
            Frame.Bounds.Y = 20;
            Frame.Bounds.Width = 800;
            Frame.Bounds.Height = 600;
            Frame.ZOrder = -1;
            Frame.Visible = false;
            AddSubview(Frame);

            Sidebar = new GameSidebar(this);
            Sidebar.Visible = false;
            AddSubview(Sidebar);

            Chat = new ChatPanel();
            Chat.Bounds.Height = 180;
            Chat.Visible = false;
            AddSubview(Chat);

            // Phase 10: HotbarPanel — 10 F-key slots above the chat panel.
            Hotbar = new HotbarPanel();
            Hotbar.Visible = false;
            AddSubview(Hotbar);
        }

        #endregion

        /// <summary>
        /// Phase 9: Disposes all client states (which dispose their TibiaGameData
        /// and all GPU textures held by GameImage objects).
        /// </summary>
        public void Dispose()
        {
            foreach (ClientState state in Clients)
                state.Dispose();
            Clients.Clear();
        }
    }
}
