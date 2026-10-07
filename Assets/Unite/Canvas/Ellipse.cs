using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Draws a circle. The size parameter is the diameter, matching p5.js.
        /// </summary>
        public void Circle(float x, float y, float diameter)
            => Circle(x, y, 0f, diameter);

        public void Circle(Vector2 position, float diameter)
            => Circle(position.x, position.y, 0f, diameter);

        public void Circle(float x, float y, float z, float diameter)
        {
            var (center, size) = ResolveShapeMode(
                new Vector3(x, y, z),
                Vector2.one * diameter,
                EllipseMode
            );
            EnqueueCenteredShape(Shape.Circle, center, size);
        }

        public void Circle(Vector3 position, float diameter)
            => Circle(position.x, position.y, position.z, diameter);

        public void Ellipse(float x, float y, float width)
            => Ellipse(x, y, 0f, width, width);

        public void Ellipse(float x, float y, float width, float height)
            => Ellipse(x, y, 0f, width, height);

        public void Ellipse(Vector2 position, Vector2 size)
            => Ellipse(position.x, position.y, 0f, size.x, size.y);

        public void Ellipse(float x, float y, float z, float width, float height)
        {
            var (center, size) = ResolveShapeMode(
                new Vector3(x, y, z),
                new Vector2(width, height),
                EllipseMode
            );
            EnqueueCenteredShape(Shape.Ellipse, center, size);
        }

        public void Ellipse(Vector3 position, Vector2 size)
            => Ellipse(position.x, position.y, position.z, size.x, size.y);
    }
}
