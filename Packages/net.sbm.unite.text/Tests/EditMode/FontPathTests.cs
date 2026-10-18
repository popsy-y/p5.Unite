using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unite.Text.Fonts;
using UnityEngine;

namespace Unite.Text.Tests
{
    public class FontPathTests
    {
        private const float Tolerance = 0.01f;
        private UniteFontAsset _font;

        [SetUp]
        public void SetUp()
        {
            _font = UniteFontAsset.LoadFont(TestFontBuilder.Build(), "TestFont");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_font);
        }

        private static TextPathOptions Options(float sampleFactor = 0.1f, float extrude = 0f) => new()
        {
            FontSize = TestFontBuilder.UnitsPerEm,
            SampleFactor = sampleFactor,
            Extrude = extrude
        };

        [Test]
        public void LoadFontRejectsInvalidBytes()
        {
            Assert.AreEqual("TestFont", _font.name);
            Assert.IsTrue(_font.TryGetFont(out _));
            Assert.Throws<InvalidDataException>(() => UniteFontAsset.LoadFont(new byte[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TextToPathsKeepsCommandsInOutputSpace()
        {
            var commands = _font.TextToPaths("A", 0f, 0f, Options());

            Assert.AreEqual(5, commands.Count); // M, 3 x L, Z for the square
            Assert.AreEqual(PathCommandType.MoveTo, commands[0].Type);
            Assert.AreEqual(PathCommandType.Close, commands[4].Type);
            // Glyph point (100, 100) lands at y = -100 in y-down output space.
            Assert.AreEqual(new Vector2(100f, -100f), commands[0].Point);
        }

        [Test]
        public void TextToPointsSamplesByPerimeter()
        {
            var points = _font.TextToPoints("A", 0f, 0f, Options(sampleFactor: 0.1f));

            Assert.AreEqual(240, points.Count); // perimeter 2400 x 0.1
            foreach (var point in points)
            {
                bool onVertical = Mathf.Abs(point.X - 100f) < Tolerance || Mathf.Abs(point.X - 700f) < Tolerance;
                bool onHorizontal = Mathf.Abs(point.Y + 100f) < Tolerance || Mathf.Abs(point.Y + 700f) < Tolerance;
                Assert.IsTrue(onVertical || onHorizontal, $"{point.Position} is off the outline");
            }
        }

        [Test]
        public void TextToContoursSimplifiesCollinearPoints()
        {
            var options = Options(sampleFactor: 0.1f);
            options.SimplifyThreshold = 0.1f;

            var contours = _font.TextToContours("A", 0f, 0f, options);

            Assert.AreEqual(1, contours.Count);
            Assert.LessOrEqual(contours[0].Count, 5); // only the corners survive
        }

        [Test]
        public void TextToModelExtrudesFacesAndWalls()
        {
            var flat = _font.TextToModel("A", 0f, 0f, 0f, 0f, Options());
            Assert.AreEqual(2 * 3, flat.triangles.Length);

            var solid = _font.TextToModel("A", 0f, 0f, 0f, 0f, Options(extrude: 50f));
            Assert.AreEqual((2 + 2 + 4 * 2) * 3, solid.triangles.Length);
            Assert.AreEqual(50f, solid.bounds.size.z, Tolerance);

            Object.DestroyImmediate(flat);
            Object.DestroyImmediate(solid);
        }

        [Test]
        public void TriangulatorCutsHoles()
        {
            var outer = new[] { new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f) };
            var hole = new[] { new Vector2(4f, 4f), new Vector2(6f, 4f), new Vector2(6f, 6f), new Vector2(4f, 6f) };
            var vertices = new List<Vector2>();
            var triangles = new List<int>();

            PolygonTriangulator.Triangulate(new[] { outer, hole }, vertices, triangles);

            float area = 0f;
            for (int index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]];
                var b = vertices[triangles[index + 1]];
                var c = vertices[triangles[index + 2]];
                float signed = ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;
                Assert.Greater(signed, 0f, "triangles must be counter-clockwise");
                area += signed;
            }
            Assert.AreEqual(96f, area, Tolerance);
        }
    }
}
