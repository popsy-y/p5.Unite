using System.Collections;
using NUnit.Framework;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text.Tests
{
    // The test fonts have glyphs .notdef, space, A, B, C (ids 0..4): A is a square
    // (100,100)-(700,700) advance 800, B a round glyph advance 600, C a composite of A
    // shifted +100 (advance 900). Variable fonts add a wght axis whose heavy master
    // (900) grows A to 800 wide, B's advance to 700 and C's to 1100.
    public class FontFormatTests
    {
        private const float Tolerance = 0.01f;
        private const int GlyphA = 2;
        private const int GlyphB = 3;
        private const int GlyphC = 4;

        private static FontFace Parse(byte[] data)
        {
            Assert.IsTrue(FontFace.TryParse(data, out var face));
            return face;
        }

        private static IEnumerable TrueTypeFormats()
        {
            yield return new TestCaseData(TestFontData.StaticWoff).SetName("WOFF");
            yield return new TestCaseData(TestFontData.StaticWoff2).SetName("WOFF2");
        }

        private static IEnumerable VariableFormats()
        {
            yield return new TestCaseData(TestFontData.Variable).SetName("TTF");
            yield return new TestCaseData(TestFontData.VariableWoff).SetName("WOFF");
            yield return new TestCaseData(TestFontData.VariableWoff2).SetName("WOFF2");
        }

        private static void AssertSameOutline(GlyphOutline expected, GlyphOutline actual)
        {
            Assert.AreEqual(expected.Contours.Length, actual.Contours.Length);
            for (int contour = 0; contour < expected.Contours.Length; contour++)
            {
                Assert.AreEqual(expected.Contours[contour].Length, actual.Contours[contour].Length);
                for (int point = 0; point < expected.Contours[contour].Length; point++)
                {
                    Assert.AreEqual(expected.Contours[contour][point].x, actual.Contours[contour][point].x, Tolerance);
                    Assert.AreEqual(expected.Contours[contour][point].y, actual.Contours[contour][point].y, Tolerance);
                }
            }

            Assert.AreEqual(expected.Commands.Length, actual.Commands.Length);
            for (int index = 0; index < expected.Commands.Length; index++)
            {
                Assert.AreEqual(expected.Commands[index].Type, actual.Commands[index].Type);
                Assert.AreEqual(expected.Commands[index].Point.x, actual.Commands[index].Point.x, Tolerance);
                Assert.AreEqual(expected.Commands[index].Point.y, actual.Commands[index].Point.y, Tolerance);
            }
        }

        [Test]
        public void TrueTypeFontParsesMetricsAndOutlines()
        {
            var face = Parse(TestFontData.StaticTtf);
            var instance = face.Default;

            Assert.AreEqual(1000, face.UnitsPerEm);
            Assert.AreEqual(5, face.GlyphCount);
            Assert.AreEqual(800, instance.Ascender);
            Assert.AreEqual(-200, instance.Descender);
            Assert.IsFalse(face.IsVariable);
            Assert.AreEqual(GlyphC, instance.GetGlyphId('C'));
            Assert.AreEqual(800f, instance.Advance(GlyphA));

            Assert.IsTrue(instance.TryGetOutline(GlyphA, out var square));
            Assert.AreEqual(new Vector2(100f, 100f), square.BoundsMin);
            Assert.AreEqual(new Vector2(700f, 700f), square.BoundsMax);

            Assert.IsTrue(instance.TryGetOutline(GlyphB, out var round));
            Assert.AreEqual(1, round.Contours.Length);
            Assert.AreEqual(100f, round.BoundsMin.x, Tolerance);
            Assert.AreEqual(500f, round.BoundsMax.x, Tolerance);

            Assert.IsTrue(instance.TryGetOutline(GlyphC, out var composite));
            Assert.AreEqual(200f, composite.BoundsMin.x, Tolerance);
            Assert.AreEqual(800f, composite.BoundsMax.x, Tolerance);
            Assert.IsFalse(instance.TryGetOutline(1, out _)); // space
        }

        [TestCaseSource(nameof(TrueTypeFormats))]
        public void WebFontWrappersMatchTheTrueTypeFont(byte[] wrapped)
        {
            var reference = Parse(TestFontData.StaticTtf).Default;
            var instance = Parse(wrapped).Default;

            Assert.AreEqual(reference.UnitsPerEm, instance.UnitsPerEm);
            Assert.AreEqual(reference.Ascender, instance.Ascender);
            for (int glyph = 0; glyph < reference.GlyphCount; glyph++)
            {
                Assert.AreEqual(reference.Advance(glyph), instance.Advance(glyph), Tolerance);
                bool expected = reference.TryGetOutline(glyph, out var expectedOutline);
                Assert.AreEqual(expected, instance.TryGetOutline(glyph, out var actualOutline));
                if (expected)
                    AssertSameOutline(expectedOutline, actualOutline);
            }
        }

        [Test]
        public void CffFontParsesCubicOutlines()
        {
            var instance = Parse(TestFontData.Cff).Default;

            Assert.AreEqual(1000, instance.UnitsPerEm);
            Assert.AreEqual(600f, instance.Advance(GlyphB));

            Assert.IsTrue(instance.TryGetOutline(GlyphA, out var square));
            Assert.AreEqual(new Vector2(100f, 100f), square.BoundsMin);
            Assert.AreEqual(new Vector2(700f, 700f), square.BoundsMax);

            Assert.IsTrue(instance.TryGetOutline(GlyphB, out var round));
            Assert.AreEqual(1, round.Contours.Length);
            Assert.AreEqual(100f, round.BoundsMin.x, Tolerance);
            Assert.AreEqual(500f, round.BoundsMax.x, Tolerance);
            Assert.AreEqual(50f, round.BoundsMin.y, Tolerance);
            Assert.AreEqual(550f, round.BoundsMax.y, Tolerance);

            int cubics = 0;
            foreach (var command in round.Commands)
                cubics += command.Type == PathCommandType.CubicTo ? 1 : 0;
            Assert.AreEqual(4, cubics);
        }

        [Test]
        public void CffInWoff2MatchesTheOpenTypeFont()
        {
            var reference = Parse(TestFontData.Cff).Default;
            var instance = Parse(TestFontData.CffWoff2).Default;

            for (int glyph = 0; glyph < reference.GlyphCount; glyph++)
            {
                bool expected = reference.TryGetOutline(glyph, out var expectedOutline);
                Assert.AreEqual(expected, instance.TryGetOutline(glyph, out var actualOutline));
                if (expected)
                    AssertSameOutline(expectedOutline, actualOutline);
            }
        }

        [Test]
        public void VariableFontExposesItsAxes()
        {
            var face = Parse(TestFontData.Variable);

            Assert.IsTrue(face.IsVariable);
            Assert.AreEqual(1, face.Axes.Count);
            Assert.AreEqual("wght", face.Axes[0].Tag);
            Assert.AreEqual(100f, face.Axes[0].Minimum);
            Assert.AreEqual(400f, face.Axes[0].Default);
            Assert.AreEqual(900f, face.Axes[0].Maximum);
        }

        [TestCaseSource(nameof(VariableFormats))]
        public void VariableInstancesInterpolateOutlinesAndAdvances(byte[] data)
        {
            var face = Parse(data);

            var regular = face.GetInstance(null);
            Assert.AreSame(face.Default, regular);
            Assert.AreEqual(800f, regular.Advance(GlyphA), Tolerance);
            Assert.IsTrue(regular.TryGetOutline(GlyphA, out var regularSquare));
            Assert.AreEqual(700f, regularSquare.BoundsMax.x, Tolerance);

            var heavy = face.GetInstance(new[] { new AxisSetting("wght", 900f) });
            Assert.AreNotSame(face.Default, heavy);
            Assert.AreEqual(1000f, heavy.Advance(GlyphA), Tolerance);
            Assert.AreEqual(700f, heavy.Advance(GlyphB), Tolerance);
            Assert.AreEqual(1100f, heavy.Advance(GlyphC), Tolerance);
            Assert.IsTrue(heavy.TryGetOutline(GlyphA, out var heavySquare));
            Assert.AreEqual(new Vector2(100f, 100f), heavySquare.BoundsMin);
            Assert.AreEqual(new Vector2(800f, 800f), heavySquare.BoundsMax);

            // The composite follows its component's variation.
            Assert.IsTrue(heavy.TryGetOutline(GlyphC, out var heavyComposite));
            Assert.AreEqual(900f, heavyComposite.BoundsMax.x, Tolerance);
        }

        [Test]
        public void AvarRemapsTheRequestedWeight()
        {
            var face = Parse(TestFontData.Variable);

            // avar maps wght 650 (halfway to the heavy master) to 80% of the way.
            var instance = face.GetInstance(new[] { new AxisSetting("wght", 650f) });

            Assert.AreEqual(800f + 0.8f * 200f, instance.Advance(GlyphA), Tolerance);
        }

        [Test]
        public void InstancesAreSharedAndKeyedByAxisValues()
        {
            var face = Parse(TestFontData.Variable);
            var settings = new[] { new AxisSetting("wght", 700f) };

            var first = face.GetInstance(settings);
            var second = face.GetInstance(new[] { new AxisSetting("wght", 700f) });
            var other = face.GetInstance(new[] { new AxisSetting("wght", 800f) });

            Assert.AreSame(first, second);
            Assert.AreNotEqual(first.Id, other.Id);
            Assert.AreNotEqual(face.Default.Id, first.Id);
            Assert.AreSame(face.Default, face.GetInstance(new[] { new AxisSetting("wdth", 50f) })); // unknown axis
            Assert.AreSame(face.Default, face.GetInstance(new[] { new AxisSetting("wght", 400f) }));
        }

        [Test]
        public void TextStateMapsStyleOntoAxesWithoutSnapping()
        {
            var face = FontFace.LoadFont(TestFontData.Variable, "variable");
            var state = new TextState { Font = face, Weight = 651f };
            Assert.IsTrue(state.TryGetInstance(out var first, out bool synthesizeWeight, out _));
            Assert.IsFalse(synthesizeWeight); // the wght axis draws the weight

            // Axis values are used as given: a nearby weight is a different instance.
            state.Weight = 653f;
            state.TryGetInstance(out var neighbor, out _, out _);
            Assert.AreNotSame(first, neighbor);
            Assert.AreNotEqual(first.Id, neighbor.Id);

            // Going back finds the same instance and the same id (glyphs stay baked).
            state.Weight = 651f;
            state.TryGetInstance(out var again, out _, out _);
            Assert.AreSame(first, again);

            // Bold maps onto the weight axis too; an explicit axis wins over TextWeight.
            state.Weight = 400f;
            state.Style = TextStyleMode.Bold;
            state.TryGetInstance(out var bold, out _, out _);
            Assert.AreSame(face.GetInstance(new[] { new AxisSetting("wght", 700f) }), bold);

            state.Variations = new[] { new AxisSetting("wght", 400f) };
            state.TryGetInstance(out var explicitInstance, out _, out _);
            Assert.AreSame(face.Default, explicitInstance);
        }

        [Test]
        public void EvictedInstancesComeBackWithTheirIds()
        {
            var face = FontFace.LoadFont(TestFontData.Variable, "variable");
            var first = face.GetInstance(new[] { new AxisSetting("wght", 500f) });
            int id = first.Id;

            for (int weight = 0; weight < 100; weight++)
                face.GetInstance(new[] { new AxisSetting("wght", 600f + weight) });

            var recreated = face.GetInstance(new[] { new AxisSetting("wght", 500f) });
            Assert.AreNotSame(first, recreated);
            Assert.AreEqual(id, recreated.Id);
        }

        [Test]
        public void StaticFontsSynthesizeStyles()
        {
            var face = FontFace.LoadFont(TestFontData.StaticTtf, "static");
            var state = new TextState { Font = face, Style = TextStyleMode.BoldItalic };
            Assert.IsTrue(state.TryGetInstance(out _, out bool synthesizeWeight, out bool synthesizeItalic));
            Assert.IsTrue(synthesizeWeight);
            Assert.IsTrue(synthesizeItalic);
        }

        [Test]
        public void StaticFontsIgnoreAxisSettings()
        {
            var face = Parse(TestFontData.StaticTtf);

            Assert.AreSame(face.Default, face.GetInstance(new[] { new AxisSetting("wght", 900f) }));
        }

        [Test]
        public void GlyphKeysSeparateInstances()
        {
            Assert.AreNotEqual(new GlyphKey(1, 2, 64, 5), new GlyphKey(1, 2, 64, 6));
            Assert.AreEqual(new GlyphKey(1, 2, 64, 5), new GlyphKey(1, 2, 64, 5));
        }

        [Test]
        public void UnsupportedOrCorruptDataIsRejectedWithAReason()
        {
            Assert.IsFalse(FontLoader.TryLoad(new byte[64], out _, out string signatureError));
            StringAssert.Contains("signature", signatureError);

            byte[] truncated = new byte[TestFontData.StaticWoff2.Length / 2];
            System.Array.Copy(TestFontData.StaticWoff2, truncated, truncated.Length);
            Assert.IsFalse(FontLoader.TryLoad(truncated, out _, out string woffError));
            Assert.IsNotEmpty(woffError);

            Assert.IsFalse(FontLoader.TryLoad(null, out _, out string nullError));
            Assert.IsNotEmpty(nullError);
        }

        [Test]
        public void FontsLoadEveryFormatAtRuntime()
        {
            foreach (var data in new[]
            {
                TestFontData.StaticTtf, TestFontData.StaticWoff, TestFontData.StaticWoff2,
                TestFontData.Cff, TestFontData.CffWoff2, TestFontData.Variable, TestFontData.VariableWoff2
            })
            {
                Assert.AreEqual(1000, FontFace.LoadFont(data, "test").UnitsPerEm);
            }

            Assert.Throws<System.IO.InvalidDataException>(() => FontFace.LoadFont(new byte[100], "broken"));
        }
    }
}
