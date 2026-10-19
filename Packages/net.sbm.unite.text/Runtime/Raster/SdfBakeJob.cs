using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unite.Text.Raster
{
    /// <summary>One glyph for <see cref="SdfBakeJob"/>: where its edges, scratch memory and output rect are.</summary>
    internal struct SdfBakeTask
    {
        public int EdgeStart;
        public int EdgeCount;
        public int Width;
        public int Height;
        public float Range;
        public int DistanceStart;
        public int CrossingStart;
        public int OutputStart;
    }

    /// <summary>
    /// Encodes many glyphs in parallel straight into one atlas page (rects of different
    /// tasks never overlap, so the shared output array needs no restriction).
    /// </summary>
    [BurstCompile(DisableSafetyChecks = true)]
    internal struct SdfBakeJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> Edges;
        [ReadOnly] public NativeArray<SdfBakeTask> Tasks;
        [NativeDisableParallelForRestriction] public NativeArray<float> Distance;
        [NativeDisableParallelForRestriction] public NativeArray<float> CrossingX;
        [NativeDisableParallelForRestriction] public NativeArray<int> CrossingWinding;
        [NativeDisableParallelForRestriction] public NativeArray<byte> Output;
        public int OutputStride;

        public void Execute(int index)
        {
            var task = Tasks[index];
            SdfKernel.Encode(
                Edges,
                task.EdgeStart,
                task.EdgeCount,
                task.Width,
                task.Height,
                task.Range,
                Distance,
                task.DistanceStart,
                CrossingX,
                CrossingWinding,
                task.CrossingStart,
                Output,
                task.OutputStart,
                OutputStride
            );
        }
    }
}
