using System;
using System.Collections.Generic;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// p5.js-style text API on <see cref="UCanvas"/>. Text attributes are saved and
    /// restored by Push()/Pop() like the other drawing styles. Getter overloads (no
    /// value argument) mirror p5's zero-argument calls such as textSize().
    /// </summary>
    public static partial class TextCanvasExtensions
    {
        #region Font

        /// <summary>Sets the font asset used by subsequent text calls on this canvas.</summary>
        public static void TextFont(this UCanvas canvas, UniteFontAsset font)
            => State(canvas).Font = font;

        /// <summary>Sets the font asset and the text size at once, like p5 textFont(font, size).</summary>
        public static void TextFont(this UCanvas canvas, UniteFontAsset font, float size)
        {
            canvas.TextFont(font);
            canvas.TextSize(size);
        }

        /// <summary>Returns the current font asset (null until TextFont() is called).</summary>
        public static UniteFontAsset TextFont(this UCanvas canvas) => State(canvas).Font;

        /// <summary>Loads a font file synchronously. See <see cref="UniteFontAsset.LoadFont(string, string)"/>.</summary>
        public static UniteFontAsset LoadFont(this UCanvas canvas, string path, string name = null)
            => UniteFontAsset.LoadFont(path, name);

        /// <summary>Loads a font file asynchronously. See <see cref="UniteFontAsset.LoadFontAsync"/>.</summary>
        public static Awaitable<UniteFontAsset> LoadFontAsync(this UCanvas canvas, string path, string name = null)
            => UniteFontAsset.LoadFontAsync(path, name);

        #endregion


        #region Size and leading

        /// <summary>
        /// Sets the text size in the current drawing mode's units (pixels for the screen
        /// modes, world units for the world modes), like p5.js textSize(). Unless
        /// TextLeading() was called, the leading follows at 1.25 x the size.
        /// </summary>
        public static void TextSize(this UCanvas canvas, float size)
            => State(canvas).Size = Mathf.Max(0.001f, size);

        /// <summary>Returns the current text size.</summary>
        public static float TextSize(this UCanvas canvas) => State(canvas).Size;

        /// <summary>Sets the distance between the baselines of consecutive lines.</summary>
        public static void TextLeading(this UCanvas canvas, float leading)
            => State(canvas).Leading = leading;

        /// <summary>Returns the current leading (1.25 x the text size unless set).</summary>
        public static float TextLeading(this UCanvas canvas) => State(canvas).EffectiveLeading;

        #endregion


        #region Alignment, wrap and direction

        /// <summary>Sets the horizontal alignment and keeps the vertical one.</summary>
        public static void TextAlign(this UCanvas canvas, TextAlignMode horizontal)
            => State(canvas).Align = horizontal;

        /// <summary>
        /// Sets how Text()'s x and y are interpreted, like p5 textAlign(horizAlign, vertAlign).
        /// Inside a text box the alignment applies to the box edges.
        /// </summary>
        public static void TextAlign(this UCanvas canvas, TextAlignMode horizontal, TextVerticalAlignMode vertical)
        {
            var state = State(canvas);
            state.Align = horizontal;
            state.VerticalAlign = vertical;
        }

        /// <summary>Returns the current horizontal and vertical alignment.</summary>
        public static (TextAlignMode Horizontal, TextVerticalAlignMode Vertical) TextAlign(this UCanvas canvas)
        {
            var state = State(canvas);
            return (state.Align, state.VerticalAlign);
        }

        /// <summary>Sets how boxed text (Text with a max width) breaks lines.</summary>
        public static void TextWrap(this UCanvas canvas, TextWrapMode wrap) => State(canvas).Wrap = wrap;

        /// <summary>Returns the current wrap mode.</summary>
        public static TextWrapMode TextWrap(this UCanvas canvas) => State(canvas).Wrap;

        /// <summary>
        /// Sets the text direction. Right-to-left reverses the visual glyph order of each
        /// line; there is no bidi reordering or shaping yet.
        /// </summary>
        public static void TextDirection(this UCanvas canvas, TextDirectionMode direction)
            => State(canvas).Direction = direction;

        /// <summary>Returns the current text direction.</summary>
        public static TextDirectionMode TextDirection(this UCanvas canvas) => State(canvas).Direction;

        #endregion


        #region Style and weight

        /// <summary>
        /// Sets NORMAL / ITALIC / BOLD / BOLDITALIC. The style is synthesized from the
        /// current font: italic slants the glyphs, bold offsets the outline outward
        /// (equivalent to weight 700 unless TextWeight() is heavier).
        /// </summary>
        public static void TextStyle(this UCanvas canvas, TextStyleMode style) => State(canvas).Style = style;

        /// <summary>Returns the current text style.</summary>
        public static TextStyleMode TextStyle(this UCanvas canvas) => State(canvas).Style;

        /// <summary>
        /// Sets the font weight (100-900, 400 = regular). Variable fonts with a "wght"
        /// axis follow it; other fonts synthesize the weight by offsetting the outline.
        /// </summary>
        public static void TextWeight(this UCanvas canvas, float weight) => State(canvas).Weight = weight;

        /// <summary>Returns the current font weight.</summary>
        public static float TextWeight(this UCanvas canvas) => State(canvas).Weight;

        #endregion


        #region Variable fonts

        /// <summary>
        /// Sets one axis of a variable font in the axis's user-space units, for example
        /// TextVariation("wdth", 75f). An explicit "wght" value takes precedence over
        /// TextWeight(); axes the font does not have are ignored.
        /// </summary>
        public static void TextVariation(this UCanvas canvas, string axis, float value)
        {
            var state = State(canvas);
            var settings = new List<AxisSetting>(state.Variations ?? Array.Empty<AxisSetting>());
            settings.RemoveAll(setting => setting.Tag == axis);
            settings.Add(new AxisSetting(axis, value));
            state.Variations = settings.ToArray();
        }

        /// <summary>The value explicitly set for an axis with TextVariation(), or null.</summary>
        public static float? TextVariation(this UCanvas canvas, string axis)
        {
            foreach (var setting in State(canvas).Variations ?? Array.Empty<AxisSetting>())
            {
                if (setting.Tag == axis)
                    return setting.Value;
            }
            return null;
        }

        /// <summary>Forgets every axis set with TextVariation().</summary>
        public static void ClearTextVariations(this UCanvas canvas) => State(canvas).Variations = null;

        /// <summary>The variation axes of the current font (empty for static fonts or before TextFont()).</summary>
        public static IReadOnlyList<VariationAxis> TextAxes(this UCanvas canvas)
        {
            var font = State(canvas).Font;
            return font != null && font.TryGetFont(out var face) ? face.Axes : Array.Empty<VariationAxis>();
        }

        #endregion

        internal static TextState State(UCanvas canvas) => TextStateStore.GetOrCreate(canvas);
    }
}
