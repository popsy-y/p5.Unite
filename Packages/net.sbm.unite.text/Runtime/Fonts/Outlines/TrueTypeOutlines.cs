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

        // Scratch memory: outlines are built on the main thread, one glyph at a time
        // (composites nest, so builders come from a small pool).
        private readonly Stack<OutlineBuilder> _builders = new();
        private readonly List<Vector2> _controls = new();
        private float[] _pointX = new float[64];
        private float[] _pointY = new float[64];
        private float[] _deltaX = new float[64];
        private float[] _deltaY = new float[64];
        private float[] _variedX = new float[64];
        private float[] _variedY = new float[64];

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
            WithPhantomPoints(glyphId, glyph, pointCount);

            int[] endPoints = glyph != null && !glyph.IsComposite ? glyph.EndPoints : null;
            if (!_gvar.TryGetDeltas(glyphId, coordinates, _pointX, _pointY, pointCount + PhantomPointCount, endPoints, _deltaX, _deltaY))
                return 0f;

            return _deltaX[pointCount + 1] - _deltaX[pointCount];
        }

        /// <summary>Parses a standalone simple glyph record (used by tests).</summary>
        internal static GlyphOutline ParseSimpleRecord(byte[] record)
        {
            var glyph = ParseRecord(new FontReader(record));
            if (glyph == null || glyph.IsComposite)
                return null;

            var builder = new OutlineBuilder(OutlineFlatness.For(1000));
            EmitContours(builder, glyph, glyph.X, glyph.Y, new List<Vector2>());
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
                int count = glyph.X.Length;
                WithPhantomPoints(glyphId, glyph, count);
                if (_gvar.TryGetDeltas(glyphId, coordinates, _pointX, _pointY, count + PhantomPointCount, glyph.EndPoints, _deltaX, _deltaY))
                {
                    x = _variedX;
                    y = _variedY;
                    for (int index = 0; index < count; index++)
                    {
                        x[index] = glyph.X[index] + _deltaX[index];
                        y[index] = glyph.Y[index] + _deltaY[index];
                    }
                }
            }

            var builder = RentBuilder();
            EmitContours(builder, glyph, x, y, _controls);
            var outline = builder.ToOutline();
            _builders.Push(builder);
            return outline;
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
                WithPhantomPoints(glyphId, glyph, components.Count);
                for (int index = 0; index < components.Count; index++)
                {
                    _pointX[index] = offsetX[index];
                    _pointY[index] = offsetY[index];
                }

                if (_gvar.TryGetDeltas(glyphId, coordinates, _pointX, _pointY, components.Count + PhantomPointCount, null, _deltaX, _deltaY))
                {
                    for (int index = 0; index < components.Count; index++)
                    {
                        offsetX[index] += _deltaX[index];
                        offsetY[index] += _deltaY[index];
                    }
                }
            }

            var builder = RentBuilder();
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

            var outline = builder.ToOutline();
            _builders.Push(builder);
            return outline;
        }

        private OutlineBuilder RentBuilder()
        {
            if (_builders.Count == 0)
                return new OutlineBuilder(_flatness);

            var builder = _builders.Pop();
            builder.Reset();
            return builder;
        }

        private void EnsurePoints(int count)
        {
            if (_pointX.Length >= count)
                return;

            int size = Mathf.Max(count, _pointX.Length * 2);
            _pointX = new float[size];
            _pointY = new float[size];
            _deltaX = new float[size];
            _deltaY = new float[size];
            _variedX = new float[size];
            _variedY = new float[size];
        }

        // Default point positions followed by the four phantom points (horizontal origin
        // and advance; the vertical pair is unused for horizontal text), in _pointX/Y.
        private void WithPhantomPoints(int glyphId, RawGlyph glyph, int pointCount)
        {
            EnsurePoints(pointCount + PhantomPointCount);
            var x = _pointX;
            var y = _pointY;
            System.Array.Clear(x, 0, pointCount + PhantomPointCount);
            System.Array.Clear(y, 0, pointCount + PhantomPointCount);
            if (glyph != null && !glyph.IsComposite)
            {
                System.Array.Copy(glyph.X, x, pointCount);
                System.Array.Copy(glyph.Y, y, pointCount);
            }

            int index = Mathf.Clamp(glyphId, 0, _advances.Length - 1);
            float origin = (glyph != null ? glyph.XMin : 0) - _bearings[index];
            x[pointCount] = origin;
            x[pointCount + 1] = origin + _advances[index];
        }

        private static void EmitContours(OutlineBuilder builder, RawGlyph glyph, float[] x, float[] y, List<Vector2> controls)
        {
            int start = 0;
            foreach (int end in glyph.EndPoints)
            {
                EmitContour(builder, glyph.OnCurve, x, y, start, end, controls);
                start = end + 1;
            }
        }

        // One closed TrueType contour: on-curve points are line vertices, runs of
        // off-curve points are quadratic controls with implied on-curve midpoints.
        private static void EmitContour(
            OutlineBuilder builder,
            bool[] onCurve,
            float[] x,
            float[] y,
            int start,
            int end,
            List<Vector2> controls
        )
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

            if (pivot < 0)
            {
                // No on-curve point at all (round glyphs such as 'o'): start on the
                // implied midpoint between the last and the first control.
                pivot = 0;
                var first = Point(x, y, start, pivot, count, 0);
                var last = Point(x, y, start, pivot, count, count - 1);
                var begin = (first + last) * 0.5f;

                controls.Clear();
                for (int index = 0; index < count; index++)
                    controls.Add(Point(x, y, start, pivot, count, index));

                builder.MoveTo(begin);
                EmitChain(builder, controls, begin, closing: true);
                builder.Close();
                return;
            }

            builder.MoveTo(Point(x, y, start, pivot, count, 0));

            for (int index = 0; index < count;)
            {
                controls.Clear();
                int scan = index + 1;
                while (scan < count && !onCurve[start + (pivot + scan) % count])
                {
                    controls.Add(Point(x, y, start, pivot, count, scan));
                    scan++;
                }

                bool closing = scan == count;
                var target = Point(x, y, start, pivot, count, closing ? 0 : scan);
                EmitChain(builder, controls, target, closing);

                if (closing)
                    break;

                index = scan;
            }

            builder.Close();
        }

        // Point of a contour, counted from its first on-curve point.
        private static Vector2 Point(float[] x, float[] y, int start, int pivot, int count, int offset)
        {
            int index = start + (pivot + offset) % count;
            return new Vector2(x[index], y[index]);
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
