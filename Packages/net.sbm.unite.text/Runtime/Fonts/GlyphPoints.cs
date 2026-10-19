using System.Collections.Generic;
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
        // Varied points live from LoadPoints until the bake job has copied them, so the
        // objects are recycled and keep their arrays (loaded and released on the main
        // thread). ArrayPool.Shared would not do: a frame rents hundreds of arrays before
        // the first one comes back, far more than it keeps per size.
        private static readonly Stack<GlyphPoints> Pool = new();
        private float[] _ownX;
        private float[] _ownY;

        public GlyphPoints(
            float[] x,
            float[] y,
            int count,
            bool[] onCurve,
            int[] endPoints,
            Vector2 boundsMin,
            Vector2 boundsMax,
            float flatness
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
        }

        private GlyphPoints()
        {
        }

        /// <summary>Point coordinates; only the first <see cref="Count"/> entries are used.</summary>
        public float[] X { get; private set; }

        public float[] Y { get; private set; }

        public int Count { get; private set; }

        /// <summary>True when the points (and their arrays) are rented from the pool (varied, short-lived points).</summary>
        public bool IsPooled { get; private set; }

        public bool[] OnCurve { get; private set; }

        /// <summary>Index of the last point of each contour.</summary>
        public int[] EndPoints { get; private set; }

        /// <summary>Bounding box of all points (curves never leave their control polygon).</summary>
        public Vector2 BoundsMin { get; private set; }

        public Vector2 BoundsMax { get; private set; }

        /// <summary>Largest allowed distance, in glyph units, between a curve and its polyline.</summary>
        public float Flatness { get; private set; }

        /// <summary>
        /// Rents <see cref="IsPooled"/> points with room for <paramref name="count"/>
        /// coordinates; the caller fills X / Y and then calls <see cref="SetShape"/>.
        /// </summary>
        public static GlyphPoints Rent(int count)
        {
            var points = Pool.Count > 0 ? Pool.Pop() : new GlyphPoints();
            if (points._ownX == null || points._ownX.Length < count)
            {
                int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 16));
                points._ownX = new float[capacity];
                points._ownY = new float[capacity];
            }
            points.X = points._ownX;
            points.Y = points._ownY;
            points.Count = count;
            points.IsPooled = true;
            return points;
        }

        public void SetShape(bool[] onCurve, int[] endPoints, Vector2 boundsMin, Vector2 boundsMax, float flatness)
        {
            OnCurve = onCurve;
            EndPoints = endPoints;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Flatness = flatness;
        }

        /// <summary>
        /// Returns pooled points once they have been copied into the bake job; they must not
        /// be read afterwards. No-op for shared (default master) points.
        /// </summary>
        public void Release()
        {
            if (!IsPooled || X == null)
                return;

            X = null;
            Y = null;
            OnCurve = null;
            EndPoints = null;
            Pool.Push(this);
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
