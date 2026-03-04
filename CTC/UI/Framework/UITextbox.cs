using System;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Phase 10: A single-line editable text input box.
    /// Reads character input from Raylib.GetCharPressed() and handles
    /// Backspace via Raylib.IsKeyPressed(KeyboardKey.Backspace).
    /// Styled with UIElementType.Textbox.
    /// </summary>
    public class UITextbox : UIView
    {
        /// <summary>Current text content.</summary>
        public string Text { get; set; } = "";

        /// <summary>Placeholder text shown when the field is empty and unfocused.</summary>
        public string Placeholder { get; set; } = "";

        /// <summary>When true, typed characters are replaced with '*' in the display.</summary>
        public bool IsPassword { get; set; } = false;

        /// <summary>Maximum number of characters allowed.</summary>
        public int MaxLength { get; set; } = 128;

        /// <summary>Whether this textbox currently holds keyboard focus.</summary>
        public bool IsFocused { get; private set; } = false;

        private float _caretBlink = 0f;

        // Minimum printable ASCII character value accepted as input.
        private const int MinPrintableChar = 32; // space

        // Fires whenever the text changes.
        public event Action<UITextbox>? TextChanged;

        public UITextbox(string placeholder = "")
        {
            Placeholder = placeholder;
            ElementType = UIElementType.Textbox;
            Bounds = new Rectangle(0, 0, 160, 20);
        }

        // -------------------------------------------------------------------------
        // Input
        // -------------------------------------------------------------------------

        public override bool MouseLeftClick(MouseState mouse)
        {
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                IsFocused = true;
                return true;
            }
            return false;
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            _caretBlink += (float)time.ElapsedGameTime.TotalSeconds;
            if (_caretBlink > 1.2f) _caretBlink = 0f;

            if (!IsFocused)
                return;

            // Lose focus when clicking elsewhere (handled by parent, but also guard here).
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Point m = new Point(Raylib.GetMouseX(), Raylib.GetMouseY());
                if (!ScreenBounds.Contains(m))
                {
                    IsFocused = false;
                    return;
                }
            }

            bool changed = false;

            // Character input
            int ch;
            while ((ch = Raylib.GetCharPressed()) != 0)
            {
                if (Text.Length < MaxLength && ch >= MinPrintableChar)
                {
                    Text += (char)ch;
                    changed = true;
                }
            }

            // Backspace
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && Text.Length > 0)
            {
                Text = Text.Substring(0, Text.Length - 1);
                changed = true;
            }

            if (changed)
                TextChanged?.Invoke(this);
        }

        // -------------------------------------------------------------------------
        // Drawing
        // -------------------------------------------------------------------------

        protected override void DrawContent()
        {
            string display = IsPassword ? new string('*', Text.Length) : Text;
            Color textColor = Color.LightGray;

            if (display.Length == 0 && !IsFocused && Placeholder.Length > 0)
            {
                display = Placeholder;
                textColor = new Color(120, 120, 120, 255);
            }

            // Clamp text to visible width
            Vector2 available = new Vector2(ClientBounds.Width, ClientBounds.Height);
            Vector2 measured = Raylib.MeasureTextEx(UIContext.StandardFont, display, UIContext.StandardFontSize, 1f);
            // Scroll so end of string is always visible
            while (measured.X > available.X - 6 && display.Length > 0)
            {
                display = display.Substring(1);
                measured = Raylib.MeasureTextEx(UIContext.StandardFont, display, UIContext.StandardFontSize, 1f);
            }

            Vector2 pos = ScreenCoordinate(3, (int)((ClientBounds.Height - measured.Y) / 2));
            Raylib.DrawTextEx(UIContext.StandardFont, display, pos, UIContext.StandardFontSize, 1f, textColor);

            // Blinking caret
            if (IsFocused && _caretBlink < 0.6f)
            {
                float caretX = pos.X + measured.X + 1;
                float caretY = pos.Y;
                Raylib.DrawLine((int)caretX, (int)caretY,
                                (int)caretX, (int)(caretY + UIContext.StandardFontSize),
                                Color.LightGray);
            }
        }

        // -------------------------------------------------------------------------
        // Public helpers
        // -------------------------------------------------------------------------

        /// <summary>Clear the text field.</summary>
        public void Clear()
        {
            Text = "";
            TextChanged?.Invoke(this);
        }

        /// <summary>Focus this textbox programmatically.</summary>
        public void Focus() => IsFocused = true;
    }
}
