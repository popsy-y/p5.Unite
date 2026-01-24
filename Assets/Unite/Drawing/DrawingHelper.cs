using UnityEngine;

namespace Unite.Drawing
{
    public static class DrawingHelper
    {
        public static Mesh UnitQuad => new Mesh
        {
            name = "UnitQuad",
            vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, 1f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            },
            // I DO NOT TOTALLY UNDERSTAND ABOUT WINDING DIRECTION
            // Triangles winding chosen so normals face -Z
            // triangles = new[] { 0, 2, 1, 0, 3, 2 },
            triangles = new[] { 0, 1, 2, 3, 0, 2 },
            normals = new[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            }
        };

        public static Vector2 V2DivV2(Vector2 v0, Vector2 v1) => new (v0.x / v1.x, v0.y / v1.y);
        public static Vector3 V3DivV3(Vector3 v0, Vector3 v1) => new (v0.x / v1.x, v0.y / v1.y, v0.z / v1.z);

        public static Vector3 Vs2Ws(Vector3 vsPos, Camera cam) => cam.ViewportToWorldPoint(vsPos);
    }
}