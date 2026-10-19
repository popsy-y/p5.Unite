using System;
using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Applies GSUB lookups to a <see cref="ShapingBuffer"/>: single, multiple, alternate
    /// and ligature substitution, (chained) contexts and reverse chained single
    /// substitution, following HarfBuzz for cluster merging and ligature components.
    /// </summary>
    internal sealed partial class SubstitutionApplier : LookupApplier
    {
        public SubstitutionApplier() : base(isPositioning: false)
        {
        }

        public void Apply(in PlannedLookup planned)
        {
            var lookup = Table.GetLookup(planned.Index);
            if (lookup == null || !lookup.Digest.MayIntersect(BufferDigest()))
                return;

            SelectLookup(lookup, planned);
            var digest = lookup.Digest;
            if (lookup.Type == 8)
            {
                // Reverse chained single substitution runs from the end.
                for (int index = Buffer.Count - 1; index >= 0; index--)
                {
                    ref var info = ref Buffer.Info[index];
                    if (!digest.MayHave(info.Glyph) || (info.Mask & LookupMask) == 0 || !CheckGlyphProperty(info))
                        continue;
                    foreach (int subtable in lookup.Subtables)
                    {
                        if (ApplyReverseChain(subtable, index))
                            break;
                    }
                }
                return;
            }

            for (int index = 0; index < Buffer.Count;)
            {
                ref var info = ref Buffer.Info[index];
                int next = index + 1;
                if (digest.MayHave(info.Glyph) && (info.Mask & LookupMask) != 0 && CheckGlyphProperty(info))
                {
                    foreach (int subtable in lookup.Subtables)
                    {
                        if (ApplySubtable(lookup.Type, subtable, index, out next))
                            break;
                        next = index + 1;
                    }
                }
                index = next;
            }
        }

        protected override bool ApplyLookupAt(int lookupIndex, int position)
        {
            var lookup = Table.GetLookup(lookupIndex);
            if (lookup == null || position >= Buffer.Count)
                return false;

            LookupFlag = lookup.Flag;
            MarkFilteringSet = lookup.MarkFilteringSet;
            if (lookup.Type == 8)
            {
                foreach (int subtable in lookup.Subtables)
                {
                    if (ApplyReverseChain(subtable, position))
                        return true;
                }
                return false;
            }

            foreach (int subtable in lookup.Subtables)
            {
                if (ApplySubtable(lookup.Type, subtable, position, out _))
                    return true;
            }
            return false;
        }

        private bool ApplySubtable(int type, int subtable, int index, out int next)
        {
            next = index + 1;
            switch (type)
            {
                case 1: return ApplySingle(subtable, index);
                case 2: return ApplyMultiple(subtable, index, out next);
                case 3: return ApplyAlternate(subtable, index);
                case 4: return ApplyLigature(subtable, index, out next);
                case 5: return ApplyContext(subtable, index, chained: false, out next);
                case 6: return ApplyContext(subtable, index, chained: true, out next);
                default: return false;
            }
        }

        private bool ApplySingle(int subtable, int index)
        {
            ref var info = ref Buffer.Info[index];
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), info.Glyph);
            if (coverage < 0)
                return false;

            int format = U16(Data, subtable);
            int glyph;
            if (format == 1)
                glyph = (info.Glyph + I16(Data, subtable + 4)) & 0xFFFF;
            else if (format == 2 && coverage < U16(Data, subtable + 4))
                glyph = U16(Data, subtable + 6 + coverage * 2);
            else
                return false;

            Replace(ref info, glyph);
            return true;
        }

        private bool ApplyMultiple(int subtable, int index, out int next)
        {
            next = index + 1;
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (coverage < 0 || coverage >= U16(Data, subtable + 4))
                return false;

            int sequence = subtable + U16(Data, subtable + 6 + coverage * 2);
            int count = U16(Data, sequence);

            // An empty sequence deletes the glyph (as Uniscribe and HarfBuzz do).
            Buffer.Expand(index, count);
            for (int item = 0; item < count; item++)
            {
                ref var info = ref Buffer.Info[index + item];
                Replace(ref info, U16(Data, sequence + 2 + item * 2));
                if (count > 1)
                {
                    // HarfBuzz numbers the parts so marks can still tell them apart.
                    info.LigatureId = 0;
                    info.LigatureComponent = item;
                }
            }
            next = index + count;
            return true;
        }

        private bool ApplyAlternate(int subtable, int index)
        {
            ref var info = ref Buffer.Info[index];
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), info.Glyph);
            if (coverage < 0 || coverage >= U16(Data, subtable + 4))
                return false;

            int set = subtable + U16(Data, subtable + 6 + coverage * 2);
            int count = U16(Data, set);
            int choice = Math.Max(1, FeatureValue) - 1;
            if (choice >= count)
                return false;

            Replace(ref info, U16(Data, set + 2 + choice * 2));
            return true;
        }

        private void Replace(ref GlyphInfo info, int glyph)
        {
            info.Glyph = glyph;
            info.Substituted = true;
            if (Layout.Definitions != null && Layout.Definitions.HasGlyphClasses)
                info.Class = Layout.Definitions.GetGlyphClass(glyph);
        }

        private bool ApplyReverseChain(int subtable, int index)
        {
            ref var info = ref Buffer.Info[index];
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), info.Glyph);
            if (coverage < 0)
                return false;

            int backtrackCount = U16(Data, subtable + 4);
            int lookahead = subtable + 6 + backtrackCount * 2;
            int lookaheadCount = U16(Data, lookahead);
            int substitutes = lookahead + 2 + lookaheadCount * 2;
            if (coverage >= U16(Data, substitutes))
                return false;

            int position = index;
            for (int item = 0; item < backtrackCount; item++)
            {
                if (!TryPrevious(ref position, context: true, MatchKind.Coverage, subtable + U16(Data, subtable + 6 + item * 2)))
                    return false;
            }

            position = index;
            for (int item = 0; item < lookaheadCount; item++)
            {
                if (!TryNext(ref position, context: true, MatchKind.Coverage, subtable + U16(Data, lookahead + 2 + item * 2)))
                    return false;
            }

            Replace(ref info, U16(Data, substitutes + 2 + coverage * 2));
            return true;
        }
    }
}
