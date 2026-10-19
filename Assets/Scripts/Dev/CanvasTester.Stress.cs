using System.Collections.Generic;
using Unite.Canvas;
using Unite.Text;
using Unite.Text.Fonts;
using UnityEngine;

namespace Dev
{
    // Stress page: every drawing feature at once, scaled by _stressShapeCount. Bands from
    // top to bottom: primitives under transforms, lines and points, curves, BeginShape paths,
    // then text (many sizes, styles, strokes, animated axes, boxes, bounds) and TextToPoints.
    public sealed partial class CanvasTester
    {
        private const string StressParagraph =
            "Creative coding asks for bold things: thousands of shapes, extreme sizes, animated "
            + "type. The library must keep up.";

        private static readonly string[] StressWords =
        {
            "Unite", "p5", "glyph", "Hamburgefonstiv", "SDF", "Burst", "atlas", "kerning",
            "0123456789", "WAVE", "type", "stress"
        };

        private static readonly ShapeKind[] StressShapeKinds =
        {
            ShapeKind.Triangles, ShapeKind.TriangleFan, ShapeKind.TriangleStrip, ShapeKind.Quads, ShapeKind.QuadStrip
        };

        [SerializeField][Min(0)] private int _stressTextCount = 300;

        private List<TextPoint> _stressPoints;

        private void DrawStressTest()
        {
            float unit = Unit();
            float time = _animateTransforms ? Time.time : 0f;
            int shapeCount = Mathf.Max(1, _stressShapeCount);

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;

            DrawStressPrimitives(unit, time, shapeCount);
            DrawStressLines(unit, time, Mathf.Max(1, shapeCount / 4));
            DrawStressCurves(unit, time, Mathf.Max(1, shapeCount / 20));
            DrawStressPaths(unit, time, Mathf.Max(1, shapeCount / 50));

            if (_font == null)
            {
                EnsureFontLoading();
                return;
            }

            _canvas.TextFont(_font);
            DrawStressText(unit, time, Mathf.Max(1, _stressTextCount));
            DrawStressTextPoints(unit, time);
        }

        // Band 1 (y 0.02..0.30): every primitive with fill + stroke under push/rotate/scale/shear.
        private void DrawStressPrimitives(float unit, float time, int count)
        {
            _canvas.Stroke(new Color(0.02f, 0.04f, 0.08f, 0.65f));
            _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * 0.002f));

            for (int index = 0; index < count; index++)
            {
                float x = 0.02f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.96f;
                float y = 0.02f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.28f;
                float variation = Mathf.Repeat((index + 1) * 0.414213562f, 1f);
                float size = Mathf.Lerp(unit * 0.006f, unit * 0.022f, variation);
                var color = Color.HSVToRGB(Mathf.Repeat(index * 0.071f, 1f), 0.65f, 0.95f);
                color.a = 0.75f;
                _canvas.Fill(color);

                _canvas.Push();
                _canvas.Translate(Position(x, y));
                _canvas.Rotate(time * (0.5f + variation) + index);
                if (index % 7 == 0)
                    _canvas.ShearX(0.3f * Mathf.Sin(time + index));
                if (index % 5 == 0)
                    _canvas.Scale(0.6f + 0.4f * Mathf.Sin(time * 2f + index));

                switch (index % 8)
                {
                    case 0: _canvas.Circle(Vector3.zero, size); break;
                    case 1: _canvas.Ellipse(Vector3.zero, new Vector2(size * 1.5f, size)); break;
                    case 2: _canvas.Rect(Vector3.zero, new Vector2(size * 1.6f, size), size * 0.2f); break;
                    case 3: _canvas.Square(Vector3.zero, size, size * 0.15f); break;
                    case 4: _canvas.Triangle(new Vector2(-size, size * 0.6f), new Vector2(size, size * 0.6f), new Vector2(0f, -size)); break;
                    case 5: _canvas.Quad(new Vector2(-size, 0f), new Vector2(0f, -size), new Vector2(size, 0f), new Vector2(0f, size * 0.7f)); break;
                    case 6: _canvas.Arc(Vector3.zero, new Vector2(size * 2f, size * 2f), 0f, Mathf.PI * (0.5f + variation), ArcMode.Pie); break;
                    default: _canvas.Arc(Vector3.zero, new Vector2(size * 2f, size * 1.4f), 0.3f, Mathf.PI * 1.6f, (ArcMode)(index % 3)); break;
                }
                _canvas.Pop();
            }
        }

        // Band 2 (y 0.31..0.40): lines with every cap and points.
        private void DrawStressLines(float unit, float time, int count)
        {
            float weight = Mathf.Max(AntiAliasMinimum(), unit * 0.004f);
            for (int index = 0; index < count; index++)
            {
                float x = 0.02f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.96f;
                float y = 0.31f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.09f;
                float angle = time + index * 0.37f;
                float length = unit * (0.01f + 0.02f * Mathf.Repeat(index * 0.31f, 1f));
                var from = Position(x, y);
                var to = from + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * length;

                _canvas.StrokeCap((StrokeCap)(index % 3));
                _canvas.StrokeWeight(weight * (1f + index % 3));
                _canvas.Stroke(Color.HSVToRGB(Mathf.Repeat(index * 0.053f, 1f), 0.7f, 0.8f));
                if (index % 4 == 0)
                    _canvas.Point(to);
                else
                    _canvas.Line(from, to);
            }
            _canvas.StrokeCap(StrokeCap.Round);
        }

        // Band 3 (y 0.41..0.52): Bezier and spline curves.
        private void DrawStressCurves(float unit, float time, int count)
        {
            _canvas.NoFill();
            _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * 0.003f));
            for (int index = 0; index < count; index++)
            {
                float x = 0.03f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.9f;
                float y = 0.43f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.07f;
                float wave = Mathf.Sin(time * 1.5f + index) * unit * 0.03f;
                var start = (Vector2)Position(x, y);
                var end = start + new Vector2(unit * 0.08f, 0f);
                var control1 = start + new Vector2(unit * 0.02f, wave);
                var control2 = end + new Vector2(-unit * 0.02f, -wave);

                _canvas.Stroke(Color.HSVToRGB(Mathf.Repeat(0.55f + index * 0.031f, 1f), 0.8f, 0.9f));
                if (index % 2 == 0)
                    _canvas.Bezier(start, control1, control2, end);
                else
                    _canvas.Spline(control1, start, end, control2);
            }
        }

        // Band 4 (y 0.53..0.62): BeginShape paths with contours, Bezier vertices and the
        // primitive shape kinds.
        private void DrawStressPaths(float unit, float time, int count)
        {
            _canvas.Stroke(DarkColor());
            _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * 0.002f));
            _canvas.StrokeJoin(StrokeJoin.Round);

            for (int index = 0; index < count; index++)
            {
                float x = 0.03f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.94f;
                float y = 0.55f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.06f;
                float radius = unit * 0.025f;
                var center = (Vector2)Position(x, y);
                var color = Color.HSVToRGB(Mathf.Repeat(index * 0.13f, 1f), 0.5f, 1f);
                color.a = 0.85f;
                _canvas.Fill(color);

                switch (index % 3)
                {
                    case 0:
                        // Star with a square hole.
                        _canvas.BeginShape();
                        for (int point = 0; point < 10; point++)
                        {
                            float angle = time * 0.5f + point * Mathf.PI / 5f;
                            float r = point % 2 == 0 ? radius : radius * 0.45f;
                            _canvas.Vertex(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r);
                        }
                        _canvas.BeginContour();
                        float hole = radius * 0.2f;
                        _canvas.Vertex(center + new Vector2(-hole, -hole));
                        _canvas.Vertex(center + new Vector2(-hole, hole));
                        _canvas.Vertex(center + new Vector2(hole, hole));
                        _canvas.Vertex(center + new Vector2(hole, -hole));
                        _canvas.EndContour();
                        _canvas.EndShape(ShapeCloseMode.Close);
                        break;
                    case 1:
                        // Blob from Bezier vertices.
                        _canvas.BeginShape();
                        _canvas.Vertex(center + new Vector2(-radius, 0f));
                        float bulge = radius * (1f + 0.4f * Mathf.Sin(time * 2f + index));
                        _canvas.BezierVertex(center + new Vector2(-radius, -bulge));
                        _canvas.BezierVertex(center + new Vector2(radius, -bulge));
                        _canvas.BezierVertex(center + new Vector2(radius, 0f));
                        _canvas.BezierVertex(center + new Vector2(radius, bulge));
                        _canvas.BezierVertex(center + new Vector2(-radius, bulge));
                        _canvas.BezierVertex(center + new Vector2(-radius, 0f));
                        _canvas.EndShape(ShapeCloseMode.Close);
                        break;
                    default:
                        _canvas.BeginShape(StressShapeKinds[index / 3 % StressShapeKinds.Length]);
                        for (int point = 0; point < 8; point++)
                        {
                            float angle = point * Mathf.PI / 4f + time;
                            _canvas.Vertex(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (point % 2 == 0 ? 1f : 0.5f));
                        }
                        _canvas.EndShape();
                        break;
                }
            }
            _canvas.StrokeJoin(StrokeJoin.Miter);
        }

        // Band 5 (y 0.64..0.90): many labels over a range of sizes, styles, fill/stroke
        // combinations and animated axes, a wrapped paragraph and measured bounds.
        private void DrawStressText(float unit, float time, int count)
        {
            _canvas.TextAlign(TextAlignMode.Left, TextVerticalAlignMode.Baseline);

            for (int index = 0; index < count; index++)
            {
                float x = 0.02f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.85f;
                float y = 0.66f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.22f;
                float sizeFactor = Mathf.Lerp(0.012f, 0.05f, Mathf.Repeat(index * 0.137f, 1f));

                _canvas.Push();
                _canvas.Translate(Position(x, y));
                _canvas.TextSize(unit * sizeFactor);
                _canvas.TextStyle((TextStyleMode)(index % 4));

                switch (index % 5)
                {
                    case 0:
                        // Animated weight: a new font instance every frame.
                        _canvas.TextVariation("wght", 400f + 300f * Mathf.Sin(time * 1.3f + index));
                        _canvas.NoStroke();
                        _canvas.Fill(DarkColor());
                        break;
                    case 1:
                        _canvas.NoFill();
                        _canvas.Stroke(new Color(0.8f, 0.3f, 0f));
                        _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * sizeFactor * 0.04f));
                        break;
                    case 2:
                        _canvas.Fill(new Color(0f, 0.45f, 0.7f));
                        _canvas.Stroke(DarkColor());
                        _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * sizeFactor * 0.03f));
                        break;
                    default:
                        _canvas.NoStroke();
                        _canvas.Fill(Color.HSVToRGB(Mathf.Repeat(index * 0.09f, 1f), 0.7f, 0.6f));
                        _canvas.Rotate(Mathf.Sin(time + index) * 0.2f);
                        break;
                }

                _canvas.Text(StressWords[index % StressWords.Length], 0f, 0f);
                _canvas.Pop();
            }

            // Wrapped paragraph with its measured bounds behind it.
            _canvas.Push();
            var box = Position(0.88f, 0.66f);
            _canvas.Translate(box);
            _canvas.TextSize(unit * 0.018f);
            _canvas.TextStyle(TextStyleMode.Normal);
            _canvas.TextWrap(TextWrapMode.Word);
            float boxWidth = unit * (0.18f + 0.04f * Mathf.Sin(time));
            var bounds = _canvas.TextBounds(StressParagraph, 0f, 0f, boxWidth);
            _canvas.RectMode = ShapeMode.Corner;
            _canvas.NoStroke();
            _canvas.Fill(new Color(1f, 0.9f, 0.4f, 0.5f));
            _canvas.Rect(bounds.x, bounds.y, bounds.width, bounds.height);
            _canvas.RectMode = ShapeMode.Center;
            _canvas.Fill(DarkColor());
            _canvas.Text(StressParagraph, 0f, 0f, boxWidth);
            _canvas.Pop();
        }

        // Band 6 (y 0.92..0.98): TextToPoints, sampled once, drawn as animated dots.
        private void DrawStressTextPoints(float unit, float time)
        {
            if (_stressPoints == null)
            {
                var options = TextPathOptions.FromCanvas(_canvas);
                options.FontSize = unit * 0.06f;
                options.SampleFactor = 0.25f;
                _stressPoints = _font.TextToPoints("p5.Unite", 0f, 0f, options);
            }

            var origin = Position(0.04f, 0.97f);
            _canvas.NoStroke();
            for (int index = 0; index < _stressPoints.Count; index++)
            {
                var point = _stressPoints[index];
                float wobble = Mathf.Sin(time * 3f + index * 0.2f) * unit * 0.002f;
                _canvas.Fill(Color.HSVToRGB(Mathf.Repeat(index * 0.01f + time * 0.1f, 1f), 0.8f, 0.9f));
                _canvas.Circle(origin + new Vector3(point.X + wobble, point.Y, 0f), unit * 0.003f);
            }
        }
    }
}
