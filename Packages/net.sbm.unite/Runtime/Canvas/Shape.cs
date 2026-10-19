using UnityEngine;

namespace Unite.Canvas
{
    public enum Shape
    {
        Arc,
        Circle,
        Ellipse,
        Line,
        Line3D,
        Point,
        Quad,
        Rectangle,
        Square,
        Triangle,
        PolygonFill,
        Text
    }

    public enum ShapeMode
    {
        Corner,
        Center,
        Corners,
        Radius
    }

    public enum ArcMode
    {
        Open,
        Chord,
        Pie
    }

    /// <summary>
    /// Defines how the ends of a stroke are rendered.
    /// </summary>
    public enum StrokeCap
    {
        Round,
        Square,
        Project
    }

    /// <summary>
    /// Defines how the joints between stroke segments are rendered.
    /// Only <see cref="StrokeJoin.Round"/> is currently produced by stroke rendering.
    /// </summary>
    public enum StrokeJoin
    {
        Miter,
        Bevel,
        Round
    }

    public partial class UCanvas
    {
        public ShapeMode RectMode
        {
            get => _styleState.RectMode;
            set => _styleState.RectMode = value;
        }

        public ShapeMode EllipseMode
        {
            get => _styleState.EllipseMode;
            set => _styleState.EllipseMode = value;
        }

        private void EnqueueCenteredShape(
            Shape shape,
            Vector3 center,
            Vector2 size,
            Vector4 cornerRadii = default
        )
        {
            CheckDrawingContext();
            if (!isActiveAndEnabled)
                return;
            if (!_styleState.FillEnabled && !_styleState.StrokeEnabled)
                return;

            _commandQueue.Add(new DrawCommand
            {
                Transform = CurrentTransform * Matrix4x4.Translate(center),
                Size = Abs(size),
                ShapeType = shape,
                CornerRadiiX = Abs(cornerRadii),
                CornerRadiiY = Abs(cornerRadii),
                FillColor = _styleState.FillColor,
                StrokeColor = _styleState.StrokeColor,
                StrokeWeight = Mathf.Max(0f, _styleState.StrokeWeight),
                FillEnabled = _styleState.FillEnabled,
                StrokeEnabled = _styleState.StrokeEnabled
            });
        }

        private void EnqueuePointShape(
            Shape shape,
            float z,
            Vector2 point0,
            Vector2 point1,
            Vector2 point2,
            Vector2 point3,
            int pointCount,
            bool allowFill,
            bool allowStroke = true
        )
        {
            CheckDrawingContext();
            if (!isActiveAndEnabled)
                return;

            bool fillEnabled = allowFill && _styleState.FillEnabled;
            bool strokeEnabled = allowStroke && _styleState.StrokeEnabled;
            if (!fillEnabled && !strokeEnabled)
                return;

            Vector2 boundsMin = point0;
            Vector2 boundsMax = point0;
            IncludePoint(point1, pointCount, 2, ref boundsMin, ref boundsMax);
            IncludePoint(point2, pointCount, 3, ref boundsMin, ref boundsMax);
            IncludePoint(point3, pointCount, 4, ref boundsMin, ref boundsMax);

            var center2 = (boundsMin + boundsMax) * 0.5f;
            var center3 = new Vector3(center2.x, center2.y, z);
            Vector2 ToShapePoint(Vector2 point) => point - center2;

            var shapePoint0 = ToShapePoint(point0);
            var shapePoint1 = ToShapePoint(point1);
            var shapePoint2 = ToShapePoint(point2);
            var shapePoint3 = ToShapePoint(point3);
            var shapeBoundsMin = shapePoint0;
            var shapeBoundsMax = shapePoint0;
            IncludePoint(shapePoint1, pointCount, 2, ref shapeBoundsMin, ref shapeBoundsMax);
            IncludePoint(shapePoint2, pointCount, 3, ref shapeBoundsMin, ref shapeBoundsMax);
            IncludePoint(shapePoint3, pointCount, 4, ref shapeBoundsMin, ref shapeBoundsMax);

            _commandQueue.Add(new DrawCommand
            {
                Transform = CurrentTransform * Matrix4x4.Translate(center3),
                Size = shapeBoundsMax - shapeBoundsMin,
                Point0 = shapePoint0,
                Point1 = shapePoint1,
                Point2 = shapePoint2,
                Point3 = shapePoint3,
                BoundsMin = shapeBoundsMin,
                BoundsMax = shapeBoundsMax,
                HasCustomBounds = true,
                ShapeType = shape,
                FillColor = _styleState.FillColor,
                StrokeColor = _styleState.StrokeColor,
                StrokeWeight = Mathf.Max(0f, _styleState.StrokeWeight),
                FillEnabled = fillEnabled,
                StrokeEnabled = strokeEnabled,
                LineCaps = PackLineCaps(_styleState.StrokeCap, _styleState.StrokeCap)
            });
        }

        internal static int PackLineCaps(StrokeCap start, StrokeCap end)
            => ((int)start & 3) | (((int)end & 3) << 2);

        private static void IncludePoint(
            Vector2 point,
            int pointCount,
            int requiredCount,
            ref Vector2 boundsMin,
            ref Vector2 boundsMax
        )
        {
            if (pointCount < requiredCount)
                return;

            boundsMin = Vector2.Min(boundsMin, point);
            boundsMax = Vector2.Max(boundsMax, point);
        }

        private static (Vector3 center, Vector2 size) ResolveShapeMode(
            Vector3 position,
            Vector2 size,
            ShapeMode mode
        )
        {
            return mode switch
            {
                ShapeMode.Center => (position, Abs(size)),
                ShapeMode.Radius => (position, Abs(size) * 2f),
                ShapeMode.Corners => (
                    new Vector3(
                        (position.x + size.x) * 0.5f,
                        (position.y + size.y) * 0.5f,
                        position.z
                    ),
                    new Vector2(Mathf.Abs(size.x - position.x), Mathf.Abs(size.y - position.y))
                ),
                _ => (
                    new Vector3(position.x + size.x * 0.5f, position.y + size.y * 0.5f, position.z),
                    Abs(size)
                )
            };
        }

        private static Vector2 Abs(Vector2 value)
            => new(Mathf.Abs(value.x), Mathf.Abs(value.y));

        private static Vector4 Abs(Vector4 value)
            => new(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z),
                Mathf.Abs(value.w)
            );
    }
}
