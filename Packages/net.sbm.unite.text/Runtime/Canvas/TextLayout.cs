using System;
using System.Collections.Generic;
using Unite.Text.Fonts;
using Unite.Text.Shaping;
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

    /// <summary>One glyph placed by the layout: pen origin on the baseline of its line.</summary>
    internal struct PlacedGlyph
    {
        public int GlyphId;

        /// <summary>Index of the font of the glyph in the font stack of the text.</summary>
        public int Face;

        /// <summary>Code point index of the cluster of the glyph in the laid-out string.</summary>
        public int Cluster;

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

    /// <summary>A line before placement: code points [Start, End) of one paragraph; VisibleEnd drops the hanging spaces.</summary>
    internal struct LineRange
    {
        public int Start;
        public int End;
        public int VisibleEnd;
        public int Paragraph;
    }

    /// <summary>
    /// Output of <see cref="TextLayout.Layout"/>. Instances can be passed back into the
    /// layout to reuse their lists, which keeps per-frame drawing free of garbage.
    /// </summary>
    internal sealed class TextLayoutResult
    {
        public readonly List<PlacedGlyph> Glyphs = new();
        public readonly List<LayoutLine> Lines = new();

        internal readonly List<LineRange> Ranges = new();
        internal byte[] LineLevels = Array.Empty<byte>();
        internal byte[] GlyphLevels = Array.Empty<byte>();
        internal int[] VisualOrder = Array.Empty<int>();

        internal void Reset()
        {
            Glyphs.Clear();
            Lines.Clear();
            Ranges.Clear();
        }

        internal void EnsureLine(int codePoints, int glyphs)
        {
            if (LineLevels.Length < codePoints)
                LineLevels = new byte[Math.Max(codePoints, LineLevels.Length * 2)];
            if (GlyphLevels.Length < glyphs)
            {
                int size = Math.Max(glyphs, GlyphLevels.Length * 2);
                GlyphLevels = new byte[size];
                VisualOrder = new int[size];
            }
        }
    }

    /// <summary>
    /// Text layout on top of the cached analysis (<see cref="ShapingCache"/>): line
    /// breaking at Unicode opportunities (WORD) or grapheme clusters (CHAR), bidi
    /// reordering per line, letter / word spacing and synthetic weight, p5.js-style
    /// horizontal and vertical alignment (free or inside a text box) and box clipping.
    /// </summary>
    internal static partial class TextLayout
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

            var shaped = ShapingCache.Get(font, text, settings.Direction);
            BreakLines(shaped, settings, box?.Width ?? 0f, result.Ranges);

            float scale = settings.Size / font.UnitsPerEm;
            float ascent = font.Ascender * scale;
            float descent = -font.Descender * scale;
            float leading = settings.Leading;
            int count = result.Ranges.Count;
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
                PlaceLine(shaped, settings, result.Ranges[index], x, box, baseline, result);
            }

            return result;
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

        // y-down: the box top is its smallest y; y-up: its largest.
        private static float BoxTop(TextBox box, float ySign)
            => ySign > 0f ? box.MinY : box.MinY + (box.HasHeight ? box.Height : 0f);
    }
}
