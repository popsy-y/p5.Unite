using Unity.Profiling;

namespace Unite.Text
{
    /// <summary>Profiler markers for the text pipeline (zero cost when the profiler is off).</summary>
    internal static class TextProfiling
    {
        public const string Category = "Unite.Text";

        public static readonly ProfilerMarker Draw = new("Unite.Text.Draw");
        public static readonly ProfilerMarker Layout = new("Unite.Text.Layout");
        public static readonly ProfilerMarker Outline = new("Unite.Text.Outline");
        public static readonly ProfilerMarker Gvar = new("Unite.Text.Outline.Gvar");
        public static readonly ProfilerMarker Flatten = new("Unite.Text.Outline.Flatten");
        public static readonly ProfilerMarker Analyze = new("Unite.Text.Shape.Analyze");
        public static readonly ProfilerMarker Shape = new("Unite.Text.Shape");
        public static readonly ProfilerMarker Normalize = new("Unite.Text.Shape.Normalize");
        public static readonly ProfilerMarker Substitute = new("Unite.Text.Shape.Gsub");
        public static readonly ProfilerMarker Position = new("Unite.Text.Shape.Gpos");
        public static readonly ProfilerMarker Allocate = new("Unite.Text.Allocate");
        public static readonly ProfilerMarker NewPage = new("Unite.Text.Allocate.NewPage");
        public static readonly ProfilerMarker Dispatch = new("Unite.Text.Bake.Dispatch");
        public static readonly ProfilerMarker Enqueue = new("Unite.Text.Enqueue");
        public static readonly ProfilerMarker Flush = new("Unite.Text.Flush");
        public static readonly ProfilerMarker Edges = new("Unite.Text.Bake.Edges");
        public static readonly ProfilerMarker Job = new("Unite.Text.Bake.JobComplete");
        public static readonly ProfilerMarker Maintain = new("Unite.Text.Maintain");
        public static readonly ProfilerMarker Upload = new("Unite.Text.Upload");
    }
}
