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
using RaylibFont = Raylib_cs.Font;

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
    // Texture2D — wrapper around a Raylib texture handle.
    // The Handle field is populated in Phase 5 (GameImage) or Phase 4 (UISkin).
    // -------------------------------------------------------------------------
    public class Texture2D : IDisposable
    {
        /// <summary>Phase 4/5: real Raylib texture handle.</summary>
        public RaylibTexture2D Handle;

        public Texture2D() { }

        public void Dispose()
        {
            if (Handle.Id != 0)
            {
                Raylib.UnloadTexture(Handle);
                Handle = default;
            }
        }
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
    // GameWindow — XNA OS-window handle stub.
    // Phase 3 keeps this as a data carrier whose ClientBounds is kept in sync
    // with Raylib.GetScreenWidth() / GetScreenHeight() each frame by Game.Run().
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
}
