using NUnit.Framework;
using Unite.Canvas;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text.Tests
{
    // Test font at size 1000 = glyph units: 'A' is a 100..700 square with an 800
    // advance, 'B' the same square shifted +100, any other code point (space) maps to
    // the blank .notdef with a 500 advance. Ascender 800, descender -200.
    public class TextLayoutTests
    {
        private const float Size = 1000f;
        private const float Tolerance = 0.01f;

        private static FontInstance Font()
        {
            Assert.IsTrue(FontFace.TryParse(TestFontBuilder.Build(), out var face));
            return face.Default;
        }

        private static TextLayoutSettings Settings() => new()
        {
            Size = Size,
            Leading = Size * TextState.DefaultLeadingFactor,
            YSign = 1f
        };

        [Test]
        public void LineWidthUsesAdvances()
        {
            var layout = TextLayout.Layout(Font(), Settings(), "AB", 0f, 0f);

            Assert.AreEqual(1, layout.Lines.Count);
            Assert.AreEqual(1600f, layout.Lines[0].Width, Tolerance);
            Assert.AreEqual(800f, layout.Glyphs[1].PenX, Tolerance);
        }

        [Test]
        public void InkBoundsFollowTheOutline()
        {
            var font = Font();
            var settings = Settings();
            var layout = TextLayout.Layout(font, settings, "AB", 0f, 0f);

            Assert.IsTrue(TextLayout.TryGetInkBounds(font, settings, layout.Glyphs[1], out var min, out var max));
            Assert.AreEqual(1000f, min.x, Tolerance); // pen 800 + composite offset 100 + 100
            Assert.AreEqual(1600f, max.x, Tolerance);
            Assert.AreEqual(-700f, min.y, Tolerance); // y-down: ink top is above the baseline
            Assert.AreEqual(-100f, max.y, Tolerance);
        }

        [Test]
        public void HorizontalAlignmentAnchorsTheLine()
        {
            var settings = Settings();
            settings.Align = TextAlignMode.Center;
            Assert.AreEqual(-800f, TextLayout.Layout(Font(), settings, "AB", 0f, 0f).Lines[0].Left, Tolerance);

            settings.Align = TextAlignMode.Right;
            Assert.AreEqual(-1600f, TextLayout.Layout(Font(), settings, "AB", 0f, 0f).Lines[0].Left, Tolerance);
        }

        [TestCase(TextVerticalAlignMode.Baseline, 0f)]
        [TestCase(TextVerticalAlignMode.Top, 800f)]
        [TestCase(TextVerticalAlignMode.Center, 300f)]
        [TestCase(TextVerticalAlignMode.Bottom, -200f)]
        public void VerticalAlignmentPlacesTheBaseline(TextVerticalAlignMode align, float expectedBaseline)
        {
            var settings = Settings();
            settings.VerticalAlign = align;

            var layout = TextLayout.Layout(Font(), settings, "A", 0f, 0f);

            Assert.AreEqual(expectedBaseline, layout.Lines[0].Baseline, Tolerance);
        }

        [Test]
        public void YUpLayoutMirrorsVerticalOffsets()
        {
            var settings = Settings();
            settings.YSign = -1f;
            settings.VerticalAlign = TextVerticalAlignMode.Top;

            var layout = TextLayout.Layout(Font(), settings, "A\nA", 0f, 0f);

            Assert.AreEqual(-800f, layout.Lines[0].Baseline, Tolerance);
            Assert.AreEqual(-2050f, layout.Lines[1].Baseline, Tolerance);
        }

        [Test]
        public void LineFeedsAdvanceByTheLeading()
        {
            var layout = TextLayout.Layout(Font(), Settings(), "A\r\nA", 0f, 0f);

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(1250f, layout.Lines[1].Baseline, Tolerance);
        }

        [Test]
        public void WordWrapBreaksAtSpaces()
        {
            // "A A" = 800 + 500 + 800 = 2100 fits 2200; the third word wraps.
            var layout = TextLayout.Layout(Font(), Settings(), "A A A", 0f, 0f, new TextBox(0f, 0f, 2200f, float.PositiveInfinity));

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(2100f, layout.Lines[0].Width, Tolerance);
            Assert.AreEqual(800f, layout.Lines[1].Width, Tolerance);
            Assert.AreEqual(800f, layout.Lines[0].Baseline, Tolerance); // box top + ascent
        }

        [Test]
        public void CharWrapBreaksInsideWords()
        {
            var settings = Settings();
            settings.Wrap = TextWrapMode.Char;

            var layout = TextLayout.Layout(Font(), settings, "AAA", 0f, 0f, new TextBox(0f, 0f, 1700f, float.PositiveInfinity));

            Assert.AreEqual(2, layout.Lines.Count);
            Assert.AreEqual(2, layout.Lines[0].GlyphCount);
        }

        [Test]
        public void BoundedBoxDropsOverflowingLines()
        {
            var layout = TextLayout.Layout(Font(), Settings(), "A\nA\nA", 0f, 0f, new TextBox(0f, 0f, 5000f, 2000f));

            Assert.AreEqual(1, layout.Lines.Count);
        }

        [Test]
        public void BoxAlignmentUsesTheBoxEdges()
        {
            var settings = Settings();
            settings.Align = TextAlignMode.Right;
            settings.VerticalAlign = TextVerticalAlignMode.Bottom;

            var layout = TextLayout.Layout(Font(), settings, "A", 0f, 0f, new TextBox(100f, 0f, 3000f, 2000f));

            Assert.AreEqual(3100f - 800f, layout.Lines[0].Left, Tolerance);
            Assert.AreEqual(1800f, layout.Lines[0].Baseline, Tolerance); // box bottom - descent
        }

        [Test]
        public void RightToLeftReversesGlyphOrder()
        {
            var settings = Settings();
            settings.Direction = TextDirectionMode.Rtl;

            var layout = TextLayout.Layout(Font(), settings, "AB", 0f, 0f);

            Assert.AreEqual(2, layout.Glyphs[0].GlyphId);
            Assert.AreEqual(1, layout.Glyphs[1].GlyphId);
        }

        [Test]
        public void SpacingAndWeightWidenTheLine()
        {
            var settings = Settings();
            settings.LetterSpacing = 10f;
            settings.Embolden = TextLayoutSettings.EmboldenFor(700f, Size);

            var layout = TextLayout.Layout(Font(), settings, "AB", 0f, 0f);

            Assert.AreEqual(21f, settings.Embolden, Tolerance);
            Assert.AreEqual(1600f + 20f + 4f * 21f, layout.Lines[0].Width, Tolerance);
        }

        [Test]
        public void TextStateFollowsPushAndPop()
        {
            var gameObject = new GameObject("TextStateFollowsPushAndPop");
            try
            {
                var canvas = gameObject.AddComponent<UCanvas>();
                canvas.TextSize(20f);
                canvas.TextAlign(TextAlignMode.Center, TextVerticalAlignMode.Top);

                canvas.Push();
                canvas.TextSize(40f);
                canvas.TextStyle(TextStyleMode.Bold);
                Assert.AreEqual(40f, canvas.TextSize());
                canvas.Pop();

                Assert.AreEqual(20f, canvas.TextSize());
                Assert.AreEqual(TextStyleMode.Normal, canvas.TextStyle());
                Assert.AreEqual((TextAlignMode.Center, TextVerticalAlignMode.Top), canvas.TextAlign());
                Assert.AreEqual(25f, canvas.TextLeading(), Tolerance); // 1.25 x size until set
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TextPropertiesRoundTrip()
        {
            var gameObject = new GameObject("TextPropertiesRoundTrip");
            try
            {
                var canvas = gameObject.AddComponent<UCanvas>();
                canvas.TextProperty("fontSize", 30);
                canvas.TextProperty("textStyle", "BOLDITALIC");
                canvas.TextProperty("textBaseline", "middle");
                canvas.TextProperty("letterSpacing", 2.5f);

                Assert.AreEqual(30f, canvas.TextSize());
                Assert.AreEqual(TextStyleMode.BoldItalic, canvas.TextProperty("fontStyle"));
                Assert.AreEqual(TextVerticalAlignMode.Center, canvas.TextAlign().Vertical);
                Assert.AreEqual(2.5f, canvas.TextProperties()["letterSpacing"]);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
