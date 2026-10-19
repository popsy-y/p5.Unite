using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Applies GPOS lookups (single and pair adjustment, cursive, mark-to-base,
    /// mark-to-ligature, mark-to-mark, contexts) in logical order with HarfBuzz's
    /// conventions; <see cref="FinishOffsets"/> then resolves attachment chains.
    /// VariationIndex device tables add the instance's deltas from GDEF.
    /// </summary>
    internal sealed partial class PositioningApplier : LookupApplier
    {
        private float[] _coordinates;
        private bool _hasAttachments;

        public PositioningApplier() : base(isPositioning: true)
        {
        }

        public void BeginInstance(float[] coordinates)
        {
            _coordinates = coordinates;
            _hasAttachments = false;
        }

        // GPOS never changes glyphs, so one digest serves every lookup of a run.
        private GlyphDigest _bufferDigest;

        public void BeginPositioning() => _bufferDigest = BufferDigest();

        public void Apply(in PlannedLookup planned)
        {
            var lookup = Table.GetLookup(planned.Index);
            if (lookup == null || !lookup.Digest.MayIntersect(_bufferDigest))
                return;

            SelectLookup(lookup, planned);
            var digest = lookup.Digest;
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
                case 2: return ApplyPair(subtable, index, out next);
                case 3: return ApplyCursive(subtable, index);
                case 4: return ApplyMarkToBase(subtable, index);
                case 5: return ApplyMarkToLigature(subtable, index);
                case 6: return ApplyMarkToMark(subtable, index);
                case 7: return ApplyContext(subtable, index, chained: false, out next);
                case 8: return ApplyContext(subtable, index, chained: true, out next);
                default: return false;
            }
        }

        private bool ApplySingle(int subtable, int index)
        {
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (coverage < 0)
                return false;

            int format = U16(Data, subtable);
            int valueFormat = U16(Data, subtable + 4);
            int value;
            if (format == 1)
                value = subtable + 6;
            else if (format == 2 && coverage < U16(Data, subtable + 6))
                value = subtable + 8 + coverage * ValueSize(valueFormat);
            else
                return false;

            ApplyValue(subtable, value, valueFormat, ref Buffer.Positions[index]);
            return true;
        }

        private bool ApplyPair(int subtable, int index, out int next)
        {
            next = index + 1;
            int coverage = Coverage(Data, subtable + U16(Data, subtable + 2), Buffer.Info[index].Glyph);
            if (coverage < 0)
                return false;

            int second = index;
            if (!TryNext(ref second, context: false))
                return false;

            int format1 = U16(Data, subtable + 4);
            int format2 = U16(Data, subtable + 6);
            int size1 = ValueSize(format1);
            int size2 = ValueSize(format2);
            int secondGlyph = Buffer.Info[second].Glyph;
            int record;

            // Device offsets are relative to the immediate parent: the PairSet in format 1,
            // the subtable in format 2.
            int deviceBase = subtable;
            if (U16(Data, subtable) == 1)
            {
                if (coverage >= U16(Data, subtable + 8))
                    return false;
                int set = subtable + U16(Data, subtable + 10 + coverage * 2);
                int recordSize = 2 + size1 + size2;
                record = FindPair(set, secondGlyph, recordSize);
                if (record < 0)
                    return false;
                record += 2;
                deviceBase = set;
            }
            else
            {
                int class1 = ClassOf(Data, Offset16(Data, subtable, subtable + 8), Buffer.Info[index].Glyph);
                int class2 = ClassOf(Data, Offset16(Data, subtable, subtable + 10), secondGlyph);
                int class1Count = U16(Data, subtable + 12);
                int class2Count = U16(Data, subtable + 14);
                if (class1 >= class1Count || class2 >= class2Count)
                    return false;
                record = subtable + 16 + (class1 * class2Count + class2) * (size1 + size2);
            }

            ApplyValue(deviceBase, record, format1, ref Buffer.Positions[index]);
            ApplyValue(deviceBase, record + size1, format2, ref Buffer.Positions[second]);
            next = format2 != 0 ? second + 1 : second;
            return true;
        }

        private int FindPair(int set, int glyph, int recordSize)
        {
            int count = U16(Data, set);
            int low = 0;
            int high = count - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                int record = set + 2 + middle * recordSize;
                int value = U16(Data, record);
                if (value == glyph)
                    return record;
                if (value < glyph)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return -1;
        }

        private static int ValueSize(int format)
        {
            int bits = format & 0xFF;
            int count = 0;
            while (bits != 0)
            {
                count += bits & 1;
                bits >>= 1;
            }
            return count * 2;
        }

        private void ApplyValue(int subtable, int value, int format, ref GlyphPosition position)
        {
            int field = value;
            if ((format & 0x0001) != 0) { position.XOffset += I16(Data, field); field += 2; }
            if ((format & 0x0002) != 0) { position.YOffset += I16(Data, field); field += 2; }
            if ((format & 0x0004) != 0) { position.XAdvance += I16(Data, field); field += 2; }
            if ((format & 0x0008) != 0) field += 2; // vertical advance
            if ((format & 0x0010) != 0) { position.XOffset += DeviceDelta(subtable, field); field += 2; }
            if ((format & 0x0020) != 0) { position.YOffset += DeviceDelta(subtable, field); field += 2; }
            if ((format & 0x0040) != 0) { position.XAdvance += DeviceDelta(subtable, field); field += 2; }
        }

        // Only VariationIndex tables matter here: hinting deltas need a ppem size.
        private float DeviceDelta(int baseOffset, int field)
        {
            int offset = U16(Data, field);
            var store = Layout.Definitions?.VariationStore;
            if (offset == 0 || store == null || _coordinates == null)
                return 0f;

            int device = baseOffset + offset;
            if (U16(Data, device + 4) != 0x8000)
                return 0f;
            return store.GetDelta(U16(Data, device), U16(Data, device + 2), _coordinates);
        }

        private void ReadAnchor(int anchor, out float x, out float y)
        {
            x = I16(Data, anchor + 2);
            y = I16(Data, anchor + 4);
            if (U16(Data, anchor) == 3)
            {
                x += DeviceDelta(anchor, anchor + 6);
                y += DeviceDelta(anchor, anchor + 8);
            }
        }
    }
}
