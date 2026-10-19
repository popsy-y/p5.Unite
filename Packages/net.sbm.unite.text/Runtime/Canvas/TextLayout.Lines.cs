using System;
using System.Collections.Generic;
using Unite.Text.Shaping;
using Unite.Text.Unicode;

namespace Unite.Text
{
    // Line fitting and glyph placement over a ShapedText.
    internal static partial class TextLayout
    {
        /// <summary>
        /// Splits the paragraphs into lines: at mandatory breaks always, and for boxed text
        /// (maxWidth > 0) greedily at break opportunities (WORD: UAX #14, a word wider than
        /// the box overflows it; CHAR: any grapheme cluster). Spaces hang at line ends.
        /// </summary>
        public static void BreakLines(ShapedText shaped, in TextLayoutSettings settings, float maxWidth, List<LineRange> lines)
        {
            bool wrap = maxWidth > 0f;
            bool anyCluster = settings.Wrap == TextWrapMode.Char;
            float limit = maxWidth * (1f + 1e-5f) + 1e-4f;

            for (int paragraph = 0; paragraph < shaped.ParagraphCount; paragraph++)
            {
                var range = shaped.Paragraphs[paragraph];
                int lineStart = range.Start;
                int candidate = -1;
                float width = 0f;

                for (int cluster = range.Start; cluster < range.End;)
                {
                    int next = NextCluster(shaped, cluster);
                    if (cluster > lineStart)
                    {
                        var opportunity = shaped.Breaks[cluster];
                        if (opportunity == LineBreakOpportunity.Mandatory)
                        {
                            AddLine(shaped, lines, lineStart, cluster, paragraph);
                            lineStart = cluster;
                            candidate = -1;
                            width = 0f;
                        }
                        else if (wrap && (opportunity == LineBreakOpportunity.Allowed || anyCluster))
                        {
                            candidate = cluster;
                        }
                    }

                    float clusterWidth = ClusterWidth(shaped, settings, cluster, next);
                    if (wrap && cluster > lineStart && candidate > lineStart
                        && !shaped.IsTrailingSpace(cluster) && width + clusterWidth > limit)
                    {
                        AddLine(shaped, lines, lineStart, candidate, paragraph);
                        width = MeasureRange(shaped, settings, candidate, cluster);
                        lineStart = candidate;
                        candidate = -1;
                    }

                    width += clusterWidth;
                    cluster = next;
                }

                AddLine(shaped, lines, lineStart, range.End, paragraph);
            }
        }

        private static void AddLine(ShapedText shaped, List<LineRange> lines, int start, int end, int paragraph)
        {
            int visibleEnd = end;
            while (visibleEnd > start && shaped.IsTrailingSpace(visibleEnd - 1))
                visibleEnd--;
            lines.Add(new LineRange { Start = start, End = end, VisibleEnd = visibleEnd, Paragraph = paragraph });
        }

        private static int NextCluster(ShapedText shaped, int cluster)
        {
            int next = cluster + 1;
            while (next < shaped.Length && !shaped.ClusterStarts[next])
                next++;
            return next;
        }

        // Advance of one cluster in drawing units with spacing and synthetic weight; clusters
        // without glyphs (controls, ignorables) take no room.
        private static float ClusterWidth(ShapedText shaped, in TextLayoutSettings settings, int cluster, int next)
        {
            if (shaped.FirstGlyph[cluster] == shaped.FirstGlyph[next])
                return 0f;

            float width = shaped.ClusterAdvances[cluster] * settings.Size
                + shaped.ClusterInkGlyphs[cluster] * 2f * settings.Embolden
                + settings.LetterSpacing;
            if (shaped.CodePoints[cluster] == ' ')
                width += settings.WordSpacing;
            return width;
        }

        private static float MeasureRange(ShapedText shaped, in TextLayoutSettings settings, int start, int end)
        {
            float width = 0f;
            for (int cluster = start; cluster < end;)
            {
                int next = NextCluster(shaped, cluster);
                width += ClusterWidth(shaped, settings, cluster, next);
                cluster = next;
            }
            return width;
        }

        private static void PlaceLine(
            ShapedText shaped,
            in TextLayoutSettings settings,
            LineRange range,
            float x,
            TextBox? box,
            float baseline,
            TextLayoutResult result
        )
        {
            float width = MeasureRange(shaped, settings, range.Start, range.VisibleEnd);
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

            int firstGlyph = shaped.FirstGlyph[range.Start];
            int glyphCount = shaped.FirstGlyph[range.VisibleEnd] - firstGlyph;
            result.EnsureLine(range.VisibleEnd - range.Start, glyphCount);
            var order = result.VisualOrder;
            OrderGlyphs(shaped, range, firstGlyph, glyphCount, result);

            float size = settings.Size;
            float pen = left;
            for (int step = 0; step < glyphCount; step++)
            {
                ref var glyph = ref shaped.Glyphs[firstGlyph + order[step]];
                result.Glyphs.Add(new PlacedGlyph
                {
                    GlyphId = glyph.GlyphId,
                    Face = glyph.Face,
                    Cluster = glyph.Cluster,
                    PenX = pen + glyph.OffsetX * size + settings.Embolden,
                    Baseline = baseline - settings.YSign * glyph.OffsetY * size
                });

                pen += glyph.Advance * size;
                if (glyph.Advance != 0f)
                    pen += 2f * settings.Embolden;

                // Letter / word spacing once per cluster, after its last glyph on screen.
                bool clusterEnds = step == glyphCount - 1 || shaped.Glyphs[firstGlyph + order[step + 1]].Cluster != glyph.Cluster;
                if (clusterEnds)
                {
                    pen += settings.LetterSpacing;
                    if (shaped.CodePoints[glyph.Cluster] == ' ')
                        pen += settings.WordSpacing;
                }
            }

            line.GlyphCount = result.Glyphs.Count - line.GlyphStart;
            result.Lines.Add(line);
        }

        // Visual order of the glyphs of a line: logical for left-to-right paragraphs,
        // otherwise rules L1 and L2 on the levels of the glyph clusters.
        private static void OrderGlyphs(ShapedText shaped, LineRange range, int firstGlyph, int glyphCount, TextLayoutResult result)
        {
            var order = result.VisualOrder;
            var paragraph = shaped.Paragraphs[range.Paragraph];
            if (paragraph.PureLeftToRight)
            {
                for (int index = 0; index < glyphCount; index++)
                    order[index] = index;
                return;
            }

            var lineLevels = result.LineLevels;
            bool trailing = true;
            for (int index = range.VisibleEnd - 1; index >= range.Start; index--)
            {
                var reset = shaped.ResetClasses[index];
                byte level = shaped.Levels[index];
                if (reset == LineResetClass.Separator)
                {
                    level = (byte)paragraph.Level;
                    trailing = true;
                }
                else if (trailing && reset == LineResetClass.Whitespace)
                {
                    level = (byte)paragraph.Level;
                }
                else
                {
                    trailing = false;
                }
                lineLevels[index - range.Start] = level;
            }

            var glyphLevels = result.GlyphLevels;
            for (int index = 0; index < glyphCount; index++)
                glyphLevels[index] = lineLevels[shaped.Glyphs[firstGlyph + index].Cluster - range.Start];

            BidiParagraph.Reorder(new ReadOnlySpan<byte>(glyphLevels, 0, glyphCount), new Span<int>(order, 0, glyphCount));
        }
    }
}
