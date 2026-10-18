using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Outlines from the glyf / loca tables. Simple glyphs are quadratic contours,
    /// composites reference other glyphs (translation + 2x2 matrix). Variable fonts apply
    /// gvar deltas to the points (or component offsets) before the path is built.
    /// </summary>
    internal sealed partial class TrueTypeOutlines : IOutlineSource
    {
        private const int MaxCompositeDepth = 8;
        private const int PhantomPointCount = 4;

        private readonly byte[] _data;
        private readonly int _glyfStart;
        private readonly int[] _locaOffsets;
        private readonly ushort[] _advances;
        private readonly short[] _bearings;
        private readonly GvarTable _gvar;
        private readonly float _flatness;
        private readonly Dictionary<int, RawGlyph> _glyphs = new();

        private TrueTypeOutlines(
            byte[] data,
            int glyfStart,
            int[] locaOffsets,
            ushort[] advances,
            short[] bearings,
            GvarTable gvar,
            float flatness
        )
        {
            _flatness = flatness;
            _data = data;
            _glyfStart = glyfStart;
            _locaOffsets = locaOffsets;
            _advances = advances;
            _bearings = bearings;
            _gvar = gvar;
        }

        public static TrueTypeOutlines Create(
            byte[] data,
            SfntDirectory directory,
            int glyphCount,
            int indexToLocFormat,
            ushort[] advances,
            short[] bearings,
            int unitsPerEm
        )
        {
            var loca = directory.Get("loca");
            int entryCount = glyphCount + 1;
            long needed = (long)(indexToLocFormat == 0 ? 2 : 4) * entryCount;
            if (needed > loca.Length)
                throw new FontFormatException("loca table is truncated.");

            var reader = new FontReader(data, loca.Offset);
            var offsets = new int[entryCount];
            for (int index = 0; index < entryCount; index++)
            {
                offsets[index] = indexToLocFormat == 0
                    ? reader.ReadU16() * 2
                    : (int)reader.ReadU32();
            }

            return new TrueTypeOutlines(
                data,
                directory.Get("glyf").Offset,
                offsets,
                advances,
                bearings,
                GvarTable.TryRead(data, directory),
                OutlineFlatness.For(unitsPerEm)
            );
        }

        public bool HasVariations => _gvar != null;

        public GlyphOutline Load(int glyphId, float[] coordinates)
            => Build(glyphId, UseVariations(coordinates), 0);

        public float AdvanceDelta(int glyphId, float[] coordinates)
        {
            coordinates = UseVariations(coordinates);
            if (coordinates == null)
                return 0f;

            var glyph = GetGlyph(glyphId);
            int pointCount = glyph == null ? 0 : (glyph.IsComposite ? glyph.Components.Count : glyph.X.Length);
            var (x, y) = WithPhantomPoints(glyphId, glyph, pointCount);
            var deltaX = new float[x.Length];
            var deltaY = new float[x.Length];

            int[] endPoints = glyph != null && !glyph.IsComposite ? glyph.EndPoints : null;
            if (!_gvar.TryGetDeltas(glyphId, coordinates, x, y, endPoints, deltaX, deltaY))
                return 0f;

            return deltaX[pointCount + 1] - deltaX[pointCount];
        }

        /// <summary>Parses a standalone simple glyph record (used by tests).</summary>
        internal static GlyphOutline ParseSimpleRecord(byte[] record)
        {
            var glyph = ParseRecord(new FontReader(record));
            if (glyph == null || glyph.IsComposite)
                return null;

            var builder = new OutlineBuilder(OutlineFlatness.For(1000));
            EmitContours(builder, glyph, glyph.X, glyph.Y);
            return builder.ToOutline();
        }

        private float[] UseVariations(float[] coordinates)
            => _gvar != null && coordinates != null && !FontVariations.IsDefault(coordinates) ? coordinates : null;

        private RawGlyph GetGlyph(int glyphId)
        {
            if (_glyphs.TryGetValue(glyphId, out var cached))
                return cached;

            RawGlyph glyph = null;
            if (glyphId >= 0 && glyphId + 1 < _locaOffsets.Length)
            {
                int start = _locaOffsets[glyphId];
                int end = _locaOffsets[glyphId + 1];
                if (end > start)
                    glyph = ParseRecord(new FontReader(_data, _glyfStart + start));
            }

            _glyphs[glyphId] = glyph;
            return glyph;
        }

        private GlyphOutline Build(int glyphId, float[] coordinates, int depth)
        {
            if (depth > MaxCompositeDepth)
                return null;

            var glyph = GetGlyph(glyphId);
            if (glyph == null)
                return null;

            return glyph.IsComposite
                ? BuildComposite(glyphId, glyph, coordinates, depth)
                : BuildSimple(glyphId, glyph, coordinates);
        }

        private GlyphOutline BuildSimple(int glyphId, RawGlyph glyph, float[] coordinates)
        {
            var x = glyph.X;
            var y = glyph.Y;

            if (coordinates != null)
            {
                var (phantomX, phantomY) = WithPhantomPoints(glyphId, glyph, x.Length);
                var deltaX = new float[phantomX.Length];
                var deltaY = new float[phantomX.Length];
                if (_gvar.TryGetDeltas(glyphId, coordinates, phantomX, phantomY, glyph.EndPoints, deltaX, deltaY))
                {
                    x = new float[x.Length];
                    y = new float[y.Length];
                    for (int index = 0; index < x.Length; index++)
                    {
                        x[index] = glyph.X[index] + deltaX[index];
                        y[index] = glyph.Y[index] + deltaY[index];
                    }
                }
            }

            var builder = new OutlineBuilder(_flatness);
            EmitContours(builder, glyph, x, y);
            return builder.ToOutline();
        }

        private GlyphOutline BuildComposite(int glyphId, RawGlyph glyph, float[] coordinates, int depth)
        {
            var components = glyph.Components;
            var offsetX = new float[components.Count];
            var offsetY = new float[components.Count];
            for (int index = 0; index < components.Count; index++)
            {
                offsetX[index] = components[index].Dx;
                offsetY[index] = components[index].Dy;
            }

            if (coordinates != null)
            {
                // Component offsets are the "points" of a composite glyph.
                var (x, y) = WithPhantomPoints(glyphId, glyph, components.Count);
                for (int index = 0; index < components.Count; index++)
                {
                    x[index] = offsetX[index];
                    y[index] = offsetY[index];
                }

                var deltaX = new float[x.Length];
                var deltaY = new float[x.Length];
                if (_gvar.TryGetDeltas(glyphId, coordinates, x, y, null, deltaX, deltaY))
                {
                    for (int index = 0; index < components.Count; index++)
                    {
                        offsetX[index] += deltaX[index];
                        offsetY[index] += deltaY[index];
                    }
                }
            }

            var builder = new OutlineBuilder(_flatness);
            for (int index = 0; index < components.Count; index++)
            {
                var component = components[index];
                var child = Build(component.GlyphId, coordinates, depth + 1);
                if (child != null)
                {
                    builder.AddTransformed(
                        child, component.A, component.B, component.C, component.D, offsetX[index], offsetY[index]
                    );
                }
            }

            return builder.ToOutline();
        }

        // Default point positions followed by the four phantom points: horizontal origin
        // and advance (the vertical pair is unused for horizontal text).
        private (float[] X, float[] Y) WithPhantomPoints(int glyphId, RawGlyph glyph, int pointCount)
        {
            var x = new float[pointCount + PhantomPointCount];
            var y = new float[pointCount + PhantomPointCount];
            if (glyph != null && !glyph.IsComposite)
            {
                System.Array.Copy(glyph.X, x, pointCount);
                System.Array.Copy(glyph.Y, y, pointCount);
            }

            int index = Mathf.Clamp(glyphId, 0, _advances.Length - 1);
            float origin = (glyph != null ? glyph.XMin : 0) - _bearings[index];
            x[pointCount] = origin;
            x[pointCount + 1] = origin + _advances[index];
            return (x, y);
        }

        private static void EmitContours(OutlineBuilder builder, RawGlyph glyph, float[] x, float[] y)
        {
            int start = 0;
            foreach (int end in glyph.EndPoints)
            {
                EmitContour(builder, glyph.OnCurve, x, y, start, end);
                start = end + 1;
            }
        }

        // One closed TrueType contour: on-curve points are line vertices, runs of
        // off-curve points are quadratic controls with implied on-curve midpoints.
        private static void EmitContour(OutlineBuilder builder, bool[] onCurve, float[] x, float[] y, int start, int end)
        {
            int count = end - start + 1;
            if (count < 2)
                return;

            int pivot = -1;
            for (int index = 0; index < count; index++)
            {
                if (onCurve[start + index])
                {
                    pivot = index;
                    break;
                }
            }

            Vector2 Point(int offset) => new(x[start + (pivot + offset) % count], y[start + (pivot + offset) % count]);

            if (pivot < 0)
            {
                // No on-curve point at all (round glyphs such as 'o'): start on the
                // implied midpoint between the last and the first control.
                pivot = 0;
                var first = Point(0);
                var last = Point(count - 1);
                var begin = (first + last) * 0.5f;

                var controls = new List<Vector2>(count);
                for (int index = 0; index < count; index++)
                    controls.Add(Point(index));

                builder.MoveTo(begin);
                EmitChain(builder, controls, begin, closing: true);
                builder.Close();
                return;
            }

            builder.MoveTo(Point(0));

            for (int index = 0; index < count;)
            {
                var run = new List<Vector2>(4);
                int scan = index + 1;
                while (scan < count && !onCurve[start + (pivot + scan) % count])
                {
                    run.Add(Point(scan));
                    scan++;
                }

                bool closing = scan == count;
                var target = closing ? Point(0) : Point(scan);
                EmitChain(builder, run, target, closing);

                if (closing)
                    break;

                index = scan;
            }

            builder.Close();
        }

        private static void EmitChain(OutlineBuilder builder, List<Vector2> controls, Vector2 end, bool closing)
        {
            if (controls.Count == 0)
            {
                // The closing straight edge comes with Close().
                if (!closing)
                    builder.LineTo(end);
                return;
            }

            for (int index = 0; index < controls.Count; index++)
            {
                var next = index == controls.Count - 1
                    ? end
                    : (controls[index] + controls[index + 1]) * 0.5f;
                builder.QuadraticTo(controls[index], next);
            }
        }
    }
}
