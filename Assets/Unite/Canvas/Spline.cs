using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Draws a cardinal spline through up to four points.
        /// With <see cref="SplineEndMode.Include"/> the curve passes through all
        /// four points. With <see cref="SplineEndMode.Exclude"/> the outer two
        /// points act as control points and only the middle span is drawn.
        /// </summary>
        public void Spline(
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float x4,
            float y4
        ) => Spline(
            new Vector2(x1, y1),
            new Vector2(x2, y2),
            new Vector2(x3, y3),
            new Vector2(x4, y4)
        );

        public void Spline(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
            => Spline(
                new Vector3(p0.x, p0.y, 0f),
                new Vector3(p1.x, p1.y, 0f),
                new Vector3(p2.x, p2.y, 0f),
                new Vector3(p3.x, p3.y, 0f)
            );

        /// <summary>
        /// Draws a cardinal spline through up to four points in 3D space.
        /// </summary>
        public void Spline(
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
        ) => Spline(
            new Vector3(x1, y1, z1),
            new Vector3(x2, y2, z2),
            new Vector3(x3, y3, z3),
            new Vector3(x4, y4, z4)
        );

        public void Spline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            _curveBuffer.Clear();

            if (_splineEnds == SplineEndMode.Include)
            {
                // The duplicated end points give the spline a natural tangent at
                // p0 and p3 while still passing through every point.
                _curveBuffer.Add(p0);
                AppendCubic(
                    _curveBuffer,
                    p0,
                    SplineControl1(p0, p0, p1),
                    SplineControl2(p0, p1, p2),
                    p1
                );
                AppendCubic(
                    _curveBuffer,
                    p1,
                    SplineControl1(p0, p1, p2),
                    SplineControl2(p1, p2, p3),
                    p2
                );
                AppendCubic(
                    _curveBuffer,
                    p2,
                    SplineControl1(p1, p2, p3),
                    SplineControl2(p2, p3, p3),
                    p3
                );
            }
            else
            {
                _curveBuffer.Add(p1);
                AppendCubic(
                    _curveBuffer,
                    p1,
                    SplineControl1(p0, p1, p2),
                    SplineControl2(p1, p2, p3),
                    p2
                );
            }

            StrokeCurveSegments(_curveBuffer);
            _curveBuffer.Clear();
        }
    }
}
