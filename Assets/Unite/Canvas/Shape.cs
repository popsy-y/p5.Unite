using UnityEngine;

namespace Unite.Canvas
{
    public enum Shape
    {
        Arc,
        Circle,
        Ellipse,
        Line,
        Point,
        Quad,
        Rectangle,
        Square,
        Triangle
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

    public partial class UCanvas
    {
        public ShapeMode RectMode { get; set; } = ShapeMode.Corner;
        public ShapeMode EllipseMode { get; set; } = ShapeMode.Center;

        private void EnqueueCenteredShape(
            Shape shape,
            Vector3 center,
            Vector2 size,
            Vector4 cornerRadii = default
        )
        {
            CheckDrawingContext();
            if (!_fillEnabled && !_strokeEnabled)
                return;

            var (rotation, scale) = GetRotationAndScale();
            var absoluteScale = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            var scaledSize = Vector2.Scale(Abs(size), absoluteScale);

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = TransformPoint(center),
                Rotation = rotation,
                Size = scaledSize,
                ShapeType = shape,
                CornerRadiiX = Abs(cornerRadii) * absoluteScale.x,
                CornerRadiiY = Abs(cornerRadii) * absoluteScale.y,
                FillColor = _fillColor,
                StrokeColor = _strokeColor,
                StrokeWeight = Mathf.Max(0f, _strokeWeight),
                FillEnabled = _fillEnabled,
                StrokeEnabled = _strokeEnabled
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
            bool allowFill
        )
        {
            CheckDrawingContext();

            bool fillEnabled = allowFill && _fillEnabled;
            if (!fillEnabled && !_strokeEnabled)
                return;

            Vector2 boundsMin = point0;
            Vector2 boundsMax = point0;
            IncludePoint(point1, pointCount, 2, ref boundsMin, ref boundsMax);
            IncludePoint(point2, pointCount, 3, ref boundsMin, ref boundsMax);
            IncludePoint(point3, pointCount, 4, ref boundsMin, ref boundsMax);

            var center2 = (boundsMin + boundsMax) * 0.5f;
            var center3 = new Vector3(center2.x, center2.y, z);
            var (rotation, scale) = GetRotationAndScale();
            var absoluteScale = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            Vector2 ToShapePoint(Vector2 point) => Vector2.Scale(point - center2, absoluteScale);

            var shapePoint0 = ToShapePoint(point0);
            var shapePoint1 = ToShapePoint(point1);
            var shapePoint2 = ToShapePoint(point2);
            var shapePoint3 = ToShapePoint(point3);
            var shapeBoundsMin = shapePoint0;
            var shapeBoundsMax = shapePoint0;
            IncludePoint(shapePoint1, pointCount, 2, ref shapeBoundsMin, ref shapeBoundsMax);
            IncludePoint(shapePoint2, pointCount, 3, ref shapeBoundsMin, ref shapeBoundsMax);
            IncludePoint(shapePoint3, pointCount, 4, ref shapeBoundsMin, ref shapeBoundsMax);

            _commandQueue.Enqueue(new DrawCommand
            {
                Position = TransformPoint(center3),
                Rotation = rotation,
                Size = shapeBoundsMax - shapeBoundsMin,
                Point0 = shapePoint0,
                Point1 = shapePoint1,
                Point2 = shapePoint2,
                Point3 = shapePoint3,
                BoundsMin = shapeBoundsMin,
                BoundsMax = shapeBoundsMax,
                HasCustomBounds = true,
                ShapeType = shape,
                FillColor = _fillColor,
                StrokeColor = _strokeColor,
                StrokeWeight = Mathf.Max(0f, _strokeWeight),
                FillEnabled = fillEnabled,
                StrokeEnabled = _strokeEnabled
            });
        }

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
