using System;
using Unite.Text.Fonts;
using Unite.Text.Unicode;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Fills a <see cref="TextAnalysis"/> (code points, paragraphs, bidi levels, line
    /// break opportunities, grapheme clusters, scripts) and shapes it into a
    /// <see cref="ShapedText"/> run by run (one bidi level and script per run). Main thread
    /// only; buffers are reused.
    /// </summary>
    internal sealed class TextAnalyzer
    {
        private readonly BidiParagraph _bidi = new();
        private readonly LineBreaker _lineBreaker = new();
        private readonly BasicShaper _shaper = new();

        public void Analyze(TextAnalysis target, string text, TextDirectionMode direction)
        {
            int length = CountCodePoints(text);
            target.Reset(length);
            target.Text = text;
            target.Direction = direction;
            DecodeCodePoints(text, target.CodePoints);

            var codePoints = new ReadOnlySpan<int>(target.CodePoints, 0, length);
            _lineBreaker.Analyze(codePoints, new Span<LineBreakOpportunity>(target.Breaks, 0, length + 1));
            FindClusters(target, codePoints);
            ResolveParagraphs(target, codePoints, direction);
            ResolveScripts(target, codePoints);
        }

        public void Shape(ShapedText target, TextAnalysis analysis, FontInstance font)
        {
            target.Reset(analysis);
            for (int paragraph = 0; paragraph < analysis.ParagraphCount; paragraph++)
            {
                var range = analysis.Paragraphs[paragraph];
                int runStart = range.Start;
                for (int index = range.Start + 1; index <= range.End; index++)
                {
                    bool split = index == range.End
                        || target.ClusterStarts[index]
                            && (analysis.Levels[index] != analysis.Levels[runStart] || analysis.Scripts[index] != analysis.Scripts[runStart]);
                    if (!split)
                        continue;

                    _shaper.Shape(target, font, 0, runStart, index, (analysis.Levels[runStart] & 1) == 1, analysis.Scripts[runStart]);
                    runStart = index;
                }
            }
            SummarizeClusters(target);
        }

        private static int CountCodePoints(string text)
        {
            int count = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                    index++;
                count++;
            }
            return count;
        }

        // Surrogate pairs become one code point; lone surrogates pass through.
        private static void DecodeCodePoints(string text, int[] codePoints)
        {
            int count = 0;
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    codePoints[count++] = char.ConvertToUtf32(current, text[index + 1]);
                    index++;
                    continue;
                }
                codePoints[count++] = current;
            }
        }

        private static void FindClusters(TextAnalysis target, ReadOnlySpan<int> codePoints)
        {
            int length = codePoints.Length;
            Array.Clear(target.GraphemeStarts, 0, length + 1);
            for (int start = 0; start < length; start = GraphemeBreaker.NextBoundary(codePoints, start))
                target.GraphemeStarts[start] = true;
            target.GraphemeStarts[length] = true;
        }

        // P1: paragraphs end after a paragraph separator (CR LF counts as one); each gets
        // its own bidi resolution. Text ending in a separator ends with an empty paragraph.
        private void ResolveParagraphs(TextAnalysis target, ReadOnlySpan<int> codePoints, TextDirectionMode direction)
        {
            int requested = direction switch
            {
                TextDirectionMode.Rtl => 1,
                TextDirectionMode.Auto => BidiParagraph.AutoLevel,
                _ => 0
            };

            int length = codePoints.Length;
            for (int index = 0; index < length; index++)
            {
                var type = UnicodeData.GetBidiClass(codePoints[index]);
                target.ResetClasses[index] = type switch
                {
                    BidiClass.S or BidiClass.B => LineResetClass.Separator,
                    BidiClass.WS or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI
                        or BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF
                        or BidiClass.BN => LineResetClass.Whitespace,
                    _ => LineResetClass.None
                };
            }

            int start = 0;
            for (int index = 0; index < length; index++)
            {
                if (UnicodeData.GetBidiClass(codePoints[index]) != BidiClass.B)
                    continue;
                if (codePoints[index] == '\r' && index + 1 < length && codePoints[index + 1] == '\n')
                    continue;

                AddParagraph(target, codePoints, start, index + 1, requested);
                start = index + 1;
            }

            if (start < length || length == 0 || UnicodeData.GetBidiClass(codePoints[length - 1]) == BidiClass.B)
                AddParagraph(target, codePoints, start, length, requested);
        }

        private void AddParagraph(TextAnalysis target, ReadOnlySpan<int> codePoints, int start, int end, int requested)
        {
            _bidi.Analyze(codePoints.Slice(start, end - start), requested);
            _bidi.Levels.CopyTo(new Span<byte>(target.Levels, start, end - start));
            target.AddParagraph(new ShapedParagraph
            {
                Start = start,
                End = end,
                Level = _bidi.ParagraphLevel,
                PureLeftToRight = _bidi.IsPureLeftToRight
            });
        }

        // Common and Inherited characters (spaces, punctuation, marks) take the script of
        // the text around them: the previous script, or the next one at the start.
        private static void ResolveScripts(TextAnalysis target, ReadOnlySpan<int> codePoints)
        {
            var scripts = target.Scripts;
            int length = codePoints.Length;
            var current = UnicodeScript.Unknown;
            int pending = 0;
            for (int index = 0; index < length; index++)
            {
                var script = UnicodeData.GetScript(codePoints[index]);
                if (script is UnicodeScript.Common or UnicodeScript.Inherited or UnicodeScript.Unknown)
                {
                    scripts[index] = current;
                    if (current == UnicodeScript.Unknown)
                        pending = index + 1;
                    continue;
                }

                if (current == UnicodeScript.Unknown)
                {
                    for (int back = 0; back < pending; back++)
                        scripts[back] = script;
                }
                current = script;
                scripts[index] = script;
            }

            if (current == UnicodeScript.Unknown)
            {
                for (int index = 0; index < length; index++)
                    scripts[index] = UnicodeScript.Common;
            }
        }

        private static void SummarizeClusters(ShapedText target)
        {
            int length = target.Length;
            Array.Clear(target.ClusterAdvances, 0, length);
            Array.Clear(target.ClusterInkGlyphs, 0, length);
            for (int index = 0; index < target.GlyphCount; index++)
            {
                ref var glyph = ref target.Glyphs[index];
                target.ClusterAdvances[glyph.Cluster] += glyph.Advance;
                if (glyph.Advance != 0f)
                    target.ClusterInkGlyphs[glyph.Cluster]++;
            }

            int next = 0;
            for (int codePoint = 0; codePoint <= length; codePoint++)
            {
                while (next < target.GlyphCount && target.Glyphs[next].Cluster < codePoint)
                    next++;
                target.FirstGlyph[codePoint] = next;
            }
        }
    }
}
