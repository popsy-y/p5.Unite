using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// WOFF2 container: one Brotli stream holding every table, with the glyf / loca (and
    /// optionally hmtx) tables stored in a transformed, more compressible form. Rebuilt
    /// into plain sfnt bytes.
    /// </summary>
    internal static class Woff2Container
    {
        public const uint Signature = 0x774F4632u; // 'wOF2'

        private const int HeaderSize = 48;
        private const long MaxUncompressedSize = 256L * 1024 * 1024;
        private const uint CollectionFlavor = 0x74746366u; // 'ttcf'
        private const int ExplicitTagFlag = 0x3F;

        private static readonly string[] KnownTags =
        {
            "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ", "fpgm", "glyf", "loca",
            "prep", "CFF ", "VORG", "EBDT", "EBLC", "gasp", "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea",
            "vmtx", "BASE", "GDEF", "GPOS", "GSUB", "EBSC", "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL",
            "SVG ", "sbix", "acnt", "avar", "bdat", "bloc", "bsln", "cvar", "fdsc", "feat", "fmtx", "fvar",
            "gvar", "hsty", "just", "lcar", "mort", "morx", "opbd", "prop", "trak", "Zapf", "Silf", "Glat",
            "Gloc", "Feat", "Sill"
        };

        private sealed class Entry
        {
            public string Tag;
            public int TransformVersion;
            public bool Transformed;
            public int OriginalLength;
            public int TransformLength;
        }

        public static byte[] ToSfnt(byte[] data)
        {
            var reader = new FontReader(data);
            reader.Skip(4); // signature
            uint flavor = reader.ReadU32();
            reader.Skip(4); // length
            int tableCount = reader.ReadU16();
            reader.Skip(2 + 4); // reserved, totalSfntSize
            long compressedLength = reader.ReadU32();
            reader.Seek(HeaderSize);

            var entries = new List<Entry>(tableCount);
            long uncompressedLength = 0;
            for (int index = 0; index < tableCount; index++)
            {
                var entry = ReadEntry(reader);
                entries.Add(entry);
                uncompressedLength += entry.TransformLength;
            }

            if (uncompressedLength > MaxUncompressedSize)
                throw new FontFormatException("WOFF2 font is implausibly large.");

            int[] fontTables = null;
            if (flavor == CollectionFlavor)
                fontTables = ReadFirstCollectionFont(reader, out flavor);

            int streamStart = reader.Position;
            if (streamStart + compressedLength > data.Length)
                throw new FontFormatException("WOFF2 compressed data is truncated.");

            byte[] tableData = BrotliDecoder.Decompress(data, streamStart, (int)compressedLength, (int)uncompressedLength);
            if (tableData.Length != uncompressedLength)
                throw new FontFormatException("WOFF2 table data has an unexpected size.");

            return Rebuild(flavor, entries, fontTables, tableData);
        }

        private static Entry ReadEntry(FontReader reader)
        {
            int flags = reader.ReadU8();
            string tag = (flags & ExplicitTagFlag) == ExplicitTagFlag
                ? reader.ReadTag()
                : KnownTags[flags & ExplicitTagFlag];

            int version = (flags >> 6) & 3;
            bool isGlyfOrLoca = tag == "glyf" || tag == "loca";
            var entry = new Entry
            {
                Tag = tag,
                TransformVersion = version,
                Transformed = isGlyfOrLoca ? version == 0 : version != 0,
                OriginalLength = (int)Woff2Streams.ReadBase128(reader)
            };

            entry.TransformLength = entry.Transformed
                ? (int)Woff2Streams.ReadBase128(reader)
                : entry.OriginalLength;
            return entry;
        }

        // The tables of the first font in a collection (indices into the directory).
        private static int[] ReadFirstCollectionFont(FontReader reader, out uint flavor)
        {
            reader.Skip(4); // collection version
            int fontCount = Woff2Streams.Read255UShort(reader);
            int[] first = null;
            flavor = SfntDirectory.TrueTypeVersion;

            for (int font = 0; font < fontCount; font++)
            {
                int count = Woff2Streams.Read255UShort(reader);
                uint fontFlavor = reader.ReadU32();
                var indices = new int[count];
                for (int index = 0; index < count; index++)
                    indices[index] = Woff2Streams.Read255UShort(reader);

                if (font != 0)
                    continue;

                first = indices;
                flavor = fontFlavor;
            }

            if (first == null)
                throw new FontFormatException("WOFF2 collection is empty.");

            return first;
        }

        private static byte[] Rebuild(uint flavor, List<Entry> entries, int[] fontTables, byte[] tableData)
        {
            var slices = new Dictionary<string, byte[]>();
            var order = new List<string>();
            int offset = 0;
            var offsets = new int[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                offsets[index] = offset;
                offset += entries[index].TransformLength;
            }

            var selected = new List<int>();
            if (fontTables == null)
            {
                for (int index = 0; index < entries.Count; index++)
                    selected.Add(index);
            }
            else
            {
                selected.AddRange(fontTables);
            }

            Entry glyf = null;
            int glyfOffset = 0;
            Entry loca = null;
            Entry hmtx = null;
            int hmtxOffset = 0;
            foreach (int index in selected)
            {
                if (index < 0 || index >= entries.Count)
                    throw new FontFormatException("WOFF2 collection refers to a missing table.");

                var entry = entries[index];
                order.Add(entry.Tag);
                switch (entry.Tag)
                {
                    case "glyf":
                        glyf = entry;
                        glyfOffset = offsets[index];
                        break;
                    case "loca":
                        loca = entry;
                        break;
                    case "hmtx":
                        hmtx = entry;
                        hmtxOffset = offsets[index];
                        break;
                }

                if (entry.Transformed)
                    continue;

                var raw = new byte[entry.OriginalLength];
                System.Buffer.BlockCopy(tableData, offsets[index], raw, 0, entry.OriginalLength);
                slices[entry.Tag] = raw;
            }

            short[] xMins = null;
            int indexFormat = 0;
            if (glyf != null && glyf.Transformed)
            {
                if (loca == null || !loca.Transformed)
                    throw new FontFormatException("WOFF2 transformed glyf needs a transformed loca.");

                Woff2GlyfTransform.Reconstruct(
                    tableData, glyfOffset, glyf.TransformLength, loca.OriginalLength,
                    out byte[] glyfTable, out byte[] locaTable, out indexFormat, out xMins
                );
                slices["glyf"] = glyfTable;
                slices["loca"] = locaTable;

                // head must agree with the index format the glyf transform chose.
                if (slices.TryGetValue("head", out var head) && head.Length >= 52)
                {
                    head[50] = (byte)(indexFormat >> 8);
                    head[51] = (byte)indexFormat;
                }
            }

            if (hmtx != null && hmtx.Transformed)
            {
                if (hmtx.TransformVersion != 1)
                    throw new FontFormatException("WOFF2 hmtx transform version is not supported.");
                if (xMins == null || !slices.TryGetValue("hhea", out var hhea) || !slices.TryGetValue("maxp", out var maxp))
                    throw new FontFormatException("WOFF2 transformed hmtx needs glyf, hhea and maxp.");

                slices["hmtx"] = Woff2GlyfTransform.ReconstructHmtx(tableData, hmtxOffset, hmtx.TransformLength, hhea, maxp, xMins);
            }

            var tables = new List<KeyValuePair<string, byte[]>>(order.Count);
            foreach (string tag in order)
            {
                if (!slices.TryGetValue(tag, out var table))
                    throw new FontFormatException($"WOFF2 table '{tag}' uses an unsupported transform.");

                tables.Add(new KeyValuePair<string, byte[]>(tag, table));
            }

            return SfntWriter.Build(flavor, tables);
        }
    }
}
