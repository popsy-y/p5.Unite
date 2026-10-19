using System;

namespace Unite.Text.Unicode
{
    /// <summary>
    /// Extended grapheme cluster boundaries (UAX #29, Unicode 16): the user-perceived
    /// characters that line breaking, CHAR wrapping and font fallback must keep together
    /// (base + combining marks, Hangul syllables, emoji ZWJ sequences, flags, CR LF).
    /// </summary>
    internal static class GraphemeBreaker
    {
        /// <summary>End (exclusive) of the cluster that starts at <paramref name="start"/>.</summary>
        public static int NextBoundary(ReadOnlySpan<int> codePoints, int start)
        {
            if (start >= codePoints.Length)
                return codePoints.Length;

            var state = new State();
            state.Advance(codePoints[start]);
            int index = start + 1;
            while (index < codePoints.Length && !state.BreaksBefore(codePoints[index]))
            {
                state.Advance(codePoints[index]);
                index++;
            }
            return index;
        }

        /// <summary>True when there is a cluster boundary between index - 1 and index.</summary>
        public static bool IsBoundary(ReadOnlySpan<int> codePoints, int index)
        {
            if (index <= 0 || index >= codePoints.Length)
                return true;

            // Walk the cluster that contains index - 1 from its start.
            int start = 0;
            while (true)
            {
                int end = NextBoundary(codePoints, start);
                if (end >= index)
                    return end == index;
                start = end;
            }
        }

        // The pair rules plus the look-behind they need: the regional indicator count for
        // GB12/13, "ExtPict Extend* ZWJ" for GB11 and "Consonant [Extend Linker]* Linker
        // [Extend Linker]*" for GB9c.
        private struct State
        {
            private GraphemeClass _previous;
            private int _regionalIndicators;
            private bool _pictographic;        // ExtPict Extend* seen
            private bool _pictographicZwj;     // ... followed by ZWJ
            private bool _consonant;           // InCB Consonant [Extend Linker]* seen
            private bool _consonantLinker;     // ... containing a Linker

            public void Advance(int codePoint)
            {
                var current = UnicodeData.GetGraphemeClass(codePoint);
                bool pictographic = UnicodeData.IsExtendedPictographic(codePoint);
                var conjunct = UnicodeData.GetIndicConjunctBreak(codePoint);

                _regionalIndicators = current == GraphemeClass.RegionalIndicator ? _regionalIndicators + 1 : 0;

                if (pictographic)
                {
                    _pictographic = true;
                    _pictographicZwj = false;
                }
                else if (current == GraphemeClass.Extend && _pictographic && !_pictographicZwj)
                {
                    // ExtPict Extend*: keep.
                }
                else if (current == GraphemeClass.ZWJ && _pictographic && !_pictographicZwj)
                {
                    _pictographicZwj = true;
                }
                else
                {
                    _pictographic = false;
                    _pictographicZwj = false;
                }

                if (conjunct == IndicConjunctBreak.Consonant)
                {
                    _consonant = true;
                    _consonantLinker = false;
                }
                else if (_consonant && conjunct == IndicConjunctBreak.Linker)
                {
                    _consonantLinker = true;
                }
                else if (!(_consonant && conjunct == IndicConjunctBreak.Extend))
                {
                    _consonant = false;
                    _consonantLinker = false;
                }

                _previous = current;
            }

            public bool BreaksBefore(int codePoint)
            {
                var previous = _previous;
                var current = UnicodeData.GetGraphemeClass(codePoint);

                // GB3, GB4, GB5
                if (previous == GraphemeClass.CR && current == GraphemeClass.LF)
                    return false;
                if (previous is GraphemeClass.Control or GraphemeClass.CR or GraphemeClass.LF)
                    return true;
                if (current is GraphemeClass.Control or GraphemeClass.CR or GraphemeClass.LF)
                    return true;

                // GB6, GB7, GB8: Hangul syllable sequences.
                if (previous == GraphemeClass.L && current is GraphemeClass.L or GraphemeClass.V or GraphemeClass.LV or GraphemeClass.LVT)
                    return false;
                if (previous is GraphemeClass.LV or GraphemeClass.V && current is GraphemeClass.V or GraphemeClass.T)
                    return false;
                if (previous is GraphemeClass.LVT or GraphemeClass.T && current == GraphemeClass.T)
                    return false;

                // GB9, GB9a, GB9b
                if (current is GraphemeClass.Extend or GraphemeClass.ZWJ or GraphemeClass.SpacingMark)
                    return false;
                if (previous == GraphemeClass.Prepend)
                    return false;

                // GB9c: Indic conjuncts.
                if (_consonantLinker && UnicodeData.GetIndicConjunctBreak(codePoint) == IndicConjunctBreak.Consonant)
                    return false;

                // GB11: emoji ZWJ sequences.
                if (_pictographicZwj && UnicodeData.IsExtendedPictographic(codePoint))
                    return false;

                // GB12, GB13: regional indicator pairs (flags).
                if (current == GraphemeClass.RegionalIndicator && _regionalIndicators % 2 == 1)
                    return false;

                return true; // GB999
            }
        }
    }
}
