namespace Unite.Text.Fonts
{
    /// <summary>
    /// Source of glyph outlines for one outline format (TrueType glyf, CFF). Fonts pick
    /// the source matching their tables; everything above it is format independent.
    /// </summary>
    internal interface IOutlineSource
    {
        /// <summary>
        /// Flattened outline in glyph units, or null for blank glyphs. Variable fonts pass
        /// their normalized axis coordinates; null means the default master.
        /// </summary>
        GlyphOutline Load(int glyphId, float[] coordinates);

        /// <summary>
        /// The contour points of a simple glyph for flattening in the bake job, or null when
        /// the format or glyph needs <see cref="Load"/> (composites, CFF, blank glyphs).
        /// </summary>
        GlyphPoints LoadPoints(int glyphId, float[] coordinates);

        /// <summary>
        /// Advance width change implied by the glyph data itself (TrueType phantom
        /// points), used when the font has no HVAR table.
        /// </summary>
        float AdvanceDelta(int glyphId, float[] coordinates);
    }
}
