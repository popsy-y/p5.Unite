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
        private readonly OpenTypeShaper _openTypeShaper = new();
        private static readonly int DefaultLanguage = Fonts.Layout.OpenTypeData.Tag("dflt");
        private TextFeatures _features = TextFeatures.Default;
        private int _language;

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

        public void Shape(ShapedText target, TextAnalysis analysis, FontInstance font, TextFeatures features = null, int languageTag = 0)
            => Shape(target, analysis, FontStack.Single(font), features, languageTag);

        /// <summary>
        /// Shapes the analysis run by run: one bidi level, script and font each. Every
        /// grapheme cluster goes to the first font of the stack that can draw it (spaces and
        /// punctuation stay with the font before them), then to a system font, else to the
        /// primary font's .notdef.
        /// </summary>
        public void Shape(ShapedText target, TextAnalysis analysis, FontStack stack, TextFeatures features = null, int languageTag = 0)
        {
            _features = features ?? TextFeatures.Default;
            _language = languageTag == 0 ? DefaultLanguage : languageTag;
            target.Reset(analysis);
            for (int paragraph = 0; paragraph < analysis.ParagraphCount; paragraph++)
            {
                var range = analysis.Paragraphs[paragraph];
                int runStart = range.Start;
                int runFace = range.Start < range.End ? FaceFor(stack, target, range.Start, -1) : 0;
                for (int index = range.Start + 1; index <= range.End; index++)
                {
                    bool split = index == range.End;
                    int face = runFace;
                    if (!split && target.ClusterStarts[index])
                    {
                        face = FaceFor(stack, target, index, runFace);
                        split = face != runFace
                            || analysis.Levels[index] != analysis.Levels[runStart]
                            || analysis.Scripts[index] != analysis.Scripts[runStart];
                    }
                    if (!split)
                        continue;

                    ShapeRun(target, stack[runFace], runFace, runStart, index, (analysis.Levels[runStart] & 1) == 1, analysis.Scripts[runStart]);
                    runStart = index;
                    runFace = face;
                }
            }
            SummarizeClusters(target);
        }

        // The face index (stack fonts, then system fonts) that draws the cluster at start.
        private static int FaceFor(FontStack stack, ShapedText target, int start, int previous)
        {
            int end = start + 1;
            while (end < target.Length && !target.ClusterStarts[end])
                end++;
            var cluster = new ReadOnlySpan<int>(target.CodePoints, start, end - start);

            var script = UnicodeData.GetScript(cluster[0]);
            bool neutral = script is UnicodeScript.Common or UnicodeScript.Inherited;
            if (neutral && previous >= 0 && SystemFonts.Covers(stack[previous], cluster))
                return previous;

            for (int face = 0; face < stack.Count; face++)
            {
                if (SystemFonts.Covers(stack[face], cluster))
                    return face;
            }

            int system = SystemFonts.FindFace(cluster);
            if (system >= 0)
                return stack.Count + system;
            return neutral && previous >= 0 ? previous : 0;
        }

        private void ShapeRun(ShapedText target, FontInstance font, int face, int start, int end, bool rightToLeft, UnicodeScript script)
        {
            var layout = font.Face.OpenType;
            if (layout != null && !layout.IsEmpty)
            {
                int glyphs = target.GlyphCount;
                try
                {
                    _openTypeShaper.Shape(target, font, face, start, end, rightToLeft, script, _language, _features);
                    return;
                }
                catch (IndexOutOfRangeException)
                {
                    // Broken layout tables: draw the run without them.
                    target.GlyphCount = glyphs;
                }
            }
            _shaper.Shape(target, font, face, start, end, rightToLeft, script);
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
