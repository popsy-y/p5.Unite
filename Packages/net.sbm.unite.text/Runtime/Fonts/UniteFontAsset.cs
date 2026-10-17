using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// A font asset: a serialized font file (as a <see cref="TextAsset"/>) plus the lazily
    /// parsed glyph data. Create one via Create > p5.Unite > Unite Font and assign a
    /// TextAsset holding the raw font bytes (drop a .ttf/.otf into the project renamed to
    /// .bytes, or load the bytes from StreamingAssets at runtime into a TextAsset).
    /// One asset represents one font family for the atlas cache.
    /// </summary>
    [CreateAssetMenu(fileName = "UniteFont", menuName = "p5.Unite/Unite Font")]
    public sealed class UniteFontAsset : ScriptableObject
    {
        [SerializeField] private TextAsset _fontFile;

        private TtfFont _font;
        private bool _warnedNoFile;
        private bool _warnedParseFailure;
        private float _nextRetryTime;

        /// <summary>
        /// The serialized font file. Direct face queries go through
        /// <see cref="TryGetFont"/>, which parses lazily and caches the result.
        /// </summary>
        public TextAsset FontFile => _fontFile;

        /// <summary>
        /// Display name used for warnings and grouping; defaults to the file asset name.
        /// </summary>
        public string FamilyName => _fontFile != null ? _fontFile.name : name;

        /// <summary>
        /// Returns the parsed font, parsing the serialized bytes on first use. A failed
        /// parse is retried after a short cooldown (so fixing the asset or reloading
        /// scripts recovers without an editor restart) and warns only once per failure.
        /// </summary>
        public bool TryGetFont(out TtfFont font)
        {
            if (_font == null && Time.realtimeSinceStartup >= _nextRetryTime)
            {
                // Cooldown so a permanently broken file is not re-parsed every frame.
                _nextRetryTime = Time.realtimeSinceStartup + 2f;

                if (_fontFile == null)
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
                else if (!TtfFont.TryParse(_fontFile.bytes, out _font))
                {
                    _font = null;
                    if (!_warnedParseFailure)
                    {
                        _warnedParseFailure = true;
                        Debug.LogWarning(
                            $"p5.Unite: font '{FamilyName}' could not be parsed. p5.Unite Text "
                            + "currently supports TrueType-outline fonts (.ttf, or .otf with glyf "
                            + "data); CFF-flavored OTF and web font wrappers are not supported yet.",
                            this
                        );
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
    }
}
