using System;
using NUnit.Framework;
using Unite.Text.Fonts;

namespace Unite.Text.Tests
{
    public class BrotliDecoderTests
    {
        private static BrotliTestVectors.Vector[] Vectors => BrotliTestVectors.All;

        [TestCaseSource(nameof(Vectors))]
        public void DecodesReferenceEncoderOutput(BrotliTestVectors.Vector vector)
        {
            byte[] compressed = Convert.FromBase64String(vector.CompressedBase64);
            byte[] expected = Convert.FromBase64String(vector.ExpectedBase64);

            byte[] decoded = BrotliDecoder.Decompress(compressed, 0, compressed.Length);

            CollectionAssert.AreEqual(expected, decoded);
        }

        [Test]
        public void ExpectedSizeHintDoesNotChangeTheResult()
        {
            var vector = Vectors[2];
            byte[] compressed = Convert.FromBase64String(vector.CompressedBase64);
            byte[] expected = Convert.FromBase64String(vector.ExpectedBase64);

            CollectionAssert.AreEqual(expected, BrotliDecoder.Decompress(compressed, 0, compressed.Length, 4));
            CollectionAssert.AreEqual(expected, BrotliDecoder.Decompress(compressed, 0, compressed.Length, 100000));
        }

        [Test]
        public void ReadsFromAnOffsetInsideALargerBuffer()
        {
            byte[] compressed = Convert.FromBase64String(Vectors[1].CompressedBase64);
            var padded = new byte[compressed.Length + 10];
            Array.Copy(compressed, 0, padded, 4, compressed.Length);

            byte[] decoded = BrotliDecoder.Decompress(padded, 4, compressed.Length);

            CollectionAssert.AreEqual(Convert.FromBase64String(Vectors[1].ExpectedBase64), decoded);
        }

        [Test]
        public void TruncatedStreamsAreRejected()
        {
            byte[] compressed = Convert.FromBase64String(Vectors[2].CompressedBase64);

            Assert.Throws<FontFormatException>(() =>
                BrotliDecoder.Decompress(compressed, 0, compressed.Length / 2)
            );
        }

        [Test]
        public void GarbageIsRejectedWithoutOtherExceptions()
        {
            var random = new Random(5);
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var garbage = new byte[64];
                random.NextBytes(garbage);

                try
                {
                    BrotliDecoder.Decompress(garbage, 0, garbage.Length);
                }
                catch (FontFormatException)
                {
                    // Expected for nearly every input; some may happen to decode.
                }
            }
        }
    }
}
