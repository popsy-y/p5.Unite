using UnityEngine;
using Unite.Coordinate;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        private CanvasTransformStack _transformStack = new();

        public void ResetTransform() => _transformStack.Reset();

        public void Push() => _transformStack.Push();

        public void Pop() => _transformStack.Pop();

        public void Translate(Vector3 translation) => _transformStack.Translate(translation);

        public void Translate(float x, float y, float z = 0f) => _transformStack.Translate(x, y, z);

        public void Rotate(float radians) => _transformStack.Rotate(radians);
        public void Rotate(Vector3 anglesRadians) => _transformStack.Rotate(anglesRadians);

        public void Scale(Vector3 scale) => _transformStack.Scale(scale);

        public void Scale(float x, float y, float z) => _transformStack.Scale(x, y, z);

        public Vector3 TransformPoint(Vector3 point) => _transformStack.TransformPoint(point);

        public (Vector3 angleDeg, Vector2 scale) GetRotationAndScale() => _transformStack.RotationAndScale();
    }
}