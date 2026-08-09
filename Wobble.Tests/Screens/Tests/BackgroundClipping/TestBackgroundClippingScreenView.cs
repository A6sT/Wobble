using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Wobble.Assets;
using Wobble.Graphics;
using Wobble.Graphics.Sprites;
using Wobble.Graphics.Sprites.Text;
using Wobble.Graphics.UI.Debugging;
using Wobble.Input;
using Wobble.Managers;
using Wobble.Screens;
using Wobble.Window;

namespace Wobble.Tests.Screens.Tests.BackgroundClipping
{
    public sealed class TestBackgroundClippingScreenView : ScreenView
    {
        private const int GridColumnCount = 64;

        private const int GridRowCount = 36;

        private const int MovingSpriteCount = 8;

        private const int StatsRefreshTime = 100;

        private const float OverlayWidth = 420;

        private const float MinimumVisibleOverlayWidth = 80;

        private static readonly Color BackgroundColor = new Color(17, 24, 32);

        private static readonly Color OverlayColor = new Color(27, 36, 48);

        private static readonly Color EnabledColor = new Color(105, 230, 166);

        private static readonly Color DisabledColor = new Color(255, 119, 119);

        private static readonly Color[] GridColors =
        {
            new Color(34, 116, 165),
            new Color(54, 162, 139),
            new Color(111, 92, 183),
            new Color(190, 113, 61)
        };

        private Rectangle? PreviousClipRectangle { get; }

        private Rectangle? AppliedClipRectangle { get; set; }

        private Sprite Background { get; }

        private Container Grid { get; }

        private List<Container> GridColumns { get; } = new List<Container>();

        private List<Sprite> MovingSprites { get; } = new List<Sprite>();

        private Sprite Overlay { get; }

        private Sprite ClipBoundary { get; }

        private SpriteTextPlus ClippingState { get; }

        private SpriteTextPlus OverlayState { get; }

        private SpriteTextPlus AnimationState { get; }

        private List<SpriteTextPlus> StatLines { get; } = new List<SpriteTextPlus>();

        private bool ClippingEnabled { get; set; } = true;

        private bool TransparentOverlay { get; set; }

        private bool AnimateClipEdge { get; set; }

        private double StatsRefreshTimer { get; set; }

        private float LastWindowWidth { get; set; } = -1;

        private float LastWindowHeight { get; set; } = -1;

        public TestBackgroundClippingScreenView(Screen screen) : base(screen)
        {
            PreviousClipRectangle = ScreenManager.DrawClipRectangle;

            Background = new Sprite
            {
                Parent = Container,
                Alignment = Alignment.TopLeft,
                Image = WobbleAssets.WhiteBox,
                Tint = BackgroundColor,
                Pivot = Vector2.Zero
            };

            Grid = new Container
            {
                Parent = Container,
                Alignment = Alignment.TopLeft
            };

            CreateGrid();
            CreateMovingSprites();
            CreateHeader();

            Overlay = new Sprite
            {
                Parent = GameBase.Game.GlobalUserInterface,
                Alignment = Alignment.TopLeft,
                Image = WobbleAssets.WhiteBox,
                Tint = OverlayColor,
                Pivot = Vector2.Zero
            };

            ClipBoundary = new Sprite
            {
                Parent = Overlay,
                Alignment = Alignment.TopLeft,
                Image = WobbleAssets.WhiteBox,
                Width = 3,
                Tint = EnabledColor,
                Pivot = Vector2.Zero,
                UsePreviousSpriteBatchOptions = true
            };

            new SpriteTextPlus(FontManager.GetWobbleFont("inter-bold"), "GLOBAL OVERLAY", 24)
            {
                Parent = Overlay,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(24, 28),
                Tint = Color.White
            };

            new SpriteTextPlus(FontManager.GetWobbleFont("inter-regular"),
                "This panel is drawn after ScreenManager.Draw.\nIt must never be clipped with the screen.", 16)
            {
                Parent = Overlay,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(24, 66),
                Tint = Color.LightGray
            };

            ClippingState = CreateOverlayText(130);
            OverlayState = CreateOverlayText(158);
            AnimationState = CreateOverlayText(186);
            CreateStats();
            UpdateStateText();
            UpdateStatsText();
        }

        public override void Update(GameTime gameTime)
        {
            var stateChanged = false;

            if (KeyboardManager.IsUniqueKeyPress(Keys.C))
            {
                ClippingEnabled = !ClippingEnabled;
                stateChanged = true;
            }

            if (KeyboardManager.IsUniqueKeyPress(Keys.T))
            {
                TransparentOverlay = !TransparentOverlay;
                stateChanged = true;
            }

            if (KeyboardManager.IsUniqueKeyPress(Keys.A))
            {
                AnimateClipEdge = !AnimateClipEdge;
                stateChanged = true;
            }

            if (KeyboardManager.IsUniqueKeyPress(Keys.R))
            {
                ClippingEnabled = true;
                TransparentOverlay = false;
                AnimateClipEdge = false;
                stateChanged = true;
            }

            if (stateChanged)
                UpdateStateText();

            UpdateLayout(gameTime.TotalGameTime.TotalSeconds);
            UpdateMovingSprites(gameTime.TotalGameTime.TotalSeconds);

            StatsRefreshTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (StatsRefreshTimer >= StatsRefreshTime)
            {
                StatsRefreshTimer = 0;
                UpdateStatsText();
            }

            Container.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GameBase.Game.GraphicsDevice.Clear(BackgroundColor);
            Container.Draw(gameTime);
        }

        public override void Destroy()
        {
            RestoreClipRectangle();
            Overlay.Destroy();
            Container.Destroy();
        }

        private void CreateGrid()
        {
            for (var columnIndex = 0; columnIndex < GridColumnCount; columnIndex++)
            {
                var column = new Container
                {
                    Parent = Grid,
                    Alignment = Alignment.TopLeft
                };

                GridColumns.Add(column);

                for (var rowIndex = 0; rowIndex < GridRowCount; rowIndex++)
                {
                    _ = new Sprite
                    {
                        Parent = column,
                        Alignment = Alignment.TopLeft,
                        Image = WobbleAssets.WhiteBox,
                        Tint = GridColors[(columnIndex + rowIndex * 3) % GridColors.Length],
                        Pivot = Vector2.Zero,
                        UsePreviousSpriteBatchOptions = true
                    };
                }
            }
        }

        private void CreateMovingSprites()
        {
            for (var i = 0; i < MovingSpriteCount; i++)
            {
                MovingSprites.Add(new Sprite
                {
                    Parent = Container,
                    Alignment = Alignment.TopLeft,
                    Image = WobbleAssets.WhiteBox,
                    Size = new ScalableVector2(180, 18),
                    Tint = Color.White,
                    Pivot = Vector2.Zero,
                    UsePreviousSpriteBatchOptions = true
                });
            }
        }

        private void CreateHeader()
        {
            new SpriteTextPlus(FontManager.GetWobbleFont("inter-bold"), "BACKGROUND CLIPPING TEST", 26)
            {
                Parent = Container,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(30, 28),
                Tint = Color.White
            };

            new SpriteTextPlus(FontManager.GetWobbleFont("inter-regular"),
                "C: clipping  |  T: transparent overlay  |  A: animated edge  |  R: reset  |  Esc: exit", 17)
            {
                Parent = Container,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(30, 66),
                Tint = Color.White
            };

            new SpriteTextPlus(FontManager.GetWobbleFont("inter-regular"),
                "Use the transparent overlay to reveal whether the covered grid is still being drawn.", 16)
            {
                Parent = Container,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(30, 96),
                Tint = Color.LightGray
            };
        }

        private SpriteTextPlus CreateOverlayText(float y) => new SpriteTextPlus(
            FontManager.GetWobbleFont("inter-semibold"), string.Empty, 16)
        {
            Parent = Overlay,
            Alignment = Alignment.TopLeft,
            Position = new ScalableVector2(24, y),
            Tint = Color.White
        };

        private void CreateStats()
        {
            new SpriteTextPlus(FontManager.GetWobbleFont("inter-bold"), "LIVE STATS", 18)
            {
                Parent = Overlay,
                Alignment = Alignment.TopLeft,
                Position = new ScalableVector2(24, 240),
                Tint = Color.White
            };

            for (var i = 0; i < 5; i++)
            {
                StatLines.Add(new SpriteTextPlus(FontManager.GetWobbleFont("inter-regular"), string.Empty, 15)
                {
                    Parent = Overlay,
                    Alignment = Alignment.TopLeft,
                    Position = new ScalableVector2(24, 274 + i * 24),
                    Tint = Color.LightGray
                });
            }
        }

        private void UpdateLayout(double totalSeconds)
        {
            var width = WindowManager.Width;
            var height = WindowManager.Height;
            var overlayWidth = Math.Min(OverlayWidth, width * 0.45f);
            var overlayX = width - overlayWidth;

            if (Math.Abs(LastWindowWidth - width) > float.Epsilon ||
                Math.Abs(LastWindowHeight - height) > float.Epsilon)
            {
                LastWindowWidth = width;
                LastWindowHeight = height;
                ResizeScreenContent(width, height);
                Overlay.Size = new ScalableVector2(overlayWidth, height);
                ClipBoundary.Height = height;
            }

            if (AnimateClipEdge)
            {
                var animation = ((float) Math.Sin(totalSeconds * 1.25) + 1) / 2;
                overlayX = MathHelper.Lerp(width - overlayWidth, width - MinimumVisibleOverlayWidth, animation);
            }

            if (Math.Abs(Overlay.X - overlayX) > float.Epsilon)
                Overlay.X = overlayX;

            ApplyClipRectangle(overlayX, height);
        }

        private void ResizeScreenContent(float width, float height)
        {
            Container.Size = new ScalableVector2(width, height);
            Background.Size = Container.Size;
            Grid.Size = Container.Size;

            var columnWidth = width / GridColumnCount;
            var rowHeight = height / GridRowCount;

            for (var columnIndex = 0; columnIndex < GridColumns.Count; columnIndex++)
            {
                var column = GridColumns[columnIndex];
                column.Position = new ScalableVector2(columnIndex * columnWidth, 0);
                column.Size = new ScalableVector2(columnWidth, height);

                for (var rowIndex = 0; rowIndex < column.Children.Count; rowIndex++)
                {
                    var cell = column.Children[rowIndex];
                    cell.Position = new ScalableVector2(1, rowIndex * rowHeight + 1);
                    cell.Size = new ScalableVector2(Math.Max(1, columnWidth - 2), Math.Max(1, rowHeight - 2));
                }
            }
        }

        private void UpdateMovingSprites(double totalSeconds)
        {
            var travelWidth = Math.Max(0, Container.Width - MovingSprites[0].Width);
            var availableHeight = Math.Max(1, Container.Height - 180);

            for (var i = 0; i < MovingSprites.Count; i++)
            {
                var progress = ((float) Math.Sin(totalSeconds * (0.65 + i * 0.07) + i) + 1) / 2;
                MovingSprites[i].X = progress * travelWidth;
                MovingSprites[i].Y = 140 + i * availableHeight / MovingSprites.Count;
            }
        }

        private void ApplyClipRectangle(float overlayX, float height)
        {
            if (!ClippingEnabled)
            {
                RestoreClipRectangle();
                return;
            }

            var clipRectangle = new Rectangle(0, 0, (int) Math.Ceiling(overlayX), (int) Math.Ceiling(height));

            if (AppliedClipRectangle == clipRectangle && ScreenManager.DrawClipRectangle == clipRectangle)
                return;

            ScreenManager.DrawClipRectangle = clipRectangle;
            AppliedClipRectangle = clipRectangle;
        }

        private void RestoreClipRectangle()
        {
            if (AppliedClipRectangle.HasValue && ScreenManager.DrawClipRectangle == AppliedClipRectangle)
                ScreenManager.DrawClipRectangle = PreviousClipRectangle;

            AppliedClipRectangle = null;
        }

        private void UpdateStateText()
        {
            ClippingState.Text = $"C  CLIPPING: {(ClippingEnabled ? "ON" : "OFF")}";
            ClippingState.Tint = ClippingEnabled ? EnabledColor : DisabledColor;
            OverlayState.Text = $"T  OVERLAY: {(TransparentOverlay ? "TRANSPARENT" : "OPAQUE")}";
            OverlayState.Tint = TransparentOverlay ? DisabledColor : EnabledColor;
            AnimationState.Text = $"A  EDGE ANIMATION: {(AnimateClipEdge ? "ON" : "OFF")}";
            AnimationState.Tint = AnimateClipEdge ? EnabledColor : Color.LightGray;
            ClipBoundary.Tint = ClippingEnabled ? EnabledColor : DisabledColor;
            Overlay.Alpha = TransparentOverlay ? 0.55f : 1;
        }

        private void UpdateStatsText()
        {
            StatLines[0].Text = $"FPS / UPS: {PerformanceStats.FrameRate} / {PerformanceStats.UpdateRate}";
            StatLines[1].Text = $"Frame: {PerformanceStats.FrameTimeMs:0.00} ms";
            StatLines[2].Text = $"Draw: {PerformanceStats.DrawTimeMs:0.00} ms";
            StatLines[3].Text = $"Screen draw: {PerformanceStats.ScreenDrawTimeMs:0.00} ms";
            StatLines[4].Text = $"Drawn drawables: {PerformanceStats.DrawnDrawableCount}";
        }
    }
}
