using System;
using Unite.Text.Fonts;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Two-level least-recently-used cache: the Unicode analysis per (string, direction)
    /// and the shaped glyphs per (string, direction, font instance). Text drawn every frame
    /// is analysed and shaped once; text whose font instance changes every frame (animated
    /// variation axes) is only re-shaped. Evicted entries are recycled, so strings that
    /// change every frame do not produce garbage either. Main thread only.
    /// </summary>
    internal static class ShapingCache
    {
        private const int AnalysisCapacity = 512;
        private const int ShapeCapacity = 1024;

        private readonly struct AnalysisKey : IEquatable<AnalysisKey>
        {
            private readonly string _text;
            private readonly TextDirectionMode _direction;
            private readonly int _hash;

            public AnalysisKey(string text, TextDirectionMode direction)
            {
                _text = text;
                _direction = direction;
                _hash = HashCode.Combine(text.GetHashCode(), (int)direction);
            }

            public bool Equals(AnalysisKey other)
                => _hash == other._hash && _direction == other._direction && string.Equals(_text, other._text, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is AnalysisKey other && Equals(other);

            public override int GetHashCode() => _hash;
        }

        private readonly struct ShapeKey : IEquatable<ShapeKey>
        {
            private readonly string _text;
            private readonly int _font;
            private readonly TextDirectionMode _direction;
            private readonly int _hash;

            public ShapeKey(string text, int font, TextDirectionMode direction)
            {
                _text = text;
                _font = font;
                _direction = direction;
                _hash = HashCode.Combine(text.GetHashCode(), font, (int)direction);
            }

            public bool Equals(ShapeKey other)
                => _hash == other._hash && _font == other._font && _direction == other._direction
                    && string.Equals(_text, other._text, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is ShapeKey other && Equals(other);

            public override int GetHashCode() => _hash;
        }

        private static readonly LruTable<AnalysisKey, TextAnalysis> Analyses = new(AnalysisCapacity);
        private static readonly LruTable<ShapeKey, ShapedText> Shapes = new(ShapeCapacity);
        private static readonly TextAnalyzer Analyzer = new();

        /// <summary>The shaped text; valid until the next call that misses the cache.</summary>
        public static ShapedText Get(FontInstance font, string text, TextDirectionMode direction)
        {
            var key = new ShapeKey(text, font.Id, direction);
            if (Shapes.TryGet(key, out var shaped))
            {
                if (!shaped.IsCurrent)
                    Analyzer.Shape(shaped, GetAnalysis(text, direction), font);
                return shaped;
            }

            var analysis = GetAnalysis(text, direction);
            int slot = Shapes.Acquire(key, out shaped);
            shaped ??= new ShapedText();
            Shapes.SetValue(slot, shaped);
            using (TextProfiling.Shape.Auto())
                Analyzer.Shape(shaped, analysis, font);
            return shaped;
        }

        public static void Clear()
        {
            Analyses.Clear();
            Shapes.Clear();
        }

        private static TextAnalysis GetAnalysis(string text, TextDirectionMode direction)
        {
            var key = new AnalysisKey(text, direction);
            if (Analyses.TryGet(key, out var analysis))
                return analysis;

            int slot = Analyses.Acquire(key, out analysis);
            analysis ??= new TextAnalysis();
            Analyses.SetValue(slot, analysis);
            using (TextProfiling.Analyze.Auto())
                Analyzer.Analyze(analysis, text, direction);
            return analysis;
        }
    }
}
