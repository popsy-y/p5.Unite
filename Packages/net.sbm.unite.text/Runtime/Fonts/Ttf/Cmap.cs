using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// cmap table access (formats 4 / 12 / 0). Subtable selection priority is
    /// (3,10) then (3,1) then any (0,*); format 12 outranks format 4 at equal
    /// platform priority. Lookups walk the chosen subtable directly.
    /// </summary>
    internal static class CmapTables
    {
        private const int EncodingRecordSize = 8;

        private struct Subtable
        {
            public int Offset;   // absolute file offset of the format subtable
            public int Priority;
            public bool Format12;
        }

        public static bool HasSupportedSubtable(byte[] data, int offset, int length)
        {
            return Find(new TtfReader(data, offset), offset, length, out _);
        }

        /// <summary>
        /// Maps a code point through the best available cmap subtable. Returns 0
        /// (.notdef) when the code point is absent or no supported subtable exists.
        /// </summary>
        public static int Lookup(TtfReader reader, int cmapOffset, int cmapLength, int codePoint)
        {
            if (!Find(reader, cmapOffset, cmapLength, out var subtable))
                return 0;

            reader.Seek(subtable.Offset);
            switch (reader.ReadU16())
            {
                case 0:
                    return LookupFormat0(reader, codePoint);
                case 4:
                    return LookupFormat4(reader, subtable.Offset, codePoint);
                case 12:
                    return LookupFormat12(reader, subtable.Offset, codePoint);
                default:
                    return 0;
            }
        }

        private static bool Find(TtfReader reader, int offset, int length, out Subtable best)
        {
            best = default;
            best.Priority = -1;

            reader.Seek(offset);
            if (!reader.Has(4))
                return false;

            reader.Skip(2); // cmap version
            int numTables = reader.ReadU16();

            for (int index = 0; index < numTables; index++)
            {
                if (!reader.Has(EncodingRecordSize))
                    break;

                int platform = reader.ReadU16();
                int encoding = reader.ReadU16();
                int subOffset = offset + (int)reader.ReadU32();

                int priority =
                    platform == 3 && encoding == 10 ? 3 :
                    platform == 3 && encoding == 1 ? 2 :
                    platform == 0 ? 1 : 0;

                if (priority < best.Priority || subOffset + 2 > reader.Length)
                    continue;

                var subReader = new TtfReader(reader.Data, subOffset);
                int format = subReader.ReadU16();

                bool format12 = format == 12;
                if (format != 0 && format != 4 && format != 12)
                    continue;

                // Equal platform priority: a format-12 subtable covers every code point
                // its format-4 sibling covers, so keep it instead.
                bool better =
                    priority > best.Priority ||
                    (priority == best.Priority && format12 && !best.Format12);

                if (!better)
                    continue;

                best.Offset = subOffset;
                best.Priority = priority;
                best.Format12 = format12;
            }

            return best.Priority >= 0;
        }

        // Format 0: 256-entry byte table for latin-1 code points.
        private static int LookupFormat0(TtfReader reader, int codePoint)
        {
            reader.Skip(4); // length, language
            if (codePoint < 0 || codePoint > 255 || !reader.Has(256))
                return 0;

            reader.Skip(codePoint);
            return reader.ReadU8();
        }

        // Format 4: segmented mapping for BMP code points. Segments are searched
        // linearly (the atlas use case only needs a handful of characters per run).
        private static int LookupFormat4(TtfReader reader, int subOffset, int codePoint)
        {
            reader.Skip(4); // length, language
            int segCount = reader.ReadU16() / 2;
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            int endCodesPosition = reader.Position;
            reader.Skip(segCount * 2 + 2); // endCode[] + reserved pad
            int startCodesPosition = reader.Position;
            reader.Skip(segCount * 2);
            int idDeltasPosition = reader.Position;
            reader.Skip(segCount * 2);
            int idRangeOffsetsPosition = reader.Position;

            if (codePoint < 0 || codePoint > 0xFFFF)
                return 0;

            for (int segment = 0; segment < segCount; segment++)
            {
                int startCode = ReadU16At(reader, startCodesPosition, segment);
                int endCode = ReadU16At(reader, endCodesPosition, segment);

                if (codePoint < startCode || codePoint > endCode)
                    continue;

                int idDelta = ReadU16At(reader, idDeltasPosition, segment);
                int idRangeOffset = ReadU16At(reader, idRangeOffsetsPosition, segment);

                int glyphId;
                if (idRangeOffset == 0)
                {
                    glyphId = (codePoint + idDelta) & 0xFFFF;
                }
                else
                {
                    // Glyph id arrays are addressed relative to the idRangeOffset entry.
                    int position = idRangeOffsetsPosition
                        + segment * 2
                        + idRangeOffset
                        + (codePoint - startCode) * 2;

                    if (position + 2 > reader.Length)
                        return 0;

                    var indexed = new TtfReader(reader.Data, position);
                    glyphId = indexed.ReadU16();

                    if (glyphId == 0)
                        return 0;

                    glyphId = (glyphId + idDelta) & 0xFFFF;
                }
                return glyphId;
            }

            return 0;
        }

        // Format 12: full-code-point group table.
        private static int LookupFormat12(TtfReader reader, int subOffset, int codePoint)
        {
            // Layout: u16 format, u16 reserved, u32 length, u32 language,
            // u32 numGroups, then groups of (u32 start, u32 end, u32 startGlyphId).
            reader.Seek(subOffset + 12);
            int numGroups = (int)reader.ReadU32();

            for (int group = 0; group < numGroups; group++)
            {
                if (!reader.Has(12))
                    break;

                int startCode = (int)reader.ReadU32();
                int endCode = (int)reader.ReadU32();
                int startGlyphId = (int)reader.ReadU32();

                if (codePoint >= startCode && codePoint <= endCode)
                    return startGlyphId + (codePoint - startCode);
            }

            return 0;
        }

        private static int ReadU16At(TtfReader reader, int position, int element)
        {
            var window = new TtfReader(reader.Data, position + element * 2);
            return window.ReadU16();
        }
    }
}
