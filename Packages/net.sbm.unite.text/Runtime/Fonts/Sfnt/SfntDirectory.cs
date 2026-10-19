using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>Location of one sfnt table inside the font byte array.</summary>
    internal readonly struct TableRecord
    {
        public TableRecord(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }
        public int Length { get; }
    }

    /// <summary>
    /// The table directory of an sfnt font (TrueType or OpenType, also the first font of
    /// a collection). WOFF and WOFF2 files are first rebuilt into plain sfnt bytes, so
    /// every later stage works on the same structure.
    /// </summary>
    internal sealed class SfntDirectory
    {
        public const uint TrueTypeVersion = 0x00010000u;
        public const uint AppleTrueTypeVersion = 0x74727565u; // 'true'
        public const uint OpenTypeVersion = 0x4F54544Fu;      // 'OTTO'
        public const uint CollectionVersion = 0x74746366u;    // 'ttcf'

        private readonly Dictionary<string, TableRecord> _tables;

        private SfntDirectory(uint version, Dictionary<string, TableRecord> tables)
        {
            Version = version;
            _tables = tables;
        }

        /// <summary>The sfnt version tag: TrueType outlines or 'OTTO' (CFF outlines).</summary>
        public uint Version { get; }

        public bool Has(string tag) => _tables.ContainsKey(tag);

        public bool TryGet(string tag, out TableRecord record) => _tables.TryGetValue(tag, out record);

        public TableRecord Get(string tag)
        {
            if (!_tables.TryGetValue(tag, out var record))
                throw new FontFormatException($"required '{tag}' table is missing.");

            return record;
        }

        /// <summary>Number of fonts in the data: the collection size for .ttc files, otherwise 1.</summary>
        public static int CountFonts(byte[] data)
        {
            var reader = new FontReader(data);
            if (reader.ReadU32() != CollectionVersion)
                return 1;
            reader.Skip(4);
            return (int)reader.ReadU32();
        }

        /// <summary>The table directory of a font; <paramref name="collectionIndex"/> picks the font of a collection.</summary>
        public static SfntDirectory Read(byte[] data, int collectionIndex = 0)
        {
            var reader = new FontReader(data);
            uint version = reader.ReadU32();

            if (version == CollectionVersion)
            {
                reader.Skip(4); // collection version
                uint fontCount = reader.ReadU32();
                if (fontCount == 0)
                    throw new FontFormatException("font collection is empty.");
                if (collectionIndex < 0 || collectionIndex >= fontCount)
                    throw new FontFormatException($"font collection has no font {collectionIndex}.");

                reader.Skip(collectionIndex * 4);
                reader.Seek((int)reader.ReadU32()); // the font's table directory
                version = reader.ReadU32();
            }

            if (version != TrueTypeVersion && version != AppleTrueTypeVersion && version != OpenTypeVersion)
                throw new FontFormatException("unknown font signature (not TrueType, OpenType or a WOFF file).");

            int tableCount = reader.ReadU16();
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            var tables = new Dictionary<string, TableRecord>(tableCount);
            for (int index = 0; index < tableCount; index++)
            {
                string tag = reader.ReadTag();
                reader.Skip(4); // checksum
                long offset = reader.ReadU32();
                long length = reader.ReadU32();

                if (offset + length > data.Length)
                    throw new FontFormatException($"table '{tag}' extends past the end of the file.");

                tables[tag] = new TableRecord((int)offset, (int)length);
            }

            return new SfntDirectory(version, tables);
        }
    }
}
