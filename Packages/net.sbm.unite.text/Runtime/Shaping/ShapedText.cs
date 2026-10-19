using System;
using Unite.Text.Unicode;

namespace Unite.Text.Shaping
{
    /// <summary>One glyph produced by shaping, in logical order. Metrics are in em (font units / unitsPerEm).</summary>
    internal struct ShapedGlyph
    {
        public int GlyphId;

        /// <summary>Index of the font in the text's font stack.</summary>
        public int Face;

        /// <summary>Code point index of the first character of the glyph's cluster.</summary>
        public int Cluster;

        public float Advance;
        public float OffsetX;
        public float OffsetY;
    }

    /// <summary>A paragraph of the text: code points [Start, End), separator included.</summary>
    internal struct ShapedParagraph
    {
        public int Start;
        public int End;

        /// <summary>Resolved paragraph embedding level (0 LTR, 1 RTL).</summary>
        public int Level;

        /// <summary>No character left level 0: lines are placed in logical order.</summary>
        public bool PureLeftToRight;
    }

    /// <summary>What the line rule L1 does with a character at the end of a line.</summary>
    internal enum LineResetClass : byte
    {
        None,

        /// <summary>Whitespace, isolate controls and X9-removed characters: reset when trailing.</summary>
        Whitespace,

        /// <summary>Segment and paragraph separators: always reset (and the whitespace before them).</summary>
        Separator
    }

    /// <summary>
    /// The font-independent analysis of one string in one paragraph direction: code
    /// points, paragraphs, bidi levels, line break opportunities, grapheme clusters and
    /// scripts. Cached per (string, direction); <see cref="Version"/> changes whenever the
    /// object is recycled for another string.
    /// </summary>
    internal sealed class TextAnalysis
    {
        public string Text;
        public TextDirectionMode Direction;
        public int Version;
        public int Length;
        public int[] CodePoints = Array.Empty<int>();
        public byte[] Levels = Array.Empty<byte>();
        public LineResetClass[] ResetClasses = Array.Empty<LineResetClass>();
        public UnicodeScript[] Scripts = Array.Empty<UnicodeScript>();

        /// <summary>Length + 1 entries: the opportunity before each code point.</summary>
        public LineBreakOpportunity[] Breaks = Array.Empty<LineBreakOpportunity>();

        /// <summary>Length + 1 entries: true where a grapheme cluster starts (and at the end).</summary>
        public bool[] GraphemeStarts = Array.Empty<bool>();

        public ShapedParagraph[] Paragraphs = Array.Empty<ShapedParagraph>();
        public int ParagraphCount;

        public void Reset(int length)
        {
            Version++;
            Length = length;
            ParagraphCount = 0;
            if (CodePoints.Length < length)
            {
                int size = Math.Max(length, 16);
                CodePoints = new int[size];
                Levels = new byte[size];
                ResetClasses = new LineResetClass[size];
                Scripts = new UnicodeScript[size];
            }

            if (Breaks.Length < length + 1)
            {
                int size = Math.Max(length + 1, 17);
                Breaks = new LineBreakOpportunity[size];
                GraphemeStarts = new bool[size];
            }
        }

        public void AddParagraph(in ShapedParagraph paragraph)
        {
            if (ParagraphCount == Paragraphs.Length)
                Array.Resize(ref Paragraphs, Math.Max(4, Paragraphs.Length * 2));
            Paragraphs[ParagraphCount++] = paragraph;
        }
    }

    /// <summary>
    /// A <see cref="TextAnalysis"/> shaped in one font instance: glyphs in logical order
    /// and per-cluster summaries for line fitting. Lines are fitted and glyphs placed from
    /// this every frame; it is cached per (string, direction, font instance) and its
    /// buffers are reused when evicted.
    /// </summary>
    internal sealed class ShapedText
    {
        public TextAnalysis Analysis;
        public int AnalysisVersion;

        /// <summary>Length + 1 entries: grapheme cluster starts, minus those merged by ligatures.</summary>
        public bool[] ClusterStarts = Array.Empty<bool>();

        /// <summary>Per cluster start: summed glyph advances (em) and the count of advancing glyphs.</summary>
        public float[] ClusterAdvances = Array.Empty<float>();
        public int[] ClusterInkGlyphs = Array.Empty<int>();

        /// <summary>Length + 1 entries: the first glyph whose cluster is at or after each code point.</summary>
        public int[] FirstGlyph = Array.Empty<int>();

        public ShapedGlyph[] Glyphs = Array.Empty<ShapedGlyph>();
        public int GlyphCount;

        /// <summary>True while the analysis this was shaped from has not been recycled.</summary>
        public bool IsCurrent => Analysis != null && Analysis.Version == AnalysisVersion;

        public int Length => Analysis.Length;
        public int[] CodePoints => Analysis.CodePoints;
        public byte[] Levels => Analysis.Levels;
        public LineResetClass[] ResetClasses => Analysis.ResetClasses;
        public LineBreakOpportunity[] Breaks => Analysis.Breaks;
        public ShapedParagraph[] Paragraphs => Analysis.Paragraphs;
        public int ParagraphCount => Analysis.ParagraphCount;

        public void Reset(TextAnalysis analysis)
        {
            Analysis = analysis;
            AnalysisVersion = analysis.Version;
            GlyphCount = 0;

            int length = analysis.Length;
            if (ClusterAdvances.Length < length)
            {
                int size = Math.Max(length, 16);
                ClusterAdvances = new float[size];
                ClusterInkGlyphs = new int[size];
            }
            if (ClusterStarts.Length < length + 1)
            {
                int size = Math.Max(length + 1, 17);
                ClusterStarts = new bool[size];
                FirstGlyph = new int[size];
            }
            Array.Copy(analysis.GraphemeStarts, ClusterStarts, length + 1);
        }

        public void AddGlyph(in ShapedGlyph glyph)
        {
            if (GlyphCount == Glyphs.Length)
                Array.Resize(ref Glyphs, Math.Max(16, Glyphs.Length * 2));
            Glyphs[GlyphCount++] = glyph;
        }

        /// <summary>True for the characters that hang at a soft line end and are not drawn there.</summary>
        public bool IsTrailingSpace(int index)
            => Analysis.CodePoints[index] == ' ' || Analysis.ResetClasses[index] != LineResetClass.None;
    }
}
