using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unite.Text
{
    /// <summary>
    /// Atlas for one font: fixed-size R8 pages, glyph entries keyed by (font id, glyph
    /// id, em bucket, instance) and age accounting. Glyph rects are never re-placed in
    /// place, so a frame's captured commands always find their atlas rects intact.
    /// <para>
    /// Glyphs of variable-font instances other than the default are "volatile": axis
    /// values that animate would otherwise fill the atlas with outdated glyphs, so
    /// those start on their own pages and expire a few frames after their last use. A
    /// volatile glyph that is still in use after <c>PromoteAfterFrames</c> (static text
    /// at a fixed weight, say) moves to the long-lived pages, so it cannot pin a page
    /// that animated glyphs keep refilling. A page whose entries have all expired is
    /// recycled instead of reallocated.
    /// </para>
    /// <para>
    /// New glyphs are placed immediately (the quad only needs the rect) and their pixels
    /// are encoded later by <see cref="Flush"/>, in parallel, once per frame before the
    /// canvas draws.
    /// </para>
    /// </summary>
    internal sealed partial class GlyphAtlasSet : IDisposable
    {
        /// <summary>Row 0 = bottom; UV space matches the y-up atlas layout.</summary>
        internal const int PageSpan = 1024;

        /// <summary>R8 value of a deep-outside SDF texel; fills untouched page area.</summary>
        internal const byte OutsideValue = 255;

        private const int SweepInterval = 512;
        private const int EvictAfterFrames = 1800;
        private const int VolatileEvictAfterFrames = 2;
        private const int PromoteAfterFrames = 24;
        private const int MaxSparePages = 2;

        /// <summary>A baked glyph inside the atlas.</summary>
        internal readonly struct CachedGlyph
        {
            public CachedGlyph(
                Texture2D texture,
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
                X = x;
                Y = y;
                W = width;
                H = height;
                Range = range;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public Texture2D Texture { get; }
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
            public Vector2 UvMin => new(X / (float)PageSpan, Y / (float)PageSpan);

            /// <summary>Sprite rect top-right in texture UVs.</summary>
            public Vector2 UvMax =>
                new((X + W) / (float)PageSpan, (Y + H) / (float)PageSpan);
        }

        private sealed class Page
        {
            public Texture2D Texture;
            public readonly SkylineAllocator Allocator = new(PageSpan, PageSpan);
            public int Live;
            public bool InUse;
            public bool Volatile;
            public bool Dirty;
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
        private readonly List<Page> _pages = new();
        private readonly List<Texture2D> _pageTextures = new();
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

        /// <summary>Number of live atlas pages (recycled and destroyed pages excluded).</summary>
        public int LivePageCount
        {
            get
            {
                int count = 0;
                foreach (var page in _pages)
                {
                    if (page.InUse) count++;
                }
                return count;
            }
        }

        /// <summary>Page textures by page index; null for pages that are not in use.</summary>
        public IReadOnlyList<Texture2D> PageTextures => _pageTextures;

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
            _entries.Clear();
            _volatileEntries.Clear();
            ClearPending();
        }

        /// <summary>
        /// Housekeeping after the frame's glyphs are baked: expires old entries, promotes
        /// volatile glyphs that stay in use and recycles pages without live glyphs. Pages
        /// referenced by this frame's commands are never recycled here, because entries
        /// expire several frames after their last use.
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
                    ReleasePage(page);
            }
        }

        private void CollectStale(Dictionary<GlyphKey, Entry> entries, int frame, int maxAge)
        {
            foreach (var pair in entries)
            {
                if (frame - pair.Value.LastUsedFrame > maxAge)
                    _stale.Add(pair.Key);
            }
        }

        private void ReleasePage(Page page)
        {
            page.InUse = false;
            _pageTextures[_pages.IndexOf(page)] = null;

            int spare = 0;
            foreach (var other in _pages)
            {
                if (!other.InUse && other.Texture != null)
                    spare++;
            }

            if (spare > MaxSparePages)
            {
                DestroyTexture(page.Texture);
                page.Texture = null;
            }
        }

        private Page AcquirePage(bool isVolatile)
        {
            Page page = null;

            // A spare page keeps its texture; reuse it before allocating another.
            foreach (var candidate in _pages)
            {
                if (!candidate.InUse && candidate.Texture != null)
                {
                    page = candidate;
                    break;
                }
            }

            if (page == null)
            {
                foreach (var candidate in _pages)
                {
                    if (!candidate.InUse)
                    {
                        page = candidate;
                        break;
                    }
                }

                if (page == null)
                {
                    page = new Page();
                    _pages.Add(page);
                    _pageTextures.Add(null);
                }

                page.Texture = new Texture2D(PageSpan, PageSpan, TextureFormat.R8, mipChain: false, linear: true)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            page.InUse = true;
            page.Volatile = isVolatile;
            page.Live = 0;
            page.Allocator.Reset();
            ClearPage(page);
            _pageTextures[_pages.IndexOf(page)] = page.Texture;
            return page;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;
#if UNITY_EDITOR
            Object.DestroyImmediate(texture);
#else
            Object.Destroy(texture);
#endif
        }

        private CachedGlyph ToCachedGlyph(Entry entry) => new(
            entry.Page.Texture,
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
