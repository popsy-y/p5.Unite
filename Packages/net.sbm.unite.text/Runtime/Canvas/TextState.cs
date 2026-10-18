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

    /// <summary>Text direction, mirroring p5.js "ltr" / "rtl" / "inherit".</summary>
    public enum TextDirectionMode
    {
        Ltr,
        Rtl,
        Inherit
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
        private AxisSetting[] _variations;

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

        /// <summary>
        /// Explicit axis values (copy-on-write, so saved Push() states stay intact).
        /// Null when none were set.
        /// </summary>
        public AxisSetting[] Variations
        {
            get => _variations;
            set
            {
                _variations = value;
                _resolved = false;
            }
        }

        public float EffectiveLeading => Leading ?? Size * DefaultLeadingFactor;

        public bool IsItalic => Style is TextStyleMode.Italic or TextStyleMode.BoldItalic;

        public bool IsBold => Style is TextStyleMode.Bold or TextStyleMode.BoldItalic;

        public TextState Clone() => (TextState)MemberwiseClone();

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
                return _variations;

            return BuildAxes(_font, out _, out _);
        }

        private void Resolve(FontFace face)
        {
            var axes = BuildAxes(face, out _synthesizeWeight, out _synthesizeItalic);
            _instance = face.GetInstance(axes);
            _resolved = true;
        }

        private List<AxisSetting> BuildAxes(FontFace face, out bool synthesizeWeight, out bool synthesizeItalic)
        {
            var axes = new List<AxisSetting>();
            if (_variations != null)
                axes.AddRange(_variations);

            synthesizeWeight = true;
            synthesizeItalic = true;
            if (!face.IsVariable)
                return axes;

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

            return axes;
        }

        private static bool HasAxis(FontFace face, string tag)
        {
            foreach (var axis in face.Axes)
            {
                if (axis.Tag == tag)
                    return true;
            }
            return false;
        }

        private static bool Contains(List<AxisSetting> axes, string tag)
        {
            foreach (var axis in axes)
            {
                if (axis.Tag == tag)
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
        }

        private static readonly ConditionalWeakTable<UCanvas, Holder> Holders = new();

        public static TextState GetOrCreate(UCanvas canvas)
        {
            if (Holders.TryGetValue(canvas, out var holder))
                return holder.Current;

            holder = new Holder();
            Holders.Add(canvas, holder);
            canvas.StatePushed += () => holder.Saved.Push(holder.Current.Clone());
            canvas.StatePopped += () =>
            {
                // Pushes made before the text state existed have nothing to restore.
                if (holder.Saved.Count > 0)
                    holder.Current = holder.Saved.Pop();
            };
            return holder.Current;
        }
    }
}
