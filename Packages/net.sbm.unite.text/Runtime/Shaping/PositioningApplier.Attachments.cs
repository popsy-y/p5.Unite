using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    // Cursive and mark attachment (GPOS 3-6) and the final offset propagation.
    internal sealed partial class PositioningApplier
    {
        private bool ApplyCursive(int subtable, int index)
        {
            int coverageTable = subtable + U16(Data, subtable + 2);
            int coverage = Coverage(Data, coverageTable, Buffer.Info[index].Glyph);
            if (coverage < 0)
                return false;

            int entry = Offset16(Data, subtable, subtable + 6 + coverage * 4);
            if (entry == 0)
                return false;

            int previous = index;
            if (!TryPrevious(ref previous, context: false))
                return false;

            int previousCoverage = Coverage(Data, coverageTable, Buffer.Info[previous].Glyph);
            if (previousCoverage < 0)
                return false;
            int exit = Offset16(Data, subtable, subtable + 6 + previousCoverage * 4 + 2);
            if (exit == 0)
                return false;

            ReadAnchor(exit, out float exitX, out float exitY);
            ReadAnchor(entry, out float entryX, out float entryY);

            var positions = Buffer.Positions;
            int i = previous;
            int j = index;
            if (!Buffer.RightToLeft)
            {
                positions[i].XAdvance = exitX + positions[i].XOffset;
                float delta = entryX + positions[j].XOffset;
                positions[j].XAdvance -= delta;
                positions[j].XOffset -= delta;
            }
            else
            {
                float delta = exitX + positions[i].XOffset;
                positions[i].XAdvance -= delta;
                positions[i].XOffset -= delta;
                positions[j].XAdvance = entryX + positions[j].XOffset;
            }

            // The child aligns itself against its parent on the cross axis.
            int child = i;
            int parent = j;
            float yOffset = entryY - exitY;
            if ((LookupFlag & LayoutLookup.RightToLeft) == 0)
            {
                child = j;
                parent = i;
                yOffset = -yOffset;
            }

            ReverseCursiveChain(child, parent);
            positions[child].AttachType = ShapingBuffer.CursiveAttachment;
            positions[child].AttachChain = parent - child;
            positions[child].YOffset = yOffset;
            _hasAttachments = true;

            if (positions[parent].AttachChain == -positions[child].AttachChain)
            {
                positions[parent].AttachChain = 0;
                positions[parent].YOffset = 0f;
            }
            return true;
        }

        // A child already chained elsewhere: reverse its old chain so the whole tree
        // attaches to the new parent.
        private void ReverseCursiveChain(int index, int newParent)
        {
            var positions = Buffer.Positions;
            int chain = positions[index].AttachChain;
            int type = positions[index].AttachType;
            if (chain == 0 || (type & ShapingBuffer.CursiveAttachment) == 0)
                return;

            positions[index].AttachChain = 0;
            int next = index + chain;
            if (next == newParent)
                return;

            ReverseCursiveChain(next, newParent);
            positions[next].YOffset = -positions[index].YOffset;
            positions[next].AttachChain = -chain;
            positions[next].AttachType = type;
        }

        private bool ApplyMarkToBase(int subtable, int index)
        {
            int markIndex = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (markIndex < 0)
                return false;

            int baseIndex = FindAttachmentBase(index, out int basePosition);
            if (baseIndex < 0)
                return false;

            int coverage = Coverage(Data, subtable + U16(Data, subtable + 4), Buffer.Info[basePosition].Glyph);
            if (coverage < 0)
                return false;

            int classCount = U16(Data, subtable + 6);
            int baseArray = subtable + U16(Data, subtable + 10);
            if (coverage >= U16(Data, baseArray))
                return false;
            return AttachMark(subtable + U16(Data, subtable + 8), markIndex, baseArray, 2 + coverage * classCount * 2, classCount, index, basePosition);
        }

        private bool ApplyMarkToLigature(int subtable, int index)
        {
            int markIndex = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (markIndex < 0)
                return false;

            if (FindAttachmentBase(index, out int ligaturePosition) < 0)
                return false;

            int coverage = Coverage(Data, subtable + U16(Data, subtable + 4), Buffer.Info[ligaturePosition].Glyph);
            if (coverage < 0)
                return false;

            int classCount = U16(Data, subtable + 6);
            int ligatureArray = subtable + U16(Data, subtable + 10);
            if (coverage >= U16(Data, ligatureArray))
                return false;

            int attach = ligatureArray + U16(Data, ligatureArray + 2 + coverage * 2);
            int components = U16(Data, attach);
            if (components == 0)
                return false;

            // A mark of this very ligature uses its component; others take the last one.
            ref var mark = ref Buffer.Info[index];
            ref var ligature = ref Buffer.Info[ligaturePosition];
            int component = ligature.LigatureId != 0 && ligature.LigatureId == mark.LigatureId && mark.LigatureComponent > 0
                ? System.Math.Min(components, mark.LigatureComponent) - 1
                : components - 1;

            return AttachMark(subtable + U16(Data, subtable + 8), markIndex, attach, 2 + component * classCount * 2, classCount, index, ligaturePosition);
        }

        private bool ApplyMarkToMark(int subtable, int index)
        {
            int markIndex = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (markIndex < 0)
                return false;

            // The previous glyph the lookup sees must be a mark (filtering flags kept).
            var saved = SaveState();
            LookupFlag &= ~0x0E;
            int previous = index;
            bool found = TryPrevious(ref previous, context: false);
            RestoreState(saved);
            if (!found || Buffer.Info[previous].Class != GlyphClass.Mark)
                return false;

            ref var mark1 = ref Buffer.Info[index];
            ref var mark2 = ref Buffer.Info[previous];
            bool sameBase = mark1.LigatureId == mark2.LigatureId
                ? mark1.LigatureId == 0 || mark1.LigatureComponent == mark2.LigatureComponent
                : (mark1.LigatureId > 0 && mark1.LigatureComponent == 0) || (mark2.LigatureId > 0 && mark2.LigatureComponent == 0);
            if (!sameBase)
                return false;

            int coverage = Coverage(Data, subtable + U16(Data, subtable + 4), mark2.Glyph);
            if (coverage < 0)
                return false;

            int classCount = U16(Data, subtable + 6);
            int mark2Array = subtable + U16(Data, subtable + 10);
            if (coverage >= U16(Data, mark2Array))
                return false;
            return AttachMark(subtable + U16(Data, subtable + 8), markIndex, mark2Array, 2 + coverage * classCount * 2, classCount, index, previous);
        }

        // The nearest earlier glyph that is not a mark (searched with IgnoreMarks).
        private int FindAttachmentBase(int index, out int position)
        {
            var saved = SaveState();
            LookupFlag = LayoutLookup.IgnoreMarks;
            position = index;
            bool found = TryPrevious(ref position, context: false);
            RestoreState(saved);
            return found ? position : -1;
        }

        // MarkArray::apply: offset the mark so its anchor meets the base / ligature / mark anchor.
        private bool AttachMark(int markArray, int markIndex, int anchorTable, int rowOffset, int classCount, int index, int basePosition)
        {
            if (markIndex >= U16(Data, markArray))
                return false;

            int record = markArray + 2 + markIndex * 4;
            int markClass = U16(Data, record);
            if (markClass >= classCount)
                return false;

            int baseAnchor = Offset16(Data, anchorTable, anchorTable + rowOffset + markClass * 2);
            if (baseAnchor == 0)
                return false;

            ReadAnchor(markArray + U16(Data, record + 2), out float markX, out float markY);
            ReadAnchor(baseAnchor, out float baseX, out float baseY);

            ref var position = ref Buffer.Positions[index];
            position.XOffset = baseX - markX;
            position.YOffset = baseY - markY;
            position.AttachType = ShapingBuffer.MarkAttachment;
            position.AttachChain = basePosition - index;
            _hasAttachments = true;
            return true;
        }

        /// <summary>Adds the offsets of the glyphs attached glyphs hang from (HarfBuzz position_finish_offsets).</summary>
        public void FinishOffsets()
        {
            if (!_hasAttachments)
                return;

            for (int index = 0; index < Buffer.Count; index++)
                Propagate(index, 64);
        }

        private void Propagate(int index, int nesting)
        {
            var positions = Buffer.Positions;
            int chain = positions[index].AttachChain;
            int type = positions[index].AttachType;
            if (chain == 0)
                return;

            positions[index].AttachChain = 0;
            int parent = index + chain;
            if (parent < 0 || parent >= Buffer.Count || nesting == 0)
                return;

            Propagate(parent, nesting - 1);
            if ((type & ShapingBuffer.CursiveAttachment) != 0)
            {
                positions[index].YOffset += positions[parent].YOffset;
                return;
            }

            positions[index].XOffset += positions[parent].XOffset;
            positions[index].YOffset += positions[parent].YOffset;
            if (!Buffer.RightToLeft)
            {
                for (int between = parent; between < index; between++)
                    positions[index].XOffset -= positions[between].XAdvance;
            }
            else
            {
                for (int between = parent + 1; between <= index; between++)
                    positions[index].XOffset += positions[between].XAdvance;
            }
        }
    }
}
