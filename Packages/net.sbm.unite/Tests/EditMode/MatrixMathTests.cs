using NUnit.Framework;
using Unite.Coordinate;
using UnityEngine;

namespace Unite.Tests
{
    // The closed forms must match the Matrix4x4 products they replace.
    public sealed class MatrixMathTests
    {
        private const float Tolerance = 0.0001f;

        private static Matrix4x4 Sample()
            => Matrix4x4.TRS(new Vector3(3f, -2f, 1.5f), Quaternion.Euler(20f, -35f, 70f), new Vector3(1.5f, -0.75f, 2f));

        private static void AssertClose(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (int index = 0; index < 16; index++)
                Assert.AreEqual(expected[index], actual[index], Tolerance, $"element {index}");
        }

        [Test]
        public void TranslateRotateAndScaleMatchMatrixProducts()
        {
            var matrix = Sample();

            var translated = matrix;
            MatrixMath.Translate(ref translated, 4f, -1f, 2.5f);
            AssertClose(matrix * Matrix4x4.Translate(new Vector3(4f, -1f, 2.5f)), translated);
            AssertClose(translated, MatrixMath.Translated(matrix, 4f, -1f, 2.5f));

            var rotated = matrix;
            MatrixMath.RotateZ(ref rotated, 0.8f);
            AssertClose(matrix * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 0.8f * Mathf.Rad2Deg)), rotated);

            var scaled = matrix;
            MatrixMath.Scale(ref scaled, 2f, -3f, 0.5f);
            AssertClose(matrix * Matrix4x4.Scale(new Vector3(2f, -3f, 0.5f)), scaled);
        }

        [Test]
        public void GlyphMatrixMatchesTheProductChain()
        {
            var matrix = Sample();
            var slant = Matrix4x4.identity;
            slant.m01 = -0.21f;

            var expected = matrix
                * Matrix4x4.Translate(new Vector3(12f, -7f, 0f))
                * slant
                * Matrix4x4.Translate(new Vector3(1.25f, -3.5f, 0f));
            AssertClose(expected, MatrixMath.Glyph(matrix, 12f, -7f, -0.21f, 1.25f, -3.5f));
        }
    }
}
