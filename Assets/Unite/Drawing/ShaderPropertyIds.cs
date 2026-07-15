using UnityEngine;

namespace Unite.Drawing
{
    public static class ShaderPropertyIds
    {
        public static int ShapeDataBuffer => Shader.PropertyToID("_ShapeData");
        public static int ParentTransform => Shader.PropertyToID("_ParentTransform");
        public static int DrawingMode => Shader.PropertyToID("_DrawingMode");
        public static int ShapeIndex => Shader.PropertyToID("_ShapeIndex");
        public static int UseShapeIndex => Shader.PropertyToID("_UseShapeIndex");
        public static int DebugShapeBounds => Shader.PropertyToID("_DebugShapeBounds");
    }
}
