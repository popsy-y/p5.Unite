using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Raster
{
    /// <summary>
    /// Bakes a single-channel signed distance field straight from closed polygon
    /// contours (pixel space, y-up; texel (i, j) is centered at (i + 0.5, j + 0.5),
    /// j counting rows from the bottom, so output[j*width+i]).
    /// The distance is the exact Euclidean distance to the nearest contour segment,
    /// so offset iso-lines (strokes) stay as smooth as the outline itself. The sign
    /// comes from the nonzero winding rule, evaluated per row with one sorted list of
    /// edge crossings. Values are normalized so the outline stores ~128, deep inside
    /// 0 and deep outside 255, saturating at the supplied range.
    /// </summary>
    public static class SdfEncoder
    {
        public static void Encode(Vector2[][] contours, int width, int height, float range, byte[] output)
        {
            if (output == null || width <= 0 || height <= 0 || output.Length != width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: SDF output must hold width * height bytes."
                );
            }

            if (range <= 0f)
            {
                throw new ArgumentException("p5.Unite: SDF range must be positive.");
            }

            var edges = BuildEdges(contours);
            var distance = new float[width * height];
            Array.Fill(distance, range * range);

            foreach (var edge in edges)
                SplatEdgeDistance(edge, width, height, range, distance);

            // The splat keeps squared distances; one square root per texel.
            for (int index = 0; index < distance.Length; index++)
                distance[index] = (float)Math.Sqrt(distance[index]);

            var crossingX = new float[edges.Count];
            var crossingWinding = new int[edges.Count];

            for (int row = 0; row < height; row++)
            {
                float py = row + 0.5f;
                int crossingCount = 0;

                // Horizontal ray crossings with the standard half-open convention.
                foreach (var edge in edges)
                {
                    if ((edge.Start.y <= py) == (edge.End.y <= py))
                        continue;

                    float t = (py - edge.Start.y) / (edge.End.y - edge.Start.y);
                    crossingX[crossingCount] = edge.Start.x + t * (edge.End.x - edge.Start.x);
                    crossingWinding[crossingCount] = edge.End.y > edge.Start.y ? 1 : -1;
                    crossingCount++;
                }

                Array.Sort(crossingX, crossingWinding, 0, crossingCount);

                int winding = 0;
                int next = 0;
                for (int column = 0; column < width; column++)
                {
                    float px = column + 0.5f;
                    while (next < crossingCount && crossingX[next] < px)
                    {
                        winding += crossingWinding[next];
                        next++;
                    }

                    int index = row * width + column;
                    float signedDistance = winding != 0 ? -distance[index] : distance[index];
                    float normalized = Mathf.Clamp(signedDistance / range, -1f, 1f);
                    output[index] = (byte)Mathf.RoundToInt((0.5f + 0.5f * normalized) * 255f);
                }
            }
        }

        private struct Edge
        {
            public Vector2 Start;
            public Vector2 End;
        }

        private static List<Edge> BuildEdges(Vector2[][] contours)
        {
            var edges = new List<Edge>(64);

            if (contours == null)
                return edges;

            foreach (var contour in contours)
            {
                if (contour == null || contour.Length < 2)
                    continue;

                for (int index = 0; index < contour.Length; index++)
                {
                    var start = contour[index];
                    var end = contour[(index + 1) % contour.Length]; // implicitly closed

                    if (start == end)
                        continue;

                    edges.Add(new Edge { Start = start, End = end });
                }
            }
            return edges;
        }

        // Distances saturate at the range, so a segment only touches the texels inside
        // its bounding box grown by the range: the cost scales with the outline length,
        // not with texel count x segment count. Works on squared distances.
        private static void SplatEdgeDistance(
            Edge edge,
            int width,
            int height,
            float range,
            float[] distanceSq
        )
        {
            int minColumn = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(edge.Start.x, edge.End.x) - range - 0.5f));
            int maxColumn = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(edge.Start.x, edge.End.x) + range - 0.5f));
            int minRow = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(edge.Start.y, edge.End.y) - range - 0.5f));
            int maxRow = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(edge.Start.y, edge.End.y) + range - 0.5f));

            float startX = edge.Start.x;
            float startY = edge.Start.y;
            float segmentX = edge.End.x - startX;
            float segmentY = edge.End.y - startY;
            float inverseLengthSq = 1f / (segmentX * segmentX + segmentY * segmentY);

            for (int row = minRow; row <= maxRow; row++)
            {
                float offsetY = row + 0.5f - startY;
                int rowStart = row * width;

                for (int column = minColumn; column <= maxColumn; column++)
                {
                    float offsetX = column + 0.5f - startX;
                    float t = (offsetX * segmentX + offsetY * segmentY) * inverseLengthSq;
                    t = t < 0f ? 0f : (t > 1f ? 1f : t);
                    float deltaX = offsetX - t * segmentX;
                    float deltaY = offsetY - t * segmentY;
                    float candidate = deltaX * deltaX + deltaY * deltaY;

                    int index = rowStart + column;
                    if (candidate < distanceSq[index])
                        distanceSq[index] = candidate;
                }
            }
        }
    }
}
