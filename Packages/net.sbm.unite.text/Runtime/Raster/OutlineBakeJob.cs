using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unite.Text.Raster
{
    /// <summary>One simple TrueType glyph for <see cref="OutlineBakeJob"/>: its contour points, edge room and output rect.</summary>
    internal struct OutlineBakeTask
    {
        public int PointStart;
        public int ContourStart;
        public int ContourCount;
        public float Scale;
        public float OriginX;
        public float OriginY;
        public float Flatness;

        /// <summary>Start and room of the glyph's edges; the crossing arrays use the same offsets.</summary>
        public int EdgeStart;
        public int EdgeCapacity;

        public int Width;
        public int Height;
        public float Range;
        public int DistanceStart;
        public int OutputStart;
    }

    /// <summary>
    /// Turns TrueType contour points into SDF pixels in one pass per glyph: resolves the
    /// implied on-curve points, flattens the quadratic curves into edges (texel space) and
    /// encodes them with <see cref="SdfKernel"/>, straight into the atlas page. Mirrors
    /// the polygons of <c>OutlineBuilder</c> without allocating managed arrays.
    /// </summary>
    [BurstCompile]
    internal struct OutlineBakeJob : IJobParallelFor
    {
        public const int MaxSubdivisions = 48;
        private const int MinSubdivisions = 2;
        private const float CoincidentSquared = 1e-6f;

        [ReadOnly] public NativeArray<float2> Points;
        [ReadOnly] public NativeArray<byte> OnCurve;
        [ReadOnly] public NativeArray<int> EndPoints;
        [ReadOnly] public NativeArray<OutlineBakeTask> Tasks;
        [NativeDisableParallelForRestriction] public NativeArray<float4> Edges;
        [NativeDisableParallelForRestriction] public NativeArray<float> Distance;
        [NativeDisableParallelForRestriction] public NativeArray<float> CrossingX;
        [NativeDisableParallelForRestriction] public NativeArray<int> CrossingWinding;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Output;
        public int OutputStride;

        public void Execute(int index)
        {
            var task = Tasks[index];

            int count = 0;
            int contourStart = 0;
            for (int contour = 0; contour < task.ContourCount; contour++)
            {
                int contourEnd = EndPoints[task.ContourStart + contour];
                AppendContour(task, contourStart, contourEnd, ref count);
                contourStart = contourEnd + 1;
            }

            SdfKernel.Encode(
                Edges,
                task.EdgeStart,
                count,
                task.Width,
                task.Height,
                task.Range,
                Distance,
                task.DistanceStart,
                CrossingX,
                CrossingWinding,
                task.EdgeStart,
                Output,
                task.OutputStart,
                OutputStride
            );
        }

        // One closed TrueType contour: on-curve points are line vertices, runs of
        // off-curve points are quadratic controls with implied on-curve midpoints.
        private void AppendContour(in OutlineBakeTask task, int start, int end, ref int count)
        {
            int length = end - start + 1;
            if (length < 2)
                return;

            int edgesBefore = count;
            int pivot = -1;
            for (int offset = 0; offset < length; offset++)
            {
                if (OnCurve[task.PointStart + start + offset] != 0)
                {
                    pivot = offset;
                    break;
                }
            }

            float2 first;
            float2 position;
            if (pivot < 0)
            {
                // No on-curve point at all (round glyphs such as 'o'): start on the
                // implied midpoint between the last and the first control.
                first = (PointAt(task, start, 0, length, 0) + PointAt(task, start, 0, length, length - 1)) * 0.5f;
                position = first;
                AppendChain(task, start, 0, length, 0, length, first, closing: true, ref position, ref count);
            }
            else
            {
                first = PointAt(task, start, pivot, length, 0);
                position = first;

                for (int offset = 0; offset < length;)
                {
                    int scan = offset + 1;
                    while (scan < length && OnCurve[task.PointStart + start + (pivot + scan) % length] == 0)
                        scan++;

                    bool closing = scan == length;
                    float2 target = PointAt(task, start, pivot, length, closing ? 0 : scan);
                    AppendChain(task, start, pivot, length, offset + 1, scan - offset - 1, target, closing, ref position, ref count);

                    if (closing)
                        break;

                    offset = scan;
                }
            }

            Close(task, first, position, edgesBefore, ref count);
        }

        // Closes the polygon: a contour that returns to its start point does not repeat it.
        private void Close(in OutlineBakeTask task, float2 first, float2 last, int edgesBefore, ref int count)
        {
            if (count > edgesBefore && math.lengthsq(last - first) < CoincidentSquared)
            {
                int index = task.EdgeStart + count - 1;
                var edge = Edges[index];
                edge.zw = Texel(task, first);
                if (math.all(edge.xy == edge.zw))
                {
                    count--;
                    return;
                }
                Edges[index] = edge;
                return;
            }

            AddEdge(task, last, first, ref count);
        }

        // The controls first..first+controlCount-1 (contour offsets from the pivot) up to target.
        private void AppendChain(
            in OutlineBakeTask task,
            int start,
            int pivot,
            int length,
            int first,
            int controlCount,
            float2 target,
            bool closing,
            ref float2 position,
            ref int count
        )
        {
            if (controlCount == 0)
            {
                // The closing straight edge comes with Close().
                if (!closing)
                {
                    AddEdge(task, position, target, ref count);
                    position = target;
                }
                return;
            }

            for (int index = 0; index < controlCount; index++)
            {
                float2 control = PointAt(task, start, pivot, length, first + index);
                float2 next = index == controlCount - 1
                    ? target
                    : (control + PointAt(task, start, pivot, length, first + index + 1)) * 0.5f;
                AppendQuadratic(task, control, next, ref position, ref count);
            }
        }

        // Polyline error is at most |B''| / (8 n^2) with |B''| = 2 |P0 - 2 P1 + P2|.
        private void AppendQuadratic(in OutlineBakeTask task, float2 control, float2 end, ref float2 position, ref int count)
        {
            float curvature = math.length(position - 2f * control + end);
            int steps = math.clamp((int)math.ceil(math.sqrt(0.25f * curvature / task.Flatness)), MinSubdivisions, MaxSubdivisions);

            float2 previous = position;
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                float u = 1f - t;
                float2 point = u * u * position + 2f * u * t * control + t * t * end;
                AddEdge(task, previous, point, ref count);
                previous = point;
            }
            position = end;
        }

        private void AddEdge(in OutlineBakeTask task, float2 from, float2 to, ref int count)
        {
            if (math.all(from == to) || count >= task.EdgeCapacity)
                return;

            Edges[task.EdgeStart + count] = new float4(Texel(task, from), Texel(task, to));
            count++;
        }

        private float2 PointAt(in OutlineBakeTask task, int start, int pivot, int length, int offset)
            => Points[task.PointStart + start + (pivot + offset) % length];

        private static float2 Texel(in OutlineBakeTask task, float2 point)
            => point * task.Scale - new float2(task.OriginX, task.OriginY);
    }
}
