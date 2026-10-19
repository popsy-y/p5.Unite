using UnityEngine;

namespace Unite.Coordinate
{
    /// <summary>
    /// Closed forms of the matrix products the drawing API applies to every shape. A
    /// translation, z rotation or scale on the right touches only a few columns, which is
    /// several times cheaper than Matrix4x4 products and the engine's Translate / Rotate
    /// constructors.
    /// </summary>
    internal static class MatrixMath
    {
        /// <summary><c>matrix * Matrix4x4.Translate(t)</c>.</summary>
        public static Matrix4x4 Translated(in Matrix4x4 matrix, float x, float y, float z)
        {
            var result = matrix;
            Translate(ref result, x, y, z);
            return result;
        }

        /// <summary>In place: <c>matrix = matrix * Matrix4x4.Translate(t)</c>.</summary>
        public static void Translate(ref Matrix4x4 matrix, float x, float y, float z)
        {
            matrix.m03 += matrix.m00 * x + matrix.m01 * y + matrix.m02 * z;
            matrix.m13 += matrix.m10 * x + matrix.m11 * y + matrix.m12 * z;
            matrix.m23 += matrix.m20 * x + matrix.m21 * y + matrix.m22 * z;
            matrix.m33 += matrix.m30 * x + matrix.m31 * y + matrix.m32 * z;
        }

        /// <summary>In place: <c>matrix = matrix * (rotation about +z by radians)</c>.</summary>
        public static void RotateZ(ref Matrix4x4 matrix, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            float m00 = matrix.m00, m10 = matrix.m10, m20 = matrix.m20, m30 = matrix.m30;
            matrix.m00 = m00 * cos + matrix.m01 * sin;
            matrix.m10 = m10 * cos + matrix.m11 * sin;
            matrix.m20 = m20 * cos + matrix.m21 * sin;
            matrix.m30 = m30 * cos + matrix.m31 * sin;
            matrix.m01 = matrix.m01 * cos - m00 * sin;
            matrix.m11 = matrix.m11 * cos - m10 * sin;
            matrix.m21 = matrix.m21 * cos - m20 * sin;
            matrix.m31 = matrix.m31 * cos - m30 * sin;
        }

        /// <summary>In place: <c>matrix = matrix * Matrix4x4.Scale(s)</c>.</summary>
        public static void Scale(ref Matrix4x4 matrix, float x, float y, float z)
        {
            matrix.m00 *= x; matrix.m10 *= x; matrix.m20 *= x; matrix.m30 *= x;
            matrix.m01 *= y; matrix.m11 *= y; matrix.m21 *= y; matrix.m31 *= y;
            matrix.m02 *= z; matrix.m12 *= z; matrix.m22 *= z; matrix.m32 *= z;
        }

        /// <summary>
        /// <c>matrix * Translate(pen) * Slant * Translate(local)</c> for a 2D shear
        /// (<c>Slant.m01 = shear</c>, otherwise identity): the matrix of a glyph quad.
        /// </summary>
        public static Matrix4x4 Glyph(in Matrix4x4 matrix, float penX, float penY, float shear, float localX, float localY)
        {
            // Translation pen + Slant * local through the unsheared columns, then the
            // shear itself, which only mixes column 0 into column 1.
            var result = matrix;
            Translate(ref result, penX + localX + shear * localY, penY + localY, 0f);
            result.m01 += matrix.m00 * shear;
            result.m11 += matrix.m10 * shear;
            result.m21 += matrix.m20 * shear;
            result.m31 += matrix.m30 * shear;
            return result;
        }
    }
}
