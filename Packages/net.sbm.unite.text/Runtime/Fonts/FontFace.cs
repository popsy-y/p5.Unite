using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// A parsed font: metrics, character map and a source of glyph outlines, plus the
    /// variation axes of variable fonts. Whatever the file format (TrueType, OpenType/CFF,
    /// WOFF, WOFF2) the face looks the same. Drawing goes through a
    /// <see cref="FontInstance"/>: the default instance, or one per set of axis values.
    /// </summary>
    public sealed class FontFace
    {
        private const int MaxCachedInstances = 64;

        private static int _nextInstanceId;

        private readonly byte[] _data;
        private readonly int _cmapOffset;
        private readonly int _cmapLength;
        private readonly ushort[] _advances;
        private readonly IOutlineSource _outlines;
        private readonly FontVariations _variations;
        private readonly Dictionary<string, FontInstance> _instances = new();
        private readonly Queue<string> _instanceOrder = new();

        private FontFace(
            byte[] data,
            int cmapOffset,
            int cmapLength,
            ushort[] advances,
            IOutlineSource outlines,
            FontVariations variations,
            int unitsPerEm,
            int ascender,
            int descender
        )
        {
            _data = data;
            _cmapOffset = cmapOffset;
            _cmapLength = cmapLength;
            _advances = advances;
            _outlines = outlines;
            _variations = variations;
            UnitsPerEm = unitsPerEm;
            Ascender = ascender;
            Descender = descender;
            Default = new FontInstance(this, null, NewInstanceId());
        }

        public int UnitsPerEm { get; }

        /// <summary>Ascender of the default master (hhea), in glyph units.</summary>
        public int Ascender { get; }

        /// <summary>Descender of the default master (hhea, negative), in glyph units.</summary>
        public int Descender { get; }

        public int GlyphCount => _advances.Length;

        /// <summary>True when the font has variation axes (fvar).</summary>
        public bool IsVariable => _variations != null;

        /// <summary>Variation axes with their user-space ranges; empty for static fonts.</summary>
        public IReadOnlyList<VariationAxis> Axes => (IReadOnlyList<VariationAxis>)_variations?.Axes ?? Array.Empty<VariationAxis>();

        /// <summary>The default instance (default axis values).</summary>
        public FontInstance Default { get; }

        /// <summary>
        /// Parses a font file: TrueType (.ttf), OpenType with TrueType or CFF outlines
        /// (.otf), WOFF and WOFF2. Returns false for anything else or for corrupt data.
        /// </summary>
        public static bool TryParse(byte[] fontData, out FontFace face)
            => FontLoader.TryLoad(fontData, out face, out _);

        /// <summary>
        /// The instance for the given axis values (user-space units, e.g. wght 700). Axes
        /// that are not mentioned keep their default; unknown axes are ignored. Up to 64
        /// instances are cached, so callers that animate axes should snap their values
        /// (text drawing does).
        /// </summary>
        public FontInstance GetInstance(IReadOnlyList<AxisSetting> settings)
        {
            if (_variations == null || settings == null || settings.Count == 0)
                return Default;

            var coordinates = _variations.Normalize(settings);
            if (FontVariations.IsDefault(coordinates))
                return Default;

            string key = CoordinateKey(coordinates);
            if (_instances.TryGetValue(key, out var instance))
                return instance;

            if (_instances.Count >= MaxCachedInstances)
                _instances.Remove(_instanceOrder.Dequeue());

            instance = new FontInstance(this, coordinates, NewInstanceId());
            _instances.Add(key, instance);
            _instanceOrder.Enqueue(key);
            return instance;
        }

        /// <summary>Character-to-glyph lookup (cmap only, no shaping); 0 (.notdef) when unmapped.</summary>
        public int GetGlyphId(int codePoint)
            => CmapTables.Lookup(new FontReader(_data), _cmapOffset, _cmapLength, codePoint);

        internal float BaseAdvance(int glyphId)
            => _advances[Mathf.Clamp(glyphId, 0, _advances.Length - 1)];

        internal float AdvanceDelta(int glyphId, float[] coordinates)
        {
            if (_variations == null)
                return 0f;

            return _variations.HasAdvanceVariations
                ? _variations.AdvanceDelta(glyphId, coordinates)
                : _outlines.AdvanceDelta(glyphId, coordinates);
        }

        internal float MetricDelta(string tag, float[] coordinates)
            => _variations == null || coordinates == null ? 0f : _variations.MetricDelta(tag, coordinates);

        internal GlyphOutline LoadOutline(int glyphId, float[] coordinates)
            => glyphId < 0 || glyphId >= GlyphCount ? null : _outlines.Load(glyphId, coordinates);

        /// <summary>Builds a face from plain sfnt bytes (see <see cref="FontLoader"/>).</summary>
        internal static FontFace Create(byte[] sfnt)
        {
            var directory = SfntDirectory.Read(sfnt);

            var head = directory.Get("head");
            if (head.Length < 54)
                throw new FontFormatException("head table is truncated.");

            var reader = new FontReader(sfnt, head.Offset + 18);
            int unitsPerEm = reader.ReadU16();
            if (unitsPerEm < 1)
                throw new FontFormatException("head unitsPerEm must be at least 1.");

            reader.Seek(head.Offset + 50);
            int indexToLocFormat = reader.ReadI16();

            var maxp = directory.Get("maxp");
            reader.Seek(maxp.Offset + 4);
            int glyphCount = reader.ReadU16();
            if (glyphCount == 0)
                throw new FontFormatException("the font has no glyphs.");

            var hhea = directory.Get("hhea");
            if (hhea.Length < 36)
                throw new FontFormatException("hhea table is truncated.");

            reader.Seek(hhea.Offset + 4);
            int ascender = reader.ReadI16();
            int descender = reader.ReadI16();
            reader.Seek(hhea.Offset + 34);
            int metricCount = reader.ReadU16();

            ReadHmtx(sfnt, directory.Get("hmtx"), glyphCount, metricCount, out var advances, out var bearings);

            var cmap = directory.Get("cmap");
            if (!CmapTables.HasSupportedSubtable(sfnt, cmap.Offset, cmap.Length))
                throw new FontFormatException("the font has no supported character map (cmap format 4, 12 or 0).");

            var variations = FontVariations.TryRead(sfnt, directory);
            var outlines = CreateOutlines(sfnt, directory, glyphCount, indexToLocFormat, advances, bearings, unitsPerEm);

            return new FontFace(
                sfnt, cmap.Offset, cmap.Length, advances, outlines, variations, unitsPerEm, ascender, descender
            );
        }

        private static IOutlineSource CreateOutlines(
            byte[] sfnt,
            SfntDirectory directory,
            int glyphCount,
            int indexToLocFormat,
            ushort[] advances,
            short[] bearings,
            int unitsPerEm
        )
        {
            if (directory.Has("glyf") && directory.Has("loca"))
            {
                if (indexToLocFormat != 0 && indexToLocFormat != 1)
                    throw new FontFormatException("unsupported loca format.");

                return TrueTypeOutlines.Create(sfnt, directory, glyphCount, indexToLocFormat, advances, bearings, unitsPerEm);
            }

            if (directory.Has("CFF "))
                return CffOutlines.Create(sfnt, directory.Get("CFF "), unitsPerEm);

            if (directory.Has("CFF2"))
                throw new FontFormatException("variable OpenType/CFF2 outlines are not supported yet.");

            throw new FontFormatException("the font has no glyph outlines (no glyf or CFF table).");
        }

        private static void ReadHmtx(
            byte[] sfnt,
            TableRecord hmtx,
            int glyphCount,
            int metricCount,
            out ushort[] advances,
            out short[] bearings
        )
        {
            if (metricCount <= 0 || metricCount > glyphCount)
                throw new FontFormatException("hhea.numberOfHMetrics is out of range.");

            long needed = (long)metricCount * 4 + (long)(glyphCount - metricCount) * 2;
            if (needed > hmtx.Length)
                throw new FontFormatException("hmtx table is truncated.");

            var reader = new FontReader(sfnt, hmtx.Offset);
            advances = new ushort[glyphCount];
            bearings = new short[glyphCount];
            for (int index = 0; index < glyphCount; index++)
            {
                // Trailing glyphs share the last tabulated advance and only store a bearing.
                advances[index] = index < metricCount ? reader.ReadU16() : advances[metricCount - 1];
                bearings[index] = reader.ReadI16();
            }
        }

        private static int NewInstanceId() => ++_nextInstanceId;

        private static string CoordinateKey(float[] coordinates)
        {
            var parts = new string[coordinates.Length];
            for (int index = 0; index < coordinates.Length; index++)
                parts[index] = BitConverter.SingleToInt32Bits(coordinates[index]).ToString();
            return string.Join(",", parts);
        }
    }
}
