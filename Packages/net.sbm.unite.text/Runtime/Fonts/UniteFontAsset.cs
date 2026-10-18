using System;
using System.Collections.Generic;
using System.IO;
using Unite.Text.Fonts;
using UnityEngine;
using UnityEngine.Networking;

namespace Unite.Text
{
    /// <summary>
    /// A font asset (p5.Font): a font file plus the lazily parsed glyph data. Create one
    /// via Create > p5.Unite > Unite Font and assign a TextAsset holding the raw font
    /// bytes (a .ttf/.otf renamed to .bytes), or load a font file at runtime with
    /// <see cref="LoadFont(string, string)"/> / <see cref="LoadFontAsync"/>.
    /// One asset represents one font family for the atlas cache.
    /// </summary>
    [CreateAssetMenu(fileName = "UniteFont", menuName = "p5.Unite/Unite Font")]
    public sealed partial class UniteFontAsset : ScriptableObject
    {
        [SerializeField] private TextAsset _fontFile;

        // Bytes of a font loaded at runtime (LoadFont); never serialized.
        private byte[] _runtimeData;

        private FontFace _font;
        private bool _warnedNoFile;
        private bool _warnedParseFailure;
        private float _nextRetryTime;

        /// <summary>
        /// The serialized font file (null for fonts loaded at runtime). Direct face
        /// queries go through <see cref="TryGetFont"/>, which parses lazily and caches
        /// the result.
        /// </summary>
        public TextAsset FontFile => _fontFile;

        /// <summary>
        /// The glyph atlas pages baked for this font so far: R8 signed distance field
        /// textures (inside = 0, outside = 255), meant for inspection and debugging. Empty
        /// until text is drawn with the font; entries can be null for released pages.
        /// </summary>
        public IReadOnlyList<Texture2D> AtlasPages
            => AtlasRegistry.TryGet(this, out var set) ? set.PageTextures : Array.Empty<Texture2D>();

        /// <summary>
        /// Display name used for warnings and grouping; defaults to the file asset name.
        /// </summary>
        public string FamilyName => _fontFile != null ? _fontFile.name : name;

        /// <summary>
        /// Creates a font from raw font file bytes (.ttf, .otf, .woff or .woff2; static or
        /// variable). Throws when the bytes cannot be parsed.
        /// </summary>
        public static UniteFontAsset LoadFont(byte[] data, string name = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!FontLoader.TryLoad(data, out var font, out string error))
                throw new InvalidDataException($"p5.Unite: font '{name}' could not be parsed: {error}");

            var asset = CreateInstance<UniteFontAsset>();
            asset.name = string.IsNullOrEmpty(name) ? "UniteFont" : name;
            asset._runtimeData = data;
            asset._font = font;
            return asset;
        }

        /// <summary>
        /// Loads a font file synchronously, like p5 loadFont(path). Relative paths are
        /// resolved against <see cref="Application.streamingAssetsPath"/>. Use
        /// <see cref="LoadFontAsync"/> for URLs and for platforms whose StreamingAssets
        /// are not plain files (Android, WebGL).
        /// </summary>
        public static UniteFontAsset LoadFont(string path, string name = null)
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
        public static async Awaitable<UniteFontAsset> LoadFontAsync(string path, string name = null)
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
        /// Returns the parsed font, parsing the font bytes on first use. A failed parse
        /// is retried after a short cooldown (so fixing the asset or reloading scripts
        /// recovers without an editor restart) and warns only once per failure.
        /// </summary>
        public bool TryGetFont(out FontFace font)
        {
            if (_font == null && Time.realtimeSinceStartup >= _nextRetryTime)
            {
                // Cooldown so a permanently broken file is not re-parsed every frame.
                _nextRetryTime = Time.realtimeSinceStartup + 2f;

                byte[] data = _fontFile != null ? _fontFile.bytes : _runtimeData;
                if (data == null)
                {
                    if (!_warnedNoFile)
                    {
                        _warnedNoFile = true;
                        Debug.LogWarning(
                            "p5.Unite: UniteFontAsset has no font file assigned.",
                            this
                        );
                    }
                }
                else if (!FontLoader.TryLoad(data, out _font, out string error))
                {
                    _font = null;
                    if (!_warnedParseFailure)
                    {
                        _warnedParseFailure = true;
                        Debug.LogWarning($"p5.Unite: font '{FamilyName}' could not be parsed: {error}", this);
                    }
                }
                else
                {
                    // Recovered from an earlier failure: allow a fresh warning if it
                    // ever breaks again.
                    _warnedParseFailure = false;
                }
            }

            font = _font;
            return font != null;
        }

        /// <summary>
        /// The font at the given variation axis values (for variable fonts; static fonts
        /// always return their single instance). Returns false while the font cannot be
        /// loaded.
        /// </summary>
        public bool TryGetInstance(IReadOnlyList<AxisSetting> axes, out FontInstance instance)
        {
            if (!TryGetFont(out var font))
            {
                instance = null;
                return false;
            }

            instance = font.GetInstance(axes);
            return true;
        }

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
