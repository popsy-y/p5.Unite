using System.Collections.Generic;
using Unite.Text.Fonts;

namespace Unite.Text
{
    /// <summary>
    /// Splits text into lines at line feeds and, for boxed text, wraps them to the box
    /// width (p5.js textWrap WORD / CHAR). Widths are loose advance widths.
    /// </summary>
    internal static class TextLineBreaker
    {
        public static List<List<int>> BreakLines(
            FontInstance font,
            in TextLayoutSettings settings,
            string text,
            float maxWidth,
            float scale,
            TextLayoutResult result
        )
        {
            var lines = result.BrokenLines;

            // Single-line text (the common case) needs no splitting.
            if (text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
            {
                BreakParagraph(font, settings, text, maxWidth, scale, result, lines);
                return lines;
            }

            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
                BreakParagraph(font, settings, paragraph, maxWidth, scale, result, lines);
            return lines;
        }

        private static void BreakParagraph(
            FontInstance font,
            in TextLayoutSettings settings,
            string paragraph,
            float maxWidth,
            float scale,
            TextLayoutResult result,
            List<List<int>> lines
        )
        {
            var codePoints = TextLayout.CodePoints(paragraph, result.RentCodePoints());
            if (maxWidth <= 0f)
            {
                lines.Add(codePoints);
                return;
            }

            if (settings.Wrap == TextWrapMode.Char)
                WrapChars(font, settings, codePoints, maxWidth, scale, lines, result);
            else
                WrapWords(font, settings, codePoints, maxWidth, scale, lines, result);
        }

        // WORD: breaks at spaces; a single word wider than the box overflows it.
        private static void WrapWords(
            FontInstance font,
            in TextLayoutSettings settings,
            List<int> codePoints,
            float maxWidth,
            float scale,
            List<List<int>> lines,
            TextLayoutResult result
        )
        {
            var current = result.RentCodePoints();
            var word = result.RentCodePoints();

            for (int index = 0; index <= codePoints.Count; index++)
            {
                bool atBreak = index == codePoints.Count || codePoints[index] == ' ';
                if (!atBreak)
                {
                    word.Add(codePoints[index]);
                    continue;
                }

                var candidate = result.RentCodePoints();
                candidate.AddRange(current);
                if (candidate.Count > 0)
                    candidate.Add(' ');
                candidate.AddRange(word);

                if (current.Count == 0 || TextLayout.MeasureLine(font, settings, candidate, scale) <= maxWidth)
                {
                    current = candidate;
                }
                else
                {
                    lines.Add(current);
                    current = result.RentCodePoints();
                    current.AddRange(word);
                }
                word.Clear();
            }

            lines.Add(current);
        }

        // CHAR: breaks wherever the next character would leave the box.
        private static void WrapChars(
            FontInstance font,
            in TextLayoutSettings settings,
            List<int> codePoints,
            float maxWidth,
            float scale,
            List<List<int>> lines,
            TextLayoutResult result
        )
        {
            var current = result.RentCodePoints();
            float width = 0f;

            foreach (int codePoint in codePoints)
            {
                float advance = TextLayout.Advance(font, settings, codePoint, scale);
                if (current.Count > 0 && width + advance > maxWidth)
                {
                    lines.Add(current);
                    current = result.RentCodePoints();
                    width = 0f;
                }

                current.Add(codePoint);
                width += advance;
            }

            lines.Add(current);
        }
    }
}
