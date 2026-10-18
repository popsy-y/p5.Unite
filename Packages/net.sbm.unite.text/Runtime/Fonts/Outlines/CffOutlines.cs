using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Outlines from a CFF table (OpenType fonts with PostScript outlines): Type 2
    /// charstrings with cubic curves, local / global subroutines, and CID-keyed fonts
    /// (FDSelect picks the private dictionary per glyph).
    /// </summary>
    internal sealed class CffOutlines : IOutlineSource
    {
        // Top DICT / Private DICT operators.
        private const int OpCharStrings = 17;
        private const int OpPrivate = 18;
        private const int OpSubrs = 19;
        private const int OpFdArray = 12 << 8 | 36;
        private const int OpFdSelect = 12 << 8 | 37;

        private readonly byte[] _data;
        private readonly CffIndex _charStrings;
        private readonly CffIndex _globalSubrs;
        private readonly PrivateData[] _privates;
        private readonly byte[] _fdSelect; // per-glyph font dict index, null = always 0
        private readonly float _flatness;
        private readonly Dictionary<int, GlyphOutline> _cache = new();

        private struct PrivateData
        {
            public CffIndex LocalSubrs;
        }

        private CffOutlines(
            byte[] data,
            CffIndex charStrings,
            CffIndex globalSubrs,
            PrivateData[] privates,
            byte[] fdSelect,
            float flatness
        )
        {
            _flatness = flatness;
            _data = data;
            _charStrings = charStrings;
            _globalSubrs = globalSubrs;
            _privates = privates;
            _fdSelect = fdSelect;
        }

        public int GlyphCount => _charStrings.Count;

        public static CffOutlines Create(byte[] data, TableRecord table, int unitsPerEm)
        {
            var reader = new FontReader(data, table.Offset);
            int major = reader.ReadU8();
            reader.Skip(1); // minor
            int headerSize = reader.ReadU8();
            reader.Skip(1); // offSize

            if (major != 1)
                throw new FontFormatException($"CFF version {major} is not supported.");

            reader.Seek(table.Offset + headerSize);
            CffIndex.Read(data, reader.Position, out int afterNames);             // Name INDEX
            var topDicts = CffIndex.Read(data, afterNames, out int afterTop);
            CffIndex.Read(data, afterTop, out int afterStrings);                  // String INDEX
            var globalSubrs = CffIndex.Read(data, afterStrings, out _);

            if (topDicts.Count < 1)
                throw new FontFormatException("CFF has no Top DICT.");

            var top = CffDict.Parse(data, topDicts.Offset(0), topDicts.Length(0));
            if (!top.TryGet(OpCharStrings, out var charStringOffset))
                throw new FontFormatException("CFF has no CharStrings.");

            var charStrings = CffIndex.Read(data, table.Offset + (int)charStringOffset[0], out _);

            PrivateData[] privates;
            byte[] fdSelect = null;
            if (top.TryGet(OpFdArray, out var fdArrayOffset))
            {
                var fontDicts = CffIndex.Read(data, table.Offset + (int)fdArrayOffset[0], out _);
                privates = new PrivateData[fontDicts.Count];
                for (int index = 0; index < fontDicts.Count; index++)
                {
                    var fontDict = CffDict.Parse(data, fontDicts.Offset(index), fontDicts.Length(index));
                    privates[index] = ReadPrivate(data, table.Offset, fontDict);
                }

                if (top.TryGet(OpFdSelect, out var fdSelectOffset))
                    fdSelect = ReadFdSelect(data, table.Offset + (int)fdSelectOffset[0], charStrings.Count);
            }
            else
            {
                privates = new[] { ReadPrivate(data, table.Offset, top) };
            }

            return new CffOutlines(data, charStrings, globalSubrs, privates, fdSelect, OutlineFlatness.For(unitsPerEm));
        }

        public GlyphOutline Load(int glyphId, float[] coordinates)
        {
            if (glyphId < 0 || glyphId >= _charStrings.Count)
                return null;

            if (_cache.TryGetValue(glyphId, out var cached))
                return cached;

            int fontDict = _fdSelect != null ? _fdSelect[glyphId] : 0;
            var localSubrs = fontDict < _privates.Length ? _privates[fontDict].LocalSubrs : default;

            var interpreter = new CffCharString(_data, _globalSubrs, localSubrs, _flatness);
            var outline = interpreter.Run(_charStrings.Offset(glyphId), _charStrings.Length(glyphId));

            _cache[glyphId] = outline;
            return outline;
        }

        public float AdvanceDelta(int glyphId, float[] coordinates) => 0f;

        private static PrivateData ReadPrivate(byte[] data, int cffOffset, CffDict dict)
        {
            var result = new PrivateData();
            if (!dict.TryGet(OpPrivate, out var privateOperands) || privateOperands.Length < 2)
                return result;

            int size = (int)privateOperands[0];
            int offset = cffOffset + (int)privateOperands[1];
            var privateDict = CffDict.Parse(data, offset, size);
            if (privateDict.TryGet(OpSubrs, out var subrs))
                result.LocalSubrs = CffIndex.Read(data, offset + (int)subrs[0], out _);

            return result;
        }

        // FDSelect formats 0 (one byte per glyph) and 3 (ranges).
        private static byte[] ReadFdSelect(byte[] data, int offset, int glyphCount)
        {
            var reader = new FontReader(data, offset);
            int format = reader.ReadU8();
            var select = new byte[glyphCount];

            if (format == 0)
            {
                for (int glyph = 0; glyph < glyphCount; glyph++)
                    select[glyph] = reader.ReadU8();
            }
            else if (format == 3)
            {
                int rangeCount = reader.ReadU16();
                int first = reader.ReadU16();
                for (int range = 0; range < rangeCount; range++)
                {
                    byte fontDict = reader.ReadU8();
                    int next = reader.ReadU16();
                    for (int glyph = first; glyph < next && glyph < glyphCount; glyph++)
                        select[glyph] = fontDict;
                    first = next;
                }
            }
            else
            {
                throw new FontFormatException($"CFF FDSelect format {format} is not supported.");
            }

            return select;
        }
    }
}
