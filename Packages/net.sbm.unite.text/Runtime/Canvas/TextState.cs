using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Horizontal text alignment, mirroring p5.js LEFT / CENTER / RIGHT.
    /// Deliberately not named TextAlignment to avoid clashing with
    /// UnityEngine.TextAlignment in files that use both namespaces.
    /// </summary>
    public enum TextAlignMode
    {
        Left,
        Center,
        Right
    }

    /// <summary>Vertical text alignment, mirroring p5.js BASELINE / TOP / CENTER / BOTTOM.</summary>
    public enum TextVerticalAlignMode
    {
        Baseline,
        Top,
        Center,
        Bottom
    }

    /// <summary>
    /// Text style, mirroring p5.js NORMAL / ITALIC / BOLD / BOLDITALIC. Styles are
    /// synthesized from the single font file: italic shears the glyphs, bold offsets
    /// the outline outward.
    /// </summary>
    public enum TextStyleMode
    {
        Normal,
        Italic,
        Bold,
        BoldItalic
    }

    /// <summary>Line breaking for boxed text, mirroring p5.js WORD / CHAR.</summary>
    public enum TextWrapMode
    {
        Word,
        Char
    }

    /// <summary>
    /// Paragraph direction, mirroring p5.js / CSS "ltr" / "rtl" / "inherit" / "auto". The
    /// Unicode Bidirectional Algorithm orders mixed text inside the paragraph either way;
    /// Auto takes the direction of the first strong character. Inherit is left-to-right
    /// (the canvas default).
    /// </summary>
    public enum TextDirectionMode
    {
        Ltr,
        Rtl,
        Inherit,
        Auto
    }

    /// <summary>Per-canvas text style state, mirroring p5.js text attributes.</summary>
    internal sealed class TextState
    {
        /// <summary>p5.js default leading: 1.25 x the text size until set explicitly.</summary>
        public const float DefaultLeadingFactor = 1.25f;

        public const float NormalWeight = 400f;

        // Italic slant (degrees, negative leans right) requested of a "slnt" axis.
        private const float ObliqueAngle = -12f;

        private FontFace _font;
        private TextStyleMode _style = TextStyleMode.Normal;
        private float _weight = NormalWeight;

        // Explicit axis values, owned by this state (Push copies them into the saved state).
        private readonly List<AxisSetting> _variations = new();
        private readonly List<AxisSetting> _resolvedAxes = new();

        // Cached resolution of the font + style + axes into a font instance.
        private bool _resolved;
        private FontInstance _instance;
        private bool _synthesizeWeight = true;
        private bool _synthesizeItalic = true;

        public float Size = 12f;
        public float? Leading;
        public TextAlignMode Align = TextAlignMode.Left;
        public TextVerticalAlignMode VerticalAlign = TextVerticalAlignMode.Baseline;
        public TextWrapMode Wrap = TextWrapMode.Word;
        public TextDirectionMode Direction = TextDirectionMode.Ltr;
        public float LetterSpacing;
        public float WordSpacing;
        public bool WarnedMissingFont;

        // CSS-like OpenType controls (textProperty fontKerning / fontVariantLigatures /
        // fontFeatureSettings / lang).
        public string FontKerning;
        public string FontVariantLigatures;
        public string FontFeatureSettings;
        public string Language;

        private Shaping.TextFeatures _features;
        private string _featuresKerning;
        private string _featuresLigatures;
        private string _featuresSettings;

        /// <summary>The interned feature settings for the current properties.</summary>
        public Shaping.TextFeatures Features
        {
            get
            {
                if (_features == null || !ReferenceEquals(_featuresKerning, FontKerning)
                    || !ReferenceEquals(_featuresLigatures, FontVariantLigatures)
                    || !ReferenceEquals(_featuresSettings, FontFeatureSettings))
                {
                    _features = Shaping.TextFeatures.From(FontKerning, FontVariantLigatures, FontFeatureSettings);
                    _featuresKerning = FontKerning;
                    _featuresLigatures = FontVariantLigatures;
                    _featuresSettings = FontFeatureSettings;
                }
                return _features;
            }
        }

        public FontFace Font
        {
            get => _font;
            set
            {
                _font = value;
                _resolved = false;
            }
        }

        public TextStyleMode Style
        {
            get => _style;
            set
            {
                _style = value;
                _resolved = false;
            }
        }

        public float Weight
        {
            get => _weight;
            set
            {
                _weight = value;
                _resolved = false;
            }
        }

        /// <summary>Explicit axis values; null when none were set.</summary>
        public IReadOnlyList<AxisSetting> Variations
        {
            get => _variations.Count == 0 ? null : _variations;
            set
            {
                _variations.Clear();
                if (value != null)
                {
                    for (int index = 0; index < value.Count; index++)
                        _variations.Add(value[index]);
                }
                _resolved = false;
            }
        }

        /// <summary>Sets one axis value; setting the value it already has keeps the resolved instance.</summary>
        public void SetVariation(string tag, float value)
        {
            for (int index = 0; index < _variations.Count; index++)
            {
                if (_variations[index].Tag != tag)
                    continue;

                if (_variations[index].Value == value)
                    return;

                _variations[index] = new AxisSetting(tag, value);
                _resolved = false;
                return;
            }

            _variations.Add(new AxisSetting(tag, value));
            _resolved = false;
        }

        public float EffectiveLeading => Leading ?? Size * DefaultLeadingFactor;

        public bool IsItalic => Style is TextStyleMode.Italic or TextStyleMode.BoldItalic;

        public bool IsBold => Style is TextStyleMode.Bold or TextStyleMode.BoldItalic;

        public TextState Clone()
        {
            var copy = new TextState();
            copy.CopyFrom(this);
            return copy;
        }

        /// <summary>Makes this state equal to another one (including its resolved instance) without allocating.</summary>
        public void CopyFrom(TextState other)
        {
            _font = other._font;
            _style = other._style;
            _weight = other._weight;
            _variations.Clear();
            _variations.AddRange(other._variations);
            _resolved = other._resolved;
            _instance = other._instance;
            _synthesizeWeight = other._synthesizeWeight;
            _synthesizeItalic = other._synthesizeItalic;
            Size = other.Size;
            Leading = other.Leading;
            Align = other.Align;
            VerticalAlign = other.VerticalAlign;
            Wrap = other.Wrap;
            Direction = other.Direction;
            LetterSpacing = other.LetterSpacing;
            WordSpacing = other.WordSpacing;
            FontKerning = other.FontKerning;
            FontVariantLigatures = other.FontVariantLigatures;
            FontFeatureSettings = other.FontFeatureSettings;
            Language = other.Language;
            WarnedMissingFont = other.WarnedMissingFont;
        }

        /// <summary>
        /// The font instance for the current font, style, weight and axes. For variable
        /// fonts a weight / italic axis takes over from the synthesized style, which the
        /// two flags report. Returns false while no usable font is set.
        /// </summary>
        public bool TryGetInstance(out FontInstance instance, out bool synthesizeWeight, out bool synthesizeItalic)
        {
            instance = null;
            synthesizeWeight = true;
            synthesizeItalic = true;

            if (_font == null)
                return false;

            if (!_resolved)
                Resolve(_font);

            instance = _instance;
            synthesizeWeight = _synthesizeWeight;
            synthesizeItalic = _synthesizeItalic;
            return true;
        }

        /// <summary>The axis values actually applied (explicit axes plus weight / italic mapped onto axes).</summary>
        public IReadOnlyList<AxisSetting> ResolveAxes()
        {
            if (_font == null)
                return Variations;

            var axes = new List<AxisSetting>();
            BuildAxes(_font, axes, out _, out _);
            return axes;
        }

        private void Resolve(FontFace face)
        {
            BuildAxes(face, _resolvedAxes, out _synthesizeWeight, out _synthesizeItalic);
            _instance = face.GetInstance(_resolvedAxes);
            _resolved = true;
        }

        private void BuildAxes(FontFace face, List<AxisSetting> axes, out bool synthesizeWeight, out bool synthesizeItalic)
        {
            axes.Clear();
            axes.AddRange(_variations);

            synthesizeWeight = true;
            synthesizeItalic = true;
            if (!face.IsVariable)
                return;

            if (HasAxis(face, "wght"))
            {
                synthesizeWeight = false;
                if (!Contains(axes, "wght"))
                    axes.Add(new AxisSetting("wght", IsBold ? Mathf.Max(Weight, 700f) : Weight));
            }

            bool italicAxis = HasAxis(face, "ital");
            bool slantAxis = HasAxis(face, "slnt");
            if (italicAxis || slantAxis)
            {
                synthesizeItalic = false;
                if (IsItalic && !Contains(axes, "ital") && !Contains(axes, "slnt"))
                {
                    axes.Add(italicAxis
                        ? new AxisSetting("ital", 1f)
                        : new AxisSetting("slnt", ObliqueAngle));
                }
            }
        }

        private static bool HasAxis(FontFace face, string tag)
        {
            var axes = face.Axes;
            for (int index = 0; index < axes.Count; index++)
            {
                if (axes[index].Tag == tag)
                    return true;
            }
            return false;
        }

        private static bool Contains(List<AxisSetting> axes, string tag)
        {
            for (int index = 0; index < axes.Count; index++)
            {
                if (axes[index].Tag == tag)
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Associates <see cref="TextState"/> with canvases without touching UCanvas itself,
    /// and saves/restores it with the canvas Push()/Pop() like p5.js does for text
    /// attributes. State is per play session (cleared on domain reload), matching p5.js
    /// sketch semantics where text style is set each time a sketch runs.
    /// </summary>
    internal static class TextStateStore
    {
        private sealed class Holder
        {
            public TextState Current = new();
            public readonly Stack<TextState> Saved = new();

            // States popped off the stack, reused by later pushes.
            private readonly Stack<TextState> _spare = new();

            public void Push()
            {
                var saved = _spare.Count > 0 ? _spare.Pop() : new TextState();
                saved.CopyFrom(Current);
                Saved.Push(saved);
            }

            public void Pop()
            {
                // Pushes made before the text state existed have nothing to restore.
                if (Saved.Count == 0)
                    return;

                _spare.Push(Current);
                Current = Saved.Pop();
            }
        }

        private static readonly ConditionalWeakTable<UCanvas, Holder> Holders = new();

        // The table lookup allocates on Mono; text calls almost always hit the same canvas.
        private static UCanvas _lastCanvas;
        private static Holder _lastHolder;

        public static TextState GetOrCreate(UCanvas canvas)
        {
            if (ReferenceEquals(canvas, _lastCanvas) && _lastHolder != null)
                return _lastHolder.Current;

            if (!Holders.TryGetValue(canvas, out var holder))
            {
                holder = new Holder();
                Holders.Add(canvas, holder);
                canvas.StatePushed += holder.Push;
                canvas.StatePopped += holder.Pop;
            }

            _lastCanvas = canvas;
            _lastHolder = holder;
            return holder.Current;
        }
    }
}
