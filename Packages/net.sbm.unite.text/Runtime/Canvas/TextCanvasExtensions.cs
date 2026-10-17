using Unite.Canvas;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// p5.js-style text API. Text draws with the glyph baseline at (x, y); horizontal
    /// alignment is provided, vertical alignment is baseline-only in this version.
    /// </summary>
    public static class TextCanvasExtensions
    {
        /// <summary>Sets the font asset used by subsequent Text calls on this canvas.</summary>
        public static void TextFont(this UCanvas canvas, UniteFontAsset font)
            => TextStateStore.GetOrCreate(canvas).Font = font;

        /// <summary>
        /// Sets the text size in the current drawing mode's units (pixels for the screen
        /// modes, world units for the world modes), like p5.js textSize().
        /// </summary>
        public static void TextSize(this UCanvas canvas, float size)
            => TextStateStore.GetOrCreate(canvas).Size = Mathf.Max(0.001f, size);

        /// <summary>Sets the horizontal alignment for subsequent Text calls.</summary>
        public static void TextAlign(this UCanvas canvas, TextAlignMode align)
            => TextStateStore.GetOrCreate(canvas).Align = align;

        /// <summary>
        /// Draws one run of text with the current style: fill via Fill(), outline via
        /// Stroke() (outline distance comes straight from the SDF sample).
        /// </summary>
        public static void Text(this UCanvas canvas, string text, float x, float y)
            => GlyphLayout.Draw(canvas, TextStateStore.GetOrCreate(canvas), text, x, y);

        /// <summary>Returns the advance width of a single text line in drawing units.</summary>
        public static float TextWidth(this UCanvas canvas, string text)
            => GlyphLayout.Measure(canvas, TextStateStore.GetOrCreate(canvas), text);
    }
}
