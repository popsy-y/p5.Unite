using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    public static partial class TextCanvasExtensions
    {
        private static readonly Regex AxisPattern = new(@"[""']?([A-Za-z0-9 ]{4})[""']?\s+(-?[0-9]*\.?[0-9]+)", RegexOptions.Compiled);

        // Canonical property names (p5.js / canvas text property spelling) and aliases.
        private static readonly Dictionary<string, string> PropertyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["textFont"] = "textFont",
            ["fontFamily"] = "textFont",
            ["textSize"] = "textSize",
            ["fontSize"] = "textSize",
            ["textLeading"] = "textLeading",
            ["lineHeight"] = "textLeading",
            ["textAlign"] = "textAlign",
            ["textBaseline"] = "textBaseline",
            ["textStyle"] = "textStyle",
            ["fontStyle"] = "textStyle",
            ["textWeight"] = "textWeight",
            ["fontWeight"] = "textWeight",
            ["textWrap"] = "textWrap",
            ["direction"] = "direction",
            ["textDirection"] = "direction",
            ["letterSpacing"] = "letterSpacing",
            ["wordSpacing"] = "wordSpacing",
            ["fontVariationSettings"] = "fontVariationSettings"
        };

        /// <summary>
        /// Sets one text property by name, like p5 textProperty(prop, value). Supported:
        /// textFont, textSize (fontSize), textLeading (lineHeight), textAlign,
        /// textBaseline, textStyle (fontStyle), textWeight (fontWeight), textWrap,
        /// direction (textDirection), letterSpacing, wordSpacing and
        /// fontVariationSettings (a CSS-like string such as "wght" 700, "wdth" 75, or an
        /// IEnumerable of <see cref="AxisSetting"/>). Enum values accept the enum or its
        /// name; sizes and spacings are in drawing units.
        /// </summary>
        public static void TextProperty(this UCanvas canvas, string property, object value)
        {
            if (!TryResolveProperty(property, out string key))
                return;

            var state = State(canvas);
            try
            {
                switch (key)
                {
                    case "textFont":
                        state.Font = (FontFace)value;
                        break;
                    case "textSize":
                        canvas.TextSize(Convert.ToSingle(value));
                        break;
                    case "textLeading":
                        state.Leading = Convert.ToSingle(value);
                        break;
                    case "textAlign":
                        state.Align = ToEnum<TextAlignMode>(value);
                        break;
                    case "textBaseline":
                        state.VerticalAlign = ToEnum<TextVerticalAlignMode>(value);
                        break;
                    case "textStyle":
                        state.Style = ToEnum<TextStyleMode>(value);
                        break;
                    case "textWeight":
                        state.Weight = Convert.ToSingle(value);
                        break;
                    case "textWrap":
                        state.Wrap = ToEnum<TextWrapMode>(value);
                        break;
                    case "direction":
                        state.Direction = ToEnum<TextDirectionMode>(value);
                        break;
                    case "letterSpacing":
                        state.LetterSpacing = Convert.ToSingle(value);
                        break;
                    case "wordSpacing":
                        state.WordSpacing = Convert.ToSingle(value);
                        break;
                    case "fontVariationSettings":
                        state.Variations = ToAxisSettings(value);
                        break;
                }
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException or ArgumentException or OverflowException)
            {
                Debug.LogWarning($"p5.Unite: textProperty '{property}' cannot take the value '{value}'.");
            }
        }

        /// <summary>Returns one text property by name (null for unsupported names).</summary>
        public static object TextProperty(this UCanvas canvas, string property)
        {
            if (!TryResolveProperty(property, out string key))
                return null;

            return canvas.TextProperties()[key];
        }

        /// <summary>Sets several text properties at once (p5 textProperties(object)).</summary>
        public static void TextProperties(this UCanvas canvas, IReadOnlyDictionary<string, object> properties)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            foreach (var pair in properties)
                canvas.TextProperty(pair.Key, pair.Value);
        }

        /// <summary>Returns every supported text property and its current value.</summary>
        public static Dictionary<string, object> TextProperties(this UCanvas canvas)
        {
            var state = State(canvas);
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["textFont"] = state.Font,
                ["textSize"] = state.Size,
                ["textLeading"] = state.EffectiveLeading,
                ["textAlign"] = state.Align,
                ["textBaseline"] = state.VerticalAlign,
                ["textStyle"] = state.Style,
                ["textWeight"] = state.Weight,
                ["textWrap"] = state.Wrap,
                ["direction"] = state.Direction,
                ["letterSpacing"] = state.LetterSpacing,
                ["wordSpacing"] = state.WordSpacing,
                ["fontVariationSettings"] = state.Variations ?? Array.Empty<AxisSetting>()
            };
        }

        private static bool TryResolveProperty(string property, out string key)
        {
            if (property != null && PropertyAliases.TryGetValue(property, out key))
                return true;

            key = null;
            Debug.LogWarning($"p5.Unite: textProperty '{property}' is not supported.");
            return false;
        }

        // Accepts a CSS font-variation-settings string or axis settings; null / empty clears.
        private static AxisSetting[] ToAxisSettings(object value)
        {
            var settings = new List<AxisSetting>();
            switch (value)
            {
                case null:
                    return null;
                case string text:
                    foreach (Match match in AxisPattern.Matches(text))
                    {
                        settings.Add(new AxisSetting(
                            match.Groups[1].Value,
                            float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
                        ));
                    }
                    break;
                case IEnumerable<AxisSetting> axes:
                    settings.AddRange(axes);
                    break;
                default:
                    throw new InvalidCastException();
            }

            return settings.Count == 0 ? null : settings.ToArray();
        }

        private static T ToEnum<T>(object value) where T : struct, Enum
        {
            if (value is T typed)
                return typed;

            if (value is string name)
            {
                // Accept p5 spellings such as "BOLDITALIC", "rtl" or "alphabetic".
                string normalized = name.Replace("_", string.Empty);
                if (typeof(T) == typeof(TextVerticalAlignMode) &&
                    normalized.Equals("alphabetic", StringComparison.OrdinalIgnoreCase))
                    return (T)(object)TextVerticalAlignMode.Baseline;
                if (typeof(T) == typeof(TextVerticalAlignMode) &&
                    normalized.Equals("middle", StringComparison.OrdinalIgnoreCase))
                    return (T)(object)TextVerticalAlignMode.Center;

                return Enum.Parse<T>(normalized, ignoreCase: true);
            }

            throw new InvalidCastException();
        }
    }
}
