using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    public class TileAnimations
    {
        public List<GameEffect> Effects = new List<GameEffect>();

        public Boolean Empty
        {
            get
            {
                return Effects.Count == 0;
            }
        }
    }

    class GameRenderer
    {
        TibiaGameData GameData;

        public GameRenderer(TibiaGameData GameData)
        {
            this.GameData = GameData;
        }

        #region Drawing Code

        public Color MakeColor(int id)
        {
            int b = (int)((id % 6) / 5f * 255);
            int g = (int)(((id / 6) % 6) / 5f * 255);
            int r = (int)((id / 36f) / 6f * 255);
            return new Color(r, g, b, 255);
        }

        /// <summary>Phase 5: draw a 32×32 game sprite image into dest using Raylib.</summary>
        private void DrawImage(GameImage Image, Rectangle dest, Color clr)
        {
            Raylib_cs.Texture2D tex = Image.GetTexture();
            Raylib.DrawTexturePro(
                tex,
                new Raylib_cs.Rectangle(0, 0, 32, 32),
                dest,
                Vector2.Zero, 0f, clr);
        }

        public void DrawSprite(GameTime Time, ClientTile? Tile, GameSprite? Sprite, int SubType, int Frame, Vector2 Position, Color clr)
        {
            Vector2 tmp = Position;
            DrawSprite(Time, Tile, Sprite, SubType, Frame, ref tmp, clr);
        }

        public void DrawSprite(GameTime Time, ClientTile? Tile, GameSprite? Sprite, int SubType, int Frame, ref Vector2 Position, Color clr)
        {
            if (Sprite == null)
                return;

            int xdiv = 0, ydiv = 0, zdiv = 0;

            if (Tile != null)
            {
                MapPosition mPos = Tile.Position;
                xdiv = mPos.X % Sprite.XDiv;
                ydiv = mPos.Y % Sprite.YDiv;
                zdiv = mPos.Z % Sprite.ZDiv;
            }

            if (Sprite.IsStackable)
            {
                if (SubType <= 1)
                    SubType = 0;
                else if (SubType <= 2)
                    SubType = 1;
                else if (SubType <= 3)
                    SubType = 2;
                else if (SubType <= 4)
                    SubType = 3;
                else if (SubType < 10)
                    SubType = 4;
                else if (SubType < 25)
                    SubType = 5;
                else if (SubType < 50)
                    SubType = 6;
                else
                    SubType = 7;
            }

            Vector2 Offset = Position;

            Offset.X += Sprite.RenderOffset;
            Offset.Y += Sprite.RenderOffset;

            if (Frame == -1)
                Frame = (int)(Time.TotalGameTime.TotalMilliseconds / Sprite.AnimationSpeed);

            for (int cx = 0; cx != Sprite.Width; cx++)
            {
                for (int cy = 0; cy != Sprite.Height; cy++)
                {
                    for (int cf = 0; cf != Sprite.BlendFrames; cf++)
                    {
                        GameImage Image = Sprite.GetImage(
                            cx, cy, cf,
                            SubType,
                            xdiv,
                            ydiv,
                            zdiv,
                            Frame
                        );

                        Rectangle rect = new Rectangle((int)Offset.X - 32 * cx, (int)Offset.Y - 32 * cy, 32, 32);
                        DrawImage(Image, rect, clr);
                    }
                }
            }

            if (Sprite.RenderHeight > 0)
            {
                Position.X -= Sprite.RenderHeight;
                Position.Y -= Sprite.RenderHeight;
            }
        }

        public void DrawInventoryItem(ClientItem Item, Rectangle rect)
        {
            if (Item.Sprite == null)
                return;

            DrawSprite(UIContext.GameTime, null, Item.Sprite, Item.Subtype, 0, new Vector2(rect.X, rect.Y), Color.White);

            if (Item.Type.IsStackable)
            {
                String count = Item.Subtype.ToString();
                Vector2 textSize = Raylib.MeasureTextEx(UIContext.StandardFont, count, UIContext.StandardFontSize, 1f);
                DrawBoldedText(count, new Vector2(rect.X + 32 - textSize.X - 1, rect.Y + 32 - textSize.Y + 1), false, Color.LightGray);
            }
        }

        public void DrawInventorySlot(Rectangle rect)
        {
            UIContext.Skin.DrawBox(UIElementType.InventorySlot, rect);
        }

        public void DrawCreature(GameTime Time, ClientCreature Creature, Vector2 Offset, Color clr)
        {
            if (Creature.Outfit.LookItem != 0)
            {
                DrawSprite(Time, null, GameData.GetItemSprite(Creature.Outfit.LookItem), 1, -1, Offset, clr);
            }
            else if (Creature.Outfit.LookType != 0)
            {
                GameSprite? Sprite = GameData.GetCreatureSprite(Creature.Outfit.LookType);

                if (Sprite != null)
                {
                    Offset.X += Sprite.RenderOffset;
                    Offset.Y += Sprite.RenderOffset;
                }
                if (Sprite != null)
                {
                    for (int cx = 0; cx != Sprite.Width; ++cx)
                    {
                        for (int cy = 0; cy != Sprite.Height; ++cy)
                        {
                            GameImage Image = Sprite.GetImage(
                                cx, cy,
                                Creature.Direction, Creature.Outfit,
                                (int)(Time.TotalGameTime.TotalMilliseconds / Sprite.AnimationSpeed)
                            );

                            Rectangle rect = new Rectangle((int)Offset.X - 32 * cx, (int)Offset.Y - 32 * cy, 32, 32);
                            DrawImage(Image, rect, clr);
                        }
                    }
                }
            }
        }

        public void DrawText(String Text, Vector2 Offset, Color Color)
        {
            Offset.X = (int)Offset.X;
            Offset.Y = (int)Offset.Y;

            Raylib.DrawTextEx(
                UIContext.StandardFont, Text, Offset,
                UIContext.StandardFontSize, 1f, Color);
        }

        public void DrawBoldedText(String Text, Vector2 Offset, Boolean Centered, Color Primary)
        {
            Offset.X = (int)Offset.X;
            Offset.Y = (int)Offset.Y;

            if (Centered)
            {
                Vector2 TextSize = Raylib.MeasureTextEx(UIContext.StandardFont, Text, UIContext.StandardFontSize, 1f);
                Offset.X -= (int)(TextSize.X / 2);
            }
            DrawText(Text, new Vector2(Offset.X + 1, Offset.Y), Color.Black);
            DrawText(Text, new Vector2(Offset.X - 1, Offset.Y), Color.Black);
            DrawText(Text, new Vector2(Offset.X, Offset.Y + 1), Color.Black);
            DrawText(Text, new Vector2(Offset.X, Offset.Y - 1), Color.Black);
            DrawText(Text, new Vector2(Offset.X, Offset.Y), Primary);
        }

        public Color LifeColorForCreature(ClientCreature Creature)
        {
            ColorGradient? cg = UIContext.Skin.Gradient("Health");
            if (cg != null)
                return cg.Sample(Creature.HealthPercent);
            return new Color(255, 255, 255, 255);
        }

        public void DrawCreatureBars(ClientCreature Creature, Vector2 Offset)
        {
            if (Creature.Name != "")
            {
                GameSprite? Sprite = GameData.GetCreatureSprite(Creature.Outfit.LookType);
                Vector2 TextSize = Raylib.MeasureTextEx(UIContext.StandardFont, Creature.Name, UIContext.StandardFontSize, 1f);
                Color LifeColor = LifeColorForCreature(Creature);
                
                // Put at the center of the sprite
                Offset.X += (Sprite?.Width ?? 1) * 16;
                Offset.Y -= (Sprite?.Height ?? 1) * 16;
                
                // Render offsets are negative
                Offset.X += Sprite?.RenderOffset ?? 0;
                Offset.Y += Sprite?.RenderOffset ?? 0;

                // Render the text
                Vector2 TextOffset = Offset;
                // Move it above the health bar
                TextOffset.X = (int)(TextOffset.X - TextSize.X / 2);
                TextOffset.Y -= 16;
                DrawBoldedText(Creature.Name, TextOffset, false, LifeColor);

                // 
                Rectangle BlackBar = new Rectangle(
                    (int)(Offset.X - 14),
                    (int)Offset.Y,
                    28, 4
                );
                UIContext.Skin.DrawBorderedRectangle(BlackBar, Color.Black);

                Rectangle InsideBar = BlackBar.Subtract(new Margin(1));
                if (Creature.MaxHealth > 0)
                    InsideBar.Width = InsideBar.Width * Creature.Health / Creature.MaxHealth;
                UIContext.Skin.DrawBorderedRectangle(InsideBar, LifeColor);
            }
        }

        public void DrawTile(GameTime Time, Vector2 Position, ClientTile? Tile, TileAnimations? Animations)
        {
            if (Tile == null)
                return;

            // Draw ground
            if (Tile.Ground != null)
                DrawSprite(Time, Tile, Tile.Ground.Sprite, Tile.Ground.Subtype, -1, ref Position, Color.White);

            foreach (ClientThing Thing in Tile.ObjectsByDrawOrder)
            {
                if (Thing is ClientCreature)
                    DrawCreature(Time, (ClientCreature)Thing, Position, Color.White);
                else
                {
                    ClientItem Item = (ClientItem)Thing;
                    DrawSprite(Time, Tile, Item.Sprite, Item.Subtype, -1, ref Position, Color.White);
                }
            }

            if (Animations != null)
            {
                foreach (GameEffect Effect in Animations.Effects)
                {
                    if (!Effect.Expired)
                    {
                        if (Effect is MagicEffect)
                        {
                            MagicEffect Magic = (MagicEffect)Effect;
                            DrawSprite(Time, Tile, Magic.Sprite, -1, Magic.Frame, Position, Color.White);
                        }
                        else if (Effect is DistanceEffect)
                        {
                            DistanceEffect Distance = (DistanceEffect)Effect;
                            DrawSprite(Time, Tile, Distance.Sprite, Distance.Frame, 0, Position + Distance.Offset, Color.White);
                        }
                    }
                }
            }
        }

        public void DrawTileForeground(GameTime Time, Vector2 Offset, ClientTile? Tile, TileAnimations? Animations)
        {
            if (Tile == null)
                return;

            foreach (ClientThing Thing in Tile.Objects)
            {
                if (Thing is ClientCreature)
                    DrawCreatureBars((ClientCreature)Thing, Offset);
            }

            if (Animations != null)
            {
                foreach (GameEffect Effect in Animations.Effects)
                {
                    if (!Effect.Expired && Effect is AnimatedText)
                    {
                        AnimatedText Text = (AnimatedText)Effect;
                        Vector2 DrawOffset = Offset + Text.Offset;
                        DrawBoldedText(Text.Text, DrawOffset, true, MakeColor(Text.Color));
                    }
                }
            }
        }

        public void DrawSceneForeground(Vector2 ScreenOffset, Vector2 Scale, GameTime Time, ClientViewport Viewport, Dictionary<MapPosition, TileAnimations>? PlayingAnimations = null)
        {
            MapPosition Center = Viewport.ViewPosition;

            Vector2 TopLeft = new Vector2(
                -(Center.X - 7) * 32,
                -(Center.Y - 5) * 32
            );

            int StartZ = 7;
            int EndZ = Center.Z;
            if (Center.Z <= 7)
            {
                Center.Y -= (7 - Center.Z);
                Center.X -= (7 - Center.Z);
            }
            else
            {
                StartZ = Math.Min(Center.Z + 2, 15);
                EndZ = Center.Z;
            }

            for (int X = Center.X - 8; X <= Center.X + 8; ++X)
            {
                for (int Y = Center.Y - 6; Y <= Center.Y + 6; ++Y)
                {
                    ClientTile? Tile = Viewport.Map[new MapPosition(X, Y, Center.Z)];

                    Vector2 DrawOffset = new Vector2(32 * X + TopLeft.X, 32 * Y + TopLeft.Y);
                    DrawOffset *= Scale;

                    TileAnimations? Animations = null;
                    if (PlayingAnimations != null && Tile != null)
                        PlayingAnimations.TryGetValue(Tile.Position, out Animations);
                    DrawTileForeground(Time, ScreenOffset + DrawOffset, Tile, Animations);
                }
            }
        }

        public void DrawScene(GameTime Time, ClientViewport Viewport, Dictionary<MapPosition, TileAnimations>? PlayingAnimations = null)
        {
            MapPosition Center = Viewport.ViewPosition;

            int StartZ = 7;
            int EndZ = Center.Z;
            if (Center.Z <= 7)
            {
                Center.Y -= (7 - Center.Z);
                Center.X -= (7 - Center.Z);
            }
            else
            {
                StartZ = Math.Min(Center.Z + 2, 15);
                EndZ = Center.Z;
                Center.X -= 2;
                Center.Y -= 2;
            }

            Vector2 TopLeft = new Vector2(
                -(Center.X - 7) * 32,
                -(Center.Y - 5) * 32
            );

            for (int Z = StartZ; Z >= EndZ; --Z)
            {
                for (int X = Center.X - 8; X <= Center.X + 9; ++X)
                {
                    for (int Y = Center.Y - 6; Y <= Center.Y + 7; ++Y)
                    {
                        ClientTile? Tile = Viewport.Map[new MapPosition(X, Y, Z)];

                        Vector2 pos = new Vector2(32 * X + TopLeft.X, 32 * Y + TopLeft.Y);
                        
                        TileAnimations? Animations = null;
                        if (PlayingAnimations != null && Tile != null)
                            PlayingAnimations.TryGetValue(Tile.Position, out Animations);
                        DrawTile(Time, pos, Tile, Animations);
                    }
                }
                TopLeft -= new Vector2(32, 32);
            }
        }

        #endregion
    }
}
