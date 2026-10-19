using System.Collections.Generic;
using NUnit.Framework;
using Unite.Text.Fonts;
using Unite.Text.Shaping;
using static Unite.Text.Tests.ShapingTestFontData;

namespace Unite.Text.Tests
{
    // OpenType shaping on the synthetic font of ShapingTestFontData (fontTools feaLib):
    // ligatures, kerning, features and languages from text properties, Arabic joining and
    // mark positioning. Metrics are in em (font units / 1000).
    public class ShapingTests
    {
        private const float Tolerance = 0.0005f;

        private static FontInstance _font;

        private static FontInstance Font()
        {
            if (_font == null)
            {
                Assert.IsTrue(FontFace.TryParse(ShapingTestFontData.Font, out var face));
                _font = face.Default;
            }
            return _font;
        }

        private static ShapedText Shape(string text, TextDirectionMode direction = TextDirectionMode.Ltr,
            string kerning = null, string ligatures = null, string settings = null, string language = null)
            => ShapingCache.Get(Font(), text, direction, TextFeatures.From(kerning, ligatures, settings), LanguageTags.For(language));

        private static int[] Glyphs(ShapedText shaped)
        {
            var glyphs = new List<int>();
            for (int index = 0; index < shaped.GlyphCount; index++)
                glyphs.Add(shaped.Glyphs[index].GlyphId);
            return glyphs.ToArray();
        }

        [Test]
        public void LigaturesFormByDefaultAndFollowTheProperties()
        {
            var shaped = Shape("fi");
            CollectionAssert.AreEqual(new[] { f_i }, Glyphs(shaped));
            Assert.AreEqual(0.52f, shaped.Glyphs[0].Advance, Tolerance);
            Assert.IsFalse(shaped.ClusterStarts[1], "a ligature joins the clusters of its parts");

            CollectionAssert.AreEqual(new[] { f, i }, Glyphs(Shape("fi", ligatures: "none")));
            CollectionAssert.AreEqual(new[] { f, i }, Glyphs(Shape("fi", settings: "\"liga\" 0")));
            CollectionAssert.AreEqual(new[] { f, i }, Glyphs(Shape("f‌i")), "ZWNJ blocks the ligature");
        }

        [Test]
        public void KerningAndFeatureSettingsApply()
        {
            var kerned = Shape("AV");
            Assert.AreEqual(0.5f, kerned.Glyphs[0].Advance, Tolerance);
            Assert.AreEqual(0.6f, Shape("AV", kerning: "none").Glyphs[0].Advance, Tolerance);

            CollectionAssert.AreEqual(new[] { a }, Glyphs(Shape("a")));
            CollectionAssert.AreEqual(new[] { a_sc }, Glyphs(Shape("a", settings: "'smcp'")));
            CollectionAssert.AreEqual(new[] { a_sc }, Glyphs(Shape("a", settings: "\"smcp\" on, \"liga\" 1")));
        }

        [Test]
        public void LanguageSelectsLocalizedForms()
        {
            CollectionAssert.AreEqual(new[] { i }, Glyphs(Shape("i")));
            CollectionAssert.AreEqual(new[] { i_TRK }, Glyphs(Shape("i", language: "tr")));
            CollectionAssert.AreEqual(new[] { i_TRK }, Glyphs(Shape("i", language: "tr-TR")));
        }

        [Test]
        public void ArabicLettersTakeTheirJoiningForms()
        {
            CollectionAssert.AreEqual(new[] { beh }, Glyphs(Shape("ب", TextDirectionMode.Rtl)));
            CollectionAssert.AreEqual(new[] { beh_init, beh_fina }, Glyphs(Shape("بب", TextDirectionMode.Rtl)));
            CollectionAssert.AreEqual(new[] { beh_init, beh_medi, beh_fina }, Glyphs(Shape("ببب", TextDirectionMode.Rtl)));
            CollectionAssert.AreEqual(new[] { beh, beh }, Glyphs(Shape("ب‌ب", TextDirectionMode.Rtl)), "ZWNJ breaks the joining");
        }

        [Test]
        public void MarksAttachToTheirBase()
        {
            var shaped = Shape("Á");
            CollectionAssert.AreEqual(new[] { A, acutecomb }, Glyphs(shaped));
            Assert.AreEqual(0f, shaped.Glyphs[1].Advance, Tolerance);
            // Anchors meet 425 units right of the base origin; offsets are relative to the
            // mark's own pen position, which follows the base's 600 advance.
            Assert.AreEqual(0.425f - 0.6f, shaped.Glyphs[1].OffsetX, Tolerance);
            Assert.AreEqual(0f, shaped.Glyphs[1].OffsetY, Tolerance);
            Assert.AreEqual(0, shaped.Glyphs[1].Cluster);
        }

        [Test]
        public void ColorGlyphsListTheirLayers()
        {
            var face = Font().Face;
            Assert.IsTrue(face.HasColorGlyphs);

            var color = face.ColorLayers;
            Assert.IsFalse(color.TryGetLayers(A, out _, out _));
            Assert.IsTrue(color.TryGetLayers(V, out int first, out int count));
            Assert.AreEqual(2, count);

            color.GetLayer(first, out int glyph, out int palette);
            Assert.AreEqual(A, glyph);
            var red = color.LayerColor(palette, new UnityEngine.Color(0f, 1f, 0f, 0.5f));
            Assert.AreEqual(1f, red.r, 0.01f);
            Assert.AreEqual(0.5f, red.a, 0.01f); // the fill alpha applies to palette colors

            color.GetLayer(first + 1, out glyph, out palette);
            Assert.AreEqual(i, glyph);
            Assert.AreEqual(UnityEngine.Color.green, color.LayerColor(palette, UnityEngine.Color.green));
        }

        [Test]
        public void TextPropertiesReachTheLayout()
        {
            var gameObject = new UnityEngine.GameObject("TextPropertiesReachTheLayout");
            try
            {
                var canvas = gameObject.AddComponent<Unite.Canvas.UCanvas>();
                canvas.TextProperty("fontKerning", "none");
                canvas.TextProperty("fontVariantLigatures", "none");
                canvas.TextProperty("fontFeatureSettings", "\"smcp\" 1");
                canvas.TextProperty("lang", "tr");
                Assert.AreEqual("none", canvas.TextProperty("fontKerning"));
                Assert.AreEqual("tr", canvas.TextProperty("lang"));

                var settings = TextLayoutSettings.FromState(TextCanvasExtensions.State(canvas), yUp: false);
                Assert.AreEqual(LanguageTags.For("tr"), settings.LanguageTag);
                Assert.AreSame(TextFeatures.From("none", "none", "\"smcp\" 1"), settings.Features);

                canvas.Push();
                canvas.TextProperty("lang", "ja");
                canvas.Pop();
                Assert.AreEqual("tr", canvas.TextProperty("lang"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
