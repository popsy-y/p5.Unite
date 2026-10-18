using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Reverses the WOFF2 table transforms: the glyf + loca transform (glyph data split
    /// into separate streams, coordinates stored as variable-size triplets) and the hmtx
    /// transform (left side bearings omitted when they equal the glyph's xMin).
    /// </summary>
    internal static class Woff2GlyfTransform
    {
        private const int StreamCount = 7;
        private const int CompositeFlagMoreComponents = 0x0020;
        private const int CompositeFlagArgsAreWords = 0x0001;
        private const int CompositeFlagHaveScale = 0x0008;
        private const int CompositeFlagHaveXYScale = 0x0040;
        private const int CompositeFlagHaveTwoByTwo = 0x0080;
        private const int CompositeFlagHaveInstructions = 0x0100;
        private const int OverlapSimpleBit = 0x40;
        private const int OverlapSimpleOption = 1;

        private struct GlyphPoint
        {
            public int X;
            public int Y;
            public bool OnCurve;
        }

        public static void Reconstruct(
            byte[] data,
            int offset,
            int length,
            int locaOriginalLength,
            out byte[] glyfTable,
            out byte[] locaTable,
            out int indexFormat,
            out short[] xMins
        )
        {
            var header = new FontReader(data, offset);
            header.Skip(2); // version
            int options = header.ReadU16();
            int glyphCount = header.ReadU16();
            indexFormat = header.ReadU16();

            int expectedLoca = (indexFormat != 0 ? 4 : 2) * (glyphCount + 1);
            if (locaOriginalLength != expectedLoca)
                throw new FontFormatException("WOFF2: loca length does not match the glyph count.");

            var sizes = new int[StreamCount];
            for (int index = 0; index < StreamCount; index++)
                sizes[index] = (int)header.ReadU32();

            int position = header.Position;
            var streams = new FontReader[StreamCount];
            for (int index = 0; index < StreamCount; index++)
            {
                if (sizes[index] < 0 || position + sizes[index] > offset + length)
                    throw new FontFormatException("WOFF2: glyf stream is out of range.");

                var bytes = new byte[sizes[index]];
                Buffer.BlockCopy(data, position, bytes, 0, sizes[index]);
                streams[index] = new FontReader(bytes);
                position += sizes[index];
            }

            byte[] overlapBitmap = null;
            if ((options & OverlapSimpleOption) != 0)
            {
                int bitmapLength = (glyphCount + 7) >> 3;
                if (position + bitmapLength > offset + length)
                    throw new FontFormatException("WOFF2: overlap bitmap is out of range.");

                overlapBitmap = new byte[bitmapLength];
                Buffer.BlockCopy(data, position, overlapBitmap, 0, bitmapLength);
            }

            var contourCounts = streams[0];
            var pointCounts = streams[1];
            var flags = streams[2];
            var triplets = streams[3];
            var composites = streams[4];
            var bounds = streams[5];
            var instructions = streams[6];

            int bboxBitmapLength = ((glyphCount + 31) >> 5) << 2;
            byte[] bboxBitmap = bounds.ReadBytes(bboxBitmapLength);

            var glyf = new ByteSink(Math.Max(1024, length * 2));
            var locaValues = new int[glyphCount + 1];
            xMins = new short[glyphCount];

            for (int glyph = 0; glyph < glyphCount; glyph++)
            {
                locaValues[glyph] = glyf.Length;
                bool hasBounds = (bboxBitmap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
                int contours = contourCounts.ReadU16();
                int start = glyf.Length;

                if (contours == 0xFFFF)
                {
                    if (!hasBounds)
                        throw new FontFormatException("WOFF2: composite glyph without a bounding box.");
                    WriteComposite(glyf, composites, glyphStream: triplets, instructions, bounds);
                }
                else if (contours > 0)
                {
                    bool overlap = overlapBitmap != null && (overlapBitmap[glyph >> 3] & (0x80 >> (glyph & 7))) != 0;
                    WriteSimple(glyf, contours, hasBounds, overlap, pointCounts, flags, triplets, instructions, bounds);
                }
                else if (hasBounds)
                {
                    throw new FontFormatException("WOFF2: empty glyph has a bounding box.");
                }

                if (contours > 0 && contours != 0xFFFF)
                    xMins[glyph] = (short)((glyf.Bytes[start + 2] << 8) | glyf.Bytes[start + 3]);

                glyf.Pad4();
            }

            locaValues[glyphCount] = glyf.Length;
            glyfTable = glyf.ToArray();
            locaTable = BuildLoca(locaValues, indexFormat);
        }

        public static byte[] ReconstructHmtx(
            byte[] data,
            int offset,
            int length,
            byte[] hhea,
            byte[] maxp,
            short[] xMins
        )
        {
            var reader = new FontReader(data, offset);
            int flags = reader.ReadU8();
            bool proportionalLsbs = (flags & 1) == 0;
            bool monospaceLsbs = (flags & 2) == 0;
            if ((flags & 0xFC) != 0 || (proportionalLsbs && monospaceLsbs))
                throw new FontFormatException("WOFF2: invalid hmtx transform flags.");

            int glyphCount = (maxp[4] << 8) | maxp[5];
            if (hhea.Length < 36)
                throw new FontFormatException("WOFF2: hhea table is truncated.");
            int metricCount = (hhea[34] << 8) | hhea[35];
            if (metricCount < 1 || metricCount > glyphCount || glyphCount != xMins.Length)
                throw new FontFormatException("WOFF2: hmtx metric count is out of range.");

            var advances = new int[metricCount];
            for (int index = 0; index < metricCount; index++)
                advances[index] = reader.ReadU16();

            var bearings = new short[glyphCount];
            for (int index = 0; index < metricCount; index++)
                bearings[index] = proportionalLsbs ? reader.ReadI16() : xMins[index];
            for (int index = metricCount; index < glyphCount; index++)
                bearings[index] = monospaceLsbs ? reader.ReadI16() : xMins[index];

            var output = new byte[2 * glyphCount + 2 * metricCount];
            int cursor = 0;
            for (int glyph = 0; glyph < glyphCount; glyph++)
            {
                if (glyph < metricCount)
                {
                    output[cursor++] = (byte)(advances[glyph] >> 8);
                    output[cursor++] = (byte)advances[glyph];
                }
                output[cursor++] = (byte)(bearings[glyph] >> 8);
                output[cursor++] = (byte)bearings[glyph];
            }
            return output;
        }

        private static void WriteComposite(
            ByteSink glyf,
            FontReader composites,
            FontReader glyphStream,
            FontReader instructions,
            FontReader bounds
        )
        {
            // Scan the component records to learn their size and whether instructions follow.
            int begin = composites.Position;
            bool haveInstructions = false;
            int flags = CompositeFlagMoreComponents;
            while ((flags & CompositeFlagMoreComponents) != 0)
            {
                flags = composites.ReadU16();
                haveInstructions |= (flags & CompositeFlagHaveInstructions) != 0;
                int argumentSize = 2 + ((flags & CompositeFlagArgsAreWords) != 0 ? 4 : 2);
                if ((flags & CompositeFlagHaveScale) != 0)
                    argumentSize += 2;
                else if ((flags & CompositeFlagHaveXYScale) != 0)
                    argumentSize += 4;
                else if ((flags & CompositeFlagHaveTwoByTwo) != 0)
                    argumentSize += 8;
                composites.Skip(argumentSize);
            }
            int size = composites.Position - begin;
            composites.Position = begin;

            int instructionLength = haveInstructions ? Woff2Streams.Read255UShort(glyphStream) : 0;

            glyf.WriteU16(0xFFFF);
            glyf.Write(bounds.ReadBytes(8));
            glyf.Write(composites.ReadBytes(size));
            if (haveInstructions)
            {
                glyf.WriteU16(instructionLength);
                glyf.Write(instructions.ReadBytes(instructionLength));
            }
        }

        private static void WriteSimple(
            ByteSink glyf,
            int contours,
            bool hasBounds,
            bool overlap,
            FontReader pointCounts,
            FontReader flagStream,
            FontReader triplets,
            FontReader instructions,
            FontReader bounds
        )
        {
            var endPoints = new int[contours];
            int total = 0;
            for (int contour = 0; contour < contours; contour++)
            {
                total += Woff2Streams.Read255UShort(pointCounts);
                endPoints[contour] = total - 1;
            }

            if (total >= (1 << 27))
                throw new FontFormatException("WOFF2: glyph has too many points.");

            var points = DecodeTriplets(flagStream.ReadBytes(total), triplets);
            int instructionLength = Woff2Streams.Read255UShort(triplets);

            glyf.WriteU16(contours);
            if (hasBounds)
                glyf.Write(bounds.ReadBytes(8));
            else
                WriteComputedBounds(glyf, points);

            foreach (int end in endPoints)
            {
                if (end > 0xFFFF)
                    throw new FontFormatException("WOFF2: glyph has too many points.");
                glyf.WriteU16(end);
            }

            glyf.WriteU16(instructionLength);
            glyf.Write(instructions.ReadBytes(instructionLength));

            // Plain encoding: one flag byte per point, 16-bit deltas.
            for (int index = 0; index < total; index++)
            {
                int flag = points[index].OnCurve ? 1 : 0;
                if (overlap && index == 0)
                    flag |= OverlapSimpleBit;
                glyf.WriteU8(flag);
            }

            int previous = 0;
            for (int index = 0; index < total; index++)
            {
                glyf.WriteU16(points[index].X - previous);
                previous = points[index].X;
            }

            previous = 0;
            for (int index = 0; index < total; index++)
            {
                glyf.WriteU16(points[index].Y - previous);
                previous = points[index].Y;
            }
        }

        private static void WriteComputedBounds(ByteSink glyf, GlyphPoint[] points)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            if (points.Length == 0)
                minX = minY = maxX = maxY = 0;

            glyf.WriteU16(minX);
            glyf.WriteU16(minY);
            glyf.WriteU16(maxX);
            glyf.WriteU16(maxY);
        }

        // Triplet coordinate decoding (WOFF2 section 5.2): the flag byte selects the byte
        // count and how the signed dx / dy are packed.
        private static GlyphPoint[] DecodeTriplets(byte[] flags, FontReader stream)
        {
            var points = new GlyphPoint[flags.Length];
            int x = 0;
            int y = 0;

            for (int index = 0; index < flags.Length; index++)
            {
                int flag = flags[index];
                bool onCurve = (flag >> 7) == 0;
                flag &= 0x7F;

                int dx;
                int dy;
                if (flag < 10)
                {
                    dx = 0;
                    dy = WithSign(flag, ((flag & 14) << 7) + stream.ReadU8());
                }
                else if (flag < 20)
                {
                    dx = WithSign(flag, (((flag - 10) & 14) << 7) + stream.ReadU8());
                    dy = 0;
                }
                else if (flag < 84)
                {
                    int b0 = flag - 20;
                    int b1 = stream.ReadU8();
                    dx = WithSign(flag, 1 + (b0 & 0x30) + (b1 >> 4));
                    dy = WithSign(flag >> 1, 1 + ((b0 & 0x0C) << 2) + (b1 & 0x0F));
                }
                else if (flag < 120)
                {
                    int b0 = flag - 84;
                    dx = WithSign(flag, 1 + ((b0 / 12) << 8) + stream.ReadU8());
                    dy = WithSign(flag >> 1, 1 + (((b0 % 12) >> 2) << 8) + stream.ReadU8());
                }
                else if (flag < 124)
                {
                    int b1 = stream.ReadU8();
                    int b2 = stream.ReadU8();
                    int b3 = stream.ReadU8();
                    dx = WithSign(flag, (b1 << 4) + (b2 >> 4));
                    dy = WithSign(flag >> 1, ((b2 & 0x0F) << 8) + b3);
                }
                else
                {
                    int dxHigh = stream.ReadU8();
                    int dxLow = stream.ReadU8();
                    int dyHigh = stream.ReadU8();
                    int dyLow = stream.ReadU8();
                    dx = WithSign(flag, (dxHigh << 8) + dxLow);
                    dy = WithSign(flag >> 1, (dyHigh << 8) + dyLow);
                }

                x += dx;
                y += dy;
                points[index] = new GlyphPoint { X = x, Y = y, OnCurve = onCurve };
            }

            return points;
        }

        private static int WithSign(int flag, int value) => (flag & 1) != 0 ? value : -value;

        private static byte[] BuildLoca(int[] values, int indexFormat)
        {
            var sink = new ByteSink(values.Length * 4);
            foreach (int value in values)
            {
                if (indexFormat == 0)
                    sink.WriteU16(value >> 1);
                else
                    sink.WriteU32(value);
            }
            return sink.ToArray();
        }

        // Growable big-endian byte buffer.
        private sealed class ByteSink
        {
            public byte[] Bytes;
            public int Length;

            public ByteSink(int capacity) => Bytes = new byte[Math.Max(capacity, 16)];

            public void WriteU8(int value)
            {
                Ensure(1);
                Bytes[Length++] = (byte)value;
            }

            public void WriteU16(int value)
            {
                Ensure(2);
                Bytes[Length++] = (byte)(value >> 8);
                Bytes[Length++] = (byte)value;
            }

            public void WriteU32(int value)
            {
                WriteU16(value >> 16);
                WriteU16(value);
            }

            public void Write(byte[] data)
            {
                Ensure(data.Length);
                Buffer.BlockCopy(data, 0, Bytes, Length, data.Length);
                Length += data.Length;
            }

            public void Pad4()
            {
                while ((Length & 3) != 0)
                    WriteU8(0);
            }

            public byte[] ToArray()
            {
                var result = new byte[Length];
                Buffer.BlockCopy(Bytes, 0, result, 0, Length);
                return result;
            }

            private void Ensure(int extra)
            {
                if (Length + extra > Bytes.Length)
                    Array.Resize(ref Bytes, Math.Max(Length + extra, Bytes.Length * 2));
            }
        }
    }
}
