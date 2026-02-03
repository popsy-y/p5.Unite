using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Drawing
{
    [GenerateHLSL]
    public struct ShapeData
    {
        public Vector3 Position;
        public Vector4 Rotation; // Quaternion
        public Vector2 Size;
        public int ShapeType;
        public Vector4 FillColor;
        public Vector4 StrokeColor;
    }
}
