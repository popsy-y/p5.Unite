using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// A <see cref="FontFace"/> at specific variation axis values (or the default master
    /// for static fonts). Metrics, advances and outlines are looked up here, and the
    /// <see cref="Id"/> keeps baked glyphs of different instances apart in the atlas.
    /// </summary>
    public sealed class FontInstance
    {
        private readonly float[] _coordinates; // normalized axis coordinates; null = defaults
        private readonly Dictionary<int, float> _advances = new();
        private readonly Dictionary<int, GlyphOutline> _outlines = new();

        internal FontInstance(FontFace face, float[] coordinates, int id)
        {
            Face = face;
            _coordinates = coordinates;
            Id = id;

            Ascender = face.Ascender + face.MetricDelta("hasc", coordinates);
            Descender = face.Descender + face.MetricDelta("hdsc", coordinates);
        }

        public FontFace Face { get; }

        /// <summary>Process-wide unique id; part of every atlas cache key.</summary>
        public int Id { get; }

        public int UnitsPerEm => Face.UnitsPerEm;
        public int GlyphCount => Face.GlyphCount;

        /// <summary>Ascender at these axis values, in glyph units.</summary>
        public float Ascender { get; }

        /// <summary>Descender (negative) at these axis values, in glyph units.</summary>
        public float Descender { get; }

        /// <summary>Character-to-glyph lookup (cmap only, no shaping); 0 (.notdef) when unmapped.</summary>
        public int GetGlyphId(int codePoint) => Face.GetGlyphId(codePoint);

        /// <summary>Advance width of one glyph in glyph units.</summary>
        public float Advance(int glyphId)
        {
            if (_coordinates == null)
                return Face.BaseAdvance(glyphId);

            if (!_advances.TryGetValue(glyphId, out float advance))
            {
                advance = Face.BaseAdvance(glyphId) + Face.AdvanceDelta(glyphId, _coordinates);
                _advances[glyphId] = advance;
            }
            return advance;
        }

        /// <summary>
        /// Flattened outline of one glyph. Returns false for blank glyphs (space, .notdef,
        /// unresolvable composites); advance widths still apply to those.
        /// </summary>
        public bool TryGetOutline(int glyphId, out GlyphOutline outline)
        {
            if (!_outlines.TryGetValue(glyphId, out outline))
            {
                outline = Face.LoadOutline(glyphId, _coordinates);
                _outlines[glyphId] = outline;
            }
            return outline != null;
        }
    }
}
