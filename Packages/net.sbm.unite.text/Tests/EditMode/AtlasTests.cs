using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Unite.Text.Tests
{
    public class GlyphAtlasSetTests
    {
        private GlyphAtlasSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = new GlyphAtlasSet(fontId: 7);
        }

        [TearDown]
        public void TearDown()
        {
            _set?.Dispose();
        }

        private static byte[] FieldOf(int width, int height)
        {
            var field = new byte[width * height];
            for (int index = 0; index < field.Length; index++)
            {
                field[index] = 128;
            }
            return field;
        }

        [Test]
        public void ReserveThenCacheHitReturnsSameRect()
        {
            var key = new GlyphKey(0, 42, 64);
            var baked = _set.Reserve(key, frame: 0, 32, 40, range: 4f, FieldOf(32, 40), 0f, 0f);

            Assert.IsTrue(_set.TryGetCached(key, frame: 1, out var cached));
            Assert.AreEqual(baked.X, cached.X);
            Assert.AreEqual(baked.Y, cached.Y);
            Assert.AreEqual(baked.W, cached.W);
            Assert.AreEqual(baked.H, cached.H);
            Assert.AreEqual(baked.Range, cached.Range);
            Assert.AreEqual(0f, cached.UvMin.x, 1e-4);
            Assert.AreEqual(0f, cached.UvMin.y, 1e-4);
        }

        [Test]
        public void ReservedGlyphsDoNotOverlap()
        {
            int frame = 0;
            var rects = new List<(int Page, int X, int Y, int W, int H)>();

            // Fill a generous set of rects; overlaps would corrupt neighbor SDF samples.
            var random = new System.Random(99);
            for (int reserved = 0; reserved < 24; reserved++)
            {
                int width = random.Next(8, 64);
                int height = random.Next(8, 64);
                var baked = _set.Reserve(
                    new GlyphKey(0, reserved, 64),
                    frame,
                    width,
                    height,
                    4f,
                    FieldOf(width, height), 0f, 0f
                );
                rects.Add((IndexOf(_set, baked.Texture), baked.X, baked.Y, baked.W, baked.H));
            }

            for (int a = 0; a < rects.Count; a++)
            {
                for (int b = a + 1; b < rects.Count; b++)
                {
                    if (rects[a].Page != rects[b].Page)
                        continue;

                    var left = rects[a];
                    var right = rects[b];

                    bool disjoint =
                        left.X + left.W <= right.X ||
                        right.X + right.W <= left.X ||
                        left.Y + left.H <= right.Y ||
                        right.Y + right.H <= left.Y;

                    Assert.IsTrue(
                        disjoint,
                        $"glyphs {a} and {b} overlap: {left} vs {right}"
                    );
                }
            }
        }

        private static int IndexOf(GlyphAtlasSet set, Texture2D texture)
        {
            for (int index = 0; index < set.PageTextures.Count; index++)
            {
                if (set.PageTextures[index] == texture)
                    return index;
            }
            return -1;
        }

        [Test]
        public void AgedEntriesAreEvictedAndPagesRelease()
        {
            var key = new GlyphKey(0, 5, 64);
            _set.Reserve(key, frame: 0, 16, 16, 2f, FieldOf(16, 16), 0f, 0f);
            Assert.AreEqual(1, _set.LivePageCount);

            // A far-future frame ages the entry out on the next sweep; the page becomes
            // empty and its texture is released.
            _set.Reserve(new GlyphKey(0, 6, 64), frame: 2_000_000, 16, 16, 2f, FieldOf(16, 16), 0f, 0f);
            Assert.IsFalse(_set.TryGetCached(key, frame: 2_000_000, out _));

            int liveCount = 0;
            foreach (var page in _set.PageTextures)
            {
                if (page != null) liveCount++;
            }
            Assert.AreEqual(1, liveCount);
        }

        [Test]
        public void RejectsBadFieldBuffers()
        {
            Assert.Throws<ArgumentException>(() =>
                _set.Reserve(
                    new GlyphKey(0, 1, 64),
                    0,
                    32,
                    32,
                    4f,
                    FieldOf(16, 16), // mismatched length
                    0f,
                    0f
                )
            );
        }
    }
}
