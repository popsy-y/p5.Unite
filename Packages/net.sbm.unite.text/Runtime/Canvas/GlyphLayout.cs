using System.Collections.Generic;
using Unite.Canvas;
using Unite.Drawing;
using Unite.Text.Fonts;
using Unite.Text.Raster;
using UnityEngine;
using CachedGlyph = Unite.Text.GlyphAtlasSet.CachedGlyph;

namespace Unite.Text
{
    /// <summary>
    /// Turns text into glyph quads: maps code points to glyph ids (no shaping in v1),
    /// looks up or bakes the SDF in the font's atlas, and enqueues one draw command
    /// per glyph through the canvas command queue.
    /// </summary>
    internal static class GlyphLayout
    {
        // Raster sizes are quantized to powers of two so text seen at different sizes
        // and camera configs reuses bake results (p5.Unite-Text plan: pow2 em buckets).
        private const int MinBucketPx = 16;
        private const int MaxBucketPx = 256;

        // World/CanvasLocal modes have no cheap pre-GPU pixel density, so text rasterizes
        // at this many pixels per world unit (bucketed). Exposed as a knob for the future
        // per-camera density pass.
        private const float PixelsPerWorldUnit = 64f;

        public static void Draw(UCanvas canvas, TextState state, string text, float x, float y)
        {
            var font = RequireFont(canvas, state);
            if (font == null || text == null)
                return;

            ResolveRaster(canvas, state, font, out int em, out float glyphScale, out float unitsPerPx);
            var atlas = AtlasRegistry.GetOrCreate(state.Font);

        // Text assumes drawing-space +y is up, matching the pipeline's clip mapping;
        // successive lines drop toward -y like p5's leading behavior.
        float lineAdvance = (font.Ascender - font.Descender) * glyphScale * unitsPerPx;
            float baseline = y;

            foreach (var line in SplitLines(text))
            {
                if (line.Length > 0)
                {
                    float startX = x - AlignmentOffset(state, line, font, glyphScale * unitsPerPx);
                    DrawLine(canvas, atlas, font, line, startX, baseline, em, glyphScale, unitsPerPx);
                }
                baseline -= lineAdvance;
            }
        }

        public static float Measure(UCanvas canvas, TextState state, string text)
        {
            var font = RequireFont(canvas, state);
            if (font == null || text == null)
                return 0f;

            ResolveRaster(canvas, state, font, out _, out float glyphScale, out float unitsPerPx);
            float scaleUnits = glyphScale * unitsPerPx;

            float widest = 0f;
            foreach (var line in SplitLines(text))
                widest = Mathf.Max(widest, MeasureLine(font, line, scaleUnits));
            return widest;
        }

        private static TtfFont RequireFont(UCanvas canvas, TextState state)
        {
            if (state.Font != null && state.Font.TryGetFont(out var font))
                return font;

            if (!state.WarnedMissingFont)
            {
                state.WarnedMissingFont = true;
                Debug.LogWarning(
                    "p5.Unite: set a font with canvas.TextFont(...) before drawing text.",
                    canvas
                );
            }
            return null;
        }

        private static void ResolveRaster(
            UCanvas canvas,
            TextState state,
            TtfFont font,
            out int emBucket,
            out float glyphScale,
            out float unitsPerPx
        )
        {
            // Pixel size of the requested text size in the mode's raster space.
            float pixelSize;
            switch (canvas.CurrentDrawingMode)
            {
                case DrawingMode.NormalizedScreen:
                    pixelSize = state.Size * Mathf.Max(1f, canvas.Height);
                    break;
                case DrawingMode.NormalizedScreenOverlay:
                    pixelSize = state.Size * Mathf.Max(1f, canvas.Height);
                    break;
                case DrawingMode.World:
                case DrawingMode.CanvasLocal:
                    pixelSize = state.Size * PixelsPerWorldUnit;
                    break;
                default: // Screen / ScreenOverlay: drawing units are already pixels.
                    pixelSize = state.Size;
                    break;
            }

            emBucket = QuantizeBucket(pixelSize);
            glyphScale = emBucket / (float)font.UnitsPerEm;
            unitsPerPx = state.Size / emBucket;
        }

        private static int QuantizeBucket(float pixelSize)
        {
            pixelSize = Mathf.Clamp(pixelSize, MinBucketPx, MaxBucketPx);
            return Mathf.NextPowerOfTwo(Mathf.Max(MinBucketPx, Mathf.CeilToInt(pixelSize)));
        }

        private static float AlignmentOffset(
            TextState state,
            string line,
            TtfFont font,
            float scaleUnits
        )
        {
            float width = MeasureLine(font, line, scaleUnits);
            return state.Align switch
            {
                TextAlignment.Center => width * 0.5f,
                TextAlignment.Right => width,
                _ => 0f
            };
        }

        private static float MeasureLine(TtfFont font, string line, float scaleUnits)
        {
            float width = 0f;

            foreach (int codePoint in EnumerateCodePoints(line))
            {
                int glyphId = font.GetGlyphId(codePoint);
                width += font.Advance(glyphId) * scaleUnits;
            }
            return width;
        }

        private static void DrawLine(
            UCanvas canvas,
            GlyphAtlasSet atlas,
            TtfFont font,
            string line,
            float startX,
            float baseline,
            int emBucket,
            float glyphScale,
            float unitsPerPx
        )
        {
            var style = canvas.StyleState;
            bool fillEnabled = style.FillEnabled;
            bool strokeEnabled = style.StrokeEnabled && Mathf.Max(0f, style.StrokeWeight) > 0f;
            if (!fillEnabled && !strokeEnabled)
                return;

            float scaleUnits = glyphScale * unitsPerPx;
            int frame = GetCurrentFrame();
            float penX = startX;

            foreach (int codePoint in EnumerateCodePoints(line))
            {
                int glyphId = font.GetGlyphId(codePoint);
                float scaledAdvance = font.Advance(glyphId) * scaleUnits;

                if (!font.TryGetOutline(glyphId, out var outline))
                {
                    penX += scaledAdvance;
                    continue;
                }

                var key = new GlyphKey(atlas.FontId, glyphId, emBucket);
                if (!atlas.TryGetCached(key, frame, out var glyph))
                    glyph = BakeGlyph(atlas, key, frame, outline, emBucket, glyphScale);

                // Sprite rect: X/Y in page texels, pen-relative (y up, y=0 at the
                // baseline). Drawing space is y-up like font space, so no flip.
                var center = new Vector3(
                    penX + (glyph.X + glyph.W * 0.5f) * unitsPerPx,
                    baseline + (glyph.Y + glyph.H * 0.5f) * unitsPerPx,
                    0f
                );

                canvas.EnqueueRawCommand(new DrawCommand
                {
                    Transform = canvas.CurrentTransform * Matrix4x4.Translate(center),
                    Size = new Vector2(glyph.W * unitsPerPx, glyph.H * unitsPerPx),
                    ShapeType = Shape.Text,
                    Point2 = glyph.UvMin,
                    Point3 = glyph.UvMax,
                    Parameters0 = new Vector4(glyph.Range, glyph.W, 0f, 0f),
                    GlyphAtlas = glyph.Texture,
                    FillColor = style.FillColor,
                    StrokeColor = style.StrokeColor,
                    StrokeWeight = Mathf.Max(0f, style.StrokeWeight),
                    FillEnabled = fillEnabled,
                    StrokeEnabled = strokeEnabled
                });

                penX += scaledAdvance;
            }
        }

        private static CachedGlyph BakeGlyph(
            GlyphAtlasSet atlas,
            GlyphKey key,
            int frame,
            GlyphOutline outline,
            int emBucket,
            float glyphScale
        )
        {
            // Spread proportional to the bucket, clamped so small glyphs stay crisp and
            // huge glyphs keep the sprite padding bounded.
            float range = Mathf.Clamp(Mathf.RoundToInt(emBucket * 0.06f), 2, 12);
            int pad = Mathf.CeilToInt(range) + 2;

            float minX = outline.BoundsMin.x * glyphScale;
            float maxX = outline.BoundsMax.x * glyphScale;
            float minY = outline.BoundsMin.y * glyphScale;
            float maxY = outline.BoundsMax.y * glyphScale;

            int width = Mathf.CeilToInt(maxX - minX) + pad * 2;
            int height = Mathf.CeilToInt(maxY - minY) + pad * 2;
            int issueX = Mathf.FloorToInt(minX) - pad;
            int issueY = Mathf.FloorToInt(minY) - pad;

            var coverage = new byte[width * height];
            var local = new Vector2[outline.Contours.Length][];
            for (int contour = 0; contour < outline.Contours.Length; contour++)
            {
                var source = outline.Contours[contour];
                var target = new Vector2[source.Length];
                for (int point = 0; point < source.Length; point++)
                {
                    target[point] = new Vector2(
                        source[point].x * glyphScale - issueX,
                        source[point].y * glyphScale - issueY
                    );
                }
                local[contour] = target;
            }

            GlyphRasterizer.Rasterize(local, width, height, coverage);

            var field = new byte[width * height];
            SdfEncoder.Encode(coverage, width, height, range, field);

            return atlas.Reserve(key, frame, width, height, range, field);
        }

        private static int GetCurrentFrame()
        {
            // Edit-mode tests never advance the frame counter; that is fine because the
            // frame value only feeds the age bookkeeping.
            return Time.frameCount;
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            var lines = new List<string>(4);
            int start = 0;

            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] != '\n')
                    continue;

                int end = index > start && text[index - 1] == '\r' ? index - 1 : index;
                lines.Add(text.Substring(start, end - start));
                start = index + 1;
            }

            lines.Add(text.Substring(start, text.Length - start));
            return lines;
        }

        private static IEnumerable<int> EnumerateCodePoints(string text)
        {
            int index = 0;
            while (index < text.Length)
            {
                int codePoint = text[index];
                index++;

                if (char.IsSurrogate(text[index - 1]) &&
                    index < text.Length &&
                    char.IsSurrogatePair(text[index - 1], text[index]))
                {
                    codePoint = char.ConvertToUtf32(text, index - 1);
                    index++;
                }

                yield return codePoint;
            }
        }
    }
}
