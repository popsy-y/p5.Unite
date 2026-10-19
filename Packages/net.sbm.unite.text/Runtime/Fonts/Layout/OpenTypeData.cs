namespace Unite.Text.Fonts.Layout
{
    /// <summary>
    /// Big-endian reads and the shared OpenType layout lookups (Coverage, ClassDef) over the
    /// raw font bytes. Offsets are absolute positions in the sfnt data. Reads outside the
    /// data throw IndexOutOfRangeException; the shaper catches it and falls back to plain
    /// glyph mapping for fonts with broken tables.
    /// </summary>
    internal static class OpenTypeData
    {
        public static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

        public static int I16(byte[] data, int offset) => (short)((data[offset] << 8) | data[offset + 1]);

        public static int U32(byte[] data, int offset)
            => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        public static int Tag(byte[] data, int offset) => U32(data, offset);

        /// <summary>A four-character tag as the int that <see cref="Tag(byte[], int)"/> reads.</summary>
        public static int Tag(string tag)
            => (tag[0] << 24) | (tag[1] << 16) | (tag[2] << 8) | tag[3];

        /// <summary>Coverage index of a glyph, or -1 when the coverage table does not list it.</summary>
        public static int Coverage(byte[] data, int table, int glyph)
        {
            int format = U16(data, table);
            int count = U16(data, table + 2);
            if (format == 1)
            {
                int low = 0;
                int high = count - 1;
                while (low <= high)
                {
                    int middle = (low + high) >> 1;
                    int value = U16(data, table + 4 + middle * 2);
                    if (value == glyph)
                        return middle;
                    if (value < glyph)
                        low = middle + 1;
                    else
                        high = middle - 1;
                }
                return -1;
            }

            if (format == 2)
            {
                int low = 0;
                int high = count - 1;
                while (low <= high)
                {
                    int middle = (low + high) >> 1;
                    int record = table + 4 + middle * 6;
                    int start = U16(data, record);
                    int end = U16(data, record + 2);
                    if (glyph < start)
                        high = middle - 1;
                    else if (glyph > end)
                        low = middle + 1;
                    else
                        return U16(data, record + 4) + glyph - start;
                }
            }
            return -1;
        }

        /// <summary>Class of a glyph in a ClassDef table (0 for unlisted glyphs or a null offset).</summary>
        public static int ClassOf(byte[] data, int table, int glyph)
        {
            if (table <= 0)
                return 0;

            int format = U16(data, table);
            if (format == 1)
            {
                int start = U16(data, table + 2);
                int count = U16(data, table + 4);
                int index = glyph - start;
                return index >= 0 && index < count ? U16(data, table + 6 + index * 2) : 0;
            }

            if (format == 2)
            {
                int count = U16(data, table + 2);
                int low = 0;
                int high = count - 1;
                while (low <= high)
                {
                    int middle = (low + high) >> 1;
                    int record = table + 4 + middle * 6;
                    int start = U16(data, record);
                    int end = U16(data, record + 2);
                    if (glyph < start)
                        high = middle - 1;
                    else if (glyph > end)
                        low = middle + 1;
                    else
                        return U16(data, record + 4);
                }
            }
            return 0;
        }

        /// <summary>Absolute position of an Offset16 stored at <paramref name="field"/>, relative to <paramref name="baseOffset"/>; 0 for null offsets.</summary>
        public static int Offset16(byte[] data, int baseOffset, int field)
        {
            int value = U16(data, field);
            return value == 0 ? 0 : baseOffset + value;
        }
    }
}
