using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        public void Rect(float x, float y, float width, float height)
            => Rect(x, y, width, height, Vector4.zero);

        public void Rect(float x, float y, float width, float height, float radius)
            => Rect(x, y, width, height, Vector4.one * radius);

        public void Rect(
            float x,
            float y,
            float width,
            float height,
            float topLeft,
            float topRight,
            float bottomRight,
            float bottomLeft
        ) => Rect(
            x,
            y,
            width,
            height,
            new Vector4(topLeft, topRight, bottomRight, bottomLeft)
        );

        public void Rect(Vector2 position, Vector2 size)
            => Rect(position.x, position.y, size.x, size.y);

        public void Rect(Vector3 position, Vector2 size)
            => Rect(position, size, Vector4.zero);

        public void Rect(Vector3 position, Vector2 size, float radius)
            => Rect(position, size, Vector4.one * radius);

        public void Rect(Vector3 position, Vector2 size, Vector4 cornerRadii)
        {
            var (center, resolvedSize) = ResolveShapeMode(position, size, RectMode);
            EnqueueCenteredShape(Shape.Rectangle, center, resolvedSize, cornerRadii);
        }

        public void Square(float x, float y, float size)
            => Square(x, y, size, Vector4.zero);

        public void Square(float x, float y, float size, float radius)
            => Square(x, y, size, Vector4.one * radius);

        public void Square(
            float x,
            float y,
            float size,
            float topLeft,
            float topRight,
            float bottomRight,
            float bottomLeft
        ) => Square(
            x,
            y,
            size,
            new Vector4(topLeft, topRight, bottomRight, bottomLeft)
        );

        public void Square(Vector2 position, float size)
            => Square(position.x, position.y, size);

        public void Square(Vector3 position, float size)
            => Square(position, size, Vector4.zero);

        public void Square(Vector3 position, float size, float radius)
            => Square(position, size, Vector4.one * radius);

        public void Square(Vector3 position, float size, Vector4 cornerRadii)
        {
            var (center, resolvedSize) = ResolveShapeMode(position, Vector2.one * size, RectMode);
            EnqueueCenteredShape(Shape.Square, center, resolvedSize, cornerRadii);
        }

        private void Rect(float x, float y, float width, float height, Vector4 cornerRadii)
        {
            Rect(new Vector3(x, y, 0f), new Vector2(width, height), cornerRadii);
        }

        private void Square(float x, float y, float size, Vector4 cornerRadii)
        {
            Square(new Vector3(x, y, 0f), size, cornerRadii);
        }
    }
}
