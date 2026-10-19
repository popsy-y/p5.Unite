using System;
using System.IO;
using Unite.Canvas;
using Unite.Text;
using Unite.Text.Fonts;
using UnityEngine;

namespace Dev
{
    // Font loading for the text pages (LoadFontAsync, with the load time logged) and the
    // variable font page: weight and width axes are shown as a ramp and animated.
    public sealed partial class CanvasTester
    {
        private static readonly float[] WeightRamp = { 100f, 200f, 300f, 400f, 500f, 600f, 700f };
        private static readonly float[] WidthRamp = { 75f, 82.5f, 90f, 100f };

        // Labels are built once (or once per value) so the page itself allocates nothing per frame.
        private static readonly string[] WeightLabels = Array.ConvertAll(WeightRamp, weight => $"wght {weight:0}  Hamburgefonstiv");
        private static readonly string[] WidthLabels = Array.ConvertAll(WidthRamp, width => $"wdth {width:0.#}  Hamburgefonstiv");
        private static readonly System.Collections.Generic.Dictionary<int, string> WeightValueLabels = new();
        private static readonly System.Collections.Generic.Dictionary<int, string> WidthValueLabels = new();

        private string _axesDescription;
        private string _loadedLabel;

        [Header("Font")]
        [SerializeField] private string _fontPath = "Assets/DevResources/Fonts/IBMPlexSans-VariableFont_wdth,wght.bytes";

        private FontFace _font;
        private bool _fontRequested;
        private string _fontStatus = "not loaded";
        private double _fontLoadMilliseconds;

        /// <summary>Used by TextPerfProbe to run a scenario without editing the scene.</summary>
        public void ConfigureForProfiling(TestPage page, bool showAtlas, bool animate, string fontPath)
        {
            _page = page;
            _showAtlas = showAtlas;
            _animateTransforms = animate;
            if (!string.IsNullOrEmpty(fontPath))
                _fontPath = fontPath;
        }

        private void DrawVariableFontPage()
        {
            float unit = Unit();
            _canvas.RectMode = ShapeMode.Center;

            if (_font == null)
            {
                // One attempt per play session: a failed load is logged, not retried every frame.
                EnsureFontLoading();

                _canvas.NoStroke();
                _canvas.Fill(new Color(1f, 0.25f, 0.55f, 0.5f));
                _canvas.Rect(Position(0.5f, 0.55f), new Vector2(unit * 0.7f, unit * 0.06f));
                return;
            }

            var textColor = new Color(0.04f, 0.07f, 0.12f);
            var accent = new Color(0.0f, 0.45f, 0.7f);

            _canvas.TextFont(_font);
            _canvas.NoStroke();
            _canvas.Fill(accent);
            _canvas.TextAlign(TextAlignMode.Left, TextVerticalAlignMode.Baseline);
            DrawVariableLabel(AxesDescription(), 0.035f, 0.04f, 0.06f);

            // 1) Weight ramp: one TextVariation("wght") per row.
            _canvas.Fill(textColor);
            for (int index = 0; index < WeightRamp.Length; index++)
            {
                float weight = WeightRamp[index];
                float y = 0.14f + index * 0.075f;
                _canvas.Push();
                _canvas.TextVariation("wght", weight);
                DrawVariableLabel(WeightLabels[index], 0.05f, 0.04f, y);
                _canvas.Pop();
            }

            // 2) Width ramp (the font only narrows: wdth 75..100).
            for (int index = 0; index < WidthRamp.Length; index++)
            {
                float width = WidthRamp[index];
                float y = 0.14f + index * 0.075f;
                _canvas.Push();
                _canvas.TextVariation("wdth", width);
                DrawVariableLabel(WidthLabels[index], 0.05f, 0.58f, y);
                _canvas.Pop();
            }

            // 3) Animated axes, one per word: axis values are used as given, so every frame
            // is a new instance whose glyphs are re-baked (volatile atlas pages).
            float time = _animateTransforms ? Time.time : 1f;
            float animatedWeight = Mathf.Lerp(100f, 700f, (Mathf.Sin(time * 1.3f) + 1f) * 0.5f);
            float animatedWidth = Mathf.Lerp(75f, 100f, (Mathf.Sin(time * 0.9f) + 1f) * 0.5f);
            _canvas.Push();
            _canvas.TextVariation("wght", animatedWeight);
            _canvas.Fill(accent);
            DrawVariableLabel("Variable", 0.1f, 0.04f, 0.78f);
            _canvas.Pop();

            _canvas.Push();
            _canvas.TextVariation("wdth", animatedWidth);
            _canvas.Fill(accent);
            DrawVariableLabel("Condensed", 0.1f, 0.3f, 0.78f);
            _canvas.Pop();

            _canvas.Fill(textColor);
            DrawVariableLabel(ValueLabel(WeightValueLabels, "wght", animatedWeight), 0.035f, 0.04f, 0.85f);
            DrawVariableLabel(ValueLabel(WidthValueLabels, "wdth", animatedWidth), 0.035f, 0.3f, 0.85f);

            // 4) Style and weight API on a variable font: bold follows the weight axis,
            // italic is synthesized (the font has no ital / slnt axis).
            _canvas.Push();
            _canvas.TextStyle(TextStyleMode.BoldItalic);
            DrawVariableLabel("BoldItalic", 0.06f, 0.62f, 0.78f);
            _canvas.Pop();

            _canvas.Push();
            _canvas.TextWeight(300f);
            _canvas.TextProperty("fontVariationSettings", "\"wdth\" 80");
            DrawVariableLabel("TextWeight 300 + wdth 80", 0.04f, 0.62f, 0.85f);
            _canvas.Pop();

            _canvas.Fill(accent);
            _loadedLabel ??= $"loaded in {_fontLoadMilliseconds:0.0} ms";
            DrawVariableLabel(_loadedLabel, 0.03f, 0.04f, 0.94f);
        }

        private void DrawVariableLabel(string text, float sizeFactor, float xNorm, float yNorm)
        {
            _canvas.TextSize(Unit() * sizeFactor);
            _canvas.Push();
            _canvas.Translate(Position(xNorm, yNorm));
            _canvas.Text(text, 0f, 0f);
            _canvas.Pop();
        }

        private static string ValueLabel(System.Collections.Generic.Dictionary<int, string> cache, string tag, float value)
        {
            int rounded = Mathf.RoundToInt(value);
            if (!cache.TryGetValue(rounded, out string label))
            {
                label = $"{tag} {rounded}";
                cache.Add(rounded, label);
            }
            return label;
        }

        private string AxesDescription()
        {
            if (_axesDescription != null)
                return _axesDescription;

            var axes = _canvas.TextAxes();
            var description = new System.Text.StringBuilder(_font.Name);
            foreach (var axis in axes)
                description.Append($"   {axis.Tag} {axis.Minimum:0.#}..{axis.Default:0.#}..{axis.Maximum:0.#}");
            _axesDescription = description.ToString();
            return _axesDescription;
        }

        // Loads the font file through LoadFontAsync (UnityWebRequest + parse) and logs how
        // long the whole load took.
        private void EnsureFontLoading()
        {
            if (!_fontRequested)
                _ = LoadFontAsync();
        }

        private async Awaitable LoadFontAsync()
        {
            _fontRequested = true;
            _fontStatus = "loading";

            string path = Path.IsPathRooted(_fontPath)
                ? _fontPath
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", _fontPath));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var font = await FontFace.LoadFontAsync(path, "IBM Plex Sans");
                stopwatch.Stop();
                _fontLoadMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                _font = font;
                _fontStatus = "loaded";

                var face = font;
                Debug.Log(
                    $"CanvasTester: LoadFontAsync loaded '{Path.GetFileName(path)}' in "
                    + $"{_fontLoadMilliseconds:0.0} ms ({face.GlyphCount} glyphs, {face.Axes.Count} axes)."
                );
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                _fontStatus = exception.Message;
                Debug.LogError($"CanvasTester: could not load the variable font '{path}' ({stopwatch.Elapsed.TotalMilliseconds:0.0} ms): {exception.Message}");
            }
        }
    }
}
