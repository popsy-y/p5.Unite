using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Text
{
    /// <summary>
    /// Builds the TextToModel mesh: a triangulated front face per glyph at z = 0 facing
    /// -z, and for a positive extrusion a back face at z = depth plus flat-shaded side
    /// walls along every contour.
    /// </summary>
    internal static class TextMeshBuilder
    {
        public static Mesh Build(List<List<Vector2[]>> glyphs, float extrude)
        {
            var positions = new List<Vector3>();
            var triangles = new List<int>();
            var faceVertices = new List<Vector2>();
            var faceTriangles = new List<int>();

            foreach (var glyph in glyphs)
            {
                faceVertices.Clear();
                faceTriangles.Clear();
                PolygonTriangulator.Triangulate(glyph, faceVertices, faceTriangles);

                AddFace(faceVertices, faceTriangles, 0f, Vector3.back, positions, triangles);
                if (extrude <= 0f)
                    continue;

                AddFace(faceVertices, faceTriangles, extrude, Vector3.forward, positions, triangles);
                foreach (var contour in glyph)
                    AddWalls(contour, extrude, positions, triangles);
            }

            var mesh = new Mesh { name = "TextModel" };
            if (positions.Count > ushort.MaxValue)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(
            List<Vector2> faceVertices,
            List<int> faceTriangles,
            float z,
            Vector3 facing,
            List<Vector3> positions,
            List<int> triangles
        )
        {
            int baseIndex = positions.Count;
            foreach (var vertex in faceVertices)
                positions.Add(new Vector3(vertex.x, vertex.y, z));

            for (int index = 0; index < faceTriangles.Count; index += 3)
            {
                AddTriangle(
                    positions,
                    triangles,
                    baseIndex + faceTriangles[index],
                    baseIndex + faceTriangles[index + 1],
                    baseIndex + faceTriangles[index + 2],
                    facing
                );
            }
        }

        private static void AddWalls(Vector2[] contour, float extrude, List<Vector3> positions, List<int> triangles)
        {
            if (contour.Length < 3)
                return;

            // Outward = away from the contour's interior, which flips with its winding.
            float area = 0f;
            for (int index = 0; index < contour.Length; index++)
            {
                var a = contour[index];
                var b = contour[(index + 1) % contour.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float outwardSign = area > 0f ? 1f : -1f;

            for (int index = 0; index < contour.Length; index++)
            {
                var a = contour[index];
                var b = contour[(index + 1) % contour.Length];
                var edge = b - a;
                if (edge.sqrMagnitude <= 1e-12f)
                    continue;

                var outward = new Vector3(edge.y, -edge.x, 0f) * outwardSign;
                int baseIndex = positions.Count;
                positions.Add(new Vector3(a.x, a.y, 0f));
                positions.Add(new Vector3(b.x, b.y, 0f));
                positions.Add(new Vector3(b.x, b.y, extrude));
                positions.Add(new Vector3(a.x, a.y, extrude));

                AddTriangle(positions, triangles, baseIndex, baseIndex + 1, baseIndex + 2, outward);
                AddTriangle(positions, triangles, baseIndex, baseIndex + 2, baseIndex + 3, outward);
            }
        }

        // Unity front faces wind so that cross(v1 - v0, v2 - v0) points toward the viewer.
        private static void AddTriangle(List<Vector3> positions, List<int> triangles, int a, int b, int c, Vector3 facing)
        {
            var normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            triangles.Add(a);
            if (Vector3.Dot(normal, facing) >= 0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
