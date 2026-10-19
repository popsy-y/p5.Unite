using System;
using Unite.Text.Fonts;
using Unite.Text.Unicode;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// HarfBuzz's normalization before shaping ("composed diacritics"): characters the font
    /// lacks are decomposed canonically when it has the pieces, combining marks are put in
    /// canonical order, and base + mark pairs are recomposed when the font has the
    /// precomposed glyph. Old Hebrew fonts without mark positioning also get the Hebrew
    /// presentation forms that standard normalization excludes.
    /// </summary>
    internal sealed class Normalizer
    {
        private const int MaxReorderedMarks = 32;

        public int[] CodePoints = new int[64];
        public int[] Clusters = new int[64];
        public int Count;

        private FontInstance _font;

        public void Run(ShapedText target, FontInstance font, int start, int end, bool hebrewFallback)
        {
            _font = font;
            Count = 0;

            int cluster = start;
            bool single = true;
            bool complex = false;
            for (int index = start; index < end; index++)
            {
                if (target.ClusterStarts[index])
                {
                    cluster = index;
                    // Characters alone in their cluster keep their precomposed glyph when the
                    // font has it; clusters with marks decompose fully, then recompose.
                    single = index + 1 >= end || target.ClusterStarts[index + 1];
                }

                int codePoint = target.CodePoints[index];

                // Printable ASCII and Latin-1 letters have no decomposition and no marks.
                if (codePoint >= 0x20 && codePoint < 0x7F || codePoint >= 0xC0 && codePoint < 0x300 && single)
                {
                    if (HasGlyph(codePoint))
                    {
                        Add(codePoint, cluster);
                        continue;
                    }
                }

                if (UnicodeData.IsDefaultIgnorable(codePoint))
                {
                    Add(codePoint, cluster);
                    continue;
                }
                if (!BasicShaper.HasGlyph(codePoint))
                    continue;

                if (codePoint == '\t')
                    codePoint = ' ';

                complex |= !single || CombiningClass(codePoint) != 0;
                bool hasGlyph = HasGlyph(codePoint);
                if (single && hasGlyph)
                    Add(codePoint, cluster);
                else if (!Decompose(codePoint, cluster, single))
                    Add(hasGlyph ? codePoint : FallbackFor(codePoint), cluster);
            }

            if (!complex)
                return;

            ReorderMarks();
            Recompose(hebrewFallback);
        }

        private bool HasGlyph(int codePoint) => _font.GetGlyphId(codePoint) != 0;

        // Spaces the font lacks become its space; U+2011 becomes U+2010 (HarfBuzz).
        private int FallbackFor(int codePoint)
        {
            if (HasGlyph(codePoint))
                return codePoint;
            if (codePoint == 0x2011 && HasGlyph(0x2010))
                return 0x2010;
            if (UnicodeData.GetGeneralCategory(codePoint) == GeneralCategory.Zs && HasGlyph(' '))
                return ' ';
            return codePoint;
        }

        // HarfBuzz decompose(): the shortest (or, for multi-character clusters, the full)
        // decomposition whose pieces the font has.
        private bool Decompose(int codePoint, int cluster, bool shortest)
        {
            if (!UnicodeData.TryDecompose(codePoint, out int first, out int second) || (second != 0 && !HasGlyph(second)))
                return false;

            bool hasFirst = HasGlyph(first);
            if (shortest && hasFirst)
            {
                Add(first, cluster);
                if (second != 0)
                    Add(second, cluster);
                return true;
            }

            int before = Count;
            if (Decompose(first, cluster, shortest))
            {
                if (second != 0)
                    Add(second, cluster);
                return true;
            }
            Count = before;

            if (hasFirst)
            {
                Add(first, cluster);
                if (second != 0)
                    Add(second, cluster);
                return true;
            }
            return false;
        }

        // Stable sort of each run of non-starters by combining class.
        private void ReorderMarks()
        {
            for (int index = 0; index < Count; index++)
            {
                if (CombiningClass(CodePoints[index]) == 0)
                    continue;

                int end = index;
                while (end < Count && CombiningClass(CodePoints[end]) != 0)
                    end++;

                if (end - index <= MaxReorderedMarks)
                {
                    for (int item = index + 1; item < end; item++)
                    {
                        int codePoint = CodePoints[item];
                        int cluster = Clusters[item];
                        int combining = CombiningClass(codePoint);
                        int scan = item - 1;
                        while (scan >= index && CombiningClass(CodePoints[scan]) > combining)
                        {
                            CodePoints[scan + 1] = CodePoints[scan];
                            Clusters[scan + 1] = Clusters[scan];
                            scan--;
                        }
                        CodePoints[scan + 1] = codePoint;
                        Clusters[scan + 1] = cluster;
                    }
                }
                index = end - 1;
            }
        }

        private void Recompose(bool hebrewFallback)
        {
            if (Count < 2)
                return;

            int output = 1;
            int starter = 0;
            for (int index = 1; index < Count; index++)
            {
                int codePoint = CodePoints[index];
                int combining = CombiningClass(codePoint);
                bool mark = IsMark(codePoint);

                if (mark
                    && (starter == output - 1 || CombiningClass(CodePoints[output - 1]) < combining)
                    && Compose(CodePoints[starter], codePoint, hebrewFallback, out int composite)
                    && HasGlyph(composite))
                {
                    CodePoints[starter] = composite;
                    continue;
                }

                // Conjoining Hangul jamo become syllables when the font has them, as the
                // Hangul shaper of HarfBuzz does before GSUB.
                if (IsJamoContinuation(codePoint) && starter == output - 1 && Clusters[starter] == Clusters[index]
                    && UnicodeData.TryCompose(CodePoints[starter], codePoint, out int syllable) && HasGlyph(syllable))
                {
                    CodePoints[starter] = syllable;
                    continue;
                }

                CodePoints[output] = codePoint;
                Clusters[output] = Clusters[index];
                output++;
                if (combining == 0)
                    starter = output - 1;
            }
            Count = output;
        }

        /// <summary>
        /// HarfBuzz's modified combining classes: Hebrew points follow the SBL order (shin /
        /// sin dots and dagesh before vowels), Arabic shadda moves before the other marks,
        /// and a few Telugu, Thai, Lao, Tibetan and Tai Tham marks reorder as fonts expect.
        /// </summary>
        public static int CombiningClass(int codePoint)
        {
            switch (codePoint)
            {
                case 0x1A60: return 254; // TAI THAM SIGN SAKOT
                case 0x0FC6: return 254; // TIBETAN SYMBOL PADMA GDAN
                case 0x0F39: return 127; // TIBETAN MARK TSA -PHRU
            }

            int combining = UnicodeData.GetCombiningClass(codePoint);
            return combining switch
            {
                10 => 22, 11 => 15, 12 => 16, 13 => 17, 14 => 23, 15 => 18, 16 => 19, 17 => 20, 18 => 21,
                19 => 14, 20 => 24, 21 => 12, 22 => 25, 23 => 13, 24 => 10, 25 => 11,
                27 => 28, 28 => 29, 29 => 30, 30 => 31, 31 => 32, 32 => 33, 33 => 27,
                84 => 4, 91 => 5, 103 => 3, 130 => 132, 132 => 131,
                _ => combining
            };
        }

        private static bool IsJamoContinuation(int codePoint)
            => codePoint is >= 0x1161 and <= 0x1175 or >= 0x11A8 and <= 0x11C2;

        private static bool IsMark(int codePoint)
            => UnicodeData.GetGeneralCategory(codePoint) is GeneralCategory.Mn or GeneralCategory.Mc or GeneralCategory.Me;

        private static bool Compose(int first, int second, bool hebrewFallback, out int composite)
        {
            if (UnicodeData.TryCompose(first, second, out composite))
                return true;
            return hebrewFallback && ComposeHebrew(first, second, out composite);
        }

        private static readonly int[] DageshForms =
        {
            0xFB30, 0xFB31, 0xFB32, 0xFB33, 0xFB34, 0xFB35, 0xFB36, 0, 0xFB38, 0xFB39, 0xFB3A, 0xFB3B, 0xFB3C,
            0, 0xFB3E, 0, 0xFB40, 0xFB41, 0, 0xFB43, 0xFB44, 0, 0xFB46, 0xFB47, 0xFB48, 0xFB49, 0xFB4A
        };

        // Hebrew presentation forms excluded from NFC, wanted by fonts without GPOS marks.
        private static bool ComposeHebrew(int first, int second, out int composite)
        {
            composite = 0;
            switch (second)
            {
                case 0x05B4: if (first == 0x05D9) composite = 0xFB1D; break;
                case 0x05B7: composite = first == 0x05F2 ? 0xFB1F : first == 0x05D0 ? 0xFB2E : 0; break;
                case 0x05B8: if (first == 0x05D0) composite = 0xFB2F; break;
                case 0x05B9: if (first == 0x05D5) composite = 0xFB4B; break;
                case 0x05BC:
                    if (first >= 0x05D0 && first <= 0x05EA)
                        composite = DageshForms[first - 0x05D0];
                    else if (first == 0xFB2A)
                        composite = 0xFB2C;
                    else if (first == 0xFB2B)
                        composite = 0xFB2D;
                    break;
                case 0x05BF: composite = first switch { 0x05D1 => 0xFB4C, 0x05DB => 0xFB4D, 0x05E4 => 0xFB4E, _ => 0 }; break;
                case 0x05C1: composite = first == 0x05E9 ? 0xFB2A : first == 0xFB49 ? 0xFB2C : 0; break;
                case 0x05C2: composite = first == 0x05E9 ? 0xFB2B : first == 0xFB49 ? 0xFB2D : 0; break;
            }
            return composite != 0;
        }

        private void Add(int codePoint, int cluster)
        {
            if (Count == CodePoints.Length)
            {
                Array.Resize(ref CodePoints, Count * 2);
                Array.Resize(ref Clusters, Count * 2);
            }
            CodePoints[Count] = codePoint;
            Clusters[Count] = cluster;
            Count++;
        }
    }
}
