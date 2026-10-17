using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Raster
{
    /// <summary>
    /// Glyph coverage rasterizer. Rasterizes closed polygon contours (pixel space,
    /// y-up) with the nonzero winding rule into a coverage grid; texel (i, j) occupies
    /// [i,i+1) x [j,j+1), j counting rows from the bottom, so coverageOut[j*width+i].
    /// Anti-aliasing uses 4x4 subsampling per texel.
    /// </summary>
    public static class GlyphRasterizer
    {
        private const int SubsamplesPerAxis = 4;
        private const int SubsampleCount = SubsamplesPerAxis * SubsamplesPerAxis;

        public static void Rasterize(Vector2[][] contours, int width, int height, byte[] coverageOut)
        {
            if (coverageOut == null || coverageOut.Length != width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: coverage buffer must hold width * height bytes."
                );
            }
            Array.Clear(coverageOut, 0, coverageOut.Length);

            var edges = BuildEdges(contours);
            if (edges.Count == 0)
                return;

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int hits = 0;
                    for (int subRow = 0; subRow < SubsamplesPerAxis; subRow++)
                    {
                        float py = row + (subRow + 0.5f) / SubsamplesPerAxis;

                        for (int subColumn = 0; subColumn < SubsamplesPerAxis; subColumn++)
                        {
                            float px = column + (subColumn + 0.5f) / SubsamplesPerAxis;
                            if (WindingNumber(edges, px, py) != 0)
                                hits++;
                        }
                    }

                    if (hits != 0)
                    {
                        coverageOut[row * width + column] =
                            (byte)Mathf.RoundToInt(255f * hits / SubsampleCount);
                    }
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

        // Horizontal ray crossing with the standard half-open convention.
        private static int WindingNumber(List<Edge> edges, float px, float py)
        {
            int winding = 0;

            for (int index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];

                bool straddles = (edge.Start.y <= py) != (edge.End.y <= py);
                if (!straddles)
                    continue;

                float t = (py - edge.Start.y) / (edge.End.y - edge.Start.y);
                float crossingX = edge.Start.x + t * (edge.End.x - edge.Start.x);

                if (px < crossingX)
                {
                    winding += edge.End.y > edge.Start.y ? 1 : -1;
                }
            }

            return winding;
        }
    }
}
