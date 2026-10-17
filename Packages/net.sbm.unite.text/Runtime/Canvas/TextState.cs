using System.Runtime.CompilerServices;
using Unite.Canvas;

namespace Unite.Text
{
    /// <summary>
    /// Horizontal text alignment, mirroring p5.js (vertical stays baseline in v1).
    /// Deliberately not named TextAlignment to avoid clashing with
    /// UnityEngine.TextAlignment in files that use both namespaces.
    /// </summary>
    public enum TextAlignMode
    {
        Left,
        Center,
        Right
    }

    /// <summary>Per-canvas text style state, mirroring p5.js text attributes.</summary>
    internal sealed class TextState
    {
        public UniteFontAsset Font;
        public float Size = 24f;
        public TextAlignMode Align = TextAlignMode.Left;
        public bool WarnedMissingFont;
    }

    /// <summary>
    /// Associates <see cref="TextState"/> with canvases without touching UCanvas itself.
    /// State is per play session (cleared on domain reload), matching p5.js sketch
    /// semantics where text style is set each time a sketch runs.
    /// </summary>
    internal static class TextStateStore
    {
        private static readonly ConditionalWeakTable<UCanvas, TextState> States = new();

        public static TextState GetOrCreate(UCanvas canvas)
        {
            var state = States.GetOrCreateValue(canvas);
            return state;
        }
    }
}
