using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Fonts.Layout
{
    /// <summary>
    /// HarfBuzz-style set digest: three 64-bit masks over glyph ids shifted by 0, 4 and 9
    /// bits. "May contain" answers are fast and never miss a member, so a lookup whose
    /// digest does not meet the text's digest can be skipped entirely.
    /// </summary>
    internal struct GlyphDigest
    {
        private ulong _mask0;
        private ulong _mask4;
        private ulong _mask9;

        public static GlyphDigest All => new() { _mask0 = ulong.MaxValue, _mask4 = ulong.MaxValue, _mask9 = ulong.MaxValue };

        public void Add(int glyph)
        {
            _mask0 |= 1UL << (glyph & 63);
            _mask4 |= 1UL << ((glyph >> 4) & 63);
            _mask9 |= 1UL << ((glyph >> 9) & 63);
        }

        public void AddRange(int first, int last)
        {
            _mask0 |= RangeMask(first, last, 0);
            _mask4 |= RangeMask(first, last, 4);
            _mask9 |= RangeMask(first, last, 9);
        }

        public void Add(in GlyphDigest other)
        {
            _mask0 |= other._mask0;
            _mask4 |= other._mask4;
            _mask9 |= other._mask9;
        }

        public bool MayHave(int glyph)
            => (_mask0 & (1UL << (glyph & 63))) != 0
                && (_mask4 & (1UL << ((glyph >> 4) & 63))) != 0
                && (_mask9 & (1UL << ((glyph >> 9) & 63))) != 0;

        public bool MayIntersect(in GlyphDigest other)
            => (_mask0 & other._mask0) != 0 && (_mask4 & other._mask4) != 0 && (_mask9 & other._mask9) != 0;

        private static ulong RangeMask(int first, int last, int shift)
        {
            int low = first >> shift;
            int high = last >> shift;
            if (high - low >= 63)
                return ulong.MaxValue;

            ulong mask = 0;
            for (int value = low; value <= high; value++)
                mask |= 1UL << (value & 63);
            return mask;
        }

        /// <summary>Adds every glyph listed by a Coverage table.</summary>
        public void AddCoverage(byte[] data, int table)
        {
            int format = U16(data, table);
            int count = U16(data, table + 2);
            if (format == 1)
            {
                for (int index = 0; index < count; index++)
                    Add(U16(data, table + 4 + index * 2));
            }
            else if (format == 2)
            {
                for (int index = 0; index < count; index++)
                {
                    int record = table + 4 + index * 6;
                    AddRange(U16(data, record), U16(data, record + 2));
                }
            }
            else
            {
                Add(All);
            }
        }

        /// <summary>
        /// The glyphs a lookup can start at: the first (input) coverage of each subtable.
        /// Unknown formats make the digest match everything.
        /// </summary>
        public static GlyphDigest ForLookup(byte[] data, LayoutLookup lookup, bool positioning)
        {
            var digest = new GlyphDigest();
            foreach (int subtable in lookup.Subtables)
            {
                int coverage = FirstCoverage(data, lookup.Type, subtable, positioning);
                if (coverage <= 0)
                    return All;
                digest.AddCoverage(data, coverage);
            }
            return digest;
        }

        private static int FirstCoverage(byte[] data, int type, int subtable, bool positioning)
        {
            bool context = positioning ? type == 7 : type == 5;
            bool chained = positioning ? type == 8 : type == 6;
            int format = U16(data, subtable);

            if (context && format == 3)
                return U16(data, subtable + 2) == 0 ? 0 : subtable + U16(data, subtable + 6);
            if (chained && format == 3)
            {
                int input = subtable + 4 + U16(data, subtable + 2) * 2;
                return U16(data, input) == 0 ? 0 : subtable + U16(data, input + 2);
            }
            return subtable + U16(data, subtable + 2);
        }
    }
}
