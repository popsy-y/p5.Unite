using UnityEngine;

namespace Unite.Canvas
{
    public struct DrawCommand
    {
        public Vector3 Position;
        public float Rotation;
        public Vector2 Size;

        public Shape ShapeType;

        public Color FillColor;
        public Color StrokeColor;
    }
}