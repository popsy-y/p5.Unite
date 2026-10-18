using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Unite.Text.Raster
{
    /// <summary>
    /// Bakes a single-channel signed distance field straight from closed polygon
    /// contours (pixel space, y-up; texel (i, j) is centered at (i + 0.5, j + 0.5),
    /// j counting rows from the bottom, so output[j*width+i]).
    /// The distance is the exact Euclidean distance to the nearest contour segment,
    /// so offset iso-lines (strokes) stay as smooth as the outline itself. The sign
    /// comes from the nonzero winding rule. Values are normalized so the outline
    /// stores ~128, deep inside 0 and deep outside 255, saturating at the supplied
    /// range. This is the single-glyph, synchronous entry point; the atlas bakes whole
    /// frames of glyphs through <see cref="SdfBakeJob"/>.
    /// </summary>
    public static class SdfEncoder
    {
        public static void Encode(Vector2[][] contours, int width, int height, float range, byte[] output)
            => Encode(contours, 1f, 0f, 0f, width, height, range, output);

        /// <summary>
        /// Encodes contours given in glyph units: each point is multiplied by scale and
        /// moved by (-originX, -originY) into texel space first. The output buffer may be
        /// larger than width * height; only the first width * height bytes are written.
        /// </summary>
        public static void Encode(
            Vector2[][] contours,
            float scale,
            float originX,
            float originY,
            int width,
            int height,
            float range,
            byte[] output
        )
        {
            if (output == null || width <= 0 || height <= 0 || output.Length < width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: SDF output must hold width * height bytes."
                );
            }

            if (range <= 0f)
            {
                throw new ArgumentException("p5.Unite: SDF range must be positive.");
            }

            var edges = new float4[0];
            int edgeCount = EdgeBuilder.Append(ref edges, 0, contours, scale, originX, originY);

            int texels = width * height;
            using var nativeEdges = new NativeArray<float4>(Math.Max(1, edgeCount), Allocator.Temp);
            NativeArray<float4>.Copy(edges, nativeEdges, edgeCount);
            using var distance = new NativeArray<float>(texels, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var crossingX = new NativeArray<float>(Math.Max(1, edgeCount), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var crossingWinding = new NativeArray<int>(Math.Max(1, edgeCount), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var result = new NativeArray<byte>(texels, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            SdfKernel.Encode(
                nativeEdges, 0, edgeCount, width, height, range,
                distance, 0, crossingX, crossingWinding, 0, result, 0, width
            );

            NativeArray<byte>.Copy(result, 0, output, 0, texels);
        }
    }

    /// <summary>Turns closed contours into the (x0, y0, x1, y1) edge list the kernel reads.</summary>
    internal static class EdgeBuilder
    {
        /// <summary>
        /// Appends the edges of the contours (each point scaled, then moved by -origin)
        /// to <paramref name="edges"/>, growing the array as needed, and returns the new count.
        /// </summary>
        public static int Append(
            ref float4[] edges,
            int count,
            Vector2[][] contours,
            float scale,
            float originX,
            float originY
        )
        {
            if (contours == null)
                return count;

            int total = 0;
            foreach (var contour in contours)
                total += contour != null && contour.Length >= 2 ? contour.Length : 0;

            if (edges.Length < count + total)
                Array.Resize(ref edges, Math.Max(count + total, edges.Length * 2));

            foreach (var contour in contours)
            {
                if (contour == null || contour.Length < 2)
                    continue;

                for (int index = 0; index < contour.Length; index++)
                {
                    var start = contour[index];
                    var end = contour[index + 1 == contour.Length ? 0 : index + 1]; // implicitly closed
                    if (start == end)
                        continue;

                    edges[count++] = new float4(
                        start.x * scale - originX,
                        start.y * scale - originY,
                        end.x * scale - originX,
                        end.y * scale - originY
                    );
                }
            }
            return count;
        }
    }
}
