using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Registry of per-font-asset atlas sets, shared across every canvas so the same font
    /// asset pays for exactly one atlas regardless of scene setup. Statics are cleared on
    /// domain reload (and via <see cref="Clear"/>); sets rebuild lazily on first use.
    /// </summary>
    internal static class AtlasRegistry
    {
        private static readonly Dictionary<UniteFontAsset, GlyphAtlasSet> Sets = new();
        private static int _nextFontId;

        public static GlyphAtlasSet GetOrCreate(UniteFontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                throw new NullReferenceException(
                    "p5.Unite: no font asset provided. Call canvas.TextFont(...) first."
                );
            }

            if (Sets.TryGetValue(fontAsset, out var set) && set != null)
                return set;

            set = new GlyphAtlasSet(_nextFontId++);
            Sets[fontAsset] = set;
            return set;
        }

        /// <summary>Destroys every atlas page. Also used by tests and on domain reload.</summary>
        public static void Clear()
        {
            foreach (var set in Sets.Values)
                set?.Dispose();

            Sets.Clear();
            _nextFontId = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            Clear();
        }
    }
}
