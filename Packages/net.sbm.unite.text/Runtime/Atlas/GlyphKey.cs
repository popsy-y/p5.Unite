using System;

namespace Unite.Text
{
    /// <summary>
    /// Identifies a cached glyph: atlas set (font asset), glyph id and the raster size
    /// bucket. Every rasterization parameter besides those three axes is fixed by the
    /// atlas policy, so the trio fully keys a baked SDF.
    /// </summary>
    internal readonly struct GlyphKey : IEquatable<GlyphKey>
    {
        public GlyphKey(int fontId, int glyphId, int emBucket)
        {
            FontId = fontId;
            GlyphId = glyphId;
            EmBucket = emBucket;
        }

        public int FontId { get; }
        public int GlyphId { get; }
        public int EmBucket { get; }

        public bool Equals(GlyphKey other) =>
            FontId == other.FontId &&
            GlyphId == other.GlyphId &&
            EmBucket == other.EmBucket;

        public override bool Equals(object obj) => obj is GlyphKey other && Equals(other);

        public override int GetHashCode() =>
            FontId * 73856093 ^ GlyphId * 19349663 ^ EmBucket * 83492791;

        public static bool operator ==(GlyphKey left, GlyphKey right) => left.Equals(right);
        public static bool operator !=(GlyphKey left, GlyphKey right) => !left.Equals(right);
    }
}
