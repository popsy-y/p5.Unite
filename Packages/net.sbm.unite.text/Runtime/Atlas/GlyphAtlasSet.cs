using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Atlas for one font: R8 pages, glyph entries keyed by (font id, glyph id, em
    /// bucket, instance) and age accounting. Glyph rects are never re-placed in place, so
    /// a frame's captured commands always find their atlas rects intact.
    /// <para>
    /// Glyphs of variable-font instances other than the default are "volatile": axis
    /// values that animate would otherwise fill the atlas with outdated glyphs, so
    /// those start on their own pages and expire a few frames after their last use. A
    /// volatile glyph that is still in use after <c>PromoteAfterFrames</c> (static text
    /// at a fixed weight, say) moves to the long-lived pages, so it cannot pin a page
    /// that animated glyphs keep refilling. A page whose entries have all expired keeps
    /// its texture as a spare for a while, so pages that come and go reuse textures
    /// instead of allocating them.
    /// </para>
    /// <para>
    /// New glyphs are placed immediately (the quad only needs the rect) and their pixels
    /// are encoded later by <see cref="Flush"/>, in parallel, once per frame before the
    /// canvas draws.
    /// </para>
    /// </summary>
    internal sealed partial class GlyphAtlasSet : IDisposable
    {
        /// <summary>Side of a regular page. Row 0 = bottom; UV space matches the y-up atlas layout.</summary>
        internal const int PageSpan = 1024;

        /// <summary>Side of the pages that hold glyph rects too large for a regular page (1024 px buckets).</summary>
        internal const int LargePageSpan = 2048;

        /// <summary>R8 value of a deep-outside SDF texel; fills untouched page area.</summary>
        internal const byte OutsideValue = 255;

        private const int SweepInterval = 512;
        private const int EvictAfterFrames = 1800;
        private const int VolatileEvictAfterFrames = 2;
        private const int PromoteAfterFrames = 24;

        /// <summary>A baked glyph inside the atlas.</summary>
        internal readonly struct CachedGlyph
        {
            public CachedGlyph(
                Texture2D texture,
                int span,
                int x,
                int y,
                int width,
                int height,
                float range,
                float offsetX,
                float offsetY
            )
            {
                Texture = texture;
                Span = span;
                X = x;
                Y = y;
                W = width;
                H = height;
                Range = range;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public Texture2D Texture { get; }

            /// <summary>Side of the page texture in texels.</summary>
            public int Span { get; }

            public int X { get; }
            public int Y { get; }
            public int W { get; }
            public int H { get; }
            public float Range { get; }

            /// <summary>
            /// Pen-relative sprite rect origin in baked pixels: X along the writing
            /// direction, Y up from the baseline (font orientation, y-up).
            /// </summary>
            public float OffsetX { get; }
            public float OffsetY { get; }

            /// <summary>Sprite rect bottom-left in texture UVs.</summary>
            public Vector2 UvMin => new(X / (float)Span, Y / (float)Span);

            /// <summary>Sprite rect top-right in texture UVs.</summary>
            public Vector2 UvMax => new((X + W) / (float)Span, (Y + H) / (float)Span);
        }

        private sealed class Entry
        {
            public Page Page;
            public int X;
            public int Y;
            public int W;
            public int H;
            public float Range;
            public float OffsetX;
            public float OffsetY;
            public int CreatedFrame;
            public int LastUsedFrame;
            public bool Volatile;
        }

        private readonly Dictionary<GlyphKey, Entry> _entries = new();
        private readonly Dictionary<GlyphKey, Entry> _volatileEntries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly List<GlyphKey> _stale = new();
        private readonly List<KeyValuePair<GlyphKey, Entry>> _promote = new();

        private int _currentFrame;
        private int _lastSweepFrame = -SweepInterval;

        public GlyphAtlasSet(int fontId)
        {
            FontId = fontId;
        }

        /// <summary>Registry-wide id; part of every cache key.</summary>
        public int FontId { get; }

        public bool TryGetCached(GlyphKey key, int frame, out CachedGlyph glyph)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.LastUsedFrame = frame;
                _currentFrame = frame;
                glyph = ToCachedGlyph(entry);
                return true;
            }

            glyph = default;
            return false;
        }

        public void Dispose()
        {
            foreach (var page in _pages)
                DestroyTexture(page.Texture);

            _pages.Clear();
            _pageTextures.Clear();
            _spareTextures.Clear();
            _entries.Clear();
            _volatileEntries.Clear();
            ClearPending();
        }

        /// <summary>
        /// Housekeeping after the frame's glyphs are baked: expires old entries, promotes
        /// volatile glyphs that stay in use and recycles pages without live glyphs. Pages
        /// referenced by this frame's commands are never recycled here, because entries
        /// expire frames after their last use.
        /// </summary>
        private void Maintain()
        {
            int frame = _currentFrame;

            if (_volatileEntries.Count > 0)
            {
                CollectStale(_volatileEntries, frame, VolatileEvictAfterFrames);
                foreach (var pair in _volatileEntries)
                {
                    if (frame - pair.Value.CreatedFrame >= PromoteAfterFrames
                        && frame - pair.Value.LastUsedFrame <= 1)
                        _promote.Add(pair);
                }
            }

            if (frame - _lastSweepFrame >= SweepInterval)
            {
                _lastSweepFrame = frame;
                CollectStale(_entries, frame, EvictAfterFrames);
            }

            foreach (var key in _stale)
            {
                if (!_entries.TryGetValue(key, out var entry))
                    continue;

                _entries.Remove(key);
                _volatileEntries.Remove(key);
                entry.Page.Live--;
                entry.Page = null;
                _entryPool.Push(entry);
            }
            _stale.Clear();

            foreach (var pair in _promote)
            {
                if (_entries.ContainsKey(pair.Key))
                    Promote(pair.Key, pair.Value);
            }
            _promote.Clear();

            foreach (var page in _pages)
            {
                if (page.InUse && page.Live <= 0)
                    ReleasePage(page, frame);
            }

            DestroyIdleSpares(frame);
        }

        private void CollectStale(Dictionary<GlyphKey, Entry> entries, int frame, int maxAge)
        {
            foreach (var pair in entries)
            {
                if (frame - pair.Value.LastUsedFrame > maxAge)
                    _stale.Add(pair.Key);
            }
        }

        private Entry RentEntry() => _entryPool.Count > 0 ? _entryPool.Pop() : new Entry();

        private CachedGlyph ToCachedGlyph(Entry entry) => new(
            entry.Page.Texture,
            entry.Page.Span,
            entry.X,
            entry.Y,
            entry.W,
            entry.H,
            entry.Range,
            entry.OffsetX,
            entry.OffsetY
        );
    }
}
