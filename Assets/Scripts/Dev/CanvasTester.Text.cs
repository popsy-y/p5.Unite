using Unite.Canvas;
using Unite.Text;
using UnityEngine;

namespace Dev
{
    // Text test pages: rendering paths (Text) and layout / metrics / outline APIs (TextLayout).
    public sealed partial class CanvasTester
    {
        private bool _textFontWarned;

        // Exercises every text feature: TextFont, TextSize (pow2 raster buckets),
        // TextAlign (left/center/right on reference lines), fill-only / outline-only /
        // fill+stroke glyph render paths, multi-line Text, TextWidth() measurement and
        // text inside Push/Translate/Rotate transforms.
        private void DrawTextPage()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.008f);
            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;

            if (!RequireTextFont(unit))
                return;

            var textColor = new Color(0.04f, 0.07f, 0.12f);
            var accent = new Color(0.0f, 0.45f, 0.7f);
            var outlineColor = new Color(0.8f, 0.3f, 0.0f);

            // 1) Size ramp: the three sizes land in different pow2 raster buckets.
            // Rows run top → bottom like the other pages (drawing +y is down).
            // Dark text keeps the samples readable over the light dev scene.
            _canvas.TextAlign(TextAlignMode.Left);
            _canvas.NoStroke();
            _canvas.Fill(textColor);
            DrawTextSample("small", 0.05f, 0.05f, 0.15f);
            DrawTextSample("medium", 0.08f, 0.28f, 0.15f);
            DrawTextSample("large", 0.12f, 0.7f, 0.15f);

            // 2) Alignment: each string anchors LEFT / CENTER / RIGHT on its line.
            DrawTextAlignmentRow(unit, strokeWeight, accent, textColor, 0.4f);

            // 3) Glyph render paths: filled, outline-only and fill+stroke.
            // Strokes are centered on the outline, so the weight stays well below the
            // glyph stem width (and inside the glyph SDF range); a heavier stroke
            // swallows the whole stem like it would in p5.
            _canvas.TextAlign(TextAlignMode.Left);
            _canvas.NoStroke();
            _canvas.Fill(textColor);
            DrawTextSample("fill", 0.07f, 0.06f, 0.6f);

            _canvas.NoFill();
            _canvas.Stroke(outlineColor);
            _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * 0.003f));
            DrawTextSample("outline", 0.07f, 0.28f, 0.6f);

            _canvas.Fill(accent);
            _canvas.Stroke(DarkColor());
            _canvas.StrokeWeight(Mathf.Max(AntiAliasMinimum(), unit * 0.002f));
            DrawTextSample("both", 0.07f, 0.52f, 0.6f);
            _canvas.NoStroke();

            // 4) Multi-line text plus a TextWidth() measurement underline.
            _canvas.Fill(textColor);
            DrawTextSample("p5.Unite\nsecond\nthird", 0.055f, 0.06f, 0.73f);

            _canvas.TextSize(unit * 0.06f);
            const string measured = "TextWidth()";
            var measuredPosition = Position(0.55f, 0.73f);
            _canvas.Push();
            _canvas.Translate(measuredPosition);
            _canvas.Text(measured, 0f, 0f);
            float measuredWidth = _canvas.TextWidth(measured);
            _canvas.Pop();
            _canvas.NoStroke();
            _canvas.Fill(accent);
            _canvas.Rect(
                new Vector3(
                    measuredPosition.x + measuredWidth * 0.5f,
                    measuredPosition.y + unit * 0.02f,
                    measuredPosition.z
                ),
                new Vector2(measuredWidth, Mathf.Max(AntiAliasMinimum(), unit * 0.004f))
            );

            // 5) Text inside Push/Translate/Rotate: glyph quads follow the current
            // transform like any other shape.
            _canvas.Push();
            _canvas.Translate(Position(0.62f, 0.93f));
            _canvas.Rotate(_animateTransforms ? Mathf.Sin(Time.time * 1.4f) * 0.55f : 0.3f);
            _canvas.TextSize(unit * 0.05f);
            _canvas.Fill(new Color(0.1f, 0.5f, 0.2f));
            _canvas.Text("transformed", 0f, 0f);
            _canvas.Pop();
        }

        // Text is drawn through Push/Translate so the glyph quads inherit the anchor's
        // drawing depth (Text(str, x, y) itself is 2D, exactly like p5's text()).
        private void DrawTextSample(string text, float sizeFactor, float xNorm, float yNorm)
        {
            _canvas.TextSize(Unit() * sizeFactor);
            _canvas.Push();
            _canvas.Translate(Position(xNorm, yNorm));
            _canvas.Text(text, 0f, 0f);
            _canvas.Pop();
        }

        private void DrawTextAlignmentRow(
            float unit,
            float strokeWeight,
            Color accent,
            Color textColor,
            float baselineY
        )
        {
            DrawTextAlignmentSample(
                "left",
                TextAlignMode.Left,
                0.2f,
                baselineY,
                unit,
                strokeWeight,
                accent,
                textColor
            );
            DrawTextAlignmentSample(
                "center",
                TextAlignMode.Center,
                0.5f,
                baselineY,
                unit,
                strokeWeight,
                accent,
                textColor
            );
            DrawTextAlignmentSample(
                "right",
                TextAlignMode.Right,
                0.8f,
                baselineY,
                unit,
                strokeWeight,
                accent,
                textColor
            );
        }

        private void DrawTextAlignmentSample(
            string label,
            TextAlignMode align,
            float xNorm,
            float baselineY,
            float unit,
            float strokeWeight,
            Color accent,
            Color textColor
        )
        {
            // Reference line the text must hug according to its alignment.
            var top = Position(xNorm, baselineY - 0.08f);
            var bottom = Position(xNorm, baselineY + 0.04f);
            _canvas.NoFill();
            _canvas.Stroke(accent);
            _canvas.StrokeWeight(strokeWeight);
            _canvas.Line(top.x, top.y, bottom.x, bottom.y, top.z);

            _canvas.NoStroke();
            _canvas.Fill(textColor);
            _canvas.TextAlign(align);
            _canvas.TextSize(unit * 0.06f);
            _canvas.Push();
            _canvas.Translate(Position(xNorm, baselineY));
            _canvas.Text(label, 0f, 0f);
            _canvas.Pop();
        }

        // Draws a placeholder bar and warns once when no font is assigned; otherwise
        // makes the tester font current.
        private bool RequireTextFont(float unit)
        {
            if (_textFont != null)
            {
                _canvas.TextFont(_textFont);
                return true;
            }

            if (!_textFontWarned)
            {
                _textFontWarned = true;
                Debug.LogWarning(
                    "CanvasTester: assign a UniteFontAsset (Create > p5.Unite > Unite Font "
                    + "with a .ttf renamed to .bytes as its TextAsset) to see the text pages.",
                    this
                );
            }

            _canvas.RectMode = ShapeMode.Center;
            _canvas.NoStroke();
            _canvas.Fill(new Color(1f, 0.25f, 0.55f, 0.5f));
            _canvas.Rect(Position(0.5f, 0.55f), new Vector2(unit * 0.7f, unit * 0.06f));
            return false;
        }

        // Exercises the layout, metrics and outline APIs: vertical alignment against a
        // guide line, WORD / CHAR wrapping inside text boxes, synthetic styles and
        // weights, TextBounds() / FontBounds() rectangles and TextToPoints() samples.
        // Everything runs inside Push/Pop, which also restores the text attributes.
        private void DrawTextLayoutPage()
        {
            float unit = Unit();
            if (!RequireTextFont(unit))
                return;

            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.003f);
            var textColor = new Color(0.04f, 0.07f, 0.12f);
            var accent = new Color(0.0f, 0.45f, 0.7f);
            var warm = new Color(0.8f, 0.35f, 0.0f);

            _canvas.Push();
            _canvas.NoStroke();
            _canvas.RectMode = ShapeMode.Corner;

            // 1) Vertical alignment: every label anchors on the same guide line.
            var guideStart = Position(0.04f, 0.12f);
            var guideEnd = Position(0.96f, 0.12f);
            _canvas.NoFill();
            _canvas.Stroke(accent);
            _canvas.StrokeWeight(strokeWeight);
            _canvas.Line(guideStart.x, guideStart.y, guideEnd.x, guideEnd.y, guideStart.z);

            _canvas.NoStroke();
            _canvas.Fill(textColor);
            _canvas.TextSize(unit * 0.045f);
            DrawAlignedLabel("Top", TextVerticalAlignMode.Top, 0.06f);
            DrawAlignedLabel("Center", TextVerticalAlignMode.Center, 0.3f);
            DrawAlignedLabel("Baseline", TextVerticalAlignMode.Baseline, 0.54f);
            DrawAlignedLabel("Bottom", TextVerticalAlignMode.Bottom, 0.78f);

            // 2) Text boxes: WORD wrap centered in the box, CHAR wrap from the top-left.
            _canvas.TextSize(unit * 0.035f);
            DrawTextBox(
                "Words wrap at spaces inside the box and the block is centered both ways.",
                TextWrapMode.Word, TextAlignMode.Center, TextVerticalAlignMode.Center,
                0.05f, 0.2f, accent, textColor, strokeWeight
            );
            DrawTextBox(
                "CHAR_wrapping_breaks_anywhere_needed_to_stay_inside_the_box",
                TextWrapMode.Char, TextAlignMode.Left, TextVerticalAlignMode.Top,
                0.53f, 0.2f, warm, textColor, strokeWeight
            );

            // 3) Synthetic styles and weights.
            _canvas.TextAlign(TextAlignMode.Left, TextVerticalAlignMode.Baseline);
            _canvas.TextSize(unit * 0.05f);
            _canvas.Fill(textColor);
            DrawStyledLabel("Normal", TextStyleMode.Normal, 400f, 0.05f, 0.62f);
            DrawStyledLabel("Italic", TextStyleMode.Italic, 400f, 0.28f, 0.62f);
            DrawStyledLabel("Bold", TextStyleMode.Bold, 400f, 0.5f, 0.62f);
            DrawStyledLabel("BoldItalic", TextStyleMode.BoldItalic, 400f, 0.7f, 0.62f);
            DrawStyledLabel("w200", TextStyleMode.Normal, 200f, 0.05f, 0.71f);
            DrawStyledLabel("w400", TextStyleMode.Normal, 400f, 0.28f, 0.71f);
            DrawStyledLabel("w700", TextStyleMode.Normal, 700f, 0.5f, 0.71f);
            DrawStyledLabel("w900", TextStyleMode.Normal, 900f, 0.7f, 0.71f);

            // 4) Bounds: FontBounds (loose, outline) around TextBounds (tight, filled).
            const string measured = "Bounds gy";
            _canvas.TextSize(unit * 0.08f);
            _canvas.Push();
            _canvas.Translate(Position(0.06f, 0.86f));
            var loose = _canvas.FontBounds(measured, 0f, 0f);
            var tight = _canvas.TextBounds(measured, 0f, 0f);
            _canvas.NoStroke();
            _canvas.Fill(new Color(accent.r, accent.g, accent.b, 0.3f));
            _canvas.Rect(new Vector3(tight.xMin, tight.yMin, 0f), tight.size);
            _canvas.NoFill();
            _canvas.Stroke(warm);
            _canvas.StrokeWeight(strokeWeight);
            _canvas.Rect(new Vector3(loose.xMin, loose.yMin, 0f), loose.size);
            _canvas.NoStroke();
            _canvas.Fill(textColor);
            _canvas.Text(measured, 0f, 0f);
            _canvas.Pop();

            // 5) TextToPoints: outline samples of "p5" drawn as dots.
            _canvas.TextSize(unit * 0.2f);
            var options = TextPathOptions.FromCanvas(_canvas);
            options.SampleFactor = 0.08f;
            var points = _textFont.TextToPoints("p5", 0f, 0f, options);
            _canvas.Push();
            _canvas.Translate(Position(0.62f, 0.9f));
            _canvas.Fill(warm);
            float dot = Mathf.Max(AntiAliasMinimum() * 2f, unit * 0.006f);
            foreach (var point in points)
                _canvas.Circle(new Vector3(point.X, point.Y, 0f), dot);
            _canvas.Pop();

            _canvas.Pop();
        }

        private void DrawAlignedLabel(string label, TextVerticalAlignMode align, float xNorm)
        {
            _canvas.TextAlign(TextAlignMode.Left, align);
            _canvas.Push();
            _canvas.Translate(Position(xNorm, 0.12f));
            _canvas.Text(label, 0f, 0f);
            _canvas.Pop();
        }

        private void DrawTextBox(
            string text,
            TextWrapMode wrap,
            TextAlignMode align,
            TextVerticalAlignMode verticalAlign,
            float xNorm,
            float yNorm,
            Color frame,
            Color textColor,
            float strokeWeight
        )
        {
            var origin = Position(xNorm, yNorm);
            var corner = Position(xNorm + 0.42f, yNorm + 0.3f);
            float width = Mathf.Abs(corner.x - origin.x);
            float height = Mathf.Abs(corner.y - origin.y);

            _canvas.Push();
            _canvas.Translate(origin);
            _canvas.NoFill();
            _canvas.Stroke(frame);
            _canvas.StrokeWeight(strokeWeight);
            _canvas.Rect(Vector3.zero, new Vector2(width, height));
            _canvas.NoStroke();
            _canvas.Fill(textColor);
            _canvas.TextWrap(wrap);
            _canvas.TextAlign(align, verticalAlign);
            _canvas.Text(text, 0f, 0f, width, height);
            _canvas.Pop();
        }

        private void DrawStyledLabel(string label, TextStyleMode style, float weight, float xNorm, float yNorm)
        {
            _canvas.Push();
            _canvas.TextStyle(style);
            _canvas.TextWeight(weight);
            _canvas.Translate(Position(xNorm, yNorm));
            _canvas.Text(label, 0f, 0f);
            _canvas.Pop();
        }
    }
}
