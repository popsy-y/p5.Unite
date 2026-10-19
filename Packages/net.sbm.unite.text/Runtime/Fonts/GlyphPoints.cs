using System.Buffers;
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
            int count,
            bool[] onCurve,
            int[] endPoints,
            Vector2 boundsMin,
            Vector2 boundsMax,
            float flatness,
            bool pooled = false
        )
        {
            X = x;
            Y = y;
            Count = count;
            OnCurve = onCurve;
            EndPoints = endPoints;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Flatness = flatness;
            IsPooled = pooled;
        }

        /// <summary>Point coordinates; only the first <see cref="Count"/> entries are used.</summary>
        public float[] X { get; private set; }

        public float[] Y { get; private set; }

        public int Count { get; }

        /// <summary>True when X / Y are rented from the shared pool (varied, short-lived points).</summary>
        public bool IsPooled { get; }

        public bool[] OnCurve { get; }

        /// <summary>Index of the last point of each contour.</summary>
        public int[] EndPoints { get; }

        /// <summary>Bounding box of all points (curves never leave their control polygon).</summary>
        public Vector2 BoundsMin { get; }

        public Vector2 BoundsMax { get; }

        /// <summary>Largest allowed distance, in glyph units, between a curve and its polyline.</summary>
        public float Flatness { get; }

        /// <summary>Rents coordinate arrays for <see cref="IsPooled"/> points.</summary>
        public static float[] Rent(int count) => ArrayPool<float>.Shared.Rent(count);

        /// <summary>
        /// Returns pooled coordinate arrays once the points have been copied into the bake
        /// job; the points must not be read afterwards. No-op for shared (default master) points.
        /// </summary>
        public void Release()
        {
            if (!IsPooled || X == null)
                return;

            ArrayPool<float>.Shared.Return(X);
            ArrayPool<float>.Shared.Return(Y);
            X = null;
            Y = null;
        }
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
