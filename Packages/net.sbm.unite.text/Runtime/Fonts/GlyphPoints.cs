using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// The contour points of a simple TrueType glyph after variation deltas, in glyph
    /// units. The atlas flattens the quadratic curves and builds the SDF edges from these
    /// inside a Burst job, so no per-glyph polygon arrays are allocated.
    /// </summary>
    internal sealed class GlyphPoints
    {
        public GlyphPoints(
            float[] x,
            float[] y,
            bool[] onCurve,
            int[] endPoints,
            Vector2 boundsMin,
            Vector2 boundsMax,
            float flatness
        )
        {
            X = x;
            Y = y;
            OnCurve = onCurve;
            EndPoints = endPoints;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Flatness = flatness;
        }

        public float[] X { get; }
        public float[] Y { get; }
        public bool[] OnCurve { get; }

        /// <summary>Index of the last point of each contour.</summary>
        public int[] EndPoints { get; }

        /// <summary>Bounding box of all points (curves never leave their control polygon).</summary>
        public Vector2 BoundsMin { get; }

        public Vector2 BoundsMax { get; }

        /// <summary>Largest allowed distance, in glyph units, between a curve and its polyline.</summary>
        public float Flatness { get; }
    }

    /// <summary>
    /// What the atlas bakes a glyph from: contour points (TrueType simple glyphs, flattened
    /// in the bake job) or a finished polygon outline (composites, CFF).
    /// </summary>
    internal readonly struct GlyphBakeSource
    {
        public GlyphBakeSource(GlyphPoints points)
        {
            Points = points;
            Outline = null;
            BoundsMin = points.BoundsMin;
            BoundsMax = points.BoundsMax;
        }

        public GlyphBakeSource(GlyphOutline outline)
        {
            Points = null;
            Outline = outline;
            BoundsMin = outline.BoundsMin;
            BoundsMax = outline.BoundsMax;
        }

        public GlyphPoints Points { get; }
        public GlyphOutline Outline { get; }
        public Vector2 BoundsMin { get; }
        public Vector2 BoundsMax { get; }

        /// <summary>True for blank glyphs (nothing to bake).</summary>
        public bool IsEmpty => Points == null && Outline == null;
    }
}
