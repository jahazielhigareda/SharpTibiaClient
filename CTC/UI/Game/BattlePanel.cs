using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Phase 10: Scrollable battle list showing all visible creatures.
    /// Corresponds to the otclientv8 <c>modules/game_battle</c> Lua module.
    ///
    /// Each row shows: creature name, a compact health bar, and skull/shield icons.
    /// Clicking a row fires <c>GameConnection.AttackAsync()</c> for that creature.
    /// </summary>
    public class BattlePanel : UIVirtualFrame
    {
        private ClientViewport? _viewport;
        private GameConnection? _connection;

        public BattlePanel()
        {
            Name = "Battle";

            Bounds.Width  = 176;
            Bounds.Height = 200;

            ((UIStackView)ContentView).StretchOtherDirection = true;
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Set the viewport whose creature list is displayed.</summary>
        public void SetViewport(ClientViewport? viewport, GameConnection? connection = null)
        {
            _viewport   = viewport;
            _connection = connection;
            RebuildList();
        }

        // -------------------------------------------------------------------------
        // List management
        // -------------------------------------------------------------------------

        private void RebuildList()
        {
            ContentView.RemoveAllSubviews();

            if (_viewport == null)
                return;

            foreach (ClientCreature creature in _viewport.Creatures.Values)
            {
                // Skip the player — only show other entities in the battle list.
                if (_viewport.Player != null && creature.ID == _viewport.Player.ID)
                    continue;

                BattleRow row = new BattleRow(creature, _connection);
                row.Bounds.Width  = ClientBounds.Width;
                row.Bounds.Height = 28;
                ContentView.AddSubview(row);
            }

            NeedsLayout = true;
        }

        // -------------------------------------------------------------------------
        // Update — refresh each frame so health bars stay current
        // -------------------------------------------------------------------------

        public override void Update(GameTime time)
        {
            // Rebuild whenever the creature roster changes.
            // A production implementation would subscribe to creature-add/remove events.
            if (_viewport != null)
                RebuildList();

            base.Update(time);
        }

        // -------------------------------------------------------------------------
        // Inner widget: one creature row
        // -------------------------------------------------------------------------

        private sealed class BattleRow : UIButton
        {
            private readonly ClientCreature  _creature;
            private readonly GameConnection? _connection;

            private const int BarHeight = 5;
            private const int BarWidth  = 110;

            public BattleRow(ClientCreature creature, GameConnection? connection)
            {
                _creature   = creature;
                _connection = connection;
                NormalType    = UIElementType.None;
                HighlightType = UIElementType.ButtonHighlight;
            }

            public override bool MouseLeftClick(MouseState mouse)
            {
                if (mouse.LeftButton == ButtonState.Released && Highlighted)
                {
                    // Fire-and-forget attack; TaskScheduler.UnobservedTaskException
                    // will surface any errors.
                    _connection?.AttackAsync(_creature.ID);
                }
                return base.MouseLeftClick(mouse);
            }

            protected override void DrawContent()
            {
                int sx = ScreenBounds.X + 4;
                int sy = ScreenBounds.Y + 4;

                // Creature name
                Raylib.DrawTextEx(UIContext.StandardFont, _creature.Name,
                    new Vector2(sx, sy),
                    UIContext.StandardFontSize, 1f, Color.White);

                // Health bar — drawn below the name
                sy += UIContext.StandardFontSize + 2;

                float hpFraction = Math.Clamp(_creature.HealthPercent, 0f, 1f);
                Color hpColor    = HealthColor(hpFraction);

                // Background (dark red)
                Raylib.DrawRectangle(sx, sy, BarWidth, BarHeight, new Color(80, 0, 0, 255));
                // Foreground (health)
                Raylib.DrawRectangle(sx, sy, (int)(BarWidth * hpFraction), BarHeight, hpColor);
                // Border
                Raylib.DrawRectangleLines(sx, sy, BarWidth, BarHeight, Color.DarkGray);

                // HP text to the right of the bar
                string hpText = $"{_creature.Health}/{_creature.MaxHealth}";
                Raylib.DrawTextEx(UIContext.StandardFont, hpText,
                    new Vector2(sx + BarWidth + 4, sy - 1),
                    UIContext.StandardFontSize - 2, 1f, Color.LightGray);
            }

            private static Color HealthColor(float fraction)
            {
                if (fraction > 0.75f) return new Color(0,   200,  0,   255);
                if (fraction > 0.50f) return new Color(180, 200,  0,   255);
                if (fraction > 0.25f) return new Color(220, 120,  0,   255);
                return                       new Color(220,  30,  30,  255);
            }
        }
    }
}
