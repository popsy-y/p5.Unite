using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Unite.Text.Unicode;

namespace Unite.Text.Tests
{
    public class UnicodeTests
    {
        internal const string DataDirectory = "Packages/net.sbm.unite.text/Tests/EditMode/Data";

        [Test]
        public void PropertiesMatchTheCharacterDatabase()
        {
            Assert.AreEqual(BidiClass.L, UnicodeData.GetBidiClass('A'));
            Assert.AreEqual(BidiClass.R, UnicodeData.GetBidiClass(0x05D0));   // HEBREW ALEF
            Assert.AreEqual(BidiClass.AL, UnicodeData.GetBidiClass(0x0627));  // ARABIC ALEF
            Assert.AreEqual(BidiClass.AL, UnicodeData.GetBidiClass(0x0870));  // ARABIC LETTER ALEF WITH ATTACHED FATHA
            Assert.AreEqual(BidiClass.EN, UnicodeData.GetBidiClass('7'));
            Assert.AreEqual(BidiClass.NSM, UnicodeData.GetBidiClass(0x0301));
            Assert.AreEqual(BidiClass.R, UnicodeData.GetBidiClass(0x05FF));   // @missing default of the Hebrew block

            Assert.AreEqual(LineBreakClass.ID, UnicodeData.GetLineBreakClass(0x6F22)); // 漢
            Assert.AreEqual(LineBreakClass.CJ, UnicodeData.GetLineBreakClass(0x3041)); // small hiragana
            Assert.AreEqual(LineBreakClass.SP, UnicodeData.GetLineBreakClass(' '));
            Assert.AreEqual(LineBreakClass.ID, UnicodeData.GetLineBreakClass(0x3FFFD)); // @missing ideographic plane
            Assert.IsTrue(UnicodeData.IsEastAsianWide(0x6F22));
            Assert.IsFalse(UnicodeData.IsEastAsianWide('A'));

            Assert.AreEqual(UnicodeScript.Latin, UnicodeData.GetScript('A'));
            Assert.AreEqual(UnicodeScript.Arabic, UnicodeData.GetScript(0x0627));
            Assert.AreEqual(UnicodeScript.Han, UnicodeData.GetScript(0x6F22));
            Assert.AreEqual(UnicodeScript.Common, UnicodeData.GetScript(' '));
            Assert.AreEqual(UnicodeScript.Inherited, UnicodeData.GetScript(0x0301));
            Assert.AreEqual(UnicodeScript.Unknown, UnicodeData.GetScript(0x10FFFF));
            Assert.AreEqual("latn", UnicodeData.OpenTypeTag(UnicodeScript.Latin));
            Assert.AreEqual("arab", UnicodeData.OpenTypeTag(UnicodeScript.Arabic));
            Assert.AreEqual("kana", UnicodeData.OpenTypeTag(UnicodeScript.Hiragana));

            Assert.AreEqual(GeneralCategory.Mn, UnicodeData.GetGeneralCategory(0x0301));
            Assert.AreEqual(GeneralCategory.Lu, UnicodeData.GetGeneralCategory('A'));
            Assert.AreEqual(GeneralCategory.Pi, UnicodeData.GetGeneralCategory(0x201C));
            Assert.AreEqual(JoiningType.D, UnicodeData.GetJoiningType(0x0628));       // BEH
            Assert.AreEqual(JoiningType.R, UnicodeData.GetJoiningType(0x0627));       // ALEF
            Assert.AreEqual(JoiningType.T, UnicodeData.GetJoiningType(0x064E));       // FATHA
            Assert.AreEqual(JoiningType.U, UnicodeData.GetJoiningType('A'));

            Assert.IsTrue(UnicodeData.IsDefaultIgnorable(0x200D));  // ZWJ
            Assert.IsTrue(UnicodeData.IsDefaultIgnorable(0xFE0F));  // VS16
            Assert.IsTrue(UnicodeData.IsEmojiPresentation(0x1F600));
            Assert.IsTrue(UnicodeData.IsExtendedPictographic(0x1F600));

            Assert.AreEqual(')', UnicodeData.GetMirror('('));
            Assert.AreEqual('A', UnicodeData.GetMirror('A'));
            Assert.AreEqual(BracketType.Open, UnicodeData.GetBracket('[', out int pair));
            Assert.AreEqual(']', pair);
            Assert.AreEqual(BracketType.None, UnicodeData.GetBracket('a', out _));
        }

        [Test]
        public void GraphemeBreaksPassTheConformanceTest()
        {
            int cases = 0;
            var failures = new List<string>();
            foreach (var (codePoints, breaks, line) in ReadBreakTest("GraphemeBreakTest.txt"))
            {
                cases++;
                var actual = new List<int>();
                for (int start = 0; start < codePoints.Length;)
                {
                    start = GraphemeBreaker.NextBoundary(codePoints, start);
                    actual.Add(start);
                }

                if (!AreEqual(breaks, actual) && failures.Count < 10)
                    failures.Add(line);
            }

            Assert.Greater(cases, 1000);
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>
        /// Parses a UCD break test file ("÷ 0020 × 0308 ÷"): the code points and the
        /// positions (exclusive end indices, the final position included) where a break is allowed.
        /// </summary>
        internal static IEnumerable<(int[] CodePoints, List<int> Breaks, string Line)> ReadBreakTest(string fileName)
        {
            foreach (string raw in File.ReadLines(Path.Combine(DataDirectory, fileName)))
            {
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0)
                    continue;

                var codePoints = new List<int>();
                var breaks = new List<int>();
                foreach (string token in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token == "÷")
                    {
                        if (codePoints.Count > 0)
                            breaks.Add(codePoints.Count);
                    }
                    else if (token != "×")
                    {
                        codePoints.Add(int.Parse(token, NumberStyles.HexNumber));
                    }
                }
                yield return (codePoints.ToArray(), breaks, raw);
            }
        }

        private static bool AreEqual(List<int> expected, List<int> actual)
        {
            if (expected.Count != actual.Count)
                return false;
            for (int index = 0; index < expected.Count; index++)
            {
                if (expected[index] != actual[index])
                    return false;
            }
            return true;
        }
    }
}
