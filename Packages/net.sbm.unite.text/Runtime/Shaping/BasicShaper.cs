using Unite.Text.Fonts;
using Unite.Text.Unicode;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Shaping without OpenType layout tables: one glyph per character through the cmap,
    /// mirrored brackets in right-to-left runs, no glyphs for line / paragraph separators
    /// and default-ignorable characters (ZWJ, variation selectors, bidi controls), tabs
    /// drawn as spaces. Combining marks keep their font advance.
    /// </summary>
    internal sealed class BasicShaper
    {
        public void Shape(ShapedText target, FontInstance font, int face, int start, int end, bool rightToLeft, UnicodeScript script)
        {
            float perUnit = 1f / font.UnitsPerEm;
            int cluster = start;
            for (int index = start; index < end; index++)
            {
                if (target.ClusterStarts[index])
                    cluster = index;

                int codePoint = target.CodePoints[index];
                if (!HasGlyph(codePoint))
                    continue;

                if (codePoint == '\t')
                    codePoint = ' ';

                int glyphId = 0;
                if (rightToLeft && UnicodeData.IsBidiMirrored(codePoint))
                    glyphId = font.GetGlyphId(UnicodeData.GetMirror(codePoint));
                if (glyphId == 0)
                    glyphId = font.GetGlyphId(codePoint);

                target.AddGlyph(new ShapedGlyph
                {
                    GlyphId = glyphId,
                    Face = face,
                    Cluster = cluster,
                    Advance = font.Advance(glyphId) * perUnit
                });
            }
        }

        /// <summary>False for characters that are never drawn (separators, default ignorables).</summary>
        public static bool HasGlyph(int codePoint)
        {
            var lineBreak = UnicodeData.GetLineBreakClass(codePoint);
            if (lineBreak is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
                return false;
            return !UnicodeData.IsDefaultIgnorable(codePoint);
        }
    }
}
