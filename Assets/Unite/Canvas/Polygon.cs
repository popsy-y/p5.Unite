using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Draws a triangle on the canvas plane.
        /// </summary>
        public void Triangle(float x1, float y1, float x2, float y2, float x3, float y3)
            => Triangle(
                new Vector2(x1, y1),
                new Vector2(x2, y2),
                new Vector2(x3, y3),
                0f
            );

        public void Triangle(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float z
        ) => Triangle(
            new Vector2(x1, y1),
            new Vector2(x2, y2),
            new Vector2(x3, y3),
            z
        );

        public void Triangle(Vector2 point1, Vector2 point2, Vector2 point3)
            => Triangle(point1, point2, point3, 0f);

        public void Triangle(Vector2 point1, Vector2 point2, Vector2 point3, float z)
        {
            EnqueuePointShape(
                Shape.Triangle,
                z,
                point1,
                point2,
                point3,
                point3,
                3,
                true
            );
        }

        /// <summary>
        /// Draws a quadrilateral on the canvas plane.
        /// </summary>
        public void Quad(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float x4,
            float y4
        ) => Quad(
            new Vector2(x1, y1),
            new Vector2(x2, y2),
            new Vector2(x3, y3),
            new Vector2(x4, y4),
            0f
        );

        public void Quad(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float x4,
            float y4,
            float z
        ) => Quad(
            new Vector2(x1, y1),
            new Vector2(x2, y2),
            new Vector2(x3, y3),
            new Vector2(x4, y4),
            z
        );

        public void Quad(Vector2 point1, Vector2 point2, Vector2 point3, Vector2 point4)
            => Quad(point1, point2, point3, point4, 0f);

        public void Quad(
            Vector2 point1,
            Vector2 point2,
            Vector2 point3,
            Vector2 point4,
            float z
        )
        {
            EnqueuePointShape(
                Shape.Quad,
                z,
                point1,
                point2,
                point3,
                point4,
                4,
                true
            );
        }
    }
}
