using System;

namespace Unite.Text.Unicode
{
    /// <summary>Indic_Conjunct_Break values (used by grapheme rule GB9c).</summary>
    internal enum IndicConjunctBreak : byte
    {
        None,
        Linker,
        Consonant,
        Extend
    }

    /// <summary>Bidi_Paired_Bracket_Type values.</summary>
    internal enum BracketType : byte
    {
        None,
        Open,
        Close
    }

    /// <summary>
    /// Unicode character properties (UCD 16.0) from the generated two-stage tables. Every
    /// lookup is a couple of array reads with no allocation; code points outside
    /// U+0000..U+10FFFF read as unassigned.
    /// </summary>
    internal static class UnicodeData
    {
        public const int MaxCodePoint = 0x10FFFF;

        public static BidiClass GetBidiClass(int codePoint)
            => (BidiClass)Read(UnicodeTables.Bidi, codePoint);

        public static LineBreakClass GetLineBreakClass(int codePoint)
            => (LineBreakClass)(Read(UnicodeTables.LineBreak, codePoint) & 0x3F);

        /// <summary>East_Asian_Width F, W or H (the $EastAsian set of line break rules LB19a / LB30).</summary>
        public static bool IsEastAsianWide(int codePoint)
            => (Read(UnicodeTables.LineBreak, codePoint) & 0x40) != 0;

        /// <summary>Unassigned Extended_Pictographic code points (line break rule LB30b).</summary>
        public static bool IsUnassignedPictographic(int codePoint)
            => (Read(UnicodeTables.LineBreak, codePoint) & 0x80) != 0;

        public static GraphemeClass GetGraphemeClass(int codePoint)
            => (GraphemeClass)(Read(UnicodeTables.Grapheme, codePoint) & 0x0F);

        public static bool IsExtendedPictographic(int codePoint)
            => (Read(UnicodeTables.Grapheme, codePoint) & 0x10) != 0;

        public static IndicConjunctBreak GetIndicConjunctBreak(int codePoint)
            => (IndicConjunctBreak)((Read(UnicodeTables.Grapheme, codePoint) >> 5) & 0x03);

        public static UnicodeScript GetScript(int codePoint)
            => (UnicodeScript)Read(UnicodeTables.Script, codePoint);

        public static GeneralCategory GetGeneralCategory(int codePoint)
            => (GeneralCategory)(Read(UnicodeTables.Category, codePoint) & 0x1F);

        public static JoiningType GetJoiningType(int codePoint)
            => (JoiningType)(Read(UnicodeTables.Category, codePoint) >> 5);

        /// <summary>Nonspacing or enclosing mark (Mn / Me).</summary>
        public static bool IsNonSpacingMark(int codePoint)
        {
            var category = GetGeneralCategory(codePoint);
            return category is GeneralCategory.Mn or GeneralCategory.Me;
        }

        public static bool IsDefaultIgnorable(int codePoint) => (Read(UnicodeTables.Flags, codePoint) & 1) != 0;

        public static bool IsEmojiPresentation(int codePoint) => (Read(UnicodeTables.Flags, codePoint) & 2) != 0;

        public static bool IsEmoji(int codePoint) => (Read(UnicodeTables.Flags, codePoint) & 4) != 0;

        public static bool IsBidiMirrored(int codePoint) => (Read(UnicodeTables.Flags, codePoint) & 8) != 0;

        /// <summary>The Bidi_Mirroring_Glyph of a code point, or the code point itself.</summary>
        public static int GetMirror(int codePoint)
        {
            int index = Array.BinarySearch(UnicodeTables.MirrorSourceList, codePoint);
            return index >= 0 ? UnicodeTables.MirrorTargetList[index] : codePoint;
        }

        /// <summary>Bidi_Paired_Bracket and its type; the pair is the code point itself for non-brackets.</summary>
        public static BracketType GetBracket(int codePoint, out int pair)
        {
            int index = Array.BinarySearch(UnicodeTables.BracketSourceList, codePoint);
            if (index < 0)
            {
                pair = codePoint;
                return BracketType.None;
            }

            pair = UnicodeTables.BracketPairList[index];
            return (BracketType)UnicodeTables.BracketTypeList[index];
        }

        /// <summary>The OpenType script tag (GSUB / GPOS ScriptList) of a script; "DFLT" for Common / Inherited / Unknown.</summary>
        public static string OpenTypeTag(UnicodeScript script) => UnicodeTables.OpenTypeScriptTags[(int)script];

        private static byte Read(in UnicodeTables.Table table, int codePoint)
        {
            if ((uint)codePoint > MaxCodePoint)
                return 0;

            int block = codePoint >> table.Shift;
            int number = table.Stage1[block * 2] | (table.Stage1[block * 2 + 1] << 8);
            return table.Stage2[(number << table.Shift) | (codePoint & ((1 << table.Shift) - 1))];
        }
    }
}
