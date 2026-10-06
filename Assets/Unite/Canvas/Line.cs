using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Draws a line between two points on the canvas plane.
        /// </summary>
        public void Line(float x1, float y1, float x2, float y2)
            => Line(new Vector2(x1, y1), new Vector2(x2, y2), 0f);

        public void Line(float x1, float y1, float x2, float y2, float z)
            => Line(new Vector2(x1, y1), new Vector2(x2, y2), z);

        public void Line(Vector2 start, Vector2 end)
            => Line(start, end, 0f);

        public void Line(Vector2 start, Vector2 end, float z)
        {
            EnqueuePointShape(Shape.Line, z, start, end, end, end, 2, false);
        }

        /// <summary>
        /// Draws a line between two points in 3D space.
        /// </summary>
        public void Line(
            float x1,
            float y1,
            float z1,
            float x2,
            float y2,
            float z2
        ) => Line(new Vector3(x1, y1, z1), new Vector3(x2, y2, z2));

        public void Line(Vector3 start, Vector3 end)
            => EnqueueLineSegment(start, end, _styleState.StrokeCap, _styleState.StrokeCap);

        /// <summary>
        /// Draws a point using the current stroke and stroke weight.
        /// </summary>
        public void Point(float x, float y)
            => Point(new Vector2(x, y), 0f);

        public void Point(float x, float y, float z)
            => Point(new Vector2(x, y), z);

        public void Point(Vector2 position)
            => Point(position, 0f);

        public void Point(Vector2 position, float z)
        {
            EnqueuePointShape(
                Shape.Point,
                z,
                position,
                position,
                position,
                position,
                1,
                false
            );
        }

        public void Point(Vector3 position)
            => Point(new Vector2(position.x, position.y), position.z);
    }
}
