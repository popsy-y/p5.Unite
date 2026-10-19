using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Unite.Text.Fonts
{
    // Loading font files (loadFont) and the glyph atlas that belongs to a loaded font.
    public sealed partial class FontFace : IDisposable
    {
        /// <summary>Display name used in warnings; defaults to the file name.</summary>
        public string Name { get; private set; } = "Font";

        /// <summary>
        /// When on (the default), characters that none of the fonts given to TextFont()
        /// can draw are looked up in the fonts installed on the machine (desktop platforms;
        /// scanned and loaded in the background). Off, they draw as the primary font's
        /// .notdef glyph.
        /// </summary>
        public static bool SystemFontFallback { get; set; } = true;

        /// <summary>
        /// The glyph atlas pages baked for this font so far: R8 signed distance field
        /// textures (inside = 0, outside = 255), meant for inspection and debugging. Empty
        /// until text is drawn with the font; entries can be null for released pages.
        /// </summary>
        public IReadOnlyList<Texture2D> AtlasPages
        {
            get
            {
                if (!AtlasRegistry.TryGet(this, out var set))
                    return Array.Empty<Texture2D>();

                set.Flush();
                return set.PageTextures;
            }
        }

        /// <summary>
        /// Textures of atlas pages that currently hold no glyphs but are kept for reuse,
        /// indexed like <see cref="AtlasPages"/> (null where a page is in use or gone).
        /// For inspection and debugging.
        /// </summary>
        public IReadOnlyList<Texture2D> SpareAtlasPages
            => AtlasRegistry.TryGet(this, out var set) ? set.SpareTextures : Array.Empty<Texture2D>();

        /// <summary>
        /// Creates a font from raw font file bytes (.ttf, .otf, .woff or .woff2; static or
        /// variable). Throws when the bytes cannot be parsed.
        /// </summary>
        public static FontFace LoadFont(byte[] data, string name = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!FontLoader.TryLoad(data, out var face, out string error))
                throw new InvalidDataException($"p5.Unite: font '{name}' could not be parsed: {error}");

            if (!string.IsNullOrEmpty(name))
                face.Name = name;
            return face;
        }

        /// <summary>One font of a collection (.ttc) or a plain sfnt file; thread-safe (no Unity calls).</summary>
        internal static FontFace LoadCollectionMember(byte[] data, int collectionIndex, string name)
        {
            var face = Create(data, collectionIndex);
            if (!string.IsNullOrEmpty(name))
                face.Name = name;
            return face;
        }

        /// <summary>
        /// Loads a font file synchronously, like p5 loadFont(path). Relative paths are
        /// resolved against <see cref="Application.streamingAssetsPath"/>. Use
        /// <see cref="LoadFontAsync"/> for URLs and for platforms whose StreamingAssets
        /// are not plain files (Android, WebGL).
        /// </summary>
        public static FontFace LoadFont(string path, string name = null)
        {
            string fullPath = ResolvePath(path);
            if (fullPath.Contains("://"))
            {
                throw new NotSupportedException(
                    $"p5.Unite: '{path}' is not a local file. Use LoadFontAsync() for URLs and "
                    + "packed StreamingAssets."
                );
            }

            return LoadFont(File.ReadAllBytes(fullPath), name ?? Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>
        /// Loads a font file asynchronously through UnityWebRequest, like
        /// <c>await loadFont(path)</c> in p5.js. Accepts URLs, absolute paths and paths
        /// relative to <see cref="Application.streamingAssetsPath"/>. Throws when the
        /// download fails or the bytes cannot be parsed.
        /// </summary>
        public static async Awaitable<FontFace> LoadFontAsync(string path, string name = null)
        {
            string fullPath = ResolvePath(path);
            string url = fullPath.Contains("://") ? fullPath : new Uri(Path.GetFullPath(fullPath)).AbsoluteUri;

            using var request = UnityWebRequest.Get(url);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"p5.Unite: could not load font '{path}': {request.error}");

            return LoadFont(request.downloadHandler.data, name ?? Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>
        /// Releases the atlas textures baked for this font. The font stays usable: text
        /// drawn with it afterwards bakes its glyphs again.
        /// </summary>
        public void Dispose() => AtlasRegistry.Release(this);

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("p5.Unite: font path is empty.", nameof(path));

            if (path.Contains("://") || Path.IsPathRooted(path))
                return path;

            return Path.Combine(Application.streamingAssetsPath, path);
        }
    }
}
