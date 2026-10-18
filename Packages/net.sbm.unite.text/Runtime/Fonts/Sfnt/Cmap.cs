using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Character map (cmap formats 4 / 12 / 0), decoded once into flat arrays so lookups
    /// are an allocation-free binary search. Subtable selection priority is (3,10) then
    /// (3,1) then any (0,*); format 12 outranks format 4 at equal platform priority.
    /// </summary>
    internal sealed class CharacterMap
    {
        private const int EncodingRecordSize = 8;

        private struct Subtable
        {
            public int Offset;   // absolute file offset of the format subtable
            public int Priority;
            public int Format;
        }

        private readonly byte[] _data;

        // Format 0
        private byte[] _latin;

        // Format 4 (segments) and 12 (groups), sorted by end code.
        private int[] _starts;
        private int[] _ends;
        private int[] _deltas;         // format 4: idDelta; format 12: start glyph id
        private int[] _rangePositions; // format 4: file offset of a segment's glyph id run, or 0

        private CharacterMap(byte[] data)
        {
            _data = data;
        }

        /// <summary>Reads the best supported subtable; null when there is none.</summary>
        public static CharacterMap Read(byte[] data, int offset)
        {
            var reader = new FontReader(data, offset);
            if (!Find(reader, offset, out var subtable))
                return null;

            var map = new CharacterMap(data);
            reader.Seek(subtable.Offset);
            switch (subtable.Format)
            {
                case 0:
                    map.ReadFormat0(reader);
                    break;
                case 4:
                    map.ReadFormat4(reader);
                    break;
                default:
                    map.ReadFormat12(reader);
                    break;
            }
            return map;
        }

        /// <summary>Glyph id of a code point; 0 (.notdef) when the code point is unmapped.</summary>
        public int Lookup(int codePoint)
        {
            if (_latin != null)
                return codePoint >= 0 && codePoint < _latin.Length ? _latin[codePoint] : 0;

            if (codePoint < 0)
                return 0;

            // First segment whose end code is not below the code point.
            int low = 0;
            int high = _ends.Length - 1;
            int found = -1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                if (_ends[middle] >= codePoint)
                {
                    found = middle;
                    high = middle - 1;
                }
                else
                {
                    low = middle + 1;
                }
            }

            if (found < 0 || codePoint < _starts[found])
                return 0;

            if (_rangePositions == null)
                return _deltas[found] + (codePoint - _starts[found]);

            if (_rangePositions[found] == 0)
                return (codePoint + _deltas[found]) & 0xFFFF;

            int position = _rangePositions[found] + (codePoint - _starts[found]) * 2;
            if (position + 2 > _data.Length)
                return 0;

            int glyphId = (_data[position] << 8) | _data[position + 1];
            return glyphId == 0 ? 0 : (glyphId + _deltas[found]) & 0xFFFF;
        }

        private static bool Find(FontReader reader, int offset, out Subtable best)
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

                int format = (reader.Data[subOffset] << 8) | reader.Data[subOffset + 1];
                if (format != 0 && format != 4 && format != 12)
                    continue;

                // Equal platform priority: a format-12 subtable covers every code point
                // its format-4 sibling covers, so keep it instead.
                bool better =
                    priority > best.Priority ||
                    (priority == best.Priority && format == 12 && best.Format != 12);

                if (!better)
                    continue;

                best.Offset = subOffset;
                best.Priority = priority;
                best.Format = format;
            }

            return best.Priority >= 0;
        }

        // Format 0: 256-entry byte table for latin-1 code points.
        private void ReadFormat0(FontReader reader)
        {
            reader.Skip(6); // format, length, language
            _latin = reader.Has(256) ? reader.ReadBytes(256) : Array.Empty<byte>();
        }

        // Format 4: segmented mapping for BMP code points.
        private void ReadFormat4(FontReader reader)
        {
            reader.Skip(6); // format, length, language
            int segCount = reader.ReadU16() / 2;
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            _ends = new int[segCount];
            _starts = new int[segCount];
            _deltas = new int[segCount];
            _rangePositions = new int[segCount];

            for (int segment = 0; segment < segCount; segment++)
                _ends[segment] = reader.ReadU16();

            reader.Skip(2); // reserved pad
            for (int segment = 0; segment < segCount; segment++)
                _starts[segment] = reader.ReadU16();

            for (int segment = 0; segment < segCount; segment++)
                _deltas[segment] = reader.ReadU16();

            for (int segment = 0; segment < segCount; segment++)
            {
                int entryPosition = reader.Position;
                int rangeOffset = reader.ReadU16();

                // Glyph id arrays are addressed relative to the idRangeOffset entry.
                _rangePositions[segment] = rangeOffset == 0 ? 0 : entryPosition + rangeOffset;
            }
        }

        // Format 12: full-code-point group table. After the 16-bit format come a u16
        // reserved, u32 length, u32 language and u32 numGroups, then (start, end, start
        // glyph id) triples.
        private void ReadFormat12(FontReader reader)
        {
            reader.Skip(12);
            int numGroups = (int)reader.ReadU32();
            numGroups = Math.Min(numGroups, Math.Max(0, (reader.Length - reader.Position) / 12));

            _starts = new int[numGroups];
            _ends = new int[numGroups];
            _deltas = new int[numGroups];
            for (int group = 0; group < numGroups; group++)
            {
                _starts[group] = (int)reader.ReadU32();
                _ends[group] = (int)reader.ReadU32();
                _deltas[group] = (int)reader.ReadU32();
            }
        }
    }
}
