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
