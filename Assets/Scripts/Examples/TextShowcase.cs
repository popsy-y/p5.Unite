using System.Collections.Generic;

using Unite.Canvas;
using Unite.Drawing;
using Unite.Text;
using Unite.Text.Fonts;

using UnityEngine;

namespace Unite.Examples
{
    /// <summary>
    /// A 15-second looping motion piece for p5.Unite Text. The first half is flat
    /// (ScreenOverlay): a weight field, OpenType features, words of many scripts on a
    /// golden-angle spiral and a breathing text box. Then it cuts to a CanvasLocal finale
    /// in 3D: an extruded title rippling in weight, a word assembled from a point cloud and
    /// a ring of scripts. Any variable font works; its wght / wdth ranges are read from it.
    /// </summary>
    [RequireComponent(typeof(UCanvas))]
    public sealed class TextShowcase : MonoBehaviour
    {
        private const float LoopSeconds = 15f;
        private const float CutSeconds = 8f;

        private static readonly string[] Letters = { "U", "N", "I", "T", "E" };
        private static readonly string[] Words =
        {
            "Text", "日本語", "مرحبا", "שָׁלוֹם", "한국어", "Ελληνικά", "Tiếng Việt", "Привет", "中文", "👩‍🚀", "🌏", "✨"
        };

        [SerializeField] private TextAsset _font;          // a variable font (.ttf / .otf renamed to .bytes)
        [SerializeField] private TextAsset _fallbackFont;  // optional, e.g. a Japanese font

        private UCanvas _canvas;
        private FontFace _face;
        private FontFace[] _fonts;
        private List<TextPoint> _cloud;
        private bool _inSpace;

        private void Start()
        {
            _canvas = GetComponent<UCanvas>();
            _face = FontFace.LoadFont(_font.bytes);
            // Characters the first font lacks come from the next one, then from the OS fonts.
            _fonts = _fallbackFont ? new[] { _face, FontFace.LoadFont(_fallbackFont.bytes) } : new[] { _face };
            _cloud = _face.TextToPoints("TEXT", 0f, 0f, new TextPathOptions
            {
                FontSize = 2.6f,
                Align = TextAlignMode.Center,
                VerticalAlign = TextVerticalAlignMode.Center,
                YUp = true,
                SampleFactor = 12f
            });
            enterSpace(false);
        }

        private void Update()
        {
            float t = Time.time % LoopSeconds;
            if (t >= CutSeconds != _inSpace)
                enterSpace(t >= CutSeconds);

            _canvas.TextFont(_fonts, 1f);
            _canvas.TextAlign(TextAlignMode.Center, TextVerticalAlignMode.Center);
            _canvas.TextStyle(TextStyleMode.Normal);
            _canvas.NoStroke();

            if (_inSpace)
                space(t - CutSeconds);
            else
                flat(t);
        }

        private void enterSpace(bool space)
        {
            _inSpace = space;
            transform.rotation = Quaternion.identity;
            _canvas.CreateCanvas(space ? DrawingMode.CanvasLocal : DrawingMode.ScreenOverlay, enableDepth: space);
        }

        // ---- First half: ScreenOverlay, four 2-second scenes ----

        private void flat(float t)
        {
            float w = _canvas.Width, h = _canvas.Height, u = Mathf.Min(w, h);
            _canvas.Fill(new Color(0.04f, 0.05f, 0.09f, 0.97f));
            _canvas.Rect(0f, 0f, w, h);
            _canvas.Translate(w * 0.5f, h * 0.5f);

            int scene = (int)(t / 2f);
            float s = t - scene * 2f;
            float a = Mathf.Clamp01(s * 5f) * Mathf.Clamp01((2f - s) * 8f);
            switch (scene)
            {
                case 0: weightField(s, u, a); break;
                case 1: features(s, u, a); break;
                case 2: spiral(s, u, a); break;
                default: breathingBox(s, u, a); break;
            }
        }

        // A wave runs out from the center; every letter takes its weight and width from it.
        private void weightField(float s, float u, float a)
        {
            float step = u * 0.075f;
            _canvas.TextSize(step * 1.15f);
            for (int y = -5; y <= 5; y++)
            {
                for (int x = -11; x <= 11; x++)
                {
                    float k = quantize(0.5f + 0.5f * Mathf.Sin(Mathf.Sqrt(x * x + y * y) * 0.7f - s * 7f));
                    setAxes(k, 1f - k);
                    _canvas.Fill(withAlpha(Color.HSVToRGB(0.52f + 0.12f * k, 0.55f, 1f), a));
                    _canvas.Text(Letters[(x + y * 3 + 100) % Letters.Length], x * step, y * step);
                }
            }
            caption("TextWeight / TextVariation(\"wdth\", ...)", u, a);
        }

        // OpenType features flip on the beat while the tracking breathes.
        private void features(float s, float u, float a)
        {
            bool on = (int)(s * 3f) % 2 == 0;
            _canvas.Fill(withAlpha(Color.white, a));
            _canvas.TextSize(u * 0.13f);
            _canvas.TextProperty("letterSpacing", u * 0.02f * Mathf.Sin(s * 4f));
            _canvas.TextProperty("fontKerning", on ? "normal" : "none");
            _canvas.Text("AVATAR", 0f, -u * 0.2f);
            _canvas.TextProperty("letterSpacing", 0f);
            _canvas.TextProperty("fontKerning", "normal");

            _canvas.TextProperty("fontFeatureSettings", on ? "'frac' 1" : "normal");
            _canvas.Text("1/2  3/4", 0f, 0f);
            _canvas.TextProperty("fontFeatureSettings", on ? "'zero' 1, 'onum' 1" : "normal");
            _canvas.Text("2026  0", 0f, u * 0.2f);

            _canvas.TextProperty("fontFeatureSettings", "normal");
            caption(on ? "fontKerning + fontFeatureSettings 'frac' 'zero' 'onum'" : "features off", u, a);
        }

        // Words of many scripts on a golden-angle spiral, a few more every frame.
        private void spiral(float s, float u, float a)
        {
            _canvas.TextDirection(TextDirectionMode.Auto);
            int count = (int)(s * 110f);
            for (int i = 1; i <= count; i++)
            {
                _canvas.Push();
                _canvas.Rotate(i * 2.39996f + s * 0.5f);
                _canvas.Translate(u * 0.034f * Mathf.Sqrt(i), 0f);
                _canvas.TextSize(u * (0.014f + 0.0022f * Mathf.Sqrt(i)));
                _canvas.Fill(withAlpha(Color.HSVToRGB(Mathf.Repeat(i * 0.031f, 1f), 0.45f, 1f), a));
                _canvas.Text(Words[i % Words.Length], 0f, 0f);
                _canvas.Pop();
            }
            _canvas.TextDirection(TextDirectionMode.Ltr);
            caption("Shaping, bidi and font fallback for every script", u, a);
        }

        // One paragraph in a box that breathes: word wrap, CJK line breaking, measured bounds.
        private void breathingBox(float s, float u, float a)
        {
            const string Paragraph =
                "Text wraps at word boundaries, 日本語は一文字ずつ折り返し、句読点は行頭に来ません。 والعربية تُكتب من اليمين إلى اليسار.";
            float width = u * Mathf.Lerp(0.3f, 1.2f, 0.5f - 0.5f * Mathf.Cos(s * Mathf.PI));
            float left = -width * 0.5f, top = -u * 0.2f;

            _canvas.TextSize(u * 0.05f);
            _canvas.TextAlign(TextAlignMode.Left, TextVerticalAlignMode.Top);
            var bounds = _canvas.TextBounds(Paragraph, left, top, width);
            _canvas.Fill(withAlpha(new Color(0.3f, 0.6f, 1f), 0.25f * a));
            _canvas.Rect(bounds.x, bounds.y, bounds.width, bounds.height);
            _canvas.Fill(withAlpha(Color.white, a));
            _canvas.Text(Paragraph, left, top, width);

            _canvas.TextAlign(TextAlignMode.Center, TextVerticalAlignMode.Center);
            _canvas.TextStyle(TextStyleMode.BoldItalic);
            _canvas.NoFill();
            _canvas.Stroke(withAlpha(new Color(1f, 0.55f, 0.35f), a));
            _canvas.StrokeWeight(u * 0.003f);
            _canvas.TextSize(u * 0.1f);
            _canvas.Text("Wrap & Bounds", 0f, -u * 0.32f);
            _canvas.NoStroke();
            _canvas.TextStyle(TextStyleMode.Normal);
            caption("TextWrap / TextBounds / TextStyle / Stroke", u, a);
        }

        private void caption(string text, float u, float a)
        {
            _canvas.ClearTextVariations();
            _canvas.TextSize(u * 0.03f);
            _canvas.Fill(withAlpha(new Color(1f, 0.55f, 0.35f), a));
            _canvas.Text(text, 0f, u * 0.44f);
        }

        // ---- Second half: CanvasLocal, the same canvas turned into a 3D object ----

        private void space(float t)
        {
            float fade = Mathf.Clamp01((LoopSeconds - CutSeconds - t) * 2f);
            transform.rotation = Quaternion.Euler(12f * Mathf.Sin(t * 0.8f), -55f + t * 14f, 0f);
            _canvas.Scale(1f + 0.6f * Mathf.Exp(-t * 6f)); // the hit of the cut

            // Extrusion: the title stacked in depth, each layer a beat later in weight.
            _canvas.TextSize(1.4f);
            for (int i = 16; i >= 0; i--)
            {
                _canvas.Push();
                _canvas.Translate(0f, 1.6f, i * 0.06f);
                setAxes(quantize(0.5f + 0.5f * Mathf.Sin(t * 3f - i * 0.3f)), 1f);
                _canvas.Fill(withAlpha(Color.Lerp(new Color(1f, 0.25f, 0.5f), new Color(0.12f, 0.08f, 0.35f), i / 16f), fade));
                _canvas.Text("p5.Unite", 0f, 0f);
                _canvas.Pop();
            }
            _canvas.ClearTextVariations();

            // A word assembled from a point cloud, rippling once it has landed.
            float settle = Mathf.SmoothStep(0f, 1f, t / 2.5f);
            _canvas.StrokeWeight(0.07f);
            for (int i = 0; i < _cloud.Count; i++)
            {
                var target = new Vector3(_cloud[i].X, _cloud[i].Y - 0.9f, 0.3f * Mathf.Sin(t * 4f + _cloud[i].X * 1.5f));
                var scatter = new Vector3(hash(i) - 0.5f, hash(i + 7919) - 0.5f, hash(i + 104729) - 0.5f) * 16f;
                _canvas.Stroke(withAlpha(Color.HSVToRGB(Mathf.Repeat(_cloud[i].X * 0.07f + t * 0.15f, 1f), 0.85f, 0.85f), fade));
                _canvas.Point(Vector3.Lerp(scatter, target, settle));
            }
            _canvas.NoStroke();

            // A ring of scripts orbiting below.
            _canvas.TextDirection(TextDirectionMode.Auto);
            _canvas.TextSize(0.5f);
            for (int i = 0; i < Words.Length; i++)
            {
                _canvas.Push();
                float angle = t * 0.6f + i * Mathf.PI * 2f / Words.Length;
                _canvas.RotateY(angle);
                _canvas.Translate(0f, -3f + 0.2f * Mathf.Sin(t * 2f + i), 3.6f);
                _canvas.RotateY(-angle); // keep facing the front
                _canvas.Fill(withAlpha(new Color(0.1f, 0.12f, 0.2f), fade));
                _canvas.Text(Words[i], 0f, 0f);
                _canvas.Pop();
            }
            _canvas.TextDirection(TextDirectionMode.Ltr);
        }

        // ---- helpers ----

        // 0..1 onto the font's own wght / wdth ranges (axes the font lacks are skipped).
        private void setAxes(float weight, float width)
        {
            var axes = _face.Axes;
            for (int i = 0; i < axes.Count; i++)
            {
                if (axes[i].Tag == "wght")
                    _canvas.TextVariation("wght", Mathf.Lerp(axes[i].Minimum, axes[i].Maximum, weight));
                else if (axes[i].Tag == "wdth")
                    _canvas.TextVariation("wdth", Mathf.Lerp(axes[i].Minimum, axes[i].Maximum, width));
            }
        }

        // A few steps instead of continuous values: repeated axis values reuse baked glyphs.
        private static float quantize(float k) => Mathf.Round(k * 16f) / 16f;

        private static float hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.547f, 1f);

        private static Color withAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
