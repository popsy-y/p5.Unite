using System.Collections.Generic;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text.Fonts
{
    // p5.Font outline functions. Coordinates are laid out exactly like Text() with the
    // given options (alignment, leading, wrapping), in output units.
    public sealed partial class FontFace
    {
        /// <summary>
        /// Points sampled evenly along the outlines of the text (p5 font.textToPoints()).
        /// Density follows <see cref="TextPathOptions.SampleFactor"/>.
        /// </summary>
        public List<TextPoint> TextToPoints(string text, float x, float y, TextPathOptions options = null)
        {
            var points = new List<TextPoint>();
            foreach (var contour in TextToContours(text, x, y, options))
                points.AddRange(contour);
            return points;
        }

        /// <summary>
        /// Sampled points grouped per closed outline contour (p5 font.textToContours()):
        /// an "o" yields two contours, the outer edge and the hole.
        /// </summary>
        public List<List<TextPoint>> TextToContours(string text, float x, float y, TextPathOptions options = null)
        {
            options ??= new TextPathOptions();
            var contours = new List<List<TextPoint>>();
            foreach (var polygon in TextOutlines.Polygons(this, text, x, y, null, options))
            {
                var sampled = TextOutlines.Sample(polygon, options.SampleFactor);
                if (options.SimplifyThreshold != 0f)
                    sampled = TextOutlines.Simplify(sampled, Mathf.Abs(options.SimplifyThreshold));
                if (sampled.Count > 0)
                    contours.Add(sampled);
            }
            return contours;
        }

        /// <summary>
        /// Outline path commands of the text (p5 font.textToPaths()): MoveTo / LineTo /
        /// QuadraticTo / Close, with the font's quadratic curves kept unflattened.
        /// </summary>
        public List<PathCommand> TextToPaths(string text, float x, float y, TextPathOptions options = null)
            => TextOutlines.Paths(this, text, x, y, null, options ?? new TextPathOptions());

        /// <summary>
        /// Outline path commands of text wrapped into a box of the given width and height
        /// (p5 font.textToPaths(str, x, y, width, height)).
        /// </summary>
        public List<PathCommand> TextToPaths(string text, float x, float y, float width, float height, TextPathOptions options = null)
            => TextOutlines.Paths(this, text, x, y, TextOutlines.Box(x, y, width, height), options ?? new TextPathOptions());

        /// <summary>
        /// A mesh of the text (p5 font.textToModel()): filled glyph faces, extruded along
        /// +z by <see cref="TextPathOptions.Extrude"/> with side walls when non-zero. Text
        /// wraps inside the width/height box (pass 0 for no box). The front faces -z.
        /// </summary>
        public Mesh TextToModel(string text, float x, float y, float width, float height, TextPathOptions options = null)
        {
            options ??= new TextPathOptions();
            var glyphs = TextOutlines.GlyphPolygons(this, text, x, y, TextOutlines.Box(x, y, width, height), options);
            return TextMeshBuilder.Build(glyphs, Mathf.Max(0f, options.Extrude));
        }
    }

    /// <summary>Outline geometry of laid-out text, shared by the p5.Font functions.</summary>
    internal static class TextOutlines
    {
        public static TextBox? Box(float x, float y, float width, float height)
            => width > 0f ? new TextBox(x, y, width, height > 0f ? height : float.PositiveInfinity) : null;

        /// <summary>Every flattened contour of the text, in output units.</summary>
        public static List<Vector2[]> Polygons(FontFace face, string text, float x, float y, TextBox? box, TextPathOptions options)
        {
            var polygons = new List<Vector2[]>();
            foreach (var glyph in GlyphPolygons(face, text, x, y, box, options))
                polygons.AddRange(glyph);
            return polygons;
        }

        /// <summary>Flattened contours grouped per glyph (holes stay with their glyph).</summary>
        public static List<List<Vector2[]>> GlyphPolygons(FontFace face, string text, float x, float y, TextBox? box, TextPathOptions options)
        {
            var result = new List<List<Vector2[]>>();
            if (!TryLayout(face, text, x, y, box, options, out var font, out var settings, out var layout))
                return result;

            float scale = settings.Size / font.UnitsPerEm;
            foreach (var placed in layout.Glyphs)
            {
                if (!font.TryGetOutline(placed.GlyphId, out var outline))
                    continue;

                var glyph = new List<Vector2[]>(outline.Contours.Length);
                foreach (var contour in outline.Contours)
                {
                    var points = new Vector2[contour.Length];
                    for (int index = 0; index < contour.Length; index++)
                        points[index] = Place(contour[index], placed, scale, settings.YSign);
                    glyph.Add(points);
                }
                result.Add(glyph);
            }
            return result;
        }

        public static List<PathCommand> Paths(FontFace face, string text, float x, float y, TextBox? box, TextPathOptions options)
        {
            var commands = new List<PathCommand>();
            if (!TryLayout(face, text, x, y, box, options, out var font, out var settings, out var layout))
                return commands;

            float scale = settings.Size / font.UnitsPerEm;
            foreach (var placed in layout.Glyphs)
            {
                if (!font.TryGetOutline(placed.GlyphId, out var outline))
                    continue;

                foreach (var command in outline.Commands)
                {
                    commands.Add(new PathCommand(
                        command.Type,
                        Place(command.Control, placed, scale, settings.YSign),
                        Place(command.Point, placed, scale, settings.YSign)
                    ));
                }
            }
            return commands;
        }

        /// <summary>
        /// Resamples a closed polygon at even arc-length steps: round(perimeter x
        /// sampleFactor) points (at least one), each with its edge direction in degrees.
        /// </summary>
        public static List<TextPoint> Sample(Vector2[] polygon, float sampleFactor)
        {
            var samples = new List<TextPoint>();
            int count = polygon.Length;
            if (count < 2)
                return samples;

            float perimeter = 0f;
            for (int index = 0; index < count; index++)
                perimeter += Vector2.Distance(polygon[index], polygon[(index + 1) % count]);
            if (perimeter <= 0f)
                return samples;

            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(perimeter * Mathf.Max(0f, sampleFactor)));
            float step = perimeter / sampleCount;
            float travelled = 0f;
            int edge = 0;
            float edgeStart = 0f;

            for (int sample = 0; sample < sampleCount; sample++)
            {
                float target = sample * step;
                var from = polygon[edge];
                var to = polygon[(edge + 1) % count];
                float length = Vector2.Distance(from, to);

                while (edgeStart + length < target && edge < count - 1)
                {
                    edgeStart += length;
                    edge++;
                    from = polygon[edge];
                    to = polygon[(edge + 1) % count];
                    length = Vector2.Distance(from, to);
                }

                travelled = length > 0f ? (target - edgeStart) / length : 0f;
                var direction = to - from;
                samples.Add(new TextPoint(
                    Vector2.Lerp(from, to, Mathf.Clamp01(travelled)),
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                ));
            }
            return samples;
        }

        /// <summary>Drops points whose incoming and outgoing edges turn by less than the threshold (radians).</summary>
        public static List<TextPoint> Simplify(List<TextPoint> points, float threshold)
        {
            if (points.Count < 4)
                return points;

            var kept = new List<TextPoint>(points.Count) { points[0] };
            for (int index = 1; index < points.Count; index++)
            {
                var previous = kept[kept.Count - 1].Position;
                var current = points[index].Position;
                var next = points[(index + 1) % points.Count].Position;
                float turn = Vector2.Angle(current - previous, next - current) * Mathf.Deg2Rad;
                if (turn >= threshold)
                    kept.Add(points[index]);
            }
            return kept;
        }

        // Glyph units (y-up) → output units around the glyph's pen origin.
        private static Vector2 Place(Vector2 point, PlacedGlyph glyph, float scale, float ySign)
            => new(glyph.PenX + point.x * scale, glyph.Baseline - ySign * point.y * scale);

        private static bool TryLayout(
            FontFace face,
            string text,
            float x,
            float y,
            TextBox? box,
            TextPathOptions options,
            out FontInstance font,
            out TextLayoutSettings settings,
            out TextLayoutResult layout
        )
        {
            settings = options.ToSettings();
            layout = null;
            font = face.GetInstance(options.Variations);
            if (string.IsNullOrEmpty(text))
                return false;

            layout = TextLayout.Layout(font, settings, text, x, y, box);
            return true;
        }
    }
}
