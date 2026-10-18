using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unite.Text
{
    // Page textures: acquisition, recycling and the spare pool.
    internal sealed partial class GlyphAtlasSet
    {
        // A page without live glyphs keeps its texture this long before it is destroyed,
        // so the page count can rise and fall from frame to frame without allocations.
        private const int SpareKeepFrames = 600;

        private sealed class Page
        {
            public Page(int span)
            {
                Span = span;
                Allocator = new SkylineAllocator(span, span);
            }

            public int Span { get; }
            public SkylineAllocator Allocator { get; }
            public Texture2D Texture;
            public int Live;
            public bool InUse;
            public bool Volatile;
            public bool Dirty;
            public int ReleasedFrame;
        }

        private static readonly Dictionary<int, byte[]> ClearFields = new();

        private readonly List<Page> _pages = new();
        private readonly List<Texture2D> _pageTextures = new();
        private readonly List<Texture2D> _spareTextures = new();

        /// <summary>Number of pages in use (spare pages excluded).</summary>
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

        /// <summary>Spare textures by page index (kept for reuse); null for pages in use or destroyed.</summary>
        public IReadOnlyList<Texture2D> SpareTextures => _spareTextures;

        /// <summary>The page side a glyph rect needs: regular pages, or large ones for the biggest buckets.</summary>
        internal static int SpanFor(int width, int height)
            => width > PageSpan || height > PageSpan ? LargePageSpan : PageSpan;

        private void ReleasePage(Page page, int frame)
        {
            page.InUse = false;
            page.ReleasedFrame = frame;
            int index = _pages.IndexOf(page);
            _pageTextures[index] = null;
            _spareTextures[index] = page.Texture;
        }

        private void DestroyIdleSpares(int frame)
        {
            for (int index = 0; index < _pages.Count; index++)
            {
                var page = _pages[index];
                if (!page.InUse && page.Texture != null && frame - page.ReleasedFrame > SpareKeepFrames)
                {
                    DestroyTexture(page.Texture);
                    page.Texture = null;
                    _spareTextures[index] = null;
                }
            }
        }

        private Page AcquirePage(bool isVolatile, int span)
        {
            Page page = null;

            // A spare page keeps its texture; reuse it before allocating another.
            foreach (var candidate in _pages)
            {
                if (!candidate.InUse && candidate.Texture != null && candidate.Span == span)
                {
                    page = candidate;
                    break;
                }
            }

            if (page == null)
            {
                int slot = -1;
                for (int index = 0; index < _pages.Count; index++)
                {
                    if (!_pages[index].InUse && _pages[index].Texture == null)
                    {
                        slot = index;
                        break;
                    }
                }

                page = new Page(span);
                if (slot >= 0)
                {
                    _pages[slot] = page;
                }
                else
                {
                    _pages.Add(page);
                    _pageTextures.Add(null);
                    _spareTextures.Add(null);
                }

                page.Texture = new Texture2D(span, span, TextureFormat.R8, mipChain: false, linear: true)
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
            int slotIndex = _pages.IndexOf(page);
            _pageTextures[slotIndex] = page.Texture;
            _spareTextures[slotIndex] = null;
            return page;
        }

        // Untouched page area must read as outside: bilinear sampling at a sprite rect's
        // border mixes in the neighboring texel, and a 0 neighbor lands on the 128 edge
        // threshold, drawing a thin line along the glyph quad.
        private void ClearPage(Page page)
        {
            if (!ClearFields.TryGetValue(page.Span, out var clear))
            {
                clear = new byte[page.Span * page.Span];
                Array.Fill(clear, OutsideValue);
                ClearFields.Add(page.Span, clear);
            }

            NativeArray<byte>.Copy(clear, page.Texture.GetRawTextureData<byte>());
            MarkDirty(page);
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
    }
}
