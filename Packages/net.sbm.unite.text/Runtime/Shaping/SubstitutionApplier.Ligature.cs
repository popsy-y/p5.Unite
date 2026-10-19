using System;
using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    // Ligature substitution with HarfBuzz's component bookkeeping (match_input / ligate_input).
    internal sealed partial class SubstitutionApplier
    {
        private bool ApplyLigature(int subtable, int index, out int next)
        {
            next = index + 1;
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (coverage < 0 || coverage >= U16(Data, subtable + 4))
                return false;

            int set = subtable + U16(Data, subtable + 6 + coverage * 2);
            int count = U16(Data, set);
            Span<int> positions = stackalloc int[MaxContextLength];
            for (int ligature = 0; ligature < count; ligature++)
            {
                int position = set + U16(Data, set + 2 + ligature * 2);
                int glyph = U16(Data, position);
                int components = U16(Data, position + 2);
                if (components == 0 || components > MaxContextLength)
                    continue;

                if (components == 1)
                {
                    // A one-component "ligature" is a single substitution.
                    Replace(ref Buffer.Info[index], glyph);
                    return true;
                }

                if (!MatchComponents(index, position + 4, components, positions, out int matchEnd, out int totalComponents))
                    continue;

                next = Ligate(positions, components, matchEnd, glyph, totalComponents);
                return true;
            }
            return false;
        }

        private bool MatchComponents(int index, int componentList, int count, Span<int> positions, out int matchEnd, out int totalComponents)
        {
            ref var first = ref Buffer.Info[index];
            int firstId = first.LigatureId;
            int firstComponent = first.LigatureComponent;
            totalComponents = ComponentCount(first);
            positions[0] = index;

            int position = index;
            for (int item = 1; item < count; item++)
            {
                if (!TryNext(ref position, context: false, MatchKind.Glyph, U16(Data, componentList + (item - 1) * 2)))
                {
                    matchEnd = 0;
                    return false;
                }

                positions[item] = position;
                ref var info = ref Buffer.Info[position];

                // Ligatures never join glyphs attached to different components of an
                // earlier ligature.
                if (firstId != 0 && firstComponent != 0)
                {
                    if (firstId != info.LigatureId || firstComponent != info.LigatureComponent)
                    {
                        matchEnd = 0;
                        return false;
                    }
                }
                else if (info.LigatureId != 0 && info.LigatureComponent != 0 && info.LigatureId != firstId)
                {
                    matchEnd = 0;
                    return false;
                }

                totalComponents += ComponentCount(info);
            }

            matchEnd = position + 1;
            return true;
        }

        private static int ComponentCount(in GlyphInfo info)
            => info.Class == GlyphClass.Ligature && info.LigatureId != 0 ? Math.Max(1, info.LigatureComponents) : 1;

        // Replaces the matched components with the ligature glyph; marks that sat between
        // them stay (now after the ligature) and learn which component they belong to.
        private int Ligate(Span<int> positions, int count, int matchEnd, int glyph, int totalComponents)
        {
            int start = positions[0];
            bool markLigature = Buffer.Info[start].Class == GlyphClass.Mark;
            bool baseLigature = Buffer.Info[start].Class == GlyphClass.Base;
            for (int item = 1; item < count; item++)
            {
                if (Buffer.Info[positions[item]].Class != GlyphClass.Mark)
                {
                    baseLigature = false;
                    markLigature = false;
                    break;
                }
            }

            bool isLigature = !baseLigature && !markLigature;
            int ligatureId = isLigature ? Buffer.NewLigatureId() : 0;

            Buffer.MergeClusters(start, matchEnd, Target);

            ref var head = ref Buffer.Info[start];
            int lastId = head.LigatureId;
            int lastComponents = ComponentCount(head);
            int soFar = lastComponents;

            head.Glyph = glyph;
            head.Substituted = true;
            if (Layout.Definitions != null && Layout.Definitions.HasGlyphClasses)
                head.Class = Layout.Definitions.GetGlyphClass(glyph);
            else if (isLigature)
                head.Class = GlyphClass.Ligature;
            if (isLigature)
            {
                head.LigatureId = ligatureId;
                head.LigatureComponent = 0;
                head.LigatureComponents = totalComponents;
            }

            // Marks between the components.
            for (int item = 1; item < count; item++)
            {
                for (int mark = positions[item - 1] + 1; mark < positions[item]; mark++)
                {
                    if (!isLigature)
                        continue;
                    ref var info = ref Buffer.Info[mark];
                    int component = info.LigatureComponent == 0 ? lastComponents : info.LigatureComponent;
                    info.LigatureId = ligatureId;
                    info.LigatureComponent = soFar - lastComponents + Math.Min(component, lastComponents);
                }

                ref var part = ref Buffer.Info[positions[item]];
                lastId = part.LigatureId;
                lastComponents = ComponentCount(part);
                soFar += lastComponents;
            }

            // Marks following the last component that belonged to it.
            if (!markLigature && lastId != 0)
            {
                for (int mark = matchEnd; mark < Buffer.Count; mark++)
                {
                    ref var info = ref Buffer.Info[mark];
                    if (info.LigatureId != lastId || info.LigatureComponent == 0)
                        break;
                    int component = info.LigatureComponent;
                    info.LigatureId = ligatureId;
                    info.LigatureComponent = soFar - lastComponents + Math.Min(component, lastComponents);
                }
            }

            // Delete the components after the first, last first so indices stay valid.
            for (int item = count - 1; item >= 1; item--)
                Buffer.Remove(positions[item], 1);

            return matchEnd - (count - 1);
        }
    }
}
