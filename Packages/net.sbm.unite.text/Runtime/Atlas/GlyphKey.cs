using System;

namespace Unite.Text
{
    /// <summary>
    /// Identifies a cached glyph: atlas set (font asset), glyph id, the raster size
    /// bucket and the font instance (variable fonts bake different outlines per set of
    /// axis values). Every other rasterization parameter is fixed by the atlas policy,
    /// so these four fully key a baked SDF.
    /// </summary>
    internal readonly struct GlyphKey : IEquatable<GlyphKey>
    {
        public GlyphKey(int fontId, int glyphId, int emBucket, int instanceId = 0)
        {
            FontId = fontId;
            GlyphId = glyphId;
            EmBucket = emBucket;
            InstanceId = instanceId;
        }

        public int FontId { get; }
        public int GlyphId { get; }
        public int EmBucket { get; }
        public int InstanceId { get; }

        public bool Equals(GlyphKey other) =>
            FontId == other.FontId &&
            GlyphId == other.GlyphId &&
            EmBucket == other.EmBucket &&
            InstanceId == other.InstanceId;

        public override bool Equals(object obj) => obj is GlyphKey other && Equals(other);

        public override int GetHashCode() =>
            FontId * 73856093 ^ GlyphId * 19349663 ^ EmBucket * 83492791 ^ InstanceId * 1274126177;

        public static bool operator ==(GlyphKey left, GlyphKey right) => left.Equals(right);
        public static bool operator !=(GlyphKey left, GlyphKey right) => !left.Equals(right);
    }
}
