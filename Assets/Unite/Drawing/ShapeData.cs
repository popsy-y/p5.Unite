using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Drawing
{
    /// <summary>
    /// Per-instance shape data passed to the GPU via StructuredBuffer.
    /// Layout must match the ShapeData struct in UniteCommon.hlsl.
    /// </summary>
    [GenerateHLSL]
    [StructLayout(LayoutKind.Sequential)]
    public struct ShapeData
    {
        public Vector4 Position;
        public Vector4 Rotation;
        public Vector4 Bounds;
        public Vector4 ShapeParameters;

        public Vector4 Point0;
        public Vector4 Point1;
        public Vector4 Point2;
        public Vector4 Point3;

        public Vector4 Parameters0;
        public Vector4 Parameters1;
        public Vector4 CornerRadiiX;
        public Vector4 CornerRadiiY;

        public Vector4 Style;

        public Vector4 FillColor;
        public Vector4 StrokeColor;
    }
}
