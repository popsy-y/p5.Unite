using UnityEngine;

namespace Unite.Drawing
{
    public static class ShaderPropertyIds
    {
        public static int ShapeDataBuffer => Shader.PropertyToID("_ShapeData");
        public static int PolygonPoints => Shader.PropertyToID("_PolygonPoints");
        public static int ParentTransform => Shader.PropertyToID("_ParentTransform");
        public static int DrawingMode => Shader.PropertyToID("_DrawingMode");
        public static int ShapeIndex => Shader.PropertyToID("_ShapeIndex");
        public static int UseShapeIndex => Shader.PropertyToID("_UseShapeIndex");
        public static int DebugShapeBounds => Shader.PropertyToID("_DebugShapeBounds");
        public static int GlyphAtlas => Shader.PropertyToID("_GlyphAtlas");
        public static int ShapeDataBase => Shader.PropertyToID("_ShapeDataBase");
        public static int SrcBlend => Shader.PropertyToID("_SrcBlend");
        public static int DstBlend => Shader.PropertyToID("_DstBlend");
        public static int ZWrite => Shader.PropertyToID("_ZWrite");
        public static int ZTest => Shader.PropertyToID("_ZTest");
    }
}
