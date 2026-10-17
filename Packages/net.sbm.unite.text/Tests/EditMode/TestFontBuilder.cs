using System;
using System.Text;

namespace Unite.Text.Tests
{
    /// <summary>
    /// Builds a minimal, valid TrueType font in memory for the engine tests. Layout:
    /// glyph 0 = blank .notdef, glyph 1 = square outline (100,100)..(700,700),
    /// glyph 2 = composite of glyph 1 translated +100 in x. unitsPerEm = 1000.
    /// </summary>
    internal static class TestFontBuilder
    {
        public const int UnitsPerEm = 1000;
        public const int AscenderValue = 800;
        public const int DescenderValue = -200;
        public const int NotdefAdvance = 500;
        public const int SquareAdvance = 800;

        public static byte[] Build()
        {
            var blobs = new[]
            {
                ("head", BuildHead()),
                ("hhea", BuildHhea()),
                ("hmtx", BuildHmtx()),
                ("maxp", BuildMaxp()),
                ("cmap", BuildCmap()),
                ("glyf", BuildSquareGlyf()),
                ("loca", BuildLoca())
            };

            int headerSize = 12 + blobs.Length * 16;
            int body = 0;

            foreach (var blob in blobs)
            {
                body += (blob.Item2.Length + 3) & ~3;
            }

            var font = new byte[headerSize + body];
            var writer = new BigEndianWriter(font);
            writer.WriteU32(0x00010000); // sfnt version
            writer.WriteU16((ushort)blobs.Length);
            writer.WriteU16(64);         // searchRange (16 * largest power of two <= 7)
            writer.WriteU16(2);          // entrySelector
            writer.WriteU16((ushort)(blobs.Length * 16 - 64));

            int offset = headerSize;
            foreach (var blob in blobs)
            {
                writer.WriteBytes(Encoding.ASCII.GetBytes(blob.Item1));
                writer.WriteU32(0); // checksum, intentionally zero (parser skips validation)
                writer.WriteU32((uint)offset);
                writer.WriteU32((uint)blob.Item2.Length);

                Array.Copy(blob.Item2, 0, font, offset, blob.Item2.Length);
                offset += (blob.Item2.Length + 3) & ~3;
            }
            return font;
        }

        private sealed class BigEndianWriter
        {
            private readonly byte[] _buffer;
            private int _position;

            public BigEndianWriter(byte[] buffer)
            {
                _buffer = buffer;
            }

            public void WriteU8(byte value) => _buffer[_position++] = value;

            public void WriteU16(ushort value)
            {
                _buffer[_position++] = (byte)(value >> 8);
                _buffer[_position++] = (byte)value;
            }

            public void WriteI16(short value) => WriteU16(unchecked((ushort)value));

            public void WriteU32(uint value)
            {
                WriteU16(unchecked((ushort)(value >> 16)));
                WriteU16(unchecked((ushort)value));
            }

            public void WriteU64(ulong value)
            {
                WriteU32((uint)(value >> 32));
                WriteU32((uint)value);
            }

            public void WriteBytes(byte[] bytes)
            {
                Array.Copy(bytes, 0, _buffer, _position, bytes.Length);
                _position += bytes.Length;
            }
        }

        private static byte[] BuildHead()
        {
            var table = new byte[54];
            var writer = new BigEndianWriter(table);

            writer.WriteU32(0x00010000);     // version
            writer.WriteU32(0x00010000);     // fontRevision
            writer.WriteU32(0);              // checkSumAdjustment
            writer.WriteU32(0x5F0F3CF5);     // magicNumber
            writer.WriteU16(0);              // flags
            writer.WriteU16(UnitsPerEm);     // unitsPerEm @ 18
            writer.WriteU64(0);              // created
            writer.WriteU64(0);              // modified
            writer.WriteI16(0);              // xMin
            writer.WriteI16(0);              // yMin
            writer.WriteI16(0);              // xMax
            writer.WriteI16(0);              // yMax
            writer.WriteU16(0);              // macStyle
            writer.WriteU16(8);              // lowestRecPPEM
            writer.WriteI16(2);              // fontDirectionHint
            writer.WriteI16(1);              // indexToLocFormat = long @ 50
            writer.WriteI16(0);              // glyphDataFormat
            return table;
        }

        private static byte[] BuildHhea()
        {
            var table = new byte[36];
            var writer = new BigEndianWriter(table);

            writer.WriteU32(0x00010000);     // version
            writer.WriteI16(AscenderValue);  // ascender @ 4
            writer.WriteI16(DescenderValue); // descender @ 6
            writer.WriteI16(100);            // lineGap
            writer.WriteU16(SquareAdvance);  // advanceWidthMax
            writer.WriteI16(0);              // minLeftSideBearing
            writer.WriteI16(0);              // minRightSideBearing
            writer.WriteI16(700);            // xMaxExtent
            writer.WriteI16(1);              // caretSlopeRise
            writer.WriteI16(0);              // caretSlopeRun
            writer.WriteI16(0);              // caretOffset
            writer.WriteI16(0);              // reserved
            writer.WriteI16(0);              // reserved
            writer.WriteI16(0);              // reserved
            writer.WriteI16(0);              // reserved
            writer.WriteI16(0);              // metricDataFormat
            writer.WriteU16(3);              // numberOfHMetrics @ 34
            return table;
        }

        private static byte[] BuildHmtx()
        {
            var table = new byte[12];
            var writer = new BigEndianWriter(table);

            // (advanceWidth, leftSideBearing) records for glyphs 0..2.
            writer.WriteU16(NotdefAdvance); writer.WriteI16(0);
            writer.WriteU16(SquareAdvance); writer.WriteI16(100);
            writer.WriteU16(SquareAdvance); writer.WriteI16(100);
            return table;
        }

        private static byte[] BuildMaxp()
        {
            var table = new byte[32];
            var writer = new BigEndianWriter(table);

            writer.WriteU32(0x00010000); // version
            writer.WriteU16(3);          // numGlyphs @ 4
            for (int index = 0; index < 13; index++)
            {
                writer.WriteU16(0);      // maxPoints ... maxComponentDepth
            }
            return table;
        }

        private static byte[] BuildCmap()
        {
            // 12-byte cmap header plus one (3,1) format 4 subtable of 40 bytes.
            var table = new byte[52];
            var writer = new BigEndianWriter(table);

            writer.WriteU16(0);  // version
            writer.WriteU16(1);  // numTables
            writer.WriteU16(3);  // platform: windows
            writer.WriteU16(1);  // encoding: unicode BMP
            writer.WriteU32(12); // subtable offset

            writer.WriteU16(4);  // format 4
            writer.WriteU16(40); // length
            writer.WriteU16(0);  // language
            writer.WriteU16(6);  // segCountX2 (3 segments incl. sentinel)
            writer.WriteU16(8);  // searchRange (ignored by the parser)
            writer.WriteU16(2);  // entrySelector
            writer.WriteU16(0);  // rangeShift (ignored)

            writer.WriteU16('A');   writer.WriteU16('B');  writer.WriteU16(0xFFFF); // endCode
            writer.WriteU16(0);     // reserved pad
            writer.WriteU16('A');   writer.WriteU16('B');  writer.WriteU16(0xFFFF); // startCode
            writer.WriteI16(-64);   writer.WriteI16(-64); writer.WriteI16(1);      // idDelta
            writer.WriteU16(0);     writer.WriteU16(0);    writer.WriteU16(0);      // idRangeOffset
            return table;
        }

        private static byte[] BuildSquareGlyf()
        {
            // One glyf table holding: blank glyph 0 (zero bytes), the square as glyph 1
            // and a composite glyph 2 referencing it with a +100 x translation.
            var square = new byte[36];
            var writer = new BigEndianWriter(square);

            writer.WriteI16(1);   // numberOfContours
            writer.WriteI16(100); writer.WriteI16(100);
            writer.WriteI16(700); writer.WriteI16(700); // bbox header (recomputed anyway)
            writer.WriteU16(3);   // endPtsOfContours[0]
            writer.WriteU16(0);   // instructionLength

            for (int point = 0; point < 4; point++)
            {
                writer.WriteU8(0x01); // on-curve; both cursors use full int16 deltas
            }

            writer.WriteI16(0);  writer.WriteI16(600); writer.WriteI16(0);   writer.WriteI16(-600);
            writer.WriteI16(0);  writer.WriteI16(0);   writer.WriteI16(600); writer.WriteI16(0);

            // glyph 0 is a blank (zero-length) record: loca[0] == loca[1] == 0.
            var composite = new byte[20];
            var compositeWriter = new BigEndianWriter(composite);

            compositeWriter.WriteI16(-1); // numberOfContours < 0 → composite
            compositeWriter.WriteI16(0);  compositeWriter.WriteI16(0);
            compositeWriter.WriteI16(0);  compositeWriter.WriteI16(0); // bbox placeholder
            compositeWriter.WriteU16(0x0002); // ARGS_ARE_XY_VALUES, byte args
            compositeWriter.WriteU16(1);      // component glyph id
            compositeWriter.WriteU8(100);     // dx (fits a signed byte)
            compositeWriter.WriteU8(0);       // dy

            var glyf = new byte[square.Length + composite.Length];
            Array.Copy(square, 0, glyf, 0, square.Length);
            Array.Copy(composite, 0, glyf, square.Length, composite.Length);
            return glyf;
        }

        private static byte[] BuildLoca()
        {
            // Long format: glyph starts [0, 0, 36, 56] — blank .notdef shares glyph 1's
            // start (zero-length record), glyph 1 ends at 36, the composite ends at 56.
            var table = new byte[16];
            var writer = new BigEndianWriter(table);

            writer.WriteU32(0);
            writer.WriteU32(0);
            writer.WriteU32(36);
            writer.WriteU32(56);
            return table;
        }
    }
}
