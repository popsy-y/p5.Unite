using System;
using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Assembles plain sfnt bytes from tables: the common form WOFF and WOFF2 contents
    /// are rebuilt into. Tables are sorted by tag and padded to four bytes.
    /// </summary>
    internal static class SfntWriter
    {
        public static byte[] Build(uint version, List<KeyValuePair<string, byte[]>> tables)
        {
            tables.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));

            int count = tables.Count;
            int headerSize = 12 + 16 * count;
            int total = headerSize;
            foreach (var table in tables)
                total += Align4(table.Value.Length);

            var output = new byte[total];
            WriteU32(output, 0, version);
            WriteU16(output, 4, count);

            int searchRange = 1;
            int entrySelector = 0;
            while (searchRange * 2 <= count)
            {
                searchRange *= 2;
                entrySelector++;
            }
            WriteU16(output, 6, searchRange * 16);
            WriteU16(output, 8, entrySelector);
            WriteU16(output, 10, count * 16 - searchRange * 16);

            int dataOffset = headerSize;
            for (int index = 0; index < count; index++)
            {
                var (tag, data) = (tables[index].Key, tables[index].Value);
                int entry = 12 + 16 * index;
                for (int character = 0; character < 4; character++)
                    output[entry + character] = (byte)tag[character];

                WriteU32(output, entry + 4, Checksum(data));
                WriteU32(output, entry + 8, (uint)dataOffset);
                WriteU32(output, entry + 12, (uint)data.Length);
                Buffer.BlockCopy(data, 0, output, dataOffset, data.Length);
                dataOffset += Align4(data.Length);
            }

            return output;
        }

        private static int Align4(int length) => (length + 3) & ~3;

        private static uint Checksum(byte[] data)
        {
            uint sum = 0;
            for (int index = 0; index < data.Length; index += 4)
            {
                uint word = 0;
                for (int part = 0; part < 4; part++)
                    word = (word << 8) | (index + part < data.Length ? data[index + part] : (byte)0);
                sum = unchecked(sum + word);
            }
            return sum;
        }

        private static void WriteU16(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
    }
}
