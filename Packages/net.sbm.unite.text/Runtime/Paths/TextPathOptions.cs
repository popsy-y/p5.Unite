using System.Collections.Generic;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Layout and sampling settings for the p5.Font outline functions
    /// (TextToPoints / TextToContours / TextToPaths / TextToModel). Defaults follow
    /// p5.js; <see cref="FromCanvas"/> copies a canvas's current text attributes, the
    /// way p5's font methods read the sketch's textSize() and textAlign().
    /// </summary>
    public sealed class TextPathOptions
    {
        /// <summary>Text size in output units (p5 default 12).</summary>
        public float FontSize = 12f;

        /// <summary>Baseline-to-baseline distance; null = 1.25 x <see cref="FontSize"/>.</summary>
        public float? Leading;

        public TextAlignMode Align = TextAlignMode.Left;
        public TextVerticalAlignMode VerticalAlign = TextVerticalAlignMode.Baseline;
        public TextWrapMode Wrap = TextWrapMode.Word;
        public TextDirectionMode Direction = TextDirectionMode.Ltr;
        public float LetterSpacing;
        public float WordSpacing;

        /// <summary>CSS-like OpenType controls, as textProperty fontKerning / fontVariantLigatures / fontFeatureSettings / lang.</summary>
        public string FontKerning;
        public string FontVariantLigatures;
        public string FontFeatureSettings;
        public string Language;

        /// <summary>
        /// Output orientation. False (default) is p5's y-down canvas space; true flips
        /// glyphs upright for y-up spaces such as the world drawing modes.
        /// </summary>
        public bool YUp;

        /// <summary>Samples per unit of outline length (TextToPoints / TextToContours, p5 default 0.1).</summary>
        public float SampleFactor = 0.1f;

        /// <summary>
        /// When non-zero, drops sampled points whose neighboring edges turn by less than
        /// this angle in radians (collinear simplification).
        /// </summary>
        public float SimplifyThreshold;

        /// <summary>Extrusion depth of TextToModel along +z (0 = flat).</summary>
        public float Extrude;

        /// <summary>
        /// Variable font axis values (user-space units, e.g. wght 700); null keeps the
        /// font's defaults. Ignored by static fonts.
        /// </summary>
        public IReadOnlyList<AxisSetting> Variations;

        /// <summary>Options matching the canvas's current text attributes and drawing mode.</summary>
        public static TextPathOptions FromCanvas(UCanvas canvas)
        {
            var state = TextStateStore.GetOrCreate(canvas);
            return new TextPathOptions
            {
                FontSize = state.Size,
                Leading = state.Leading,
                Align = state.Align,
                VerticalAlign = state.VerticalAlign,
                Wrap = state.Wrap,
                Direction = state.Direction,
                LetterSpacing = state.LetterSpacing,
                WordSpacing = state.WordSpacing,
                FontKerning = state.FontKerning,
                FontVariantLigatures = state.FontVariantLigatures,
                FontFeatureSettings = state.FontFeatureSettings,
                Language = state.Language,
                YUp = GlyphLayout.IsYUp(canvas.CurrentDrawingMode),
                Variations = state.ResolveAxes()
            };
        }

        internal TextLayoutSettings ToSettings() => new()
        {
            Size = Mathf.Max(0.001f, FontSize),
            Leading = Leading ?? FontSize * TextState.DefaultLeadingFactor,
            Align = Align,
            VerticalAlign = VerticalAlign,
            Wrap = Wrap,
            Direction = Direction,
            LetterSpacing = LetterSpacing,
            WordSpacing = WordSpacing,
            Features = Shaping.TextFeatures.From(FontKerning, FontVariantLigatures, FontFeatureSettings),
            LanguageTag = Shaping.LanguageTags.For(Language),
            YSign = YUp ? -1f : 1f
        };
    }

    /// <summary>
    /// A point sampled on a text outline, like p5's {x, y, alpha}: position in output
    /// units and the outline's direction at that point in degrees.
    /// </summary>
    public readonly struct TextPoint
    {
        public TextPoint(Vector2 position, float alpha)
        {
            Position = position;
            Alpha = alpha;
        }

        public Vector2 Position { get; }
        public float X => Position.x;
        public float Y => Position.y;
        public float Alpha { get; }
    }
}
