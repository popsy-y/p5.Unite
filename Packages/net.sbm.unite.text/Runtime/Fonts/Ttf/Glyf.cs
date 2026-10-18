using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// glyf table parsing: simple glyphs (with quadratic curve flattening) and
    /// composite glyphs (translation + scale / 2x2 matrix). Point-matching
    /// components are not supported and produce blank outlines; hinting
    /// instructions are ignored (v1 limitations, see the package README).
    /// </summary>
    internal static class GlyfTables
    {
        private const ushort ArgsAreXyValues = 0x0002;
        private const ushort MoreComponents = 0x0020;
        private const ushort WeHaveAScale = 0x0008;
        private const ushort WeHaveXAndYScale = 0x0040;
        private const ushort WeHaveTwoByTwo = 0x0080;

        private const int QuadraticSubdivisions = 8;
        private const int MaxCompositeDepth = 8;

        /// <summary>Parses one glyf record; returns null for blank/unresolvable glyphs.</summary>
        public static GlyphOutline ParseOutline(TtfReader reader, int recordLength, TtfFont font)
        {
            int numContours = reader.ReadI16();

            if (numContours < 0)
            {
                reader.Skip(8); // composite bbox header is not used
                var components = ParseComposite(reader, font);
                if (components == null || components.Contours.Count == 0)
                    return null;
                return components.ToOutline();
            }

            if (numContours == 0)
                return null; // blank glyph

            reader.Skip(8); // bbox header: bounds are recomputed from the points

            return ParseSimple(reader, numContours).ToOutline();
        }

        private sealed class Result
        {
            public readonly List<Vector2[]> Contours = new();

            public void AddContour(Vector2[] points)
            {
                if (points.Length >= 2)
                {
                    Contours.Add(points);
                }
            }

            public GlyphOutline ToOutline()
            {
                var contourArray = Contours.ToArray();
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);

                foreach (var contour in contourArray)
                {
                    foreach (var point in contour)
                    {
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }

                return new GlyphOutline(contourArray, min, max);
            }
        }

        private struct OnCurve
        {
            public Vector2 Position;
            public bool On;
        }

        private static Result ParseSimple(TtfReader reader, int numContours)
        {
            var result = new Result();
            var endPoints = new int[numContours];

            for (int index = 0; index < numContours; index++)
                endPoints[index] = reader.ReadU16();

            int instructionLength = reader.ReadU16();
            reader.Skip(instructionLength);

            int numPoints = endPoints[numContours - 1] + 1;
            var points = DecodeCoordinates(reader, numPoints);

            for (int contour = 0; contour < numContours; contour++)
            {
                int start = contour == 0 ? 0 : endPoints[contour - 1] + 1;
                result.AddContour(FlattenContour(points, start, endPoints[contour] + 1));
            }

            return result;
        }

        // Flags: bit0 on-curve, bit1 x-short, bit2 y-short, bit3 repeat,
        // bit4 x-same-or-positive, bit5 y-same-or-positive.
        private static OnCurve[] DecodeCoordinates(TtfReader reader, int numPoints)
        {
            var flags = new byte[numPoints];

            for (int index = 0; index < numPoints;)
            {
                byte flag = reader.ReadU8();
                flags[index++] = flag;

                if ((flag & 0x08) != 0) // repeat bit: one count byte follows
                {
                    byte repeats = reader.ReadU8();
                    for (int repeat = 0; repeat < repeats && index < numPoints; repeat++)
                        flags[index++] = flag;
                }
            }

            var points = new OnCurve[numPoints];

            // The deltas accumulate from the glyph origin (0, 0): the first stored
            // delta is the first point's absolute coordinate, not an offset from the
            // bbox minimum.
            int cursorX = 0;
            for (int index = 0; index < numPoints; index++)
            {
                byte flag = flags[index];
                points[index].On = (flag & 0x01) != 0;

                if ((flag & 0x02) != 0) // x short vector
                {
                    int delta = reader.ReadU8();
                    cursorX += (flag & 0x10) != 0 ? delta : -delta;
                }
                else if ((flag & 0x10) == 0) // not same: full int16 delta
                {
                    cursorX += reader.ReadI16();
                }

                points[index].Position = new Vector2(cursorX, 0f);
            }

            int cursorY = 0;
            for (int index = 0; index < numPoints; index++)
            {
                byte flag = flags[index];

                if ((flag & 0x04) != 0) // y short vector
                {
                    int delta = reader.ReadU8();
                    cursorY += (flag & 0x20) != 0 ? delta : -delta;
                }
                else if ((flag & 0x20) == 0)
                {
                    cursorY += reader.ReadI16();
                }

                var position = points[index].Position;
                points[index].Position = new Vector2(position.x, cursorY);
            }

            return points;
        }

        private static Vector2[] FlattenContour(OnCurve[] points, int from, int to)
        {
            int count = to - from;
            if (count < 2)
                return new Vector2[0];

            var output = new List<Vector2>(count * QuadraticSubdivisions);
            int pivot = -1;

            for (int index = 0; index < count; index++)
            {
                if (points[from + index].On)
                {
                    pivot = index;
                    break;
                }
            }

            if (pivot < 0)
            {
                // No on-curve point anywhere (common for round glyphs such as 'o'):
                // every point is a control and the curve starts on the implied on-curve
                // midpoint between the last and the first control.
                var controlPoints = new List<Vector2>(count);
                for (int index = 0; index < count; index++)
                    controlPoints.Add(points[from + index].Position);

                var start = Midpoint(points[from + count - 1].Position, points[from].Position);
                output.Add(start);
                EmitQuadraticChain(output, start, controlPoints, start, emitFinalVertex: false);
                return output.ToArray();
            }

            var rotated = new OnCurve[count];
            for (int index = 0; index < count; index++)
            {
                rotated[index] = points[from + (pivot + index) % count];
            }

            output.Add(rotated[0].Position);

            for (int index = 0; index < count; index++)
            {
                var anchor = rotated[index].Position;

                int scan = index + 1;
                while (scan < count && !rotated[scan].On)
                    scan++;

                bool closes = scan == count;
                var end = closes ? rotated[0].Position : rotated[scan].Position;

                var controls = new List<Vector2>(4);
                for (int middle = index + 1; middle < (closes ? count : scan); middle++)
                    controls.Add(rotated[middle].Position);

                EmitQuadraticChain(output, anchor, controls, end, emitFinalVertex: !closes);

                if (closes)
                    break;
                index = scan - 1;
            }

            return output.ToArray();
        }

        // start → (controls..., end): consecutive off-curve points imply on-curve
        // points at the midpoint of each pair, so the chain is walked quad by quad.
        private static void EmitQuadraticChain(
            List<Vector2> output,
            Vector2 start,
            List<Vector2> controls,
            Vector2 end,
            bool emitFinalVertex
        )
        {
            if (controls.Count == 0)
            {
                if (emitFinalVertex)
                {
                    output.Add(end);
                }
                return;
            }

            var cursor = start;
            for (int index = 0; index < controls.Count; index++)
            {
                var control = controls[index];
                var next = index == controls.Count - 1
                    ? end
                    : Midpoint(control, controls[index + 1]);

                // Only the chain's very last vertex may already be present; the implied
                // on-curve midpoints inside the chain are always emitted.
                bool lastQuad = index == controls.Count - 1;
                EmitQuadratic(output, cursor, control, next, emitFinalVertex || !lastQuad);
                cursor = next;
            }
        }

        private static void EmitQuadratic(
            List<Vector2> output,
            Vector2 start,
            Vector2 control,
            Vector2 end,
            bool emitFinalVertex
        )
        {
            for (int step = 1; step <= QuadraticSubdivisions; step++)
            {
                // Fully flattened chains end on an already-present vertex, so the
                // final subdivision's endpoint is skipped there.
                if (!emitFinalVertex && step == QuadraticSubdivisions)
                    break;

                float t = step / (float)QuadraticSubdivisions;
                float u = 1f - t;
                output.Add(new Vector2(
                    u * u * start.x + 2f * u * t * control.x + t * t * end.x,
                    u * u * start.y + 2f * u * t * control.y + t * t * end.y
                ));
            }
        }

        private static Result ParseComposite(TtfReader reader, TtfFont font)
        {
            var result = new Result();
            ushort flags;

            do
            {
                flags = reader.ReadU16();
                int component = reader.ReadU16();

                int dx;
                int dy;

                if ((flags & 0x0001) != 0) // args are words
                {
                    dx = reader.ReadI16();
                    dy = reader.ReadI16();
                }
                else
                {
                    dx = (sbyte)reader.ReadU8();
                    dy = (sbyte)reader.ReadU8();
                }

                if ((flags & ArgsAreXyValues) == 0)
                    return null; // point matching is not supported

                float a = 1f, b = 0f, c = 0f, d = 1f;

                if ((flags & WeHaveAScale) != 0)
                {
                    a = d = ReadF2Dot14(reader);
                }
                else if ((flags & WeHaveXAndYScale) != 0)
                {
                    a = ReadF2Dot14(reader);
                    d = ReadF2Dot14(reader);
                }
                else if ((flags & WeHaveTwoByTwo) != 0)
                {
                    a = ReadF2Dot14(reader);
                    b = ReadF2Dot14(reader);
                    c = ReadF2Dot14(reader);
                    d = ReadF2Dot14(reader);
                }

                if (font != null && font.TryGetOutline(component, out var componentOutline))
                {
                    foreach (var contour in componentOutline.Contours)
                    {
                        var transformed = new List<Vector2>(contour.Length);
                        foreach (var point in contour)
                        {
                            transformed.Add(new Vector2(
                                a * point.x + c * point.y + dx,
                                b * point.x + d * point.y + dy
                            ));
                        }
                        result.AddContour(transformed.ToArray());
                    }
                }
            } while ((flags & MoreComponents) != 0);

            return result;
        }

        private static float ReadF2Dot14(TtfReader reader)
        {
            return reader.ReadI16() / 16384f;
        }

        private static Vector2 Midpoint(Vector2 from, Vector2 to) => new(
            (from.x + to.x) * 0.5f,
            (from.y + to.y) * 0.5f
        );
    }
}
