using System;
using Unite.Text.Fonts.Layout;

namespace Unite.Text.Shaping
{
    /// <summary>One glyph while shaping (logical order).</summary>
    internal struct GlyphInfo
    {
        public int Glyph;
        public int CodePoint;
        public int Cluster;

        /// <summary>Feature bits this glyph takes part in (global bit included).</summary>
        public uint Mask;

        public GlyphClass Class;
        public bool DefaultIgnorable;
        public bool Zwnj;
        public bool Zwj;

        /// <summary>Ligature id (0 = none) and 1-based component of a mark inside a ligature.</summary>
        public int LigatureId;
        public int LigatureComponent;

        /// <summary>Number of components of a ligature glyph (0 for others).</summary>
        public int LigatureComponents;

        /// <summary>Set by GSUB on glyphs it produced (keeps GDEF classes in sync).</summary>
        public bool Substituted;
    }

    /// <summary>Position of one glyph in font units, following HarfBuzz conventions (logical order).</summary>
    internal struct GlyphPosition
    {
        public float XAdvance;
        public float YAdvance;
        public float XOffset;
        public float YOffset;

        /// <summary>Relative index of the glyph this one is attached to (0 = none).</summary>
        public int AttachChain;

        /// <summary>1 = mark attachment, 2 = cursive attachment.</summary>
        public int AttachType;
    }

    /// <summary>
    /// The glyph sequence of one run while it is shaped: infos, positions and the
    /// insertion / deletion operations GSUB needs. Buffers are reused between runs.
    /// </summary>
    internal sealed class ShapingBuffer
    {
        public const int MarkAttachment = 1;
        public const int CursiveAttachment = 2;

        public GlyphInfo[] Info = new GlyphInfo[64];
        public GlyphPosition[] Positions = new GlyphPosition[64];
        public int Count;
        public bool RightToLeft;

        private int _nextLigatureId;

        public void Clear(bool rightToLeft)
        {
            Count = 0;
            RightToLeft = rightToLeft;
            _nextLigatureId = 0;
        }

        public void Add(in GlyphInfo info)
        {
            Ensure(Count + 1);
            Info[Count++] = info;
        }

        public int NewLigatureId()
        {
            _nextLigatureId = _nextLigatureId % 7 + 1; // HarfBuzz keeps ids small (3 bits)
            return _nextLigatureId;
        }

        /// <summary>Replaces the glyph at <paramref name="index"/> with <paramref name="count"/> copies of it (to be filled in).</summary>
        public void Expand(int index, int count)
        {
            if (count == 1)
                return;

            if (count == 0)
            {
                Remove(index, 1);
                return;
            }

            Ensure(Count + count - 1);
            Array.Copy(Info, index + 1, Info, index + count, Count - index - 1);
            for (int offset = 1; offset < count; offset++)
                Info[index + offset] = Info[index];
            Count += count - 1;
        }

        public void Remove(int index, int count)
        {
            Array.Copy(Info, index + count, Info, index, Count - index - count);
            Count -= count;
        }

        /// <summary>Gives every glyph in [start, end) the smallest cluster value among them.</summary>
        public void MergeClusters(int start, int end, ShapedText target)
        {
            if (end - start < 2)
                return;

            int min = int.MaxValue;
            int max = int.MinValue;
            for (int index = start; index < end; index++)
            {
                min = Math.Min(min, Info[index].Cluster);
                max = Math.Max(max, Info[index].Cluster);
            }
            if (min == max)
                return;

            // Neighbours that share the boundary clusters join too.
            while (start > 0 && Info[start - 1].Cluster == Info[start].Cluster)
                start--;
            while (end < Count && Info[end].Cluster == Info[end - 1].Cluster)
                end++;

            for (int index = start; index < end; index++)
                Info[index].Cluster = min;

            // The merged characters no longer start clusters of their own.
            for (int codePoint = min + 1; codePoint <= max; codePoint++)
                target.ClusterStarts[codePoint] = false;
        }

        public void ClearPositions()
        {
            Ensure(Count);
            Array.Clear(Positions, 0, Count);
        }

        private void Ensure(int capacity)
        {
            if (Info.Length >= capacity)
                return;

            int size = Math.Max(capacity, Info.Length * 2);
            Array.Resize(ref Info, size);
            Array.Resize(ref Positions, size);
        }
    }
}
