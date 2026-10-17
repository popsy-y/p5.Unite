using System;
using UnityEngine;

namespace Unite.Text.Raster
{
    /// <summary>
    /// Converts a coverage grid into a single-channel signed distance field.
    /// Distance is approximated with a (1, sqrt2) chamfer transform (2 passes),
    /// then the standard half-texel edge correction is applied, then the value is
    /// normalized so a boundary texel stores ~128, deep inside approaches 0 and deep
    /// outside approaches 255, at exactly the supplied range.
    /// </summary>
    public static class SdfEncoder
    {
        private const float OrthogonalWeight = 1f;
        private const float DiagonalWeight = 1.41421356237f;

        public static void Encode(byte[] coverage, int width, int height, float range, byte[] output)
        {
            if (coverage == null || output == null ||
                coverage.Length != width * height || output.Length != width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: coverage and output must both hold width * height bytes."
                );
            }

            if (range <= 0f)
            {
                throw new ArgumentException("p5.Unite: SDF range must be positive.");
            }

            int size = width * height;

            var inside = new bool[size];
            for (int index = 0; index < size; index++)
                inside[index] = coverage[index] >= 128;

            // Distance from every cell to the nearest cell with the OPPOSITE mask.
            var toOutside = DistanceField(inside, width, height, wantInside: false);
            var toInside = DistanceField(inside, width, height, wantInside: true);

            for (int index = 0; index < size; index++)
            {
                float distance = inside[index]
                    ? -(toOutside[index] - 0.5f)
                    : toInside[index] - 0.5f;

                float normalized = Mathf.Clamp(distance / range, -1f, 1f);
                output[index] = (byte)Mathf.RoundToInt(
                    Mathf.Clamp01(0.5f + 0.5f * normalized) * 255f
                );
            }
        }

        // Chamfer distance transform: for each cell, the distance to the nearest seed
        // cell (cells matching wantInside). Forward + backward passes over the grid.
        private static float[] DistanceField(
            bool[] inside,
            int width,
            int height,
            bool wantInside
        )
        {
            int size = width * height;
            var field = new float[size];

            const float infinity = float.MaxValue;

            for (int index = 0; index < size; index++)
            {
                field[index] = inside[index] == wantInside ? 0f : infinity;
            }

            // Forward pass (top-left → bottom-right).
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int index = row * width + column;
                    float value = field[index];

                    if (column > 0)
                        value = Mathf.Min(value, field[index - 1] + OrthogonalWeight);

                    if (row > 0)
                    {
                        int up = index - width;
                        value = Mathf.Min(value, field[up] + OrthogonalWeight);

                        if (column > 0)
                            value = Mathf.Min(value, field[up - 1] + DiagonalWeight);
                        if (column < width - 1)
                            value = Mathf.Min(value, field[up + 1] + DiagonalWeight);
                    }

                    field[index] = value;
                }
            }

            // Backward pass (bottom-right → top-left).
            for (int row = height - 1; row >= 0; row--)
            {
                for (int column = width - 1; column >= 0; column--)
                {
                    int index = row * width + column;
                    float value = field[index];

                    if (column < width - 1)
                        value = Mathf.Min(value, field[index + 1] + OrthogonalWeight);

                    if (row < height - 1)
                    {
                        int down = index + width;
                        value = Mathf.Min(value, field[down] + OrthogonalWeight);

                        if (column < width - 1)
                            value = Mathf.Min(value, field[down + 1] + DiagonalWeight);
                        if (column > 0)
                            value = Mathf.Min(value, field[down - 1] + DiagonalWeight);
                    }

                    field[index] = value;
                }
            }

            return field;
        }
    }
}
