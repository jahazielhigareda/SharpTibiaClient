using System;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;
using KeyboardKey = Raylib_cs.KeyboardKey;

namespace CTC
{
    /// <summary>
    /// Phase 10: 10-slot horizontal action bar mapped to F1–F10.
    /// Corresponds to the 10-slot hotbar in the otclientv8 game interface.
    ///
    /// Each slot stores a text label (spell name, item name, etc.) that the
    /// player can read; pressing the matching function key fires the
    /// <see cref="SlotActivated"/> event so callers can execute the bound action.
    /// </summary>
    public class HotbarPanel : UIView
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        public const int SlotCount = 10;

        private const int SlotSize    = 40;
        private const int SlotPadding =  2;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private readonly HotbarSlot[] _slots = new HotbarSlot[SlotCount];

        // Maps Raylib keyboard keys to slot indices (F1 → 0, …, F10 → 9)
        private static readonly KeyboardKey[] FunctionKeys = {
            KeyboardKey.F1, KeyboardKey.F2, KeyboardKey.F3, KeyboardKey.F4, KeyboardKey.F5,
            KeyboardKey.F6, KeyboardKey.F7, KeyboardKey.F8, KeyboardKey.F9, KeyboardKey.F10,
        };

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Fired when a slot is activated (by keyboard shortcut or mouse click).
        /// The int parameter is the zero-based slot index (0 = F1 … 9 = F10).
        /// </summary>
        public event Action<int>? SlotActivated;

        // -------------------------------------------------------------------------
        // Construction
        // -------------------------------------------------------------------------

        public HotbarPanel()
            : base(null, UIElementType.BorderlessWindow)
        {
            Bounds.Width  = SlotCount * (SlotSize + SlotPadding) + SlotPadding;
            Bounds.Height = SlotSize  + SlotPadding * 2;

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = new HotbarSlot(i);
                slot.Bounds = new Rectangle(
                    SlotPadding + i * (SlotSize + SlotPadding),
                    SlotPadding,
                    SlotSize,
                    SlotSize
                );
                int capturedI = i;
                slot.ButtonReleasedInside += (_, __) => SlotActivated?.Invoke(capturedI);
                _slots[i] = slot;
                AddSubview(slot);
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Set the display label for a slot (shown as a tooltip/abbreviation).</summary>
        public void SetSlotLabel(int index, string label)
        {
            if (index >= 0 && index < SlotCount)
                _slots[index].SlotLabel = label;
        }

        /// <summary>Clear the label from a slot.</summary>
        public void ClearSlot(int index)
        {
            if (index >= 0 && index < SlotCount)
                _slots[index].SlotLabel = "";
        }

        // -------------------------------------------------------------------------
        // Update — read F-key presses each frame
        // -------------------------------------------------------------------------

        public override void Update(GameTime time)
        {
            base.Update(time);

            for (int i = 0; i < SlotCount; i++)
            {
                if (Raylib.IsKeyPressed(FunctionKeys[i]))
                    SlotActivated?.Invoke(i);
            }
        }

        // -------------------------------------------------------------------------
        // Inner widget: one hotbar slot
        // -------------------------------------------------------------------------

        private sealed class HotbarSlot : UIButton
        {
            public string SlotLabel = "";
            private readonly int _index;

            // Labels for F1–F10 keys
            private static readonly string[] KeyLabels =
                { "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10" };

            public HotbarSlot(int index)
            {
                _index        = index;
                NormalType    = UIElementType.InventorySlot;
                HighlightType = UIElementType.ButtonHighlight;
            }

            protected override void DrawContent()
            {
                // Slot background
                UIContext.Skin.DrawBox(UIElementType.InventorySlot, ScreenBounds);

                // Slot content label (spell / item abbreviation)
                if (SlotLabel.Length > 0)
                {
                    // Truncate to fit
                    string display = SlotLabel;
                    while (display.Length > 1)
                    {
                        Vector2 s = Raylib.MeasureTextEx(UIContext.StandardFont, display,
                            UIContext.StandardFontSize - 1, 1f);
                        if (s.X <= ClientBounds.Width - 4)
                            break;
                        display = display.Substring(0, display.Length - 1);
                    }

                    Vector2 textSize = Raylib.MeasureTextEx(UIContext.StandardFont, display,
                        UIContext.StandardFontSize - 1, 1f);
                    Vector2 pos = ScreenCoordinate(
                        (int)((ClientBounds.Width  - textSize.X) / 2),
                        (int)((ClientBounds.Height - textSize.Y) / 2)
                    );
                    Raylib.DrawTextEx(UIContext.StandardFont, display, pos,
                        UIContext.StandardFontSize - 1, 1f, Color.White);
                }

                // Keyboard shortcut label (bottom-right corner, small)
                string keyLabel = KeyLabels[_index];
                Vector2 keySize = Raylib.MeasureTextEx(UIContext.StandardFont, keyLabel,
                    UIContext.StandardFontSize - 2, 1f);
                Raylib.DrawTextEx(UIContext.StandardFont, keyLabel,
                    ScreenCoordinate(
                        (int)(ClientBounds.Width  - keySize.X - 2),
                        (int)(ClientBounds.Height - keySize.Y - 1)
                    ),
                    UIContext.StandardFontSize - 2, 1f, new Color(180, 180, 0, 255));
            }
        }
    }
}
