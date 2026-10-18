using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>CFF INDEX: an array of variable-length objects addressed by offset table.</summary>
    internal readonly struct CffIndex
    {
        private readonly int[] _offsets; // absolute file offsets, Count + 1 entries

        private CffIndex(int[] offsets) => _offsets = offsets;

        public int Count => _offsets == null ? 0 : _offsets.Length - 1;
        public int Offset(int index) => _offsets[index];
        public int Length(int index) => _offsets[index + 1] - _offsets[index];

        /// <summary>Reads the INDEX at <paramref name="position"/>; <paramref name="end"/> is the first byte after it.</summary>
        public static CffIndex Read(byte[] data, int position, out int end)
        {
            var reader = new FontReader(data, position);
            int count = reader.ReadU16();
            if (count == 0)
            {
                end = position + 2;
                return new CffIndex(null);
            }

            int offsetSize = reader.ReadU8();
            if (offsetSize < 1 || offsetSize > 4)
                throw new FontFormatException("CFF INDEX has an invalid offset size.");

            int dataStart = position + 3 + (count + 1) * offsetSize - 1;
            var offsets = new int[count + 1];
            for (int index = 0; index <= count; index++)
            {
                long value = 0;
                for (int part = 0; part < offsetSize; part++)
                    value = (value << 8) | reader.ReadU8();

                offsets[index] = (int)(dataStart + value);
                if (offsets[index] > data.Length || (index > 0 && offsets[index] < offsets[index - 1]))
                    throw new FontFormatException("CFF INDEX offsets are invalid.");
            }

            end = offsets[count];
            return new CffIndex(offsets);
        }
    }

    /// <summary>A parsed CFF DICT: operator → operand list.</summary>
    internal sealed class CffDict
    {
        private readonly Dictionary<int, double[]> _entries = new();

        public bool TryGet(int op, out double[] operands) => _entries.TryGetValue(op, out operands);

        public static CffDict Parse(byte[] data, int offset, int length)
        {
            var dict = new CffDict();
            var operands = new List<double>();
            int position = offset;
            int end = offset + length;

            while (position < end)
            {
                int b0 = data[position++];
                if (b0 <= 21)
                {
                    int op = b0;
                    if (b0 == 12)
                        op = (12 << 8) | data[position++];

                    dict._entries[op] = operands.ToArray();
                    operands.Clear();
                }
                else if (b0 == 28)
                {
                    operands.Add((short)((data[position] << 8) | data[position + 1]));
                    position += 2;
                }
                else if (b0 == 29)
                {
                    operands.Add((data[position] << 24) | (data[position + 1] << 16) | (data[position + 2] << 8) | data[position + 3]);
                    position += 4;
                }
                else if (b0 == 30)
                {
                    operands.Add(ReadReal(data, ref position));
                }
                else if (b0 >= 32 && b0 <= 246)
                {
                    operands.Add(b0 - 139);
                }
                else if (b0 >= 247 && b0 <= 250)
                {
                    operands.Add((b0 - 247) * 256 + data[position++] + 108);
                }
                else if (b0 >= 251 && b0 <= 254)
                {
                    operands.Add(-(b0 - 251) * 256 - data[position++] - 108);
                }
            }

            return dict;
        }

        // Nibble-encoded real number; only its magnitude matters for the offsets we read.
        private static double ReadReal(byte[] data, ref int position)
        {
            var text = new System.Text.StringBuilder();
            while (position < data.Length)
            {
                int pair = data[position++];
                foreach (int nibble in new[] { pair >> 4, pair & 0xF })
                {
                    switch (nibble)
                    {
                        case 0xF:
                            return double.TryParse(text.ToString(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : 0.0;
                        case 0xA: text.Append('.'); break;
                        case 0xB: text.Append('E'); break;
                        case 0xC: text.Append("E-"); break;
                        case 0xE: text.Append('-'); break;
                        case 0xD: break;
                        default: text.Append((char)('0' + nibble)); break;
                    }
                }
            }
            return 0.0;
        }
    }
}
