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
    public sealed partial class FontFace
    {
        private const int MaxCachedInstances = 64;

        // Ids of evicted instances are remembered for this many coordinate sets (oldest
        // first out), so continuously animated axes do not grow the table without bound.
        internal const int MaxRememberedInstanceIds = 4096;

        private static int _nextInstanceId;

        private readonly CharacterMap _characters;
        private readonly ushort[] _advances;
        private readonly IOutlineSource _outlines;
        private readonly FontVariations _variations;
        private readonly Dictionary<CoordinateKey, FontInstance> _instances = new();
        private readonly Dictionary<CoordinateKey, int> _instanceIds = new();
        private CoordinateKey[] _instanceIdOrder;
        private int _instanceIdNext;
        private float[] _coordinateScratch;

        private FontFace(
            CharacterMap characters,
            ushort[] advances,
            IOutlineSource outlines,
            FontVariations variations,
            int unitsPerEm,
            int ascender,
            int descender
        )
        {
            _characters = characters;
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

        /// <summary>GDEF / GSUB / GPOS / kern tables for shaping.</summary>
        internal Layout.OpenTypeLayout OpenType { get; private set; }

        /// <summary>
        /// Parses a font file: TrueType (.ttf), OpenType with TrueType or CFF outlines
        /// (.otf), WOFF and WOFF2. Returns false for anything else or for corrupt data.
        /// </summary>
        public static bool TryParse(byte[] fontData, out FontFace face)
            => FontLoader.TryLoad(fontData, out face, out _);

        /// <summary>
        /// The instance for the given axis values (user-space units, e.g. wght 700). Axes
        /// that are not mentioned keep their default; unknown axes are ignored. The 64 most
        /// recently used instances are cached.
        /// </summary>
        public FontInstance GetInstance(IReadOnlyList<AxisSetting> settings)
        {
            if (_variations == null || settings == null || settings.Count == 0)
                return Default;

            // Looking up an instance that already exists allocates nothing: the key wraps a
            // scratch array and is copied only when a new instance is stored.
            _coordinateScratch ??= new float[_variations.Axes.Length];
            _variations.Normalize(settings, _coordinateScratch);
            if (FontVariations.IsDefault(_coordinateScratch))
                return Default;

            var key = new CoordinateKey(_coordinateScratch);
            if (_instances.TryGetValue(key, out var instance))
            {
                instance.Touch();
                return instance;
            }

            if (_instances.Count >= MaxCachedInstances)
                EvictLeastRecentInstance();

            var coordinates = (float[])_coordinateScratch.Clone();
            key = new CoordinateKey(coordinates);

            // The id depends on the coordinates only, so a re-created instance finds its
            // glyphs still baked in the atlas.
            if (!_instanceIds.TryGetValue(key, out int id))
            {
                id = NewInstanceId();
                RememberInstanceId(key, id);
            }

            instance = new FontInstance(this, coordinates, id);
            _instances.Add(key, instance);
            return instance;
        }

        /// <summary>Number of coordinate sets whose instance id is remembered (bounded).</summary>
        internal int RememberedInstanceIdCount => _instanceIds.Count;

        /// <summary>Character-to-glyph lookup (cmap only, no shaping); 0 (.notdef) when unmapped.</summary>
        public int GetGlyphId(int codePoint)
            => _characters.Lookup(codePoint);

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

        internal GlyphPoints LoadPoints(int glyphId, float[] coordinates)
            => glyphId < 0 || glyphId >= GlyphCount ? null : _outlines.LoadPoints(glyphId, coordinates);

        internal GlyphOutline LoadOutline(int glyphId, float[] coordinates)
            => glyphId < 0 || glyphId >= GlyphCount ? null : _outlines.Load(glyphId, coordinates);

        /// <summary>Builds a face from plain sfnt bytes (see <see cref="FontLoader"/>).</summary>
        internal static FontFace Create(byte[] sfnt, int collectionIndex = 0)
        {
            var directory = SfntDirectory.Read(sfnt, collectionIndex);

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
            var characters = CharacterMap.Read(sfnt, cmap.Offset);
            if (characters == null)
                throw new FontFormatException("the font has no supported character map (cmap format 4, 12 or 0).");

            var variations = FontVariations.TryRead(sfnt, directory);
            var outlines = CreateOutlines(sfnt, directory, glyphCount, indexToLocFormat, advances, bearings, unitsPerEm);

            return new FontFace(
                characters, advances, outlines, variations, unitsPerEm, ascender, descender
            )
            {
                OpenType = new Layout.OpenTypeLayout(sfnt, directory)
            };
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

        private void EvictLeastRecentInstance()
        {
            CoordinateKey? oldest = null;
            long oldestUse = long.MaxValue;
            foreach (var pair in _instances)
            {
                if (pair.Value.LastUse < oldestUse)
                {
                    oldestUse = pair.Value.LastUse;
                    oldest = pair.Key;
                }
            }

            if (oldest != null)
                _instances.Remove(oldest.Value);
        }

        // A ring of the remembered keys: when it is full the oldest id is forgotten. Its
        // instance may still be cached (and keeps its id); only a later re-creation gets a
        // new id, whose glyphs are then baked again.
        private void RememberInstanceId(CoordinateKey key, int id)
        {
            _instanceIdOrder ??= new CoordinateKey[MaxRememberedInstanceIds];
            if (_instanceIds.Count >= MaxRememberedInstanceIds)
                _instanceIds.Remove(_instanceIdOrder[_instanceIdNext]);

            _instanceIdOrder[_instanceIdNext] = key;
            _instanceIdNext = (_instanceIdNext + 1) % MaxRememberedInstanceIds;
            _instanceIds.Add(key, id);
        }

        // Faces may be created on a worker thread (system font fallback).
        private static int NewInstanceId() => System.Threading.Interlocked.Increment(ref _nextInstanceId);

        /// <summary>Normalized axis coordinates as a dictionary key, compared by value.</summary>
        private readonly struct CoordinateKey : IEquatable<CoordinateKey>
        {
            private readonly float[] _values;
            private readonly int _hash;

            public CoordinateKey(float[] values)
            {
                _values = values;
                int hash = 17;
                foreach (float value in values)
                    hash = hash * 31 + value.GetHashCode();
                _hash = hash;
            }

            public bool Equals(CoordinateKey other)
            {
                if (_hash != other._hash || _values.Length != other._values.Length)
                    return false;

                for (int index = 0; index < _values.Length; index++)
                {
                    if (_values[index] != other._values[index])
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj) => obj is CoordinateKey other && Equals(other);

            public override int GetHashCode() => _hash;
        }
    }
}
