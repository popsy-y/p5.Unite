using NUnit.Framework;
using Unite.Canvas;
using Unite.Text.Fonts;
using Unite.Text.Shaping;
using UnityEngine;

namespace Unite.Text.Tests
{
    /// <summary>System fonts load in the background, so tests run without them (deterministic results).</summary>
    [SetUpFixture]
    public class TextTestSetup
    {
        private bool _previous;

        [OneTimeSetUp]
        public void DisableSystemFonts()
        {
            _previous = FontFace.SystemFontFallback;
            FontFace.SystemFontFallback = false;
        }

        [OneTimeTearDown]
        public void RestoreSystemFonts() => FontFace.SystemFontFallback = _previous;
    }

    // Font stacks: TestFontBuilder's font (A, B) first, the shaping test font (a, f, i,
    // Arabic beh, ...) as fallback.
    public class FallbackTests
    {
        private static FontFace Primary()
        {
            Assert.IsTrue(FontFace.TryParse(TestFontBuilder.Build(), out var face));
            return face;
        }

        private static FontFace Secondary()
        {
            Assert.IsTrue(FontFace.TryParse(ShapingTestFontData.Font, out var face));
            return face;
        }

        [Test]
        public void ClustersUseTheFirstFontThatHasThem()
        {
            var stack = FontStack.Of(new[] { Primary().Default, Secondary().Default });
            var shaped = ShapingCache.Get(stack, "Aa A", TextDirectionMode.Ltr);

            Assert.AreEqual(4, shaped.GlyphCount);
            Assert.AreEqual(0, shaped.Glyphs[0].Face); // A: primary
            Assert.AreEqual(1, shaped.Glyphs[1].Face); // a: only the fallback has it
            Assert.AreEqual(1, shaped.Glyphs[2].Face); // space stays with the font before it
            Assert.AreEqual(0, shaped.Glyphs[3].Face); // A: back to the primary
            Assert.AreEqual(ShapingTestFontData.a, shaped.Glyphs[1].GlyphId);
        }

        [Test]
        public void MissingCharactersDrawThePrimaryNotdef()
        {
            var stack = FontStack.Of(new[] { Primary().Default, Secondary().Default });
            var shaped = ShapingCache.Get(stack, "A一", TextDirectionMode.Ltr);

            Assert.AreEqual(2, shaped.GlyphCount);
            Assert.AreEqual(0, shaped.Glyphs[1].Face);
            Assert.AreEqual(0, shaped.Glyphs[1].GlyphId);
        }

        [Test]
        public void FallbackFontsShapeTheirOwnRuns()
        {
            // The ligature and the Arabic forms come from the fallback's own GSUB.
            var stack = FontStack.Of(new[] { Primary().Default, Secondary().Default });
            var shaped = ShapingCache.Get(stack, "Afi", TextDirectionMode.Ltr);
            Assert.AreEqual(2, shaped.GlyphCount);
            Assert.AreEqual(ShapingTestFontData.f_i, shaped.Glyphs[1].GlyphId);

            shaped = ShapingCache.Get(stack, "بب", TextDirectionMode.Rtl);
            Assert.AreEqual(ShapingTestFontData.beh_init, shaped.Glyphs[0].GlyphId);
            Assert.AreEqual(ShapingTestFontData.beh_fina, shaped.Glyphs[1].GlyphId);
        }

        [Test]
        public void TextFontTakesAStack()
        {
            var gameObject = new GameObject("TextFontTakesAStack");
            try
            {
                var canvas = gameObject.AddComponent<UCanvas>();
                var primary = Primary();
                var secondary = Secondary();
                canvas.TextFont(primary, secondary);

                Assert.AreSame(primary, canvas.TextFont());
                CollectionAssert.AreEqual(new[] { primary, secondary }, canvas.TextFonts());

                var state = TextCanvasExtensions.State(canvas);
                Assert.IsTrue(state.TryGetStack(out var stack));
                Assert.AreEqual(2, stack.Count);

                canvas.TextFont(primary);
                CollectionAssert.AreEqual(new[] { primary }, canvas.TextFonts());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
