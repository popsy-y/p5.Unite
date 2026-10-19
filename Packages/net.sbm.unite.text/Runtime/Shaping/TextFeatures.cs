using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unite.Text.Fonts.Layout;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// OpenType feature settings requested by the CSS-like text properties fontKerning,
    /// fontVariantLigatures and fontFeatureSettings (the latter wins, as in CSS).
    /// Instances are interned: equal settings share one object and one <see cref="Id"/>,
    /// which keys the shaping caches.
    /// </summary>
    internal sealed class TextFeatures
    {
        private static readonly Dictionary<string, TextFeatures> Interned = new(StringComparer.Ordinal);
        private static int _nextId;

        public static readonly TextFeatures Default = Intern(new List<(int, int)>());

        private TextFeatures(int id, (int Tag, int Value)[] settings)
        {
            Id = id;
            Settings = settings;
        }

        public int Id { get; }

        /// <summary>Feature tag and value (0 = off, 1 = on, n = alternate n).</summary>
        public (int Tag, int Value)[] Settings { get; }

        public static TextFeatures From(string kerning, string ligatures, string featureSettings)
        {
            if (string.IsNullOrEmpty(kerning) && string.IsNullOrEmpty(ligatures) && string.IsNullOrEmpty(featureSettings))
                return Default;

            var settings = new List<(int Tag, int Value)>();
            if (kerning != null && kerning.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                Set(settings, "kern", 0);

            if (!string.IsNullOrEmpty(ligatures))
                ApplyLigatures(settings, ligatures);

            if (!string.IsNullOrEmpty(featureSettings))
                ApplyFeatureSettings(settings, featureSettings);

            return Intern(settings);
        }

        // CSS font-variant-ligatures keywords.
        private static void ApplyLigatures(List<(int, int)> settings, string value)
        {
            foreach (string keyword in value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                switch (keyword.ToLowerInvariant())
                {
                    case "none":
                        Set(settings, "liga", 0);
                        Set(settings, "clig", 0);
                        Set(settings, "dlig", 0);
                        Set(settings, "hlig", 0);
                        Set(settings, "calt", 0);
                        break;
                    case "common-ligatures": Set(settings, "liga", 1); Set(settings, "clig", 1); break;
                    case "no-common-ligatures": Set(settings, "liga", 0); Set(settings, "clig", 0); break;
                    case "discretionary-ligatures": Set(settings, "dlig", 1); break;
                    case "no-discretionary-ligatures": Set(settings, "dlig", 0); break;
                    case "historical-ligatures": Set(settings, "hlig", 1); break;
                    case "no-historical-ligatures": Set(settings, "hlig", 0); break;
                    case "contextual": Set(settings, "calt", 1); break;
                    case "no-contextual": Set(settings, "calt", 0); break;
                }
            }
        }

        // CSS font-feature-settings: "liga" 0, 'smcp', "salt" 2, "ss01" on.
        private static void ApplyFeatureSettings(List<(int, int)> settings, string value)
        {
            foreach (string item in value.Split(','))
            {
                string entry = item.Trim();
                if (entry.Length < 4)
                    continue;

                int quote = entry[0] == '"' || entry[0] == '\'' ? 1 : 0;
                if (entry.Length < quote + 4)
                    continue;
                string tag = entry.Substring(quote, 4);
                string rest = entry.Substring(Math.Min(entry.Length, quote * 2 + 4)).Trim();

                int featureValue = 1;
                if (rest.Equals("off", StringComparison.OrdinalIgnoreCase))
                    featureValue = 0;
                else if (rest.Length > 0 && !rest.Equals("on", StringComparison.OrdinalIgnoreCase)
                    && !int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out featureValue))
                    continue;

                Set(settings, tag, Math.Max(0, featureValue));
            }
        }

        private static void Set(List<(int Tag, int Value)> settings, string tag, int value)
        {
            int key = OpenTypeData.Tag(tag.PadRight(4));
            for (int index = 0; index < settings.Count; index++)
            {
                if (settings[index].Tag == key)
                {
                    settings[index] = (key, value);
                    return;
                }
            }
            settings.Add((key, value));
        }

        private static TextFeatures Intern(List<(int Tag, int Value)> settings)
        {
            settings.Sort((left, right) => left.Tag.CompareTo(right.Tag));
            var key = new StringBuilder();
            foreach (var (tag, value) in settings)
                key.Append(tag).Append('=').Append(value).Append(';');

            string text = key.ToString();
            if (!Interned.TryGetValue(text, out var features))
            {
                features = new TextFeatures(_nextId++, settings.ToArray());
                Interned.Add(text, features);
            }
            return features;
        }
    }

    /// <summary>BCP 47 language tags (lang) → OpenType language system tags, for 'locl' and friends.</summary>
    internal static class LanguageTags
    {
        public static readonly int Default = OpenTypeData.Tag("dflt");

        private static readonly Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase)
        {
            ["af"] = "AFK ", ["am"] = "AMH ", ["ar"] = "ARA ", ["as"] = "ASM ", ["az"] = "AZE ", ["be"] = "BEL ",
            ["bg"] = "BGR ", ["bn"] = "BEN ", ["bo"] = "TIB ", ["ca"] = "CAT ", ["chr"] = "CHR ", ["cr"] = "CRE ",
            ["crh"] = "CRT ", ["cs"] = "CSY ", ["cy"] = "WEL ", ["da"] = "DAN ", ["de"] = "DEU ", ["dz"] = "DZN ",
            ["el"] = "ELL ", ["en"] = "ENG ", ["es"] = "ESP ", ["et"] = "ETI ", ["eu"] = "EUQ ", ["fa"] = "FAR ",
            ["fi"] = "FIN ", ["fil"] = "PIL ", ["fr"] = "FRA ", ["ga"] = "IRI ", ["gl"] = "GAL ", ["gu"] = "GUJ ",
            ["ha"] = "HAU ", ["he"] = "IWR ", ["hi"] = "HIN ", ["hr"] = "HRV ", ["hu"] = "HUN ", ["hy"] = "HYE ",
            ["id"] = "IND ", ["ig"] = "IBO ", ["is"] = "ISL ", ["it"] = "ITA ", ["iu"] = "INU ", ["ja"] = "JAN ",
            ["ka"] = "KAT ", ["kk"] = "KAZ ", ["km"] = "KHM ", ["kn"] = "KAN ", ["ko"] = "KOR ", ["ks"] = "KSH ",
            ["ku"] = "KUR ", ["ky"] = "KIR ", ["la"] = "LAT ", ["lo"] = "LAO ", ["lt"] = "LTH ", ["lv"] = "LVI ",
            ["mk"] = "MKD ", ["ml"] = "MAL ", ["mn"] = "MNG ", ["mo"] = "MOL ", ["mr"] = "MAR ", ["ms"] = "MLY ",
            ["mt"] = "MTS ", ["my"] = "BRM ", ["ne"] = "NEP ", ["nl"] = "NLD ", ["no"] = "NOR ", ["nb"] = "NOR ",
            ["nn"] = "NYN ", ["or"] = "ORI ", ["pa"] = "PAN ", ["pl"] = "PLK ", ["ps"] = "PAS ", ["pt"] = "PTG ",
            ["ro"] = "ROM ", ["ru"] = "RUS ", ["sa"] = "SAN ", ["sd"] = "SND ", ["si"] = "SNH ", ["sk"] = "SKY ",
            ["sl"] = "SLV ", ["sq"] = "SQI ", ["sr"] = "SRB ", ["sv"] = "SVE ", ["sw"] = "SWK ", ["ta"] = "TAM ",
            ["te"] = "TEL ", ["tg"] = "TAJ ", ["th"] = "THA ", ["ti"] = "TGY ", ["tk"] = "TKM ", ["tl"] = "PIL ",
            ["tr"] = "TRK ", ["tt"] = "TAT ", ["ug"] = "UYG ", ["uk"] = "UKR ", ["ur"] = "URD ", ["uz"] = "UZB ",
            ["vi"] = "VIT ", ["xh"] = "XHS ", ["yi"] = "JII ", ["yo"] = "YBA ", ["zu"] = "ZUL ",
            ["zh"] = "ZHS ", ["zh-hans"] = "ZHS ", ["zh-cn"] = "ZHS ", ["zh-sg"] = "ZHS ",
            ["zh-hant"] = "ZHT ", ["zh-tw"] = "ZHT ", ["zh-hk"] = "ZHH ", ["zh-mo"] = "ZHH "
        };

        private static string _lastLanguage;
        private static int _lastTag = Default;

        /// <summary>The OpenType tag of a language such as "ja", "zh-Hant" or "sr-Latn"; dflt when unknown or empty.</summary>
        public static int For(string language)
        {
            if (string.IsNullOrEmpty(language))
                return Default;
            if (language == _lastLanguage)
                return _lastTag;

            string normalized = language.Replace('_', '-');
            int tag = Default;
            if (Tags.TryGetValue(normalized, out string full))
            {
                tag = OpenTypeData.Tag(full);
            }
            else
            {
                int dash = normalized.IndexOf('-');
                if (dash > 0 && Tags.TryGetValue(normalized.Substring(0, dash), out string primary))
                    tag = OpenTypeData.Tag(primary);
            }

            _lastLanguage = language;
            _lastTag = tag;
            return tag;
        }
    }
}
