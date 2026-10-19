using System;
using System.Collections.Generic;
using Unite.Canvas;
using UnityEngine;

namespace Unite.Drawing
{
    /// <summary>
    /// The draw commands of one frame and the data they point into: polygon points and
    /// glyph atlas textures. Commands are plain values that refer to that data by index,
    /// so the shape data is built from them by a Burst-compiled kernel.
    /// </summary>
    internal sealed class DrawCommandBuffer
    {
        private DrawCommand[] _items = new DrawCommand[256];
        private List<Vector4> _polygonPoints = new();
        private List<Texture> _atlases = new();

        public int Count { get; private set; }

        /// <summary>Command storage; only the first <see cref="Count"/> entries are used.</summary>
        public DrawCommand[] Items => _items;

        /// <summary>Points of every PolygonFill command, addressed by PolygonPointStart / PolygonPointCount.</summary>
        public List<Vector4> PolygonPoints => _polygonPoints;

        public void Add(in DrawCommand command)
        {
            if (Count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = command;
        }

        /// <summary>The value of <see cref="DrawCommand.GlyphAtlasIndex"/> for an atlas texture (1-based; 0 = none).</summary>
        public int AtlasIndex(Texture atlas)
        {
            if (atlas == null)
                return 0;

            // Consecutive glyphs mostly share a page, and a frame uses a few pages.
            for (int index = _atlases.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(_atlases[index], atlas))
                    return index + 1;
            }

            _atlases.Add(atlas);
            return _atlases.Count;
        }

        public Texture AtlasAt(int atlasIndex) => atlasIndex == 0 ? null : _atlases[atlasIndex - 1];

        public void Clear()
        {
            Count = 0;
            _polygonPoints.Clear();
            _atlases.Clear();
        }

        /// <summary>Exchanges the contents of two buffers (the recorded frame becomes the drawn one).</summary>
        public void SwapWith(DrawCommandBuffer other)
        {
            (_items, other._items) = (other._items, _items);
            (_polygonPoints, other._polygonPoints) = (other._polygonPoints, _polygonPoints);
            (_atlases, other._atlases) = (other._atlases, _atlases);
            (Count, other.Count) = (other.Count, Count);
        }
    }
}
