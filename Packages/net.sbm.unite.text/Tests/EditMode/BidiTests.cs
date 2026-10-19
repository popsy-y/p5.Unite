using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unite.Text.Unicode;

namespace Unite.Text.Tests
{
    public class BidiTests
    {
        [Test]
        public void BidiPassesTheConformanceTestSubset()
        {
            var bidi = new BidiParagraph();
            int cases = 0;
            var failures = new List<string>();
            foreach (string raw in File.ReadLines(Path.Combine(UnicodeTests.DataDirectory, "BidiCharacterTestSubset.txt")))
            {
                if (raw.Length == 0 || raw[0] == '#')
                    continue;

                string[] fields = raw.Split(';');
                int[] codePoints = Array.ConvertAll(fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries), token => int.Parse(token, NumberStyles.HexNumber));
                int direction = int.Parse(fields[1]);
                int expectedParagraph = int.Parse(fields[2]);
                string[] expectedLevels = fields[3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string expectedOrder = fields[4].Trim();
                cases++;

                bidi.Analyze(codePoints, direction == 2 ? BidiParagraph.AutoLevel : direction);
                var levels = new byte[codePoints.Length];
                bidi.GetLineLevels(0, codePoints.Length, levels);

                bool ok = bidi.ParagraphLevel == expectedParagraph;
                for (int index = 0; ok && index < codePoints.Length; index++)
                {
                    if (expectedLevels[index] != "x" && int.Parse(expectedLevels[index]) != levels[index])
                        ok = false;
                }

                var order = new int[codePoints.Length];
                BidiParagraph.Reorder(levels, order);
                var visual = new StringBuilder();
                foreach (int index in order)
                {
                    if (expectedLevels[index] == "x")
                        continue;
                    if (visual.Length > 0)
                        visual.Append(' ');
                    visual.Append(index);
                }
                ok &= visual.ToString() == expectedOrder;

                if (!ok && failures.Count < 10)
                    failures.Add($"{raw}\n   got paragraph {bidi.ParagraphLevel}, levels {string.Join(" ", levels)}, order {visual}");
            }

            Assert.Greater(cases, 6000);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void LeftToRightTextTakesTheFastPath()
        {
            var bidi = new BidiParagraph();
            bidi.Analyze(new[] { (int)'a', ' ', '1', '.', '5' }, BidiParagraph.AutoLevel);
            Assert.IsTrue(bidi.IsPureLeftToRight);
            Assert.AreEqual(0, bidi.ParagraphLevel);

            // Hebrew word inside English: the word reverses, the paragraph stays LTR.
            int[] mixed = { 'a', ' ', 0x05D0, 0x05D1, ' ', 'b' };
            bidi.Analyze(mixed, BidiParagraph.AutoLevel);
            Assert.IsFalse(bidi.IsPureLeftToRight);
            var levels = new byte[mixed.Length];
            bidi.GetLineLevels(0, mixed.Length, levels);
            var order = new int[mixed.Length];
            BidiParagraph.Reorder(levels, order);
            CollectionAssert.AreEqual(new[] { 0, 1, 3, 2, 4, 5 }, order);
        }
    }
}
