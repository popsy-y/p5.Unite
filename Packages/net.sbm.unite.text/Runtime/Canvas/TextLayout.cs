using System;
using System.Collections.Generic;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>Layout inputs, in drawing units. Built from a canvas text state or path options.</summary>
    internal struct TextLayoutSettings
    {
        // Synthetic style constants: FreeType-like oblique slant, and an outline offset
        // of ~1/48 em per side at bold (700) that scales linearly with the weight.
        public const float ItalicSlant = 0.21f;
        private const float EmboldenPerWeight = 0.021f / 300f;
        private const float MinWeight = 100f;
        private const float MaxWeight = 900f;

        public float Size;
        public float Leading;
        public TextAlignMode Align;
        public TextVerticalAlignMode VerticalAlign;
        public TextWrapMode Wrap;
        public TextDirectionMode Direction;
        public float LetterSpacing;
        public float WordSpacing;

        /// <summary>Outward outline offset (synthetic weight); widens each advance by twice this.</summary>
        public float Embolden;

        /// <summary>Horizontal shear per unit of height above the baseline (synthetic italic).</summary>
        public float Slant;

        /// <summary>+1 when drawing space is y-down (p5 / screen modes), -1 when y-up (world modes).</summary>
        public float YSign;

        public static TextLayoutSettings FromState(TextState state, bool yUp)
        {
            // Variable fonts with weight / italic axes draw those styles from the font itself.
            state.TryGetInstance(out _, out bool synthesizeWeight, out bool synthesizeItalic);

            float weight = state.IsBold ? Mathf.Max(state.Weight, 700f) : state.Weight;
            return new TextLayoutSettings
            {
                Size = state.Size,
                Leading = state.EffectiveLeading,
                Align = state.Align,
                VerticalAlign = state.VerticalAlign,
                Wrap = state.Wrap,
                Direction = state.Direction,
                LetterSpacing = state.LetterSpacing,
                WordSpacing = state.WordSpacing,
                Embolden = synthesizeWeight ? EmboldenFor(weight, state.Size) : 0f,
                Slant = synthesizeItalic && state.IsItalic ? ItalicSlant : 0f,
                YSign = yUp ? -1f : 1f
            };
        }

        public static float EmboldenFor(float weight, float size)
            => (Mathf.Clamp(weight, MinWeight, MaxWeight) - TextState.NormalWeight) * EmboldenPerWeight * size;
    }

    /// <summary>
    /// Text box for text(str, x, y, w, h): (Left, Bottom-most coordinate) corner, the
    /// box is read from its top edge in the reading direction. Height may be infinite
    /// (width-only boxes wrap without clipping).
    /// </summary>
    internal readonly struct TextBox
    {
        public TextBox(float left, float minY, float width, float height)
        {
            Left = left;
            MinY = minY;
            Width = width;
            Height = height;
        }

        public float Left { get; }
        public float MinY { get; }
        public float Width { get; }
        public float Height { get; }
        public bool HasHeight => !float.IsInfinity(Height);
    }

    /// <summary>One glyph placed by the layout: pen origin on its line's baseline.</summary>
    internal struct PlacedGlyph
    {
        public int GlyphId;
        public float PenX;
        public float Baseline;
    }

    /// <summary>One laid-out line: loose (advance-based) horizontal extent and baseline.</summary>
    internal struct LayoutLine
    {
        public float Left;
        public float Width;
        public float Baseline;
        public int GlyphStart;
        public int GlyphCount;
    }

    /// <summary>
    /// Output of <see cref="TextLayout.Layout"/>. Instances can be passed back into the
    /// layout to reuse their lists, which keeps per-frame drawing free of garbage.
    /// </summary>
    internal sealed class TextLayoutResult
    {
        public readonly List<PlacedGlyph> Glyphs = new();
        public readonly List<LayoutLine> Lines = new();

        internal readonly List<List<int>> BrokenLines = new();
        private readonly List<List<int>> _codePointLists = new();
        private int _codePointListsInUse;

        /// <summary>A cleared code point list that stays valid until the next <see cref="Reset"/>.</summary>
        internal List<int> RentCodePoints()
        {
            if (_codePointListsInUse == _codePointLists.Count)
                _codePointLists.Add(new List<int>());

            var list = _codePointLists[_codePointListsInUse++];
            list.Clear();
            return list;
        }

        internal void Reset()
        {
            Glyphs.Clear();
            Lines.Clear();
            BrokenLines.Clear();
            _codePointListsInUse = 0;
        }
    }

    /// <summary>
    /// Shaping-free text layout: line splitting, WORD/CHAR wrapping, letter/word spacing,
    /// p5.js-style horizontal and vertical alignment (free or inside a text box), box
    /// clipping and simple right-to-left glyph ordering.
    /// </summary>
    internal static class TextLayout
    {
        public static TextLayoutResult Layout(
            FontInstance font,
            in TextLayoutSettings settings,
            string text,
            float x,
            float y,
            TextBox? box = null,
            TextLayoutResult reuse = null
        )
        {
            var result = reuse ?? new TextLayoutResult();
            result.Reset();
            if (font == null || string.IsNullOrEmpty(text))
                return result;

            float scale = settings.Size / font.UnitsPerEm;
            var lines = TextLineBreaker.BreakLines(font, settings, text, box?.Width ?? 0f, scale, result);

            float ascent = font.Ascender * scale;
            float descent = -font.Descender * scale;
            float leading = settings.Leading;
            int count = lines.Count;
            float blockBelowFirstBaseline = (count - 1) * leading + descent;

            // Offset of the first baseline from the reference edge, measured in the
            // reading direction (down).
            float firstOffset;
            float referenceY;
            if (box is { HasHeight: true } boxed)
            {
                float height = boxed.Height;
                referenceY = BoxTop(boxed, settings.YSign);
                firstOffset = settings.VerticalAlign switch
                {
                    TextVerticalAlignMode.Center => (height - (ascent + blockBelowFirstBaseline)) * 0.5f + ascent,
                    TextVerticalAlignMode.Bottom => height - blockBelowFirstBaseline,
                    _ => ascent // TOP and BASELINE both start at the box top.
                };
            }
            else
            {
                referenceY = box.HasValue ? BoxTop(box.Value, settings.YSign) : y;
                firstOffset = settings.VerticalAlign switch
                {
                    TextVerticalAlignMode.Top => ascent,
                    TextVerticalAlignMode.Center => (ascent - blockBelowFirstBaseline) * 0.5f,
                    TextVerticalAlignMode.Bottom => -blockBelowFirstBaseline,
                    _ => box.HasValue ? ascent : 0f
                };
            }

            for (int index = 0; index < count; index++)
            {
                float offset = firstOffset + index * leading;

                // Lines that leave a bounded box are not drawn (p5 text box overflow).
                if (box is { HasHeight: true } clip &&
                    (offset - ascent < -0.001f || offset + descent > clip.Height + 0.001f))
                    continue;

                float baseline = referenceY + settings.YSign * offset;
                PlaceLine(font, settings, lines[index], x, box, baseline, scale, result);
            }

            return result;
        }

        /// <summary>Loose advance width of one line of code points, including spacing.</summary>
        public static float MeasureLine(FontInstance font, in TextLayoutSettings settings, List<int> codePoints, float scale)
        {
            float width = 0f;
            foreach (int codePoint in codePoints)
                width += Advance(font, settings, codePoint, scale);
            return width;
        }

        /// <summary>
        /// Ink box of one placed glyph in drawing units, including the synthetic slant and
        /// outline offset. Returns false for blank glyphs.
        /// </summary>
        public static bool TryGetInkBounds(
            FontInstance font,
            in TextLayoutSettings settings,
            PlacedGlyph glyph,
            out Vector2 min,
            out Vector2 max
        )
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            if (!font.TryGetOutline(glyph.GlyphId, out var outline))
                return false;

            float scale = settings.Size / font.UnitsPerEm;
            float grow = settings.Embolden;

            for (int corner = 0; corner < 4; corner++)
            {
                float localX = ((corner & 1) == 0 ? outline.BoundsMin.x : outline.BoundsMax.x) * scale;
                float height = ((corner & 2) == 0 ? outline.BoundsMin.y : outline.BoundsMax.y) * scale;
                localX += (corner & 1) == 0 ? -grow : grow;
                height += (corner & 2) == 0 ? -grow : grow;

                var point = new Vector2(
                    glyph.PenX + localX + height * settings.Slant,
                    glyph.Baseline - settings.YSign * height
                );
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return true;
        }

        public static List<int> CodePoints(string text) => CodePoints(text, new List<int>(text.Length));

        public static List<int> CodePoints(string text, List<int> codePoints)
        {
            for (int index = 0; index < text.Length; index++)
            {
                if (char.IsHighSurrogate(text[index]) &&
                    index + 1 < text.Length &&
                    char.IsLowSurrogate(text[index + 1]))
                {
                    codePoints.Add(char.ConvertToUtf32(text[index], text[index + 1]));
                    index++;
                    continue;
                }

                codePoints.Add(text[index]);
            }
            return codePoints;
        }

        // y-down: the box top is its smallest y; y-up: its largest.
        private static float BoxTop(TextBox box, float ySign)
            => ySign > 0f ? box.MinY : box.MinY + (box.HasHeight ? box.Height : 0f);

        internal static float Advance(FontInstance font, in TextLayoutSettings settings, int codePoint, float scale)
        {
            float advance = font.Advance(font.GetGlyphId(codePoint)) * scale
                + settings.LetterSpacing
                + settings.Embolden * 2f;
            if (codePoint == ' ')
                advance += settings.WordSpacing;
            return advance;
        }

        private static void PlaceLine(
            FontInstance font,
            in TextLayoutSettings settings,
            List<int> codePoints,
            float x,
            TextBox? box,
            float baseline,
            float scale,
            TextLayoutResult result
        )
        {
            float width = MeasureLine(font, settings, codePoints, scale);
            float anchor = box.HasValue
                ? settings.Align switch
                {
                    TextAlignMode.Center => box.Value.Left + box.Value.Width * 0.5f,
                    TextAlignMode.Right => box.Value.Left + box.Value.Width,
                    _ => box.Value.Left
                }
                : x;

            float left = settings.Align switch
            {
                TextAlignMode.Center => anchor - width * 0.5f,
                TextAlignMode.Right => anchor - width,
                _ => anchor
            };

            var line = new LayoutLine
            {
                Left = left,
                Width = width,
                Baseline = baseline,
                GlyphStart = result.Glyphs.Count
            };

            // No bidi or shaping: right-to-left only reverses the visual glyph order.
            bool reverse = settings.Direction == TextDirectionMode.Rtl;
            float pen = left;
            for (int step = 0; step < codePoints.Count; step++)
            {
                int codePoint = codePoints[reverse ? codePoints.Count - 1 - step : step];
                result.Glyphs.Add(new PlacedGlyph
                {
                    GlyphId = font.GetGlyphId(codePoint),
                    PenX = pen + settings.Embolden,
                    Baseline = baseline
                });
                pen += Advance(font, settings, codePoint, scale);
            }

            line.GlyphCount = result.Glyphs.Count - line.GlyphStart;
            result.Lines.Add(line);
        }
    }
}
