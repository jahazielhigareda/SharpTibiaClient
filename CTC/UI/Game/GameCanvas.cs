using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    public class GameCanvas : UIView, ICleanupable
    {
        public GameCanvas(ClientState State) : base(null, UIElementType.Window)
        {
            Protocol = State.Protocol;
            Viewport = State.Viewport;

            RegisterEvents();

            UpdateName();
        }

        private ClientViewport Viewport;
        private TibiaGameProtocol Protocol;
        private GameRenderer? Renderer;

        private double LastCleanup = 0;

        // Phase 5: Raylib off-screen render texture (replaces RenderTarget2D).
        private RenderTexture2D Backbuffer;

        private Dictionary<MapPosition, TileAnimations> PlayingAnimations = new Dictionary<MapPosition, TileAnimations>();


        #region Logic Code

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

            if (Backbuffer.Id == 0)
            {
                Renderer = new GameRenderer(Viewport.GameData);
                // Phase 5: allocate a 480×352 off-screen render texture via Raylib.
                Backbuffer = Raylib.LoadRenderTexture(480, 352);
            }
        }

        public override void Update(GameTime Time)
        {
            base.Update(Time);

            foreach (TileAnimations Animations in PlayingAnimations.Values)
            {
                foreach (GameEffect Effect in Animations.Effects)
                    if (!Effect.Expired)
                        Effect.Update(Time);
            }

            LastCleanup += Time.ElapsedGameTime.TotalMilliseconds;
            if (LastCleanup > 1000)
            {
                Cleanup();
                LastCleanup = 0;
            }
        }

        public void Cleanup()
        {
            List<GameEffect> ToRemove = new List<GameEffect>();
            List<MapPosition> ToRemoveAnims = new List<MapPosition>();

            foreach (KeyValuePair<MapPosition, TileAnimations> Anim in PlayingAnimations)
            {
                ToRemove.Clear();

                foreach (GameEffect Effect in Anim.Value.Effects)
                    if (Effect.Expired)
                        ToRemove.Add(Effect);
                
                foreach (GameEffect Effect in ToRemove)
                    Anim.Value.Effects.Remove(Effect);

                if (Anim.Value.Empty)
                    ToRemoveAnims.Add(Anim.Key);
            }

            foreach (MapPosition Position in ToRemoveAnims)
                PlayingAnimations.Remove(Position);
        }

        private void UpdateName() { }

        #endregion


        #region Drawing Code

        protected override void DrawBackground()
        {
            // do nothing — the game scene fills the canvas
        }

        public override void Draw(Rectangle BoundingBox)
        {
            if (!Visible)
                return;
            if (!BoundingBox.Overlaps(ScreenBounds))
                return;

            // Ensure renderer and backbuffer exist (LayoutSubviews may not have run yet)
            if (Backbuffer.Id == 0)
                LayoutSubviews();

            // 1. Render the game scene into the off-screen render texture.
            Raylib.BeginTextureMode(Backbuffer);
            Raylib.ClearBackground(Color.Black);
            Renderer!.DrawScene(UIContext.GameTime, Viewport, PlayingAnimations);
            Raylib.EndTextureMode();

            // 2. Blit the render texture to the screen inside this view's clip rect.
            Rectangle scb = ScreenClientBounds;
            Rectangle clip = GetClipRectangle();
            Raylib.BeginScissorMode(clip.X, clip.Y, clip.Width, clip.Height);

            // OpenGL render textures have Y flipped — negate height in source rect to correct it.
            Raylib.DrawTexturePro(
                Backbuffer.Texture,
                new Raylib_cs.Rectangle(0, 0, Backbuffer.Texture.Width, -Backbuffer.Texture.Height),
                new Raylib_cs.Rectangle(scb.X, scb.Y, scb.Width, scb.Height),
                Vector2.Zero, 0f, Color.White);

            // 3. Draw creature bars and animated text (foreground overlay, same coordinate space).
            Vector2 Offset = new Vector2(scb.X, scb.Y);
            Vector2 Scale  = new Vector2(Bounds.Width / 480f, Bounds.Height / 352f);
            Renderer!.DrawSceneForeground(Offset, Scale, UIContext.GameTime, Viewport, PlayingAnimations);

            // 4. Draw the window border.
            DrawBorder();

            Raylib.EndScissorMode();
        }

        #endregion


        #region Protocol Event Handlers

        private void RegisterEvents()
        {
            Protocol.PlayerLogin.Add(OnPlayerLogin);
            Protocol.Effect.Add(OnMagicEffect);
            Protocol.ShootEffect.Add(OnShootEffect);
            Protocol.AnimatedText.Add(OnAnimatedText);
        }

        private void OnPlayerLogin(Packet props)
        {
            Protocol.MapDescription.Add(OnMapDescription);
        }

        private void OnMapDescription(Packet props)
        {
            UpdateName();
            Protocol.MapDescription.Remove(OnMapDescription);
        }

        private void OnShootEffect(Packet props)
        {
            MapPosition FromPosition = (MapPosition)props["From"];
            MapPosition ToPosition = (MapPosition)props["To"];
            int Type = (int)props["Effect"];

            MapPosition Max = new MapPosition();
            Max.X = Math.Max(FromPosition.X, ToPosition.X);
            Max.Y = Math.Max(FromPosition.Y, ToPosition.Y);
            Max.Z = ToPosition.Z;

            TileAnimations? Animations = null;
            if (!PlayingAnimations.TryGetValue(Max, out Animations))
            {
                Animations = new TileAnimations();
                PlayingAnimations.Add(Max, Animations);
            }
            Animations.Effects.Add(new DistanceEffect(Viewport.GameData, Type, FromPosition, ToPosition));
        }

        private void OnMagicEffect(Packet props)
        {
            MapPosition Position = (MapPosition)props["Position"];
            MagicEffect Effect = new MagicEffect(Viewport.GameData, (int)props["Effect"]);

            TileAnimations? Animations = null;
            if (!PlayingAnimations.TryGetValue(Position, out Animations))
            {
                Animations = new TileAnimations();
                PlayingAnimations.Add(Position, Animations);
            }
            Animations.Effects.Add(Effect);
        }

        private void OnAnimatedText(Packet props)
        {
            MapPosition Position = (MapPosition)props["Position"];
            String Text = (String)props["Text"];
            int Color = (int)props["Color"];

            TileAnimations? Animations = null;
            if (!PlayingAnimations.TryGetValue(Position, out Animations))
            {
                Animations = new TileAnimations();
                PlayingAnimations.Add(Position, Animations);
            }
            Animations.Effects.Add(new AnimatedText(Text, Color));
        }

        #endregion
    }
}
