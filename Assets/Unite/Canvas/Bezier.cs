using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Draws a cubic Bezier curve between two anchors with two control points.
        /// </summary>
        public void Bezier(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float x4,
            float y4
        ) => Bezier(
            new Vector2(x1, y1),
            new Vector2(x2, y2),
            new Vector2(x3, y3),
            new Vector2(x4, y4)
        );

        public void Bezier(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
            => Bezier(
                new Vector3(start.x, start.y, 0f),
                new Vector3(control1.x, control1.y, 0f),
                new Vector3(control2.x, control2.y, 0f),
                new Vector3(end.x, end.y, 0f)
            );

        /// <summary>
        /// Draws a cubic Bezier curve between two anchors in 3D space.
        /// </summary>
        public void Bezier(
            float x1,
            float y1,
            float z1,
            float x2,
            float y2,
            float z2,
            float x3,
            float y3,
            float z3,
            float x4,
            float y4,
            float z4
        ) => Bezier(
            new Vector3(x1, y1, z1),
            new Vector3(x2, y2, z2),
            new Vector3(x3, y3, z3),
            new Vector3(x4, y4, z4)
        );

        public void Bezier(Vector3 start, Vector3 control1, Vector3 control2, Vector3 end)
            => StrokeCurveSpan(start, control1, control2, end);
    }
}
