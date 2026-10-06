using System;
using System.Collections.Generic;
using UnityEngine;
using StrokeCapValue = Unite.Canvas.StrokeCap;

namespace Unite.Canvas
{
    /// <summary>
    /// Controls whether a spline includes the segments that touch its end points.
    /// </summary>
    public enum SplineEndMode
    {
        /// <summary>The spline passes through every point, including the end points.</summary>
        Include,

        /// <summary>The end points act as control points and only the inner span is drawn.</summary>
        Exclude
    }

    public partial class UCanvas
    {
        private const int DefaultCurveDetail = 20;

        private int _curveDetail = DefaultCurveDetail;
        private int _bezierOrder = 3;
        private float _splineTightness;
        private SplineEndMode _splineEnds = SplineEndMode.Include;

        private readonly List<Vector3> _curveBuffer = new();

        /// <summary>
        /// Sets the number of line segments used to draw one curve span.
        /// </summary>
        public void CurveDetail(int segments) => _curveDetail = Mathf.Clamp(segments, 1, 512);

        /// <summary>
        /// Gets the number of line segments used to draw one curve span.
        /// </summary>
        public int GetCurveDetail() => _curveDetail;

        /// <summary>
        /// Sets the interpolation order used by BezierVertex(). Only 2 and 3 are valid.
        /// </summary>
        public void BezierOrder(int order)
        {
            if (order != 2 && order != 3)
            {
                Debug.LogWarning($"p5.Unite: BezierOrder() expects 2 or 3 but received {order}.");
                return;
            }

            _bezierOrder = order;
        }

        /// <summary>
        /// Gets the interpolation order used by BezierVertex().
        /// </summary>
        public int GetBezierOrder() => _bezierOrder;

        /// <summary>
        /// Controls how tightly a spline fits its points. Positive values are tighter
        /// and negative values are looser. The default value is 0 (Catmull-Rom).
        /// </summary>
        public float SplineTightness
        {
            get => _splineTightness;
            set => _splineTightness = value;
        }

        /// <summary>
        /// Controls whether splines include their end segments.
        /// </summary>
        public SplineEndMode SplineEnds
        {
            get => _splineEnds;
            set => _splineEnds = value;
        }

        /// <summary>
        /// Sets the spline tightness using the p5.js property name.
        /// </summary>
        public void SplineProperty(string property, float value)
        {
            if (MatchesProperty(property, "tightness"))
            {
                _splineTightness = value;
                return;
            }

            Debug.LogWarning($"p5.Unite: Unknown spline property '{property}'.");
        }

        /// <summary>
        /// Sets the spline end mode using the p5.js property name.
        /// </summary>
        public void SplineProperty(string property, SplineEndMode value)
        {
            if (MatchesProperty(property, "ends"))
            {
                _splineEnds = value;
                return;
            }

            Debug.LogWarning($"p5.Unite: Unknown spline property '{property}'.");
        }

        /// <summary>
        /// Gets a spline property using the p5.js property name.
        /// </summary>
        public object GetSplineProperty(string property)
        {
            if (MatchesProperty(property, "tightness"))
                return _splineTightness;

            if (MatchesProperty(property, "ends"))
                return _splineEnds;

            Debug.LogWarning($"p5.Unite: Unknown spline property '{property}'.");
            return null;
        }

        /// <summary>
        /// Sets the spline tightness and end mode at once.
        /// </summary>
        public void SplineProperties(
            float tightness = 0f,
            SplineEndMode ends = SplineEndMode.Include
        )
        {
            _splineTightness = tightness;
            _splineEnds = ends;
        }

        /// <summary>
        /// Gets the current spline tightness and end mode.
        /// </summary>
        public (float tightness, SplineEndMode ends) GetSplineProperties()
            => (_splineTightness, _splineEnds);

        private static bool MatchesProperty(string property, string expected)
            => string.Equals(property, expected, StringComparison.OrdinalIgnoreCase);

        #region Bezier Interpolation

        /// <summary>
        /// Calculates one coordinate of a point on a cubic Bezier curve.
        /// </summary>
        public float BezierPoint(float a, float b, float c, float d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        public Vector2 BezierPoint(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
            => new(
                BezierPoint(a.x, b.x, c.x, d.x, t),
                BezierPoint(a.y, b.y, c.y, d.y, t)
            );

        public Vector3 BezierPoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            => new(
                BezierPoint(a.x, b.x, c.x, d.x, t),
                BezierPoint(a.y, b.y, c.y, d.y, t),
                BezierPoint(a.z, b.z, c.z, d.z, t)
            );

        /// <summary>
        /// Calculates one coordinate of the tangent of a cubic Bezier curve.
        /// </summary>
        public float BezierTangent(float a, float b, float c, float d, float t)
        {
            float u = 1f - t;
            return 3f * (d - c) * t * t + 6f * (c - b) * u * t + 3f * (b - a) * u * u;
        }

        public Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
            => new(
                BezierTangent(a.x, b.x, c.x, d.x, t),
                BezierTangent(a.y, b.y, c.y, d.y, t)
            );

        public Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            => new(
                BezierTangent(a.x, b.x, c.x, d.x, t),
                BezierTangent(a.y, b.y, c.y, d.y, t),
                BezierTangent(a.z, b.z, c.z, d.z, t)
            );

        #endregion


        #region Spline Interpolation

        /// <summary>
        /// Calculates one coordinate of a point on the span between the middle two
        /// points of a cardinal spline. Uses the current spline tightness.
        /// </summary>
        public float SplinePoint(float a, float b, float c, float d, float t)
            => SplinePoint(a, b, c, d, t, _splineTightness);

        public float SplinePoint(float a, float b, float c, float d, float t, float tightness)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float s = tightness;
            float f1 = ((s - 1f) * 0.5f) * t3 + (1f - s) * t2 + ((s - 1f) * 0.5f) * t;
            float f2 = ((s + 3f) * 0.5f) * t3 + ((-5f - s) * 0.5f) * t2 + 1f;
            float f3 = ((-3f - s) * 0.5f) * t3 + (s + 2f) * t2 + ((1f - s) * 0.5f) * t;
            float f4 = ((1f - s) * 0.5f) * t3 + ((s - 1f) * 0.5f) * t2;
            return a * f1 + b * f2 + c * f3 + d * f4;
        }

        public Vector2 SplinePoint(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
            => new(
                SplinePoint(a.x, b.x, c.x, d.x, t),
                SplinePoint(a.y, b.y, c.y, d.y, t)
            );

        public Vector3 SplinePoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            => new(
                SplinePoint(a.x, b.x, c.x, d.x, t),
                SplinePoint(a.y, b.y, c.y, d.y, t),
                SplinePoint(a.z, b.z, c.z, d.z, t)
            );

        public Vector3 SplinePoint(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            float t,
            float tightness
        )
            => new(
                SplinePoint(a.x, b.x, c.x, d.x, t, tightness),
                SplinePoint(a.y, b.y, c.y, d.y, t, tightness),
                SplinePoint(a.z, b.z, c.z, d.z, t, tightness)
            );

        /// <summary>
        /// Calculates one coordinate of the tangent of a cardinal spline.
        /// Uses the current spline tightness.
        /// </summary>
        public float SplineTangent(float a, float b, float c, float d, float t)
            => SplineTangent(a, b, c, d, t, _splineTightness);

        public float SplineTangent(
            float a,
            float b,
            float c,
            float d,
            float t,
            float tightness
        )
        {
            float t2 = t * t;
            float s = tightness;
            float tt3 = 3f * t2;
            float t2d = 2f * t;
            float f1 = ((s - 1f) * 0.5f) * tt3 + (1f - s) * t2d + (s - 1f) * 0.5f;
            float f2 = ((s + 3f) * 0.5f) * tt3 + ((-5f - s) * 0.5f) * t2d;
            float f3 = ((-3f - s) * 0.5f) * tt3 + (s + 2f) * t2d + (1f - s) * 0.5f;
            float f4 = ((1f - s) * 0.5f) * tt3 + ((s - 1f) * 0.5f) * t2d;
            return a * f1 + b * f2 + c * f3 + d * f4;
        }

        public Vector2 SplineTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
            => new(
                SplineTangent(a.x, b.x, c.x, d.x, t),
                SplineTangent(a.y, b.y, c.y, d.y, t)
            );

        public Vector3 SplineTangent(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
            => new(
                SplineTangent(a.x, b.x, c.x, d.x, t),
                SplineTangent(a.y, b.y, c.y, d.y, t),
                SplineTangent(a.z, b.z, c.z, d.z, t)
            );

        #endregion


        #region Curve Tessellation Helpers

        /// <summary>
        /// Appends the sampled points of one cubic Bezier span, excluding the start point.
        /// </summary>
        private void AppendCubic(
            List<Vector3> points,
            Vector3 start,
            Vector3 control1,
            Vector3 control2,
            Vector3 end
        )
        {
            int segments = Mathf.Clamp(_curveDetail, 1, 512);
            for (int index = 1; index <= segments; index++)
            {
                float t = index / (float)segments;
                points.Add(CubicPoint(start, control1, control2, end, t));
            }
        }

        private static Vector3 CubicPoint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a
                 + 3f * u * u * t * b
                 + 3f * u * t * t * c
                 + t * t * t * d;
        }

        /// <summary>
        /// Converts the four point window of a cardinal spline to the first
        /// cubic Bezier control point of the b to c span.
        /// </summary>
        private Vector3 SplineControl1(Vector3 a, Vector3 b, Vector3 c)
            => b + (c - a) * ((1f - _splineTightness) / 6f);

        /// <summary>
        /// Converts the four point window of a cardinal spline to the second
        /// cubic Bezier control point of the b to c span.
        /// </summary>
        private Vector3 SplineControl2(Vector3 b, Vector3 c, Vector3 d)
            => c + (b - d) * ((1f - _splineTightness) / 6f);

        /// <summary>
        /// Strokes a sampled polyline. Interior joints are rounded so that the
        /// stroke stays continuous, while the open ends use the current stroke cap.
        /// </summary>
        private void StrokeCurveSegments(IReadOnlyList<Vector3> points)
        {
            if (points.Count < 2)
                return;

            StrokeCap cap = _styleState.StrokeCap;
            for (int index = 0; index < points.Count - 1; index++)
            {
                Vector3 start = points[index];
                Vector3 end = points[index + 1];
                if ((end - start).sqrMagnitude <= 1e-12f)
                    continue;

                StrokeCap startCap = index == 0 ? cap : StrokeCapValue.Round;
                StrokeCap endCap = index == points.Count - 2 ? cap : StrokeCapValue.Round;
                EnqueueLineSegment(start, end, startCap, endCap);
            }
        }

        private void StrokeCurveSpan(
            Vector3 start,
            Vector3 control1,
            Vector3 control2,
            Vector3 end
        )
        {
            _curveBuffer.Clear();
            _curveBuffer.Add(start);
            AppendCubic(_curveBuffer, start, control1, control2, end);
            StrokeCurveSegments(_curveBuffer);
            _curveBuffer.Clear();
        }

        #endregion
    }
}
