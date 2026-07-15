using UnityEngine;

namespace Unite.Canvas
{
    public struct DrawCommand
    {
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector2 Size;

        public Vector2 Point0;
        public Vector2 Point1;
        public Vector2 Point2;
        public Vector2 Point3;

        public Vector4 Parameters0;
        public Vector4 Parameters1;
        public Vector4 CornerRadiiX;
        public Vector4 CornerRadiiY;

        public Shape ShapeType;
        public ArcMode ArcMode;

        public Color FillColor;
        public Color StrokeColor;
        public float StrokeWeight;
        public bool FillEnabled;
        public bool StrokeEnabled;
    }
}
