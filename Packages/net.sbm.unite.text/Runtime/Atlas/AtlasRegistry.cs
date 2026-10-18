using System;
using System.Collections.Generic;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Registry of per-font atlas sets, shared across every canvas so the same font pays
    /// for exactly one atlas regardless of scene setup. Statics are cleared on domain
    /// reload (and via <see cref="Clear"/>); sets rebuild lazily on first use.
    /// </summary>
    internal static class AtlasRegistry
    {
        private static readonly Dictionary<FontFace, GlyphAtlasSet> Sets = new();
        private static int _nextFontId;
        private static bool _subscribed;

        public static GlyphAtlasSet GetOrCreate(FontFace font)
        {
            if (font == null)
            {
                throw new NullReferenceException(
                    "p5.Unite: no font provided. Call canvas.TextFont(...) first."
                );
            }

            if (Sets.TryGetValue(font, out var set) && set != null)
                return set;

            Subscribe();
            set = new GlyphAtlasSet(_nextFontId++);
            Sets[font] = set;
            return set;
        }

        /// <summary>The atlas set of a font, if one has been created by drawing text with it.</summary>
        public static bool TryGet(FontFace font, out GlyphAtlasSet set)
        {
            set = null;
            return font != null && Sets.TryGetValue(font, out set) && set != null;
        }

        /// <summary>Encodes and uploads every glyph queued this frame. Runs before the canvases capture their commands.</summary>
        public static void FlushAll()
        {
            foreach (var set in Sets.Values)
                set.Flush();
        }

        /// <summary>Destroys the atlas pages of one font.</summary>
        public static void Release(FontFace font)
        {
            if (font != null && Sets.TryGetValue(font, out var set))
            {
                set.Dispose();
                Sets.Remove(font);
            }
        }

        /// <summary>Destroys every atlas page. Also used by tests and on domain reload.</summary>
        public static void Clear()
        {
            foreach (var set in Sets.Values)
                set?.Dispose();

            Sets.Clear();
            _nextFontId = 0;
        }

        private static void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            UCanvas.FrameCapturing -= FlushAll;
            UCanvas.FrameCapturing += FlushAll;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            _subscribed = false;
            Clear();
        }
    }
}
