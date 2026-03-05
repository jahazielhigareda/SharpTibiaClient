using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    class ItemButton : UIButton
    {
        public GameRenderer? Renderer;
        public ClientItem? Item;

        public ItemButton(GameRenderer? Renderer, ClientItem? Item)
        {
            this.Item = Item;
            this.Renderer = Renderer;
            this.Padding = new Margin
            {
                Top = -1,
                Right = 1,
                Bottom = 1,
                Left = -1
            };

            Bounds.Width = 34;
            Bounds.Height = 34;

            NormalType = UIElementType.InventorySlot;
            HighlightType = UIElementType.InventorySlot;
        }

        public override bool MouseLeftClick(MouseState mouse)
        {
            return base.MouseLeftClick(mouse);
        }

        protected override void DrawContent()
        {
            Renderer?.DrawInventorySlot(ScreenBounds);

            if (Item != null)
                Renderer?.DrawInventoryItem(Item, ScreenClientBounds);
        }
    }

    /// <summary>
    /// Renders an inventory slot.
    /// This is different from the above in that it reads the Viewport to get
    /// its data, and it also draws a background image if there is no item
    /// in that slot.
    /// </summary>
    class InventoryItemButton : ItemButton
    {
        protected ClientViewport? Viewport;
        protected InventorySlot Slot = InventorySlot.None;

        public InventoryItemButton(ClientViewport? Viewport, InventorySlot Slot)
            : base(null, null)
        {
            this.Viewport = Viewport;
            this.Slot = Slot;
        }

        public void ViewportChanged(ClientViewport? NewViewport)
        {
            Viewport = NewViewport;
        }

        public override void Draw(Rectangle BoundingBox)
        {
            // Phase 5: lazily create renderer on first draw (replaces BeginDraw override).
            if (Renderer == null && Viewport != null)
                this.Renderer = new GameRenderer(Viewport.GameData);

            base.Draw(BoundingBox);
        }

        protected override void DrawContent()
        {
            if (Viewport != null)
            {
                Item = Viewport.Inventory[(int)Slot];

                base.DrawContent();

                if (Item == null)
                {
                    // TODO: Draw the background image for empty slot
                }
            }
        }
    }
}
