using System.Runtime.CompilerServices;
using Unite.Canvas;
using Unite.Drawing;
using Unite.Text.Fonts;
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
        private const int MaxBucketPx = 1024;

        // World text rasterizes at this many pixels per world unit when no camera is
        // available to measure the on-screen size.
        private const float FallbackPixelsPerWorldUnit = 64f;

        private static readonly ConditionalWeakTable<UCanvas, object> WarnedCanvases = new();

        // Drawing is not re-entrant, so one layout buffer serves every Draw call.
        private static readonly TextLayoutResult LayoutBuffer = new();

        public static void Draw(UCanvas canvas, TextState state, string text, float x, float y, TextBox? box = null)
        {
            var fonts = RequireStack(canvas, state);
            if (fonts == null || text == null)
                return;

            var style = canvas.StyleState;
            bool fillEnabled = style.FillEnabled;
            bool strokeEnabled = style.StrokeEnabled && Mathf.Max(0f, style.StrokeWeight) > 0f;
            if (!fillEnabled && !strokeEnabled)
                return;

            var settings = SettingsFor(canvas, state);
            using var drawScope = TextProfiling.Draw.Auto();

            TextLayoutResult layout;
            using (TextProfiling.Layout.Auto())
                layout = TextLayout.Layout(fonts, settings, text, x, y, box, LayoutBuffer);

            // The raster bucket depends on the size only; each face scales its own units.
            ResolveRaster(canvas, state, fonts.Primary, x, y, out int em, out _, out float unitsPerPx);
            int frame = GetCurrentFrame();

            // Synthetic italic: x shifts right with the height above the baseline. In
            // y-down drawing space "up" is -y, in y-up space it is +y.
            var slant = Matrix4x4.identity;
            slant.m01 = -settings.YSign * settings.Slant;

            int currentFace = -1;
            var context = new GlyphContext
            {
                Canvas = canvas,
                Em = em,
                UnitsPerPx = unitsPerPx,
                Frame = frame,
                Slant = slant,
                YSign = settings.YSign,
                Embolden = settings.Embolden,
                StrokeColor = style.StrokeColor,
                StrokeWeight = Mathf.Max(0f, style.StrokeWeight)
            };

            foreach (var placed in layout.Glyphs)
            {
                if (placed.Face != currentFace)
                {
                    currentFace = placed.Face;
                    context.Font = fonts[currentFace];
                    context.Atlas = AtlasRegistry.GetOrCreate(context.Font.Face);
                    context.GlyphScale = em / (float)context.Font.UnitsPerEm;
                }
                var font = context.Font;

                // COLR color glyphs: one glyph per layer in its palette color; a stroke
                // follows the base glyph's own (monochrome) outline.
                var color = font.Face.ColorLayers;
                if (color != null && color.TryGetLayers(placed.GlyphId, out int firstLayer, out int layers))
                {
                    if (fillEnabled)
                    {
                        for (int layer = 0; layer < layers; layer++)
                        {
                            color.GetLayer(firstLayer + layer, out int layerGlyph, out int paletteIndex);
                            EmitGlyph(context, placed, layerGlyph, color.LayerColor(paletteIndex, style.FillColor), fill: true, stroke: false);
                        }
                    }
                    if (strokeEnabled)
                        EmitGlyph(context, placed, placed.GlyphId, style.FillColor, fill: false, stroke: true);
                    continue;
                }

                EmitGlyph(context, placed, placed.GlyphId, style.FillColor, fillEnabled, strokeEnabled);
            }
        }

        private struct GlyphContext
        {
            public UCanvas Canvas;
            public FontInstance Font;
            public GlyphAtlasSet Atlas;
            public int Em;
            public float GlyphScale;
            public float UnitsPerPx;
            public int Frame;
            public Matrix4x4 Slant;
            public float YSign;
            public float Embolden;
            public Color StrokeColor;
            public float StrokeWeight;
        }

        // Bakes (or finds) one glyph and enqueues its quad at the placed glyph's pen.
        private static void EmitGlyph(in GlyphContext context, in PlacedGlyph placed, int glyphId, Color fillColor, bool fill, bool stroke)
        {
            // Baked glyphs need no outline lookup; blank glyphs are never cached, so they
            // fall through to the (cached, cheap) bake source check each time.
            var font = context.Font;
            var key = new GlyphKey(context.Atlas.FontId, glyphId, context.Em, font.Id);
            if (!context.Atlas.TryGetCached(key, context.Frame, out var glyph))
            {
                if (!font.TryGetBakeSource(glyphId, out var source))
                    return;

                bool allocated;
                using (TextProfiling.Allocate.Auto())
                    allocated = TryAllocateGlyph(context.Atlas, key, context.Frame, !font.IsDefault, source, context.Em, context.GlyphScale, out glyph);
                if (!allocated)
                    return;
            }

            // Sprite rect: pen-relative origin (OffsetX/Y, font orientation y-up) + size in
            // baked pixels, mapped into drawing space around the pen.
            float unitsPerPx = context.UnitsPerPx;
            var local = new Vector3(
                (glyph.OffsetX + glyph.W * 0.5f) * unitsPerPx,
                -context.YSign * (glyph.OffsetY + glyph.H * 0.5f) * unitsPerPx,
                0f
            );

            // The quad's -y half must sample the sprite's top in y-down space and its
            // bottom in y-up space.
            bool yDown = context.YSign > 0f;
            using var enqueueScope = TextProfiling.Enqueue.Auto();
            context.Canvas.EnqueueRawCommand(new DrawCommand
            {
                Transform = context.Canvas.CurrentTransform
                    * Matrix4x4.Translate(new Vector3(placed.PenX, placed.Baseline, 0f))
                    * context.Slant
                    * Matrix4x4.Translate(local),
                Size = new Vector2(glyph.W * unitsPerPx, glyph.H * unitsPerPx),
                ShapeType = Shape.Text,
                Point2 = new Vector2(glyph.UvMin.x, yDown ? glyph.UvMax.y : glyph.UvMin.y),
                Point3 = new Vector2(glyph.UvMax.x, yDown ? glyph.UvMin.y : glyph.UvMax.y),
                Parameters0 = new Vector4(glyph.Range, glyph.W, context.Embolden, 0f),
                GlyphAtlasIndex = context.Canvas.GlyphAtlasIndex(glyph.Texture),
                FillColor = fillColor,
                StrokeColor = context.StrokeColor,
                StrokeWeight = context.StrokeWeight,
                FillEnabled = fill,
                StrokeEnabled = stroke
            });
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
            => RequireStack(canvas, state)?.Primary;

        /// <summary>The primary font and its fallbacks, or null (warned once per canvas) without a usable font.</summary>
        public static Shaping.FontStack RequireStack(UCanvas canvas, TextState state)
        {
            if (state.TryGetStack(out var stack))
                return stack;

            if (!WarnedCanvases.TryGetValue(canvas, out _))
            {
                WarnedCanvases.Add(canvas, null);
                Debug.LogWarning(
                    state.Font == null
                        ? "p5.Unite: set a font with canvas.TextFont(...) before drawing text."
                        : $"p5.Unite: font '{state.Font.Name}' cannot be used for text "
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
            float x,
            float y,
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
                DrawingMode.World or DrawingMode.CanvasLocal => WorldPixelSize(canvas, state.Size, x, y),
                _ => state.Size // Screen / ScreenOverlay: drawing units are already pixels.
            };

            emBucket = QuantizeBucket(pixelSize);
            glyphScale = emBucket / (float)font.UnitsPerEm;
            unitsPerPx = state.Size / emBucket;
        }

        // On-screen height in pixels of an em of the given size in the canvas's units: the
        // text's world scale times the pixels one world unit covers at its distance from
        // the camera. Text close to the camera gets a larger bucket, so it stays sharp.
        private static float WorldPixelSize(UCanvas canvas, float size, float x, float y)
        {
            var camera = canvas.ActiveCamera;
            if (camera == null)
                return size * FallbackPixelsPerWorldUnit;

            var matrix = canvas.CurrentDrawingMode == DrawingMode.CanvasLocal
                ? canvas.transform.localToWorldMatrix * canvas.CurrentTransform
                : canvas.CurrentTransform;

            float worldSize = size * Mathf.Max(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude);
            float pixelsPerUnit;
            if (camera.orthographic)
            {
                pixelsPerUnit = camera.pixelHeight / (2f * Mathf.Max(0.0001f, camera.orthographicSize));
            }
            else
            {
                var position = matrix.MultiplyPoint3x4(new Vector3(x, y, 0f));
                var cameraTransform = camera.transform;
                float depth = Vector3.Dot(position - cameraTransform.position, cameraTransform.forward);
                depth = Mathf.Max(depth, camera.nearClipPlane);
                pixelsPerUnit = camera.pixelHeight / (2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            }

            return worldSize * pixelsPerUnit;
        }

        private static int QuantizeBucket(float pixelSize)
        {
            pixelSize = Mathf.Clamp(pixelSize, MinBucketPx, MaxBucketPx);
            return Mathf.NextPowerOfTwo(Mathf.Max(MinBucketPx, Mathf.CeilToInt(pixelSize)));
        }

        // Reserves the glyph's atlas rect now; its pixels are encoded at the frame's flush.
        private static bool TryAllocateGlyph(
            GlyphAtlasSet atlas,
            GlyphKey key,
            int frame,
            bool isVolatile,
            GlyphBakeSource source,
            int emBucket,
            float glyphScale,
            out CachedGlyph glyph
        )
        {
            // Spread proportional to the bucket (1/8 em), clamped so tiny buckets keep
            // room for strokes and huge glyphs keep the sprite padding bounded. Strokes
            // wider than the spread saturate, so it errs on the generous side.
            float range = Mathf.Clamp(Mathf.RoundToInt(emBucket * 0.125f), 4, 24);
            int pad = Mathf.CeilToInt(range) + 2;

            float minX = source.BoundsMin.x * glyphScale;
            float maxX = source.BoundsMax.x * glyphScale;
            float minY = source.BoundsMin.y * glyphScale;
            float maxY = source.BoundsMax.y * glyphScale;

            int width = Mathf.CeilToInt(maxX - minX) + pad * 2;
            int height = Mathf.CeilToInt(maxY - minY) + pad * 2;
            int issueX = Mathf.FloorToInt(minX) - pad;
            int issueY = Mathf.FloorToInt(minY) - pad;

            if (width > GlyphAtlasSet.LargePageSpan || height > GlyphAtlasSet.LargePageSpan)
            {
                glyph = default;
                return false;
            }

            // (issueX, issueY) is the pen-relative sprite rect origin: the glyph's
            // baked pixel box measured from the pen origin / baseline (y-up).
            glyph = atlas.Allocate(
                key, frame, isVolatile, width, height, range, issueX, issueY, source, glyphScale, issueX, issueY
            );
            return true;
        }

        private static int GetCurrentFrame()
        {
            // Edit-mode tests never advance the frame counter; that is fine because the
            // frame value only feeds the age bookkeeping.
            return Time.frameCount;
        }
    }
}
