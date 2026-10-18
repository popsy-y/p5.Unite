using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// WOFF 1.0 container: an sfnt whose tables are individually zlib-compressed.
    /// Rebuilt into plain sfnt bytes.
    /// </summary>
    internal static class WoffContainer
    {
        public const uint Signature = 0x774F4646u; // 'wOFF'

        private const int HeaderSize = 44;
        private const int TableEntrySize = 20;

        public static byte[] ToSfnt(byte[] data)
        {
            var reader = new FontReader(data);
            reader.Skip(4); // signature
            uint flavor = reader.ReadU32();
            reader.Skip(4); // length
            int tableCount = reader.ReadU16();
            reader.Seek(HeaderSize);

            var tables = new List<KeyValuePair<string, byte[]>>(tableCount);
            for (int index = 0; index < tableCount; index++)
            {
                reader.Seek(HeaderSize + index * TableEntrySize);
                string tag = reader.ReadTag();
                long offset = reader.ReadU32();
                long compressedLength = reader.ReadU32();
                long originalLength = reader.ReadU32();

                if (offset + compressedLength > data.Length || compressedLength > originalLength)
                    throw new FontFormatException($"WOFF table '{tag}' is out of range.");

                byte[] table = compressedLength == originalLength
                    ? Slice(data, (int)offset, (int)originalLength)
                    : Inflate(data, (int)offset, (int)compressedLength, (int)originalLength, tag);

                tables.Add(new KeyValuePair<string, byte[]>(tag, table));
            }

            return SfntWriter.Build(flavor, tables);
        }

        private static byte[] Slice(byte[] data, int offset, int length)
        {
            var result = new byte[length];
            System.Buffer.BlockCopy(data, offset, result, 0, length);
            return result;
        }

        // zlib stream: two header bytes, deflate data, adler32 trailer (not verified).
        private static byte[] Inflate(byte[] data, int offset, int length, int originalLength, string tag)
        {
            if (length < 3)
                throw new FontFormatException($"WOFF table '{tag}' has no compressed data.");

            var result = new byte[originalLength];
            using var input = new MemoryStream(data, offset + 2, length - 2, writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);

            int total = 0;
            while (total < originalLength)
            {
                int read = deflate.Read(result, total, originalLength - total);
                if (read <= 0)
                    throw new FontFormatException($"WOFF table '{tag}' is shorter than its declared length.");
                total += read;
            }

            return result;
        }
    }
}
