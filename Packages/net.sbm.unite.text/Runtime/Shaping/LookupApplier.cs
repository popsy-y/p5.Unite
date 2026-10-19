using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// What GSUB and GPOS share: the current lookup (flags, mark filtering, feature mask),
    /// the glyph skipping rules of OpenType lookup flags (with HarfBuzz's handling of
    /// ZWJ / ZWNJ and default ignorables) and the contextual lookup formats.
    /// </summary>
    internal abstract partial class LookupApplier
    {
        protected enum Skip
        {
            No,
            Maybe,
            Yes
        }

        protected enum MatchKind
        {
            Any,
            Glyph,
            Class,
            Coverage
        }

        protected ShapingBuffer Buffer;
        protected OpenTypeLayout Layout;
        protected byte[] Data;
        protected LayoutTable Table;
        protected ShapedText Target;

        // The lookup being applied.
        protected int LookupFlag;
        protected int MarkFilteringSet;
        protected uint LookupMask;
        protected bool AutoZwj;
        protected bool AutoZwnj;
        protected int FeatureValue;

        private readonly bool _isPositioning;

        protected LookupApplier(bool isPositioning)
        {
            _isPositioning = isPositioning;
        }

        /// <summary>Longest context or ligature matched (HarfBuzz HB_MAX_CONTEXT_LENGTH).</summary>
        protected const int MaxContextLength = 64;

        public void Begin(ShapingBuffer buffer, OpenTypeLayout layout, LayoutTable table, ShapedText target)
        {
            Buffer = buffer;
            Layout = layout;
            Table = table;
            Data = layout.Data;
            Target = target;
        }

        /// <summary>Digest of the glyphs currently in the buffer.</summary>
        protected GlyphDigest BufferDigest()
        {
            var digest = new GlyphDigest();
            for (int index = 0; index < Buffer.Count; index++)
                digest.Add(Buffer.Info[index].Glyph);
            return digest;
        }

        protected void SelectLookup(LayoutLookup lookup, in PlannedLookup planned)
        {
            LookupFlag = lookup.Flag;
            MarkFilteringSet = lookup.MarkFilteringSet;
            LookupMask = planned.Mask;
            AutoZwj = planned.AutoZwj;
            AutoZwnj = planned.AutoZwnj;
            FeatureValue = planned.FeatureValue;
        }

        /// <summary>Applies one lookup (nested from a contextual rule) at a buffer position.</summary>
        protected abstract bool ApplyLookupAt(int lookupIndex, int position);

        /// <summary>Whether the lookup flags let the lookup see this glyph at all.</summary>
        protected bool CheckGlyphProperty(in GlyphInfo info)
        {
            int properties = info.Class switch
            {
                GlyphClass.Base => LayoutLookup.IgnoreBaseGlyphs,
                GlyphClass.Ligature => LayoutLookup.IgnoreLigatures,
                GlyphClass.Mark => LayoutLookup.IgnoreMarks,
                _ => 0
            };
            if ((properties & LookupFlag & 0x0E) != 0)
                return false;

            if (info.Class == GlyphClass.Mark)
            {
                if ((LookupFlag & LayoutLookup.UseMarkFilteringSet) != 0)
                    return Layout.Definitions != null && Layout.Definitions.IsInMarkGlyphSet(MarkFilteringSet, info.Glyph);

                int attachType = (LookupFlag >> 8) & 0xFF;
                if (attachType != 0)
                    return Layout.Definitions != null && Layout.Definitions.GetMarkAttachClass(info.Glyph) == attachType;
            }
            return true;
        }

        private Skip MaySkip(in GlyphInfo info, bool context)
        {
            if (!CheckGlyphProperty(info))
                return Skip.Yes;

            bool ignoreZwnj = _isPositioning || (context && AutoZwnj);
            bool ignoreZwj = context || AutoZwj;
            if (info.DefaultIgnorable && !info.Substituted && (ignoreZwnj || !info.Zwnj) && (ignoreZwj || !info.Zwj))
                return Skip.Maybe;
            return Skip.No;
        }

        private bool? MayMatch(in GlyphInfo info, bool context, MatchKind kind, int value)
        {
            if (!context && (info.Mask & LookupMask) == 0)
                return false;
            return kind switch
            {
                MatchKind.Glyph => info.Glyph == value,
                MatchKind.Class => ClassOf(Data, MatchClassTable, info.Glyph) == value,
                MatchKind.Coverage => Coverage(Data, value, info.Glyph) >= 0,
                _ => null
            };
        }

        /// <summary>ClassDef used by <see cref="MatchKind.Class"/> matches.</summary>
        protected int MatchClassTable;

        /// <summary>
        /// Moves <paramref name="index"/> to the next glyph the lookup sees that matches
        /// (input matching when <paramref name="context"/> is false, backtrack / lookahead
        /// otherwise). Returns false when a non-skippable glyph does not match.
        /// </summary>
        protected bool TryNext(ref int index, bool context, MatchKind kind = MatchKind.Any, int value = 0)
        {
            while (++index < Buffer.Count)
            {
                ref var info = ref Buffer.Info[index];
                var skip = MaySkip(info, context);
                if (skip == Skip.Yes)
                    continue;

                bool? match = MayMatch(info, context, kind, value);
                if (match == true || (match == null && skip == Skip.No))
                    return true;
                if (skip == Skip.No)
                    return false;
            }
            return false;
        }

        protected bool TryPrevious(ref int index, bool context, MatchKind kind = MatchKind.Any, int value = 0)
        {
            while (--index >= 0)
            {
                ref var info = ref Buffer.Info[index];
                var skip = MaySkip(info, context);
                if (skip == Skip.Yes)
                    continue;

                bool? match = MayMatch(info, context, kind, value);
                if (match == true || (match == null && skip == Skip.No))
                    return true;
                if (skip == Skip.No)
                    return false;
            }
            return false;
        }

        /// <summary>Saved lookup state around a nested (contextual) lookup.</summary>
        protected struct LookupState
        {
            public int Flag;
            public int MarkFilteringSet;
            public int ClassTable;
        }

        protected LookupState SaveState() => new() { Flag = LookupFlag, MarkFilteringSet = MarkFilteringSet, ClassTable = MatchClassTable };

        protected void RestoreState(in LookupState state)
        {
            LookupFlag = state.Flag;
            MarkFilteringSet = state.MarkFilteringSet;
            MatchClassTable = state.ClassTable;
        }
    }
}
