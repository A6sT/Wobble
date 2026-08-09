using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Wobble.Graphics.Sprites;

namespace Wobble.Graphics.Primitives
{
    public class PrimitiveLineBatch : Sprite
    {
        /// <summary>
        ///     The list of points to be drawn.
        /// </summary>
        public List<Vector2> Points { get; set; }

        /// <summary>
        ///     The thickness of the lines.
        /// </summary>
        public float Thickness { get; set; }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        /// <param name="points"></param>
        /// <param name="thickness"></param>
        public PrimitiveLineBatch(List<Vector2> points, float thickness = 1)
        {
            Points = points;
            Thickness = thickness;
        }

        /// <inheritdoc />
        /// <summary>
        /// </summary>
        public override void DrawToSpriteBatch()
        {
            if (!Visible)
                return;

            Primitives2D.DrawPoints(GameBase.Game.SpriteBatch,
                new Vector2(RenderRectangle.X, RenderRectangle.Y), Points, Tint * Alpha, Thickness);
        }

        protected override RectangleF GetDrawBounds()
        {
            if (Points == null || Points.Count == 0)
                return base.GetDrawBounds();

            var minX = Points[0].X;
            var minY = Points[0].Y;
            var maxX = minX;
            var maxY = minY;

            for (var i = 1; i < Points.Count; i++)
            {
                minX = MathHelper.Min(minX, Points[i].X);
                minY = MathHelper.Min(minY, Points[i].Y);
                maxX = MathHelper.Max(maxX, Points[i].X);
                maxY = MathHelper.Max(maxY, Points[i].Y);
            }

            var padding = Thickness / 2f;
            return new RectangleF(RenderRectangle.X + minX - padding, RenderRectangle.Y + minY - padding,
                maxX - minX + Thickness, maxY - minY + Thickness);
        }
    }
}
