using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Bottom-left skyline rectangle packer for the atlas pages. Rects are only ever
    /// added; space of dropped glyph entries is reclaimed by destroying whole pages once
    /// they hold no live glyphs, so no free-list bookkeeping exists here (revisit with
    /// compaction per the p5.Unite-Text plan eventually).
    /// </summary>
    internal sealed class SkylineAllocator
    {
        private struct Node
        {
            public int X;      // left edge of the shelf segment
            public int Height; // skyline height over [X, next node's X)
        }

        private readonly List<Node> _nodes = new() { new Node { X = 0, Height = 0 } };
        private List<Node> _spare = new();

        public SkylineAllocator(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
        public long UsedArea { get; private set; }

        /// <summary>Forgets every placed rect: the whole page is free again.</summary>
        public void Reset()
        {
            _nodes.Clear();
            _nodes.Add(new Node { X = 0, Height = 0 });
            UsedArea = 0;
        }

        /// <summary>
        /// Places a rect at the (lowest, then left-most) skyline spot. (x, y) is the
        /// rect's bottom-left corner. Returns false when nothing fits.
        /// </summary>
        public bool TryPlace(int width, int height, out int x, out int y)
        {
            x = -1;
            y = -1;

            if (width <= 0 || height <= 0 || width > Width || height > Height)
                return false;

            int bestX = -1;
            int bestY = int.MaxValue;

            for (int node = 0; node < _nodes.Count; node++)
            {
                int left = _nodes[node].X;
                if (left + width > Width)
                    break;

                int level = LevelAt(node, width);
                if (level + height > Height)
                    continue;

                if (level < bestY || (level == bestY && left < bestX))
                {
                    bestY = level;
                    bestX = left;
                }
            }

            if (bestX < 0)
                return false;

            x = bestX;
            y = bestY;
            UsedArea += (long)width * height;
            RaiseSkyline(bestX, bestY + height, width);
            return true;
        }

        // Highest skyline height under [node.X, node.X + width).
        private int LevelAt(int nodeIndex, int width)
        {
            int level = _nodes[nodeIndex].Height;
            int end = _nodes[nodeIndex].X + width;

            for (int index = nodeIndex + 1; index < _nodes.Count && _nodes[index].X < end; index++)
                level = Mathf.Max(level, _nodes[index].Height);

            return level;
        }

        // Lifts the skyline over [x, x + width) to top, splitting segments as needed.
        private void RaiseSkyline(int x, int top, int width)
        {
            int end = x + width;
            var replaced = _spare;
            replaced.Clear();

            for (int index = 0; index < _nodes.Count; index++)
            {
                int segmentStart = _nodes[index].X;
                int segmentEnd = index + 1 < _nodes.Count ? _nodes[index + 1].X : Width;
                int height = _nodes[index].Height;

                // Disjoint segments keep their original span untouched.
                if (segmentEnd <= x || segmentStart >= end)
                {
                    replaced.Add(new Node { X = segmentStart, Height = height });
                    continue;
                }

                if (segmentStart < x)
                {
                    replaced.Add(new Node { X = segmentStart, Height = height });
                }

                // The covered span becomes a segment at the new height.
                int middleStart = segmentStart > x ? segmentStart : x;
                int middleEnd = segmentEnd < end ? segmentEnd : end;
                if (middleStart < middleEnd)
                {
                    replaced.Add(new Node { X = middleStart, Height = top });
                }

                if (segmentEnd > end)
                {
                    replaced.Add(new Node { X = end, Height = height });
                }
            }

            // Merge adjacent segments that share the same skyline height.
            _nodes.Clear();
            int previousHeight = int.MinValue;

            foreach (var node in replaced)
            {
                if (node.Height == previousHeight)
                    continue;

                _nodes.Add(node);
                previousHeight = node.Height;
            }

            _spare = replaced;
        }
    }
}

// skyline fix r2: disjoint segments keep their spans
