using System.Runtime.CompilerServices;
using Unite.Canvas;
using Unite.Drawing;
using Unite.Text.Fonts;
using Unite.Text.Raster;
using UnityEngine;
using CachedGlyph = Unite.Text.GlyphAtlasSet.CachedGlyph;

namespace Unite.Text
{
    /// <summary>
    /// Turns laid-out text into glyph quads: looks up or bakes each glyph's SDF in the
    /// font's atlas and enqueues one draw command per glyph through the canvas command
    /// queue, applying the synthetic italic slant and weight offset.
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

        private static readonly ConditionalWeakTable<UCanvas, object> WarnedCanvases = new();

        public static void Draw(UCanvas canvas, TextState state, string text, float x, float y, TextBox? box = null)
        {
            var font = RequireFont(canvas, state);
            if (font == null || text == null)
                return;

            var style = canvas.StyleState;
            bool fillEnabled = style.FillEnabled;
            bool strokeEnabled = style.StrokeEnabled && Mathf.Max(0f, style.StrokeWeight) > 0f;
            if (!fillEnabled && !strokeEnabled)
                return;

            var settings = SettingsFor(canvas, state);
            var layout = TextLayout.Layout(font, settings, text, x, y, box);

            ResolveRaster(canvas, state, font, out int em, out float glyphScale, out float unitsPerPx);
            var atlas = AtlasRegistry.GetOrCreate(state.Font);
            int frame = GetCurrentFrame();

            // Synthetic italic: x shifts right with the height above the baseline. In
            // y-down drawing space "up" is -y, in y-up space it is +y.
            var slant = Matrix4x4.identity;
            slant.m01 = -settings.YSign * settings.Slant;

            foreach (var placed in layout.Glyphs)
            {
                if (!font.TryGetOutline(placed.GlyphId, out var outline))
                    continue;

                var key = new GlyphKey(atlas.FontId, placed.GlyphId, em, font.Id);
                if (!atlas.TryGetCached(key, frame, out var glyph))
                    glyph = BakeGlyph(atlas, key, frame, outline, em, glyphScale);

                // Sprite rect: pen-relative origin (OffsetX/Y, font orientation y-up) +
                // size in baked pixels, mapped into drawing space around the pen.
                var local = new Vector3(
                    (glyph.OffsetX + glyph.W * 0.5f) * unitsPerPx,
                    -settings.YSign * (glyph.OffsetY + glyph.H * 0.5f) * unitsPerPx,
                    0f
                );

                // The quad's -y half must sample the sprite's top in y-down space and its
                // bottom in y-up space.
                bool yDown = settings.YSign > 0f;
                canvas.EnqueueRawCommand(new DrawCommand
                {
                    Transform = canvas.CurrentTransform
                        * Matrix4x4.Translate(new Vector3(placed.PenX, placed.Baseline, 0f))
                        * slant
                        * Matrix4x4.Translate(local),
                    Size = new Vector2(glyph.W * unitsPerPx, glyph.H * unitsPerPx),
                    ShapeType = Shape.Text,
                    Point2 = new Vector2(glyph.UvMin.x, yDown ? glyph.UvMax.y : glyph.UvMin.y),
                    Point3 = new Vector2(glyph.UvMax.x, yDown ? glyph.UvMin.y : glyph.UvMax.y),
                    Parameters0 = new Vector4(glyph.Range, glyph.W, settings.Embolden, 0f),
                    GlyphAtlas = glyph.Texture,
                    FillColor = style.FillColor,
                    StrokeColor = style.StrokeColor,
                    StrokeWeight = Mathf.Max(0f, style.StrokeWeight),
                    FillEnabled = fillEnabled,
                    StrokeEnabled = strokeEnabled
                });
            }
        }

        /// <summary>Layout settings for the canvas's current text state and drawing mode.</summary>
        public static TextLayoutSettings SettingsFor(UCanvas canvas, TextState state)
            => TextLayoutSettings.FromState(state, IsYUp(canvas.CurrentDrawingMode));

        /// <summary>
        /// World and CanvasLocal drawing space is y-up, so text keeps glyphs upright and
        /// advances lines toward -y there; the screen modes follow p5's y-down canvas.
        /// </summary>
        public static bool IsYUp(DrawingMode mode) => mode is DrawingMode.World or DrawingMode.CanvasLocal;

        public static FontInstance RequireFont(UCanvas canvas, TextState state)
        {
            if (state.TryGetInstance(out var instance, out _, out _))
                return instance;

            if (!WarnedCanvases.TryGetValue(canvas, out _))
            {
                WarnedCanvases.Add(canvas, null);
                Debug.LogWarning(
                    state.Font == null
                        ? "p5.Unite: set a font with canvas.TextFont(...) before drawing text."
                        : $"p5.Unite: font '{state.Font.FamilyName}' cannot be used for text "
                            + "(it failed to load; see the font warning above).",
                    canvas
                );
            }
            return null;
        }

        private static void ResolveRaster(
            UCanvas canvas,
            TextState state,
            FontInstance font,
            out int emBucket,
            out float glyphScale,
            out float unitsPerPx
        )
        {
            // Pixel size of the requested text size in the mode's raster space.
            float pixelSize = canvas.CurrentDrawingMode switch
            {
                DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay
                    => state.Size * Mathf.Max(1f, canvas.Height),
                DrawingMode.World or DrawingMode.CanvasLocal => state.Size * PixelsPerWorldUnit,
                _ => state.Size // Screen / ScreenOverlay: drawing units are already pixels.
            };

            emBucket = QuantizeBucket(pixelSize);
            glyphScale = emBucket / (float)font.UnitsPerEm;
            unitsPerPx = state.Size / emBucket;
        }

        private static int QuantizeBucket(float pixelSize)
        {
            pixelSize = Mathf.Clamp(pixelSize, MinBucketPx, MaxBucketPx);
            return Mathf.NextPowerOfTwo(Mathf.Max(MinBucketPx, Mathf.CeilToInt(pixelSize)));
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
            // Spread proportional to the bucket (1/8 em), clamped so tiny buckets keep
            // room for strokes and huge glyphs keep the sprite padding bounded. Strokes
            // wider than the spread saturate, so it errs on the generous side.
            float range = Mathf.Clamp(Mathf.RoundToInt(emBucket * 0.125f), 4, 24);
            int pad = Mathf.CeilToInt(range) + 2;

            float minX = outline.BoundsMin.x * glyphScale;
            float maxX = outline.BoundsMax.x * glyphScale;
            float minY = outline.BoundsMin.y * glyphScale;
            float maxY = outline.BoundsMax.y * glyphScale;

            int width = Mathf.CeilToInt(maxX - minX) + pad * 2;
            int height = Mathf.CeilToInt(maxY - minY) + pad * 2;
            int issueX = Mathf.FloorToInt(minX) - pad;
            int issueY = Mathf.FloorToInt(minY) - pad;

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

            var field = new byte[width * height];
            SdfEncoder.Encode(local, width, height, range, field);

            // (issueX, issueY) is the pen-relative sprite rect origin: the glyph's
            // baked pixel box measured from the pen origin / baseline (y-up).
            return atlas.Reserve(key, frame, width, height, range, field, issueX, issueY);
        }

        private static int GetCurrentFrame()
        {
            // Edit-mode tests never advance the frame counter; that is fine because the
            // frame value only feeds the age bookkeeping.
            return Time.frameCount;
        }
    }
}
