using NUnit.Framework;
using Unite.Text.Fonts;
using Unite.Text.Raster;
using UnityEngine;

namespace Unite.Text.Tests
{
    public class FontEngineTests
    {
        private static FontInstance BuildTestFont()
        {
            Assert.IsTrue(FontFace.TryParse(TestFontBuilder.Build(), out var face));
            return face.Default;
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
            Assert.IsFalse(FontFace.TryParse(new byte[] { 0, 0, 1, 0 }, out _));
            Assert.IsFalse(FontFace.TryParse(null, out _));
        }

        [Test]
        public void FlattensContourWithoutOnCurvePoints()
        {
            // One contour of four off-curve points (0,0) (100,0) (100,100) (0,100):
            // the curve runs through the implied midpoints and touches every side of
            // the control square. Dropping a control flattens one side instead.
            var record = new byte[]
            {
                0, 1,             // numberOfContours
                0, 0, 0, 0, 0, 0, 0, 0, // bbox (recomputed)
                0, 3,             // endPtsOfContours[0]
                0, 0,             // instructionLength
                0, 0, 0, 0,       // flags: off-curve, int16 deltas
                0, 0, 0, 100, 0, 0, 0xFF, 0x9C, // x deltas: 0, +100, 0, -100
                0, 0, 0, 0, 0, 100, 0, 0        // y deltas: 0, 0, +100, 0
            };

            var outline = TrueTypeOutlines.ParseSimpleRecord(record);

            Assert.AreEqual(1, outline.Contours.Length);
            Assert.AreEqual(0f, outline.BoundsMin.x, 1e-3);
            Assert.AreEqual(0f, outline.BoundsMin.y, 1e-3);
            Assert.AreEqual(100f, outline.BoundsMax.x, 1e-3);
            Assert.AreEqual(100f, outline.BoundsMax.y, 1e-3);
            CollectionAssert.Contains(outline.Contours[0], new Vector2(0f, 50f));
            CollectionAssert.Contains(outline.Contours[0], new Vector2(50f, 0f));
            CollectionAssert.Contains(outline.Contours[0], new Vector2(100f, 50f));
            CollectionAssert.Contains(outline.Contours[0], new Vector2(50f, 100f));
        }

        // Glyph records for the bake equivalence test: a contour of four off-curve points, and
        // one mixing on-curve corners with an off-curve control.
        private static readonly byte[] RoundRecord =
        {
            0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 100, 0, 0, 0xFF, 0x9C,
            0, 0, 0, 0, 0, 100, 0, 0
        };

        private static readonly byte[] MixedRecord =
        {
            0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0,
            1, 1, 0, 1,
            0, 0, 0, 100, 0, 0, 0xFF, 0x9C,
            0, 0, 0, 0, 0, 100, 0, 0
        };

        [TestCase(0)]
        [TestCase(1)]
        public void PointBakeMatchesOutlineBake(int recordIndex)
        {
            var record = recordIndex == 0 ? RoundRecord : MixedRecord;
            var outline = TrueTypeOutlines.ParseSimpleRecord(record);
            var points = TrueTypeOutlines.ParseSimplePoints(record);
            Assert.IsNotNull(points);

            const float scale = 0.32f;
            const float origin = -8f;
            const int size = 48;
            const float range = 6f;

            var expected = new byte[size * size];
            SdfEncoder.Encode(outline.Contours, scale, origin, origin, size, size, range, expected);

            using var set = new GlyphAtlasSet(fontId: 3);
            var baked = set.Allocate(
                new GlyphKey(0, 1, 32), frame: 0, isVolatile: false, size, size, range, 0f, 0f,
                new GlyphBakeSource(points), scale, origin, origin
            );
            set.Flush();

            var pixels = baked.Texture.GetPixelData<byte>(0);
            int stride = baked.Texture.width;
            int worst = 0;
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    int actual = pixels[(baked.Y + row) * stride + baked.X + column];
                    worst = Mathf.Max(worst, Mathf.Abs(actual - expected[row * size + column]));
                }
            }
            Assert.LessOrEqual(worst, 2, "the point bake must match the outline bake");
        }

        // Counter-clockwise square whose edges pass through texel centers on rows 4/11.
        private static Vector2[] Square() => new[]
        {
            new Vector2(3.5f, 4.5f),
            new Vector2(11.5f, 4.5f),
            new Vector2(11.5f, 11.5f),
            new Vector2(3.5f, 11.5f)
        };

        private static byte[] EncodeField(Vector2[][] contours, float range)
        {
            var field = new byte[16 * 16];
            SdfEncoder.Encode(contours, 16, 16, range, field);
            return field;
        }

        [Test]
        public void SdfEncodesSignedRange()
        {
            var field = EncodeField(new[] { Square() }, 2f);

            Assert.AreEqual(0, field[8 * 16 + 8]);       // deep inside saturates to 0
            Assert.AreEqual(255, field[0 * 16 + 0]);     // deep outside saturates to 255
            Assert.AreEqual(128, field[4 * 16 + 8], 1);  // texel center on the edge
        }

        [Test]
        public void SdfDistancesAreEuclidean()
        {
            var field = EncodeField(new[] { Square() }, 4f);

            // (1.5, 8.5) is 2 texels left of the x = 3.5 edge.
            Assert.AreEqual(Encoded(2f, 4f), field[8 * 16 + 1], 1);
            // (1.5, 1.5) is nearest to the (3.5, 4.5) corner: sqrt(2² + 3²).
            Assert.AreEqual(Encoded(Mathf.Sqrt(13f), 4f), field[1 * 16 + 1], 1);
            // (8.5, 8.5) is 3 texels inside the nearest (left/right) edges.
            Assert.AreEqual(Encoded(-3f, 4f), field[8 * 16 + 8], 1);
        }

        [Test]
        public void SdfUsesNonzeroWinding()
        {
            // A reversed inner square cuts a hole; a same-direction one does not.
            var hole = new[]
            {
                new Vector2(6.5f, 6.5f),
                new Vector2(6.5f, 9.5f),
                new Vector2(9.5f, 9.5f),
                new Vector2(9.5f, 6.5f)
            };
            var holeField = EncodeField(new[] { Square(), hole }, 4f);
            Assert.Greater(holeField[8 * 16 + 8], 128);

            System.Array.Reverse(hole);
            var overlapField = EncodeField(new[] { Square(), hole }, 4f);
            Assert.Less(overlapField[8 * 16 + 8], 128);
        }

        [Test]
        public void SdfEncoderValidatesBuffers()
        {
            var contours = new[] { Square() };

            Assert.Throws<System.ArgumentException>(() =>
                SdfEncoder.Encode(contours, 16, 16, 4f, new byte[8])
            );
            Assert.Throws<System.ArgumentException>(() =>
                SdfEncoder.Encode(contours, 16, 16, 0f, new byte[16 * 16])
            );
        }

        private static int Encoded(float distance, float range) =>
            Mathf.RoundToInt((0.5f + 0.5f * Mathf.Clamp(distance / range, -1f, 1f)) * 255f);
    }
}

