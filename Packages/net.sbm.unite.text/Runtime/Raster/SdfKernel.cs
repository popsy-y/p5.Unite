using Unity.Collections;
using Unity.Mathematics;

namespace Unite.Text.Raster
{
    /// <summary>
    /// The signed distance field algorithm, written against native arrays so the same
    /// code runs inside Burst jobs (<see cref="SdfBakeJob"/>) and from managed callers
    /// (<see cref="SdfEncoder"/>).
    /// Texel (i, j) is centered at (i + 0.5, j + 0.5) in a y-up pixel space. The
    /// distance is the exact Euclidean distance to the nearest edge; the sign comes
    /// from the nonzero winding rule, evaluated per row from the edge crossings. Values
    /// are normalized so the outline stores ~128, deep inside 0 and deep outside 255,
    /// saturating at the range.
    /// </summary>
    internal static class SdfKernel
    {
        /// <summary>
        /// Encodes the edges [edgeStart, edgeStart + edgeCount) (x0, y0, x1, y1 in texel
        /// space) into a width x height field written at <paramref name="outputStart"/> with
        /// the given row stride. <paramref name="distance"/> needs width * height floats
        /// and the crossing arrays edgeCount entries, all starting at their given offsets.
        /// </summary>
        public static void Encode(
            NativeArray<float4> edges,
            int edgeStart,
            int edgeCount,
            int width,
            int height,
            float range,
            NativeArray<float> distance,
            int distanceStart,
            NativeArray<float> crossingX,
            NativeArray<int> crossingWinding,
            int crossingStart,
            NativeArray<byte> output,
            int outputStart,
            int outputStride
        )
        {
            float rangeSq = range * range;
            int texels = width * height;
            for (int index = 0; index < texels; index++)
                distance[distanceStart + index] = rangeSq;

            for (int edge = 0; edge < edgeCount; edge++)
                SplatEdge(edges[edgeStart + edge], width, height, range, distance, distanceStart);

            float inverseRange = 1f / range;

            for (int row = 0; row < height; row++)
            {
                // Crossings of the scanline ray with the half-open convention
                // min(y) <= py < max(y), sorted by x (a handful per row).
                float py = row + 0.5f;
                int crossings = 0;
                for (int edge = 0; edge < edgeCount; edge++)
                {
                    float4 segment = edges[edgeStart + edge];
                    float low = math.min(segment.y, segment.w);
                    float high = math.max(segment.y, segment.w);
                    if (py < low || py >= high)
                        continue;

                    float t = (py - segment.y) / (segment.w - segment.y);
                    float x = segment.x + t * (segment.z - segment.x);
                    int winding = segment.w > segment.y ? 1 : -1;

                    int slot = crossings++;
                    while (slot > 0 && crossingX[crossingStart + slot - 1] > x)
                    {
                        crossingX[crossingStart + slot] = crossingX[crossingStart + slot - 1];
                        crossingWinding[crossingStart + slot] = crossingWinding[crossingStart + slot - 1];
                        slot--;
                    }
                    crossingX[crossingStart + slot] = x;
                    crossingWinding[crossingStart + slot] = winding;
                }

                int windingNumber = 0;
                int next = 0;
                for (int column = 0; column < width; column++)
                {
                    float px = column + 0.5f;
                    while (next < crossings && crossingX[crossingStart + next] < px)
                    {
                        windingNumber += crossingWinding[crossingStart + next];
                        next++;
                    }

                    float squared = distance[distanceStart + row * width + column];

                    // Saturated texels skip the square root.
                    float normalized = squared >= rangeSq ? 1f : math.sqrt(squared) * inverseRange;
                    if (windingNumber != 0)
                        normalized = -normalized;

                    output[outputStart + row * outputStride + column] = (byte)((0.5f + 0.5f * normalized) * 255f + 0.5f);
                }
            }
        }

        // Distances saturate at the range, so an edge only touches the texels inside its
        // bounding box grown by the range: the cost scales with the outline length, not
        // with texel count x edge count. Works on squared distances.
        private static void SplatEdge(
            float4 segment,
            int width,
            int height,
            float range,
            NativeArray<float> distance,
            int distanceStart
        )
        {
            float startX = segment.x;
            float startY = segment.y;
            float segmentX = segment.z - startX;
            float segmentY = segment.w - startY;

            int minColumn = math.max(0, (int)math.floor(math.min(segment.x, segment.z) - range - 0.5f));
            int maxColumn = math.min(width - 1, (int)math.ceil(math.max(segment.x, segment.z) + range - 0.5f));
            int minRow = math.max(0, (int)math.floor(math.min(segment.y, segment.w) - range - 0.5f));
            int maxRow = math.min(height - 1, (int)math.ceil(math.max(segment.y, segment.w) + range - 0.5f));

            float inverseLengthSq = 1f / (segmentX * segmentX + segmentY * segmentY);

            for (int row = minRow; row <= maxRow; row++)
            {
                float offsetY = row + 0.5f - startY;
                int rowStart = distanceStart + row * width;

                for (int column = minColumn; column <= maxColumn; column++)
                {
                    float offsetX = column + 0.5f - startX;
                    float t = math.clamp((offsetX * segmentX + offsetY * segmentY) * inverseLengthSq, 0f, 1f);
                    float deltaX = offsetX - t * segmentX;
                    float deltaY = offsetY - t * segmentY;
                    float candidate = deltaX * deltaX + deltaY * deltaY;

                    int index = rowStart + column;
                    if (candidate < distance[index])
                        distance[index] = candidate;
                }
            }
        }
    }
}
