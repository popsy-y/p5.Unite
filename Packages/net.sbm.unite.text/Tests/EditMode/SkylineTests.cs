using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Unite.Text.Tests
{
    public class SkylineAllocatorTests
    {
        [Test]
        public void FillsBelowRectangles()
        {
            var packer = new SkylineAllocator(64, 64);

            Assert.IsTrue(packer.TryPlace(64, 16, out var firstX, out var firstY));
            Assert.AreEqual(0, firstX);
            Assert.AreEqual(0, firstY);
            Assert.AreEqual(1024L, packer.UsedArea);

            Assert.IsTrue(packer.TryPlace(64, 16, out var secondX, out var secondY));
            Assert.AreEqual(0, secondX);
            Assert.AreEqual(16, secondY);

            Assert.IsTrue(packer.TryPlace(64, 32, out _, out var thirdY));
            Assert.AreEqual(32, thirdY);
        }

        [Test]
        public void RejectsOversizedRectangles()
        {
            var packer = new SkylineAllocator(64, 64);
            Assert.IsFalse(packer.TryPlace(65, 1, out _, out _));
            Assert.IsFalse(packer.TryPlace(1, 65, out _, out _));
            Assert.IsFalse(packer.TryPlace(-1, 8, out _, out _));
        }

        [Test]
        public void FillsRunsOutOfSpace()
        {
            var packer = new SkylineAllocator(16, 16);
            Assert.IsTrue(packer.TryPlace(16, 16, out _, out _));
            Assert.IsFalse(packer.TryPlace(1, 1, out _, out _));
            Assert.AreEqual(256L, packer.UsedArea);
        }

        [Test]
        public void ArrangesTightAndStaysWithinBounds()
        {
            // Multiple small rects fill a compact skyline strip; every placement must
            // land back inside the packer bounds.
            var packer = new SkylineAllocator(32, 32);
            var random = new System.Random(1234);

            for (int placed = 0; placed < 64; placed++)
            {
                int width = random.Next(2, 12);
                int height = random.Next(2, 12);
                bool fit = packer.TryPlace(width, height, out var x, out var y);

                if (fit)
                {
                    Assert.GreaterOrEqual(x, 0);
                    Assert.GreaterOrEqual(y, 0);
                    Assert.LessOrEqual(x + width, 32);
                    Assert.LessOrEqual(y + height, 32);
                }
                else
                {
                    break; // full page; nothing to pack
                }
            }

            Assert.LessOrEqual(packer.UsedArea, 32L * 32L);
        }

        [Test]
        public void GlyphKeyEqualityWorks()
        {
            var key = new GlyphKey(1, 2, 64);
            var same = new GlyphKey(1, 2, 64);
            var different = new GlyphKey(1, 2, 32);

            Assert.AreEqual(key, same);
            Assert.AreEqual(key.GetHashCode(), same.GetHashCode());
            Assert.AreNotEqual(key, different);
            Assert.IsTrue(key == same);
            Assert.IsTrue(key != different);
        }
    }
}
