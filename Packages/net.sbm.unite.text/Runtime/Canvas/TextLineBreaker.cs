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
            float scale
        )
        {
            var lines = new List<List<int>>();
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                var codePoints = TextLayout.CodePoints(paragraph);
                if (maxWidth <= 0f)
                {
                    lines.Add(codePoints);
                    continue;
                }

                if (settings.Wrap == TextWrapMode.Char)
                    WrapChars(font, settings, codePoints, maxWidth, scale, lines);
                else
                    WrapWords(font, settings, codePoints, maxWidth, scale, lines);
            }
            return lines;
        }

        // WORD: breaks at spaces; a single word wider than the box overflows it.
        private static void WrapWords(
            FontInstance font,
            in TextLayoutSettings settings,
            List<int> codePoints,
            float maxWidth,
            float scale,
            List<List<int>> lines
        )
        {
            var current = new List<int>();
            var word = new List<int>();

            for (int index = 0; index <= codePoints.Count; index++)
            {
                bool atBreak = index == codePoints.Count || codePoints[index] == ' ';
                if (!atBreak)
                {
                    word.Add(codePoints[index]);
                    continue;
                }

                var candidate = new List<int>(current);
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
                    current = new List<int>(word);
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
            List<List<int>> lines
        )
        {
            var current = new List<int>();
            float width = 0f;

            foreach (int codePoint in codePoints)
            {
                float advance = TextLayout.Advance(font, settings, codePoint, scale);
                if (current.Count > 0 && width + advance > maxWidth)
                {
                    lines.Add(current);
                    current = new List<int>();
                    width = 0f;
                }

                current.Add(codePoint);
                width += advance;
            }

            lines.Add(current);
        }
    }
}
