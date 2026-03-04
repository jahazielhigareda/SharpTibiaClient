// Phase 2: Compile-time stubs for XNA types that are replaced in later phases.
// Each stub is annotated with the phase that will replace it.
// DO NOT add real logic here — these exist only so the project compiles during migration.
#pragma warning disable CS0649 // stub fields intentionally left unassigned
#pragma warning disable CS0067 // stub events intentionally left unused

using System;
using System.Numerics;
using Raylib_cs;
using RaylibColor = Raylib_cs.Color;
using RaylibTexture2D = Raylib_cs.Texture2D;
using RaylibRectangle = Raylib_cs.Rectangle;

namespace CTC
{
    // -------------------------------------------------------------------------
    // Rectangle — int-coordinate rectangle that preserves the XNA API surface
    // (Left, Right, Top, Bottom, Intersects, Contains).
    // Replaces in: all code — keeps int layout math; rendering converts to
    // Raylib_cs.Rectangle at the draw call site (Phase 5).
    // -------------------------------------------------------------------------
    public struct Rectangle
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rectangle(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public int Left   => X;
        public int Top    => Y;
        public int Right  => X + Width;
        public int Bottom => Y + Height;

        public bool Intersects(Rectangle other) =>
            Left < other.Right && Right > other.Left &&
            Top < other.Bottom && Bottom > other.Top;

        public bool Contains(Point p) =>
            p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

        /// <summary>Implicit conversion to Raylib_cs.Rectangle for rendering calls.</summary>
        public static implicit operator RaylibRectangle(Rectangle r) =>
            new RaylibRectangle(r.X, r.Y, r.Width, r.Height);

        /// <summary>Explicit conversion from Raylib_cs.Rectangle (may lose sub-pixel precision).</summary>
        public static explicit operator Rectangle(RaylibRectangle r) =>
            new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);

        public override string ToString() => $"{{X:{X} Y:{Y} W:{Width} H:{Height}}}";
    }

    // -------------------------------------------------------------------------
    // Point — integer 2-D point (XNA API).
    // -------------------------------------------------------------------------
    public struct Point
    {
        public int X;
        public int Y;
        public Point(int x, int y) { X = x; Y = y; }
    }

    // -------------------------------------------------------------------------
    // Texture2D — XNA reference-type texture stub.
    // Replaced in Phase 5 (Rendering Layer) and Phase 9 (Sprite Loading).
    // -------------------------------------------------------------------------
    public class Texture2D : IDisposable
    {
        /// <summary>Raw Raylib texture handle — populated in Phase 9 (Sprite Loading).</summary>
        internal RaylibTexture2D Handle;

        // Parameterless constructor required for ContentManager.Load<Texture2D>().
        public Texture2D() { }

        // XNA-style constructor signatures kept so existing code compiles.
        public Texture2D(GraphicsDevice device, int width, int height) { }
        public Texture2D(GraphicsDevice device, int width, int height, bool mipmap, SurfaceFormat format) { }

        public void SetData<T>(T[] data) where T : struct { }

        public void Dispose() { }
    }

    // -------------------------------------------------------------------------
    // GameTime — XNA timing stub.
    // Replaced in Phase 3 (Game Loop) via Raylib.GetFrameTime().
    // -------------------------------------------------------------------------
    public class GameTime
    {
        public TimeSpan TotalGameTime  { get; set; } = TimeSpan.Zero;
        public TimeSpan ElapsedGameTime { get; set; } = TimeSpan.Zero;
        public bool IsRunningSlowly    { get; set; } = false;
    }

    // -------------------------------------------------------------------------
    // GraphicsDevice — XNA GPU device stub.
    // Replaced in Phase 3 (Game Loop) / Phase 5 (Rendering).
    // -------------------------------------------------------------------------
    public class GraphicsDevice
    {
        public Rectangle ScissorRectangle { get; set; }
        public void Clear(RaylibColor color) { }
        public void SetRenderTarget(RenderTarget2D? target) { }
    }

    // -------------------------------------------------------------------------
    // GraphicsDeviceManager — XNA display setup stub.
    // Replaced in Phase 3 (Game Loop) with Raylib.InitWindow().
    // -------------------------------------------------------------------------
    public class GraphicsDeviceManager : IDisposable
    {
        public int PreferredBackBufferWidth  { get; set; } = 1280;
        public int PreferredBackBufferHeight { get; set; } = 800;
        public bool SynchronizeWithVerticalRetrace { get; set; }
        public GraphicsDevice GraphicsDevice { get; } = new GraphicsDevice();

        public event EventHandler<PreparingDeviceSettingsEventArgs>? PreparingDeviceSettings;

        public GraphicsDeviceManager(object? game) { }
        public void ApplyChanges() { }
        public void Dispose() { }
    }

    // -------------------------------------------------------------------------
    // PreparingDeviceSettingsEventArgs — used in Game.cs PrepareDevice callback.
    // Removed in Phase 3 (Game Loop).
    // -------------------------------------------------------------------------
    public class PreparingDeviceSettingsEventArgs : EventArgs
    {
        public GraphicsDeviceInformation GraphicsDeviceInformation { get; } = new GraphicsDeviceInformation();
    }

    public class GraphicsDeviceInformation
    {
        public PresentationParameters PresentationParameters { get; } = new PresentationParameters();
    }

    public class PresentationParameters
    {
        public RenderTargetUsage RenderTargetUsage { get; set; }
    }

    // -------------------------------------------------------------------------
    // RasterizerState — XNA pipeline state stub.
    // Replaced in Phase 5 (Rendering) with Raylib's built-in scissor support.
    // -------------------------------------------------------------------------
    public class RasterizerState
    {
        public bool ScissorTestEnable { get; set; }
    }

    // -------------------------------------------------------------------------
    // SpriteSortMode — XNA rendering sort mode enum stub.
    // Removed in Phase 5 (Rendering).
    // -------------------------------------------------------------------------
    public enum SpriteSortMode { Deferred, Immediate, Texture, BackToFront, FrontToBack }

    // -------------------------------------------------------------------------
    // BlendState, SamplerState, DepthStencilState — XNA pipeline state stubs.
    // Removed in Phase 5 (Rendering).
    // -------------------------------------------------------------------------
    public class BlendState    { public static readonly BlendState AlphaBlend = new BlendState(); }
    public class SamplerState  { public static readonly SamplerState LinearClamp = new SamplerState(); }
    public class DepthStencilState { public static readonly DepthStencilState Default = new DepthStencilState(); }

    // -------------------------------------------------------------------------
    // SpriteBatch — XNA 2-D sprite renderer stub.
    // Replaced in Phase 5 (Rendering) with direct Raylib draw calls.
    // -------------------------------------------------------------------------
    public class SpriteBatch : IDisposable
    {
        public GraphicsDevice GraphicsDevice { get; } = new GraphicsDevice();

        public SpriteBatch(GraphicsDevice device) { }

        public void Begin() { }
        public void Begin(SpriteSortMode sortMode, BlendState? blend = null,
                          SamplerState? sampler = null, DepthStencilState? depth = null,
                          RasterizerState? rasterizer = null) { }
        public void End() { }

        public void Draw(Texture2D texture, Vector2 position, Rectangle sourceRectangle, RaylibColor color) { }
        public void Draw(Texture2D texture, Rectangle destinationRectangle, RaylibColor color) { }
        public void Draw(Texture2D texture, Vector2 position, RaylibColor color) { }

        public void DrawString(SpriteFont font, string text, Vector2 position, RaylibColor color) { }
        public void DrawString(SpriteFont font, string text, Vector2 position,
                               RaylibColor color, float rotation, Vector2 origin,
                               float scale, SpriteEffects effects, float layerDepth) { }

        public void Dispose() { }
    }

    // -------------------------------------------------------------------------
    // SpriteFont — XNA bitmap font stub.
    // Replaced in Phase 4 (Asset Loading) with Raylib font loading.
    // -------------------------------------------------------------------------
    public class SpriteFont
    {
        public Vector2 MeasureString(string text) => new Vector2(text.Length * 8f, 12f);
    }

    // -------------------------------------------------------------------------
    // ContentManager — XNA content pipeline stub.
    // Replaced in Phase 4 (Asset Loading) with direct Raylib asset calls.
    // -------------------------------------------------------------------------
    public class ContentManager : IDisposable
    {
        public string RootDirectory { get; set; } = "Content";

        public ContentManager(object? serviceProvider) { }

        public T? Load<T>(string assetName) where T : class => null;

        public void Dispose() { }
    }

    // -------------------------------------------------------------------------
    // GameWindow — XNA OS-window handle stub.
    // Phase 3 keeps this as a data carrier whose ClientBounds is kept in sync
    // with Raylib.GetScreenWidth() / GetScreenHeight() each frame by Game.Run().
    // Removed in Phase 3 as a dependency — the window is now a Raylib window.
    // -------------------------------------------------------------------------
    public class GameWindow
    {
        public Rectangle ClientBounds { get; set; } = new Rectangle(0, 0, 1280, 800);
        public bool AllowUserResizing { get; set; }
        public string Title { get; set; } = "SharpTibiaClient";

        public event EventHandler<EventArgs>? ClientSizeChanged;

        /// <summary>
        /// Fires the ClientSizeChanged event. Called by Game.Run() when
        /// Raylib.IsWindowResized() returns true.
        /// </summary>
        public void RaiseClientSizeChanged() =>
            ClientSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    // -------------------------------------------------------------------------
    // MouseState — XNA mouse input stub.
    // Replaced in Phase 6 (Input Handling) with Raylib.GetMousePosition() etc.
    // -------------------------------------------------------------------------
    public struct MouseState
    {
        public int X { get; }
        public int Y { get; }
        public ButtonState LeftButton  { get; }
        public ButtonState RightButton { get; }
        public ButtonState MiddleButton { get; }

        public MouseState(int x, int y, ButtonState left = ButtonState.Released,
                          ButtonState right = ButtonState.Released,
                          ButtonState middle = ButtonState.Released)
        {
            X = x; Y = y;
            LeftButton = left; RightButton = right; MiddleButton = middle;
        }
    }

    public enum ButtonState { Released, Pressed }

    // -------------------------------------------------------------------------
    // Surface / depth / target enums — XNA rendering enum stubs.
    // Removed in Phase 5 (Rendering).
    // -------------------------------------------------------------------------
    public enum SurfaceFormat  { Color, Bgr565, Bgra5551, Bgra4444, Dxt1, Dxt3, Dxt5, Rgba1010102 }
    public enum DepthFormat    { None, Depth16, Depth24, Depth24Stencil8 }
    public enum RenderTargetUsage { DiscardContents, PreserveContents, PlatformContents }
    public enum SpriteEffects  { None = 0, FlipHorizontally = 1, FlipVertically = 2 }

    // -------------------------------------------------------------------------
    // RenderTarget2D — XNA off-screen render target stub.
    // Replaced in Phase 5 (Rendering) with Raylib RenderTexture2D.
    // -------------------------------------------------------------------------
    public class RenderTarget2D : Texture2D
    {
        public RenderTarget2D(GraphicsDevice device, int width, int height, bool mipmap,
                              SurfaceFormat format, DepthFormat depthFormat,
                              int multiSampleCount, RenderTargetUsage usage)
            : base(device, width, height, mipmap, format) { }
    }
}
