namespace Unite.Text.Fonts
{
    /// <summary>The curve flattening tolerance of the outline builders.</summary>
    internal static class OutlineFlatness
    {
        // 1/4000 em: about 0.06 px at the largest glyph bucket (256 px).
        public static float For(int unitsPerEm) => unitsPerEm / 4000f;
    }
}
