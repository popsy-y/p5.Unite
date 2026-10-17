using NUnit.Framework;
using Unite.Text.Fonts;
using Unite.Text.Raster;
using UnityEngine;

namespace Unite.Text.Tests
{
    public class FontEngineTests
    {
        private static TtfFont BuildTestFont()
        {
            Assert.IsTrue(TtfFont.TryParse(TestFontBuilder.Build(), out var font));
            return font;
        }

        [Test]
        public void ParsesHeadMetrics()
        {
            var font = BuildTestFont();

            Assert.AreEqual(1000, font.UnitsPerEm);
            Assert.AreEqual(800, font.Ascender);
            Assert.AreEqual(-200, font.Descender);
            Assert.AreEqual(3, font.GlyphCount);
        }

        [Test]
        public void MapsCodePointsThroughCmapFormat4()
        {
            var font = BuildTestFont();

            Assert.AreEqual(1, font.GetGlyphId('A'));
            Assert.AreEqual(2, font.GetGlyphId('B'));
            Assert.AreEqual(0, font.GetGlyphId('Z'));
        }

        [Test]
        public void AdvancesComeFromHmtx()
        {
            var font = BuildTestFont();

            Assert.AreEqual(500f, font.Advance(0));
            Assert.AreEqual(800f, font.Advance(1));
            Assert.AreEqual(800f, font.Advance(2));
        }

        [Test]
        public void ParsesSimpleGlyphOutline()
        {
            var font = BuildTestFont();
            Assert.IsTrue(font.TryGetOutline(1, out var outline));

            Assert.AreEqual(1, outline.Contours.Length);
            var square = outline.Contours[0];

            Assert.AreEqual(4, square.Length);
            Assert.AreEqual(new Vector2(100f, 100f), square[0]);
            Assert.AreEqual(new Vector2(700f, 100f), square[1]);
            Assert.AreEqual(new Vector2(700f, 700f), square[2]);
            Assert.AreEqual(new Vector2(100f, 700f), square[3]);

            Assert.AreEqual(new Vector2(100f, 100f), outline.BoundsMin);
            Assert.AreEqual(new Vector2(700f, 700f), outline.BoundsMax);
        }

        [Test]
        public void ResolvesCompositeGlyphTranslation()
        {
            var font = BuildTestFont();
            Assert.IsTrue(font.TryGetOutline(2, out var outline));

            Assert.AreEqual(1, outline.Contours.Length);
            Assert.AreEqual(new Vector2(200f, 100f), outline.Contours[0][0]);
            Assert.AreEqual(new Vector2(800f, 100f), outline.Contours[0][1]);

            Assert.AreEqual(new Vector2(200f, 100f), outline.BoundsMin);
            Assert.AreEqual(new Vector2(800f, 700f), outline.BoundsMax);
        }

        [Test]
        public void BlankGlyphsHaveNoOutline()
        {
            var font = BuildTestFont();
            Assert.IsFalse(font.TryGetOutline(0, out _));
        }

        [Test]
        public void ParseRejectsGarbageWithoutThrowing()
        {
            Assert.IsFalse(TtfFont.TryParse(new byte[] { 0, 0, 1, 0 }, out _));
            Assert.IsFalse(TtfFont.TryParse(null, out _));
        }

        [Test]
        public void RasterizesSquareCoverage()
        {
            // Square with edges on half-texel boundaries so boundary cells mix.
            var square = new[]
            {
                new Vector2(3.5f, 4.5f),
                new Vector2(11.5f, 4.5f),
                new Vector2(11.5f, 11.5f),
                new Vector2(3.5f, 11.5f)
            };

            var coverage = new byte[16 * 16];
            GlyphRasterizer.Rasterize(new[] { square }, 16, 16, coverage);

            Assert.GreaterOrEqual(coverage[8 * 16 + 8], 240);    // deep inside
            Assert.Less(coverage[1 * 16 + 1], 20);               // deep outside
            Assert.Greater(coverage[4 * 16 + 4], 20);            // y edge straddles
            Assert.Less(coverage[4 * 16 + 4], 240);

            for (int index = 0; index < coverage.Length; index++)
            {
                // Nothing may light up beyond the square's anti-aliased bounds.
                bool insideOrBoundary = index % 16 >= 3 && index % 16 <= 11;
                bool rowsOnly = index / 16 >= 4 && index / 16 <= 11;
                if (!insideOrBoundary || !rowsOnly)
                {
                    Assert.Less(coverage[index], 20, $"texel {index} unexpectedly covered");
                }
            }
        }

        [Test]
        public void SdfEncodesSignedRange()
        {
            var square = new[]
            {
                new Vector2(3.5f, 4.5f),
                new Vector2(11.5f, 4.5f),
                new Vector2(11.5f, 11.5f),
                new Vector2(3.5f, 11.5f)
            };

            var coverage = new byte[16 * 16];
            GlyphRasterizer.Rasterize(new[] { square }, 16, 16, coverage);

            var field = new byte[16 * 16];
            SdfEncoder.Encode(coverage, 16, 16, 4f, field);

            Assert.Less(field[8 * 16 + 8], 40);      // deep inside → ~0
            Assert.Greater(field[0 * 16 + 0], 240);  // deep outside → ~255
            Assert.Greater(field[4 * 16 + 8], 60);   // boundary texel → mid grays
            Assert.Less(field[4 * 16 + 8], 200);
        }

        [Test]
        public void SdfEncoderValidatesBuffers()
        {
            var coverage = new byte[16 * 16];

            Assert.Throws<System.ArgumentException>(() =>
                SdfEncoder.Encode(coverage, 16, 16, 4f, new byte[8])
            );
            Assert.Throws<System.ArgumentException>(() =>
                SdfEncoder.Encode(coverage, 16, 16, 0f, new byte[16 * 16])
            );
        }
    }
}

