using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unite.Text
{
    /// <summary>
    /// Atlas for one font asset: fixed-size R8 pages preallocated up front (the
    /// p5.Unite-Text plan removes run-time enlargement), glyph entries keyed by
    /// (font id, glyph id, em bucket) and age accounting. Aged entries are dropped
    /// from the dictionary; a page is destroyed once no live entry remains. Glyph
    /// rects are never re-placed in place, so a frame's captured commands always find
    /// their atlas rects intact.
    /// </summary>
    internal sealed class GlyphAtlasSet : IDisposable
    {
        /// <summary>Row 0 = bottom; UV space matches the y-up atlas layout.</summary>
        internal const int PageSpan = 1024;

        /// <summary>A baked glyph inside the atlas.</summary>
        internal readonly struct CachedGlyph
        {
            public CachedGlyph(Texture2D texture, int x, int y, int width, int height, float range)
            {
                Texture = texture;
                X = x;
                Y = y;
                W = width;
                H = height;
                Range = range;
            }

            public Texture2D Texture { get; }
            public int X { get; }
            public int Y { get; }
            public int W { get; }
            public int H { get; }
            public float Range { get; }

            /// <summary>Sprite rect bottom-left in texture UVs.</summary>
            public Vector2 UvMin => new(X / (float)PageSpan, Y / (float)PageSpan);

            /// <summary>Sprite rect top-right in texture UVs.</summary>
            public Vector2 UvMax =>
                new((X + W) / (float)PageSpan, (Y + H) / (float)PageSpan);
        }

        private sealed class Entry
        {
            public int PageIndex;
            public int X;
            public int Y;
            public int W;
            public int H;
            public float Range;
            public int LastUsedFrame;
        }

        private const int SweepInterval = 512;
        private const int EvictAfterFrames = 1800;

        private readonly Dictionary<GlyphKey, Entry> _entries = new();
        private readonly List<SkylineAllocator> _pages = new();
        private readonly List<Texture2D> _pageTextures = new();
        private readonly List<int> _pageLiveCounts = new();

        private int _lastSweepFrame = -SweepInterval;

        public GlyphAtlasSet(int fontId)
        {
            FontId = fontId;
        }

        /// <summary>Registry-wide id; part of every cache key.</summary>
        public int FontId { get; }

        /// <summary>Number of live atlas pages (null slots for destroyed pages excluded).</summary>
        public int LivePageCount
        {
            get
            {
                int count = 0;
                foreach (var page in _pages)
                {
                    if (page != null) count++;
                }
                return count;
            }
        }

        public IReadOnlyList<Texture2D> PageTextures => _pageTextures;

        public bool TryGetCached(GlyphKey key, int frame, out CachedGlyph glyph)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.LastUsedFrame = frame;
                glyph = ToCachedGlyph(entry);
                return true;
            }

            glyph = default;
            return false;
        }

        /// <summary>
        /// Reserves space on a page, uploads the SDF bytes and caches the entry. The
        /// field buffer must hold width*height bytes (row 0 = bottom).
        /// </summary>
        public CachedGlyph Reserve(GlyphKey key, int frame, int width, int height, float range, byte[] field)
        {
            EnsureBuffersMatch(field, width, height);
            EvictIfNeeded(frame);

            int pageIndex = -1;
            int x = 0;
            int y = 0;

            for (int index = 0; index < _pages.Count; index++)
            {
                if (_pages[index] == null)
                    continue;

                if (_pages[index].TryPlace(width, height, out x, out y))
                {
                    pageIndex = index;
                    break;
                }
            }

            if (pageIndex < 0)
            {
                pageIndex = CreatePage();
                // Glyph rects are bounded well below PageSpan by the layout bucket clamps.
                if (!_pages[pageIndex].TryPlace(width, height, out x, out y))
                {
                    throw new ArgumentException(
                        $"p5.Unite: glyph rect {width}x{height} does not fit an atlas page."
                    );
                }
            }

            Upload(pageIndex, x, y, width, height, field);

            var entry = new Entry
            {
                PageIndex = pageIndex,
                X = x,
                Y = y,
                W = width,
                H = height,
                Range = range,
                LastUsedFrame = frame
            };
            _entries.Add(key, entry);
            _pageLiveCounts[pageIndex]++;
            return ToCachedGlyph(entry);
        }

        public void Dispose()
        {
            foreach (var texture in _pageTextures)
            {
                if (texture == null) continue;
#if UNITY_EDITOR
                Object.DestroyImmediate(texture);
#else
                Object.Destroy(texture);
#endif
            }
            _pageTextures.Clear();
            _pages.Clear();
            _pageLiveCounts.Clear();
            _entries.Clear();
        }

        /// <summary>Drops entries unused for EvictAfterFrames and destroys empty pages.</summary>
        private void EvictIfNeeded(int frame)
        {
            if (frame - _lastSweepFrame < SweepInterval)
                return;

            _lastSweepFrame = frame;

            List<GlyphKey> stale = null;
            foreach (var pair in _entries)
            {
                if (frame - pair.Value.LastUsedFrame > EvictAfterFrames)
                {
                    stale ??= new List<GlyphKey>();
                    stale.Add(pair.Key);
                }
            }

            if (stale == null)
                return;

            foreach (var key in stale)
            {
                var entry = _entries[key];
                _entries.Remove(key);
                _pageLiveCounts[entry.PageIndex]--;
            }

            // A page with no live glyphs frees its whole texture.
            for (int index = 0; index < _pages.Count; index++)
            {
                if (_pages[index] == null || _pageLiveCounts[index] > 0)
                    continue;

#if UNITY_EDITOR
                Object.DestroyImmediate(_pageTextures[index]);
#else
                Object.Destroy(_pageTextures[index]);
#endif
                _pageTextures[index] = null;
                _pages[index] = null;
            }
        }

        private int CreatePage()
        {
            // Reuse a destroyed-page slot when one exists.
            for (int index = 0; index < _pages.Count; index++)
            {
                if (_pages[index] == null)
                {
                    _pages[index] = new SkylineAllocator(PageSpan, PageSpan);
                    _pageTextures[index] = AllocatePageTexture();
                    _pageLiveCounts[index] = 0;
                    return index;
                }
            }

            _pages.Add(new SkylineAllocator(PageSpan, PageSpan));
            _pageTextures.Add(AllocatePageTexture());
            _pageLiveCounts.Add(0);
            return _pages.Count - 1;
        }

        private static Texture2D AllocatePageTexture()
        {
            var texture = new Texture2D(PageSpan, PageSpan, TextureFormat.R8, mipChain: false, linear: true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // R8 black maps to a fully-outside SDF (value 0), so untouched page area
            // never renders a glyph edge.
            texture.SetPixelData(new byte[PageSpan * PageSpan], 0);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        // Graphics.CopyTexture resolves GPU-side: a tiny staging texture per bake keeps
        // uploads proportional to the glyph rect, not the page.
        private void Upload(int pageIndex, int x, int y, int width, int height, byte[] field)
        {
            var staging = new Texture2D(width, height, TextureFormat.R8, mipChain: false, linear: true);
            staging.SetPixelData(field, 0);
            staging.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            Graphics.CopyTexture(staging, 0, 0, 0, 0, width, height, _pageTextures[pageIndex], 0, 0, x, y);
#if UNITY_EDITOR
            Object.DestroyImmediate(staging);
#else
            Object.Destroy(staging);
#endif
        }

        private static void EnsureBuffersMatch(byte[] field, int width, int height)
        {
            if (field == null || field.Length != width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: glyph field buffer must hold width * height bytes."
                );
            }
        }

        private CachedGlyph ToCachedGlyph(Entry entry) => new(
            _pageTextures[entry.PageIndex],
            entry.X,
            entry.Y,
            entry.W,
            entry.H,
            entry.Range
        );
    }
}
