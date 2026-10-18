using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    // glyf record parsing: simple glyphs (contour points) and composite glyphs
    // (component references). Hinting instructions are skipped.
    internal sealed partial class TrueTypeOutlines
    {
        private const int ArgsAreWords = 0x0001;
        private const int ArgsAreXyValues = 0x0002;
        private const int MoreComponents = 0x0020;
        private const int HaveScale = 0x0008;
        private const int HaveXYScale = 0x0040;
        private const int HaveTwoByTwo = 0x0080;

        private struct Component
        {
            public int GlyphId;
            public float Dx;
            public float Dy;
            public float A;
            public float B;
            public float C;
            public float D;
        }

        private sealed class RawGlyph
        {
            public int XMin;
            public bool IsComposite;

            // Simple glyphs.
            public int[] EndPoints;
            public float[] X;
            public float[] Y;
            public bool[] OnCurve;

            // Composite glyphs.
            public List<Component> Components;
        }

        /// <summary>Parses one glyf record; null for blank or unsupported glyphs.</summary>
        private static RawGlyph ParseRecord(FontReader reader)
        {
            int contourCount = reader.ReadI16();
            int xMin = reader.ReadI16();
            reader.Skip(6); // yMin, xMax, yMax

            if (contourCount == 0)
                return null;

            if (contourCount < 0)
            {
                var components = ParseComponents(reader);
                return components == null
                    ? null
                    : new RawGlyph { XMin = xMin, IsComposite = true, Components = components };
            }

            var endPoints = new int[contourCount];
            for (int index = 0; index < contourCount; index++)
                endPoints[index] = reader.ReadU16();

            reader.Skip(reader.ReadU16()); // instructions

            int pointCount = endPoints[contourCount - 1] + 1;
            var glyph = new RawGlyph
            {
                XMin = xMin,
                EndPoints = endPoints,
                X = new float[pointCount],
                Y = new float[pointCount],
                OnCurve = new bool[pointCount]
            };
            DecodeCoordinates(reader, glyph);
            return glyph;
        }

        // Flags: bit0 on-curve, bit1 x-short, bit2 y-short, bit3 repeat,
        // bit4 x-same-or-positive, bit5 y-same-or-positive.
        private static void DecodeCoordinates(FontReader reader, RawGlyph glyph)
        {
            int count = glyph.X.Length;
            var flags = new byte[count];

            for (int index = 0; index < count;)
            {
                byte flag = reader.ReadU8();
                flags[index++] = flag;

                if ((flag & 0x08) == 0)
                    continue;

                byte repeats = reader.ReadU8();
                for (int repeat = 0; repeat < repeats && index < count; repeat++)
                    flags[index++] = flag;
            }

            // Coordinates are deltas accumulated from the glyph origin (0, 0).
            int cursor = 0;
            for (int index = 0; index < count; index++)
            {
                byte flag = flags[index];
                glyph.OnCurve[index] = (flag & 0x01) != 0;

                if ((flag & 0x02) != 0)
                {
                    int delta = reader.ReadU8();
                    cursor += (flag & 0x10) != 0 ? delta : -delta;
                }
                else if ((flag & 0x10) == 0)
                {
                    cursor += reader.ReadI16();
                }

                glyph.X[index] = cursor;
            }

            cursor = 0;
            for (int index = 0; index < count; index++)
            {
                byte flag = flags[index];

                if ((flag & 0x04) != 0)
                {
                    int delta = reader.ReadU8();
                    cursor += (flag & 0x20) != 0 ? delta : -delta;
                }
                else if ((flag & 0x20) == 0)
                {
                    cursor += reader.ReadI16();
                }

                glyph.Y[index] = cursor;
            }
        }

        // Component list; null when a component uses point matching (unsupported).
        private static List<Component> ParseComponents(FontReader reader)
        {
            var components = new List<Component>();
            int flags;

            do
            {
                flags = reader.ReadU16();
                var component = new Component { GlyphId = reader.ReadU16(), A = 1f, D = 1f };

                if ((flags & ArgsAreXyValues) == 0)
                    return null;

                if ((flags & ArgsAreWords) != 0)
                {
                    component.Dx = reader.ReadI16();
                    component.Dy = reader.ReadI16();
                }
                else
                {
                    component.Dx = reader.ReadI8();
                    component.Dy = reader.ReadI8();
                }

                if ((flags & HaveScale) != 0)
                {
                    component.A = component.D = reader.ReadF2Dot14();
                }
                else if ((flags & HaveXYScale) != 0)
                {
                    component.A = reader.ReadF2Dot14();
                    component.D = reader.ReadF2Dot14();
                }
                else if ((flags & HaveTwoByTwo) != 0)
                {
                    component.A = reader.ReadF2Dot14();
                    component.B = reader.ReadF2Dot14();
                    component.C = reader.ReadF2Dot14();
                    component.D = reader.ReadF2Dot14();
                }

                components.Add(component);
            } while ((flags & MoreComponents) != 0);

            return components;
        }
    }
}
