using UnityEngine;

namespace Unite.Drawing
{
    public static class ShaderPropertyIds
    {
        public static int ShapeDataBuffer => Shader.PropertyToID("_ShapeData");
        public static int ParentTransform => Shader.PropertyToID("_ParentTransform");
    }
}