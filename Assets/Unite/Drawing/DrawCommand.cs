using UnityEngine;

namespace Unite.Canvas
{
    public struct DrawCommand
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector2 Size;

        public Shape ShapeType;

        public Color FillColor;
        public Color StrokeColor;
        public float StrokeWeight;
    }
}