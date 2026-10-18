using Unite.Canvas;
using UnityEngine;

namespace Unite.Text
{
    public static partial class TextCanvasExtensions
    {
        #region Drawing

        /// <summary>
        /// Draws text with the current style: fill via Fill(), outline via Stroke().
        /// (x, y) is interpreted through TextAlign(); line feeds start new lines.
        /// </summary>
        public static void Text(this UCanvas canvas, string text, float x, float y)
            => GlyphLayout.Draw(canvas, State(canvas), text, x, y);

        /// <summary>
        /// Draws text inside a box of the given width (and optional height), wrapping
        /// with TextWrap() and aligning to the box edges. The box follows RectMode like
        /// p5's text(str, x, y, maxWidth, maxHeight); lines outside a bounded box are
        /// not drawn.
        /// </summary>
        public static void Text(this UCanvas canvas, string text, float x, float y, float maxWidth, float maxHeight = float.PositiveInfinity)
            => GlyphLayout.Draw(canvas, State(canvas), text, x, y, ResolveBox(canvas, x, y, maxWidth, maxHeight));

        #endregion


        #region Widths

        /// <summary>
        /// Tight width of the text: the visible ink extent of the widest line, so leading
        /// and trailing spaces do not count (p5.js 2.x textWidth()).
        /// </summary>
        public static float TextWidth(this UCanvas canvas, string text)
        {
            if (!TryLayout(canvas, text, out var font, out var settings, out var layout, alignToOrigin: true))
                return 0f;

            float widest = 0f;
            foreach (var line in layout.Lines)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                for (int index = line.GlyphStart; index < line.GlyphStart + line.GlyphCount; index++)
                {
                    if (!TextLayout.TryGetInkBounds(font, settings, layout.Glyphs[index], out var inkMin, out var inkMax))
                        continue;
                    min = Mathf.Min(min, inkMin.x);
                    max = Mathf.Max(max, inkMax.x);
                }

                if (max > min)
                    widest = Mathf.Max(widest, max - min);
            }
            return widest;
        }

        /// <summary>
        /// Loose width of the text from the font's advance widths (spaces included), the
        /// widest line wins (p5.js fontWidth()).
        /// </summary>
        public static float FontWidth(this UCanvas canvas, string text)
        {
            if (!TryLayout(canvas, text, out _, out _, out var layout, alignToOrigin: true))
                return 0f;

            float widest = 0f;
            foreach (var line in layout.Lines)
                widest = Mathf.Max(widest, line.Width);
            return widest;
        }

        #endregion


        #region Ascent and descent

        /// <summary>
        /// Ascent above the baseline: of the given text's ink when text is passed,
        /// otherwise of the current font at the current size.
        /// </summary>
        public static float TextAscent(this UCanvas canvas, string text = null)
        {
            if (string.IsNullOrEmpty(text))
                return canvas.FontAscent();
            return InkExtent(canvas, text, above: true);
        }

        /// <summary>
        /// Descent below the baseline (positive): of the given text's ink when text is
        /// passed, otherwise of the current font at the current size.
        /// </summary>
        public static float TextDescent(this UCanvas canvas, string text = null)
        {
            if (string.IsNullOrEmpty(text))
                return canvas.FontDescent();
            return InkExtent(canvas, text, above: false);
        }

        /// <summary>Loose ascent from the font's intrinsic metrics at the current size.</summary>
        public static float FontAscent(this UCanvas canvas)
        {
            var state = State(canvas);
            var font = GlyphLayout.RequireFont(canvas, state);
            return font == null ? 0f : font.Ascender * state.Size / font.UnitsPerEm;
        }

        /// <summary>Loose descent (positive) from the font's intrinsic metrics at the current size.</summary>
        public static float FontDescent(this UCanvas canvas)
        {
            var state = State(canvas);
            var font = GlyphLayout.RequireFont(canvas, state);
            return font == null ? 0f : -font.Descender * state.Size / font.UnitsPerEm;
        }

        #endregion


        #region Bounds

        /// <summary>
        /// Tight bounding box of the text's ink as drawn by the matching Text() call
        /// (alignment, wrapping and box clipping included). Rect min = smallest x / y.
        /// </summary>
        public static Rect TextBounds(this UCanvas canvas, string text, float x, float y, float maxWidth = 0f, float maxHeight = float.PositiveInfinity)
        {
            if (!TryLayoutAt(canvas, text, x, y, maxWidth, maxHeight, out var font, out var settings, out var layout))
                return new Rect(x, y, 0f, 0f);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var glyph in layout.Glyphs)
            {
                if (!TextLayout.TryGetInkBounds(font, settings, glyph, out var inkMin, out var inkMax))
                    continue;
                min = Vector2.Min(min, inkMin);
                max = Vector2.Max(max, inkMax);
            }

            return max.x >= min.x ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : new Rect(x, y, 0f, 0f);
        }

        /// <summary>
        /// Loose bounding box from the font's ascent, descent and advance widths, as
        /// drawn by the matching Text() call. Rect min = smallest x / y.
        /// </summary>
        public static Rect FontBounds(this UCanvas canvas, string text, float x, float y, float maxWidth = 0f, float maxHeight = float.PositiveInfinity)
        {
            if (!TryLayoutAt(canvas, text, x, y, maxWidth, maxHeight, out var font, out var settings, out var layout) ||
                layout.Lines.Count == 0)
                return new Rect(x, y, 0f, 0f);

            float scale = settings.Size / font.UnitsPerEm;
            float ascent = font.Ascender * scale;
            float descent = -font.Descender * scale;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var line in layout.Lines)
            {
                float top = line.Baseline - settings.YSign * ascent;
                float bottom = line.Baseline + settings.YSign * descent;
                min = Vector2.Min(min, new Vector2(line.Left, Mathf.Min(top, bottom)));
                max = Vector2.Max(max, new Vector2(line.Left + line.Width, Mathf.Max(top, bottom)));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        #endregion


        #region Helpers

        private static float InkExtent(UCanvas canvas, string text, bool above)
        {
            if (!TryLayout(canvas, text, out var font, out var settings, out var layout, alignToOrigin: true))
                return 0f;

            float extent = 0f;
            foreach (var glyph in layout.Glyphs)
            {
                if (!TextLayout.TryGetInkBounds(font, settings, glyph, out var min, out var max))
                    continue;

                // Heights relative to the glyph's baseline, measured upward.
                float top = settings.YSign > 0f ? glyph.Baseline - min.y : max.y - glyph.Baseline;
                float bottom = settings.YSign > 0f ? max.y - glyph.Baseline : glyph.Baseline - min.y;
                extent = Mathf.Max(extent, above ? top : bottom);
            }
            return extent;
        }

        // Measurement layout at the origin: left / baseline alignment, no box.
        private static bool TryLayout(
            UCanvas canvas,
            string text,
            out Fonts.FontInstance font,
            out TextLayoutSettings settings,
            out TextLayoutResult layout,
            bool alignToOrigin
        )
        {
            var state = State(canvas);
            font = GlyphLayout.RequireFont(canvas, state);
            settings = default;
            layout = null;
            if (font == null || string.IsNullOrEmpty(text))
                return false;

            settings = GlyphLayout.SettingsFor(canvas, state);
            if (alignToOrigin)
            {
                settings.Align = TextAlignMode.Left;
                settings.VerticalAlign = TextVerticalAlignMode.Baseline;
            }
            layout = TextLayout.Layout(font, settings, text, 0f, 0f);
            return true;
        }

        private static bool TryLayoutAt(
            UCanvas canvas,
            string text,
            float x,
            float y,
            float maxWidth,
            float maxHeight,
            out Fonts.FontInstance font,
            out TextLayoutSettings settings,
            out TextLayoutResult layout
        )
        {
            var state = State(canvas);
            font = GlyphLayout.RequireFont(canvas, state);
            settings = default;
            layout = null;
            if (font == null || string.IsNullOrEmpty(text))
                return false;

            settings = GlyphLayout.SettingsFor(canvas, state);
            TextBox? box = maxWidth > 0f ? ResolveBox(canvas, x, y, maxWidth, maxHeight) : null;
            layout = TextLayout.Layout(font, settings, text, x, y, box);
            return true;
        }

        // Interprets (x, y, w, h) through the canvas RectMode, like p5's text box.
        private static TextBox ResolveBox(UCanvas canvas, float x, float y, float width, float height)
        {
            bool bounded = !float.IsInfinity(height);
            switch (canvas.StyleState.RectMode)
            {
                case ShapeMode.Corners:
                    // width / height carry the opposite corner.
                    return new TextBox(
                        Mathf.Min(x, width),
                        bounded ? Mathf.Min(y, height) : y,
                        Mathf.Abs(width - x),
                        bounded ? Mathf.Abs(height - y) : height
                    );
                case ShapeMode.Center:
                    return new TextBox(x - width * 0.5f, bounded ? y - height * 0.5f : y, width, height);
                case ShapeMode.Radius:
                    return new TextBox(x - width, bounded ? y - height : y, width * 2f, bounded ? height * 2f : height);
                default:
                    return new TextBox(x, y, width, height);
            }
        }

        #endregion
    }
}
