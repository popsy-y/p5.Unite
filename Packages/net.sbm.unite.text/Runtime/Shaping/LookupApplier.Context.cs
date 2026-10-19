using System;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    // Sequence context (GSUB 5 / GPOS 7) and chained sequence context (GSUB 6 / GPOS 8),
    // formats 1-3, with the nested lookup records.
    internal abstract partial class LookupApplier
    {
        /// <summary>Applies a (chained) context subtable at <paramref name="index"/>; <paramref name="end"/> is where processing continues.</summary>
        protected bool ApplyContext(int subtable, int index, bool chained, out int end)
        {
            end = index + 1;
            int glyph = Buffer.Info[index].Glyph;
            int format = U16(Data, subtable);
            return chained
                ? format switch
                {
                    1 => ApplyChainFormat1(subtable, index, glyph, out end),
                    2 => ApplyChainFormat2(subtable, index, glyph, out end),
                    3 => ApplyChainFormat3(subtable, index, out end),
                    _ => false
                }
                : format switch
                {
                    1 => ApplyContextFormat1(subtable, index, glyph, out end),
                    2 => ApplyContextFormat2(subtable, index, glyph, out end),
                    3 => ApplyContextFormat3(subtable, index, out end),
                    _ => false
                };
        }

        // Format 1: rules of glyph ids, chosen by the coverage index of the first glyph.
        private bool ApplyContextFormat1(int subtable, int index, int glyph, out int end)
        {
            end = index + 1;
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), glyph);
            if (coverage < 0 || coverage >= U16(Data, subtable + 4))
                return false;

            int ruleSet = Offset16(Data, subtable, subtable + 6 + coverage * 2);
            if (ruleSet == 0)
                return false;

            int rules = U16(Data, ruleSet);
            for (int rule = 0; rule < rules; rule++)
            {
                int position = ruleSet + U16(Data, ruleSet + 2 + rule * 2);
                int glyphCount = U16(Data, position);
                int lookupCount = U16(Data, position + 2);
                if (TryRule(index, 0, 0, 0, position + 4, glyphCount, MatchKind.Glyph, 0, 0, 0, position + 4 + (glyphCount - 1) * 2, lookupCount, out end))
                    return true;
            }
            return false;
        }

        // Format 2: rules of classes, chosen by the class of the first glyph.
        private bool ApplyContextFormat2(int subtable, int index, int glyph, out int end)
        {
            end = index + 1;
            if (Coverage(Data, subtable + U16(Data, subtable + 2), glyph) < 0)
                return false;

            int classDef = Offset16(Data, subtable, subtable + 4);
            int glyphClass = ClassOf(Data, classDef, glyph);
            if (glyphClass >= U16(Data, subtable + 6))
                return false;

            int ruleSet = Offset16(Data, subtable, subtable + 8 + glyphClass * 2);
            if (ruleSet == 0)
                return false;

            int rules = U16(Data, ruleSet);
            for (int rule = 0; rule < rules; rule++)
            {
                int position = ruleSet + U16(Data, ruleSet + 2 + rule * 2);
                int glyphCount = U16(Data, position);
                int lookupCount = U16(Data, position + 2);
                if (TryRule(index, 0, 0, 0, position + 4, glyphCount, MatchKind.Class, classDef, 0, 0, position + 4 + (glyphCount - 1) * 2, lookupCount, out end))
                    return true;
            }
            return false;
        }

        // Format 3: one coverage per input position.
        private bool ApplyContextFormat3(int subtable, int index, out int end)
        {
            end = index + 1;
            int glyphCount = U16(Data, subtable + 2);
            int lookupCount = U16(Data, subtable + 4);
            if (glyphCount == 0 || Coverage(Data, subtable + U16(Data, subtable + 6), Buffer.Info[index].Glyph) < 0)
                return false;

            return TryRule(index, 0, 0, 0, subtable + 8, glyphCount, MatchKind.Coverage, subtable, 0, 0, subtable + 6 + glyphCount * 2, lookupCount, out end, coverageBase: subtable);
        }

        private bool ApplyChainFormat1(int subtable, int index, int glyph, out int end)
        {
            end = index + 1;
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), glyph);
            if (coverage < 0 || coverage >= U16(Data, subtable + 4))
                return false;

            int ruleSet = Offset16(Data, subtable, subtable + 6 + coverage * 2);
            if (ruleSet == 0)
                return false;

            int rules = U16(Data, ruleSet);
            for (int rule = 0; rule < rules; rule++)
            {
                int position = ruleSet + U16(Data, ruleSet + 2 + rule * 2);
                if (TryChainRule(index, position, MatchKind.Glyph, 0, 0, 0, out end))
                    return true;
            }
            return false;
        }

        private bool ApplyChainFormat2(int subtable, int index, int glyph, out int end)
        {
            end = index + 1;
            if (Coverage(Data, subtable + U16(Data, subtable + 2), glyph) < 0)
                return false;

            int backtrackClasses = Offset16(Data, subtable, subtable + 4);
            int inputClasses = Offset16(Data, subtable, subtable + 6);
            int lookaheadClasses = Offset16(Data, subtable, subtable + 8);
            int glyphClass = ClassOf(Data, inputClasses, glyph);
            if (glyphClass >= U16(Data, subtable + 10))
                return false;

            int ruleSet = Offset16(Data, subtable, subtable + 12 + glyphClass * 2);
            if (ruleSet == 0)
                return false;

            int rules = U16(Data, ruleSet);
            for (int rule = 0; rule < rules; rule++)
            {
                int position = ruleSet + U16(Data, ruleSet + 2 + rule * 2);
                if (TryChainRule(index, position, MatchKind.Class, backtrackClasses, inputClasses, lookaheadClasses, out end))
                    return true;
            }
            return false;
        }

        private bool ApplyChainFormat3(int subtable, int index, out int end)
        {
            end = index + 1;
            int backtrackCount = U16(Data, subtable + 2);
            int input = subtable + 4 + backtrackCount * 2;
            int inputCount = U16(Data, input);
            int lookahead = input + 2 + inputCount * 2;
            int lookaheadCount = U16(Data, lookahead);
            int records = lookahead + 2 + lookaheadCount * 2;
            int lookupCount = U16(Data, records);

            if (inputCount == 0 || Coverage(Data, subtable + U16(Data, input + 2), Buffer.Info[index].Glyph) < 0)
                return false;

            return TryRule(
                index,
                backtrackCount, subtable + 4, lookaheadCount,
                input + 4, inputCount, MatchKind.Coverage, subtable,
                lookahead + 2, 0,
                records + 2, lookupCount, out end, coverageBase: subtable
            );
        }

        // ChainedSequenceRule / ChainedClassSequenceRule: backtrack, input, lookahead, records.
        private bool TryChainRule(int index, int rule, MatchKind kind, int backtrackClasses, int inputClasses, int lookaheadClasses, out int end)
        {
            int backtrackCount = U16(Data, rule);
            int input = rule + 2 + backtrackCount * 2;
            int inputCount = U16(Data, input);
            int lookahead = input + 2 + Math.Max(0, inputCount - 1) * 2;
            int lookaheadCount = U16(Data, lookahead);
            int records = lookahead + 2 + lookaheadCount * 2;
            int lookupCount = U16(Data, records);

            return TryRule(
                index,
                backtrackCount, rule + 2, lookaheadCount,
                input + 2, inputCount, kind, inputClasses,
                lookahead + 2, kind == MatchKind.Class ? lookaheadClasses : 0,
                records + 2, lookupCount, out end, backtrackClasses: backtrackClasses
            );
        }

        /// <summary>
        /// Matches backtrack (backward from index), input (index plus inputCount - 1 more
        /// items listed at inputValues) and lookahead, then applies the lookup records.
        /// Coverage-based rules list Offset16s relative to <paramref name="coverageBase"/>
        /// and include the first input glyph in their list.
        /// </summary>
        private bool TryRule(
            int index,
            int backtrackCount, int backtrackValues, int lookaheadCount,
            int inputValues, int inputCount, MatchKind kind, int inputClasses,
            int lookaheadValues, int lookaheadClasses,
            int records, int lookupCount,
            out int end,
            int coverageBase = 0,
            int backtrackClasses = 0
        )
        {
            end = index + 1;
            if (inputCount == 0 || inputCount > MaxContextLength)
                return false;

            bool coverage = kind == MatchKind.Coverage;
            Span<int> positions = stackalloc int[MaxContextLength];
            positions[0] = index;
            var saved = SaveState();
            MatchClassTable = inputClasses;

            // Input: coverage rules list every position, the others all but the first.
            int position = index;
            for (int item = 1; item < inputCount; item++)
            {
                int value = coverage
                    ? coverageBase + U16(Data, inputValues + (item - 1) * 2)
                    : U16(Data, inputValues + (item - 1) * 2);
                if (!TryNext(ref position, context: false, kind, value))
                {
                    RestoreState(saved);
                    return false;
                }
                positions[item] = position;
            }
            int matchEnd = position + 1;

            // Backtrack, nearest glyph first.
            MatchClassTable = backtrackClasses;
            position = index;
            for (int item = 0; item < backtrackCount; item++)
            {
                int value = coverage ? coverageBase + U16(Data, backtrackValues + item * 2) : U16(Data, backtrackValues + item * 2);
                if (!TryPrevious(ref position, context: true, kind, value))
                {
                    RestoreState(saved);
                    return false;
                }
            }

            MatchClassTable = lookaheadClasses;
            position = matchEnd - 1;
            for (int item = 0; item < lookaheadCount; item++)
            {
                int value = coverage ? coverageBase + U16(Data, lookaheadValues + item * 2) : U16(Data, lookaheadValues + item * 2);
                if (!TryNext(ref position, context: true, kind, value))
                {
                    RestoreState(saved);
                    return false;
                }
            }

            RestoreState(saved);
            end = ApplyRecords(positions, inputCount, matchEnd, records, lookupCount);
            return true;
        }

        // HarfBuzz apply_lookup: nested lookups may change the buffer length, which shifts
        // the remaining match positions.
        private int ApplyRecords(Span<int> positions, int count, int end, int records, int lookupCount)
        {
            var saved = SaveState();
            for (int record = 0; record < lookupCount; record++)
            {
                int sequenceIndex = U16(Data, records + record * 4);
                int lookupIndex = U16(Data, records + record * 4 + 2);
                if (sequenceIndex >= count)
                    continue;

                int originalLength = Buffer.Count;
                bool applied = ApplyLookupAt(lookupIndex, positions[sequenceIndex]);
                RestoreState(saved);
                if (!applied)
                    continue;

                int delta = Buffer.Count - originalLength;
                if (delta == 0)
                    continue;

                end += delta;
                if (end < positions[sequenceIndex])
                {
                    delta += positions[sequenceIndex] - end;
                    end = positions[sequenceIndex];
                }

                int next = sequenceIndex + 1;
                if (delta > 0)
                {
                    if (delta + count > MaxContextLength)
                        break;
                }
                else
                {
                    delta = Math.Max(delta, next - count);
                    next -= delta;
                }

                // Shift the positions after the recursed one, then fill in new entries.
                int moved = count - next;
                if (moved > 0)
                    positions.Slice(next, moved).CopyTo(positions.Slice(next + delta, moved));
                next += delta;
                count += delta;
                for (int item = sequenceIndex + 1; item < next; item++)
                    positions[item] = positions[item - 1] + 1;
                for (; next < count; next++)
                    positions[next] += delta;
            }
            return end;
        }
    }
}
