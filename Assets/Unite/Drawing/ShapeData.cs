using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Drawing
{
    /// <summary>
    /// Per-instance shape data passed to the GPU via StructuredBuffer.
    /// Layout must match the ShapeData struct in UniteCommon.hlsl.
    /// </summary>
    [GenerateHLSL]
    public struct ShapeData
    {
        public Vector3 Position;
        public Vector4 Rotation;
        public Vector2 Size;
        public int ShapeType;
        public Vector4 FillColor;
        public Vector4 StrokeColor;
        /// <summary>Stroke thickness in UV space, per axis.</summary>
        public Vector2 StrokeWeight;
    }
}
