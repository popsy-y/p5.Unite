using System;
using System.Collections.Generic;
using Unite.Text.Fonts;
using Unite.Text.Fonts.Layout;
using Unite.Text.Unicode;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// OpenType shaping of one run (one font, script and direction), modelled on
    /// HarfBuzz: cmap mapping with mirroring, GDEF classes (synthesized from Unicode when
    /// missing), Arabic-style joining, GSUB stages, default advances, GPOS (or the legacy
    /// kern table), mark advance zeroing and attachment offsets. Indic and other
    /// reordering scripts use the default shaper (no reordering).
    /// </summary>
    internal sealed class OpenTypeShaper
    {
        private readonly ShapingBuffer _buffer = new();
        private readonly Normalizer _normalizer = new();
        private readonly SubstitutionApplier _substitutions = new();
        private readonly PositioningApplier _positions = new();
        private readonly List<int> _scriptTags = new(2);

        public void Shape(ShapedText target, FontInstance font, int face, int start, int end, bool rightToLeft, UnicodeScript script, int languageTag)
        {
            var layout = font.Face.OpenType;
            var plan = PlanCache.Get(font, layout, script, languageTag, rightToLeft, _scriptTags);
            var buffer = _buffer;
            buffer.Clear(rightToLeft);
            Fill(target, font, layout, plan, start, end, rightToLeft, script);

            if (ArabicJoining.UsesJoining(script))
                ArabicJoining.Apply(buffer, plan, target.CodePoints, start, end, target.Length);

            if (layout.Substitutions != null)
            {
                using var substituteScope = TextProfiling.Substitute.Auto();
                _substitutions.Begin(buffer, layout, layout.Substitutions, target);
                foreach (var stage in plan.SubstitutionStages)
                {
                    foreach (var lookup in stage)
                        _substitutions.Apply(lookup);
                }
            }

            using (TextProfiling.Position.Auto())
                Position(font, layout, plan);
            Output(target, font, face);
        }

        private void Fill(ShapedText target, FontInstance font, OpenTypeLayout layout, ShapePlan plan, int start, int end, bool rightToLeft, UnicodeScript script)
        {
            using (TextProfiling.Normalize.Auto())
                _normalizer.Run(target, font, start, end, script == UnicodeScript.Hebrew && !plan.HasMarkPositioning);

            var definitions = layout.Definitions;
            bool gdefClasses = definitions != null && definitions.HasGlyphClasses;
            for (int index = 0; index < _normalizer.Count; index++)
            {
                int codePoint = _normalizer.CodePoints[index];
                int glyph = 0;
                if (rightToLeft && UnicodeData.IsBidiMirrored(codePoint))
                    glyph = font.GetGlyphId(UnicodeData.GetMirror(codePoint));
                if (glyph == 0)
                    glyph = font.GetGlyphId(codePoint);

                var glyphClass = gdefClasses
                    ? definitions.GetGlyphClass(glyph)
                    : UnicodeData.GetGeneralCategory(codePoint) == GeneralCategory.Mn ? GlyphClass.Mark : GlyphClass.Base;

                _buffer.Add(new GlyphInfo
                {
                    Glyph = glyph,
                    CodePoint = codePoint,
                    Cluster = _normalizer.Clusters[index],
                    Mask = ShapePlan.GlobalMask,
                    Class = glyphClass,
                    DefaultIgnorable = UnicodeData.IsDefaultIgnorable(codePoint),
                    Zwnj = codePoint == 0x200C,
                    Zwj = codePoint == 0x200D
                });
            }
        }

        private void Position(FontInstance font, OpenTypeLayout layout, ShapePlan plan)
        {
            var buffer = _buffer;
            buffer.ClearPositions();
            for (int index = 0; index < buffer.Count; index++)
                buffer.Positions[index].XAdvance = font.Advance(buffer.Info[index].Glyph);

            bool positioned = layout.Positions != null;
            _positions.BeginInstance(font.Coordinates);
            if (positioned)
            {
                _positions.Begin(buffer, layout, layout.Positions, null);
                _positions.BeginPositioning();
                foreach (var lookup in plan.PositionLookups)
                    _positions.Apply(lookup);
            }

            bool legacyKerning = layout.Kerning != null && plan.KerningEnabled && (!positioned || !plan.HasPositionedKerning);
            if (legacyKerning)
                ApplyKerningTable(layout.Kerning);

            // Marks take no room (GDEF, late). Without positioning tables, a forward mark
            // also shifts back so it hangs over the glyph before it.
            bool adjust = !positioned && !legacyKerning && !buffer.RightToLeft;
            for (int index = 0; index < buffer.Count; index++)
            {
                ref var position = ref buffer.Positions[index];
                if (buffer.Info[index].Class == GlyphClass.Mark)
                {
                    if (adjust)
                        position.XOffset -= position.XAdvance;
                    position.XAdvance = 0f;
                }
                if (buffer.Info[index].DefaultIgnorable && !buffer.Info[index].Substituted)
                {
                    position.XAdvance = 0f;
                    position.XOffset = 0f;
                    position.YOffset = 0f;
                }
            }

            if (positioned)
                _positions.FinishOffsets();
        }

        // The 'kern' table pairs consecutive non-mark glyphs (HarfBuzz hb_kern_machine_t).
        private void ApplyKerningTable(KerningTable kerning)
        {
            var buffer = _buffer;
            int previous = -1;
            for (int index = 0; index < buffer.Count; index++)
            {
                if (buffer.Info[index].Class == GlyphClass.Mark || buffer.Info[index].DefaultIgnorable)
                    continue;

                if (previous >= 0)
                {
                    int kern = kerning.GetKerning(buffer.Info[previous].Glyph, buffer.Info[index].Glyph);
                    if (kern != 0)
                    {
                        int first = kern >> 1;
                        int second = kern - first;
                        buffer.Positions[previous].XAdvance += first;
                        buffer.Positions[index].XAdvance += second;
                        buffer.Positions[index].XOffset += second;
                    }
                }
                previous = index;
            }
        }

        private void Output(ShapedText target, FontInstance font, int face)
        {
            var buffer = _buffer;
            float perUnit = 1f / font.UnitsPerEm;
            for (int index = 0; index < buffer.Count; index++)
            {
                ref var info = ref buffer.Info[index];
                if (info.DefaultIgnorable && !info.Substituted)
                    continue;

                ref var position = ref buffer.Positions[index];
                target.AddGlyph(new ShapedGlyph
                {
                    GlyphId = info.Glyph,
                    Face = face,
                    Cluster = info.Cluster,
                    Advance = position.XAdvance * perUnit,
                    OffsetX = position.XOffset * perUnit,
                    OffsetY = position.YOffset * perUnit
                });
            }
        }
    }

    /// <summary>Shape plans per font, script, language, direction and variation instance.</summary>
    internal static class PlanCache
    {
        private readonly struct Key : IEquatable<Key>
        {
            private readonly OpenTypeLayout _layout;
            private readonly UnicodeScript _script;
            private readonly int _language;
            private readonly bool _rightToLeft;
            private readonly int _variation;

            public Key(OpenTypeLayout layout, UnicodeScript script, int language, bool rightToLeft, int variation)
            {
                _layout = layout;
                _script = script;
                _language = language;
                _rightToLeft = rightToLeft;
                _variation = variation;
            }

            public bool Equals(Key other)
                => ReferenceEquals(_layout, other._layout) && _script == other._script && _language == other._language
                    && _rightToLeft == other._rightToLeft && _variation == other._variation;

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_layout, (int)_script, _language, _rightToLeft, _variation);
        }

        private static readonly Dictionary<Key, ShapePlan> Plans = new();

        public static ShapePlan Get(FontInstance font, OpenTypeLayout layout, UnicodeScript script, int languageTag, bool rightToLeft, List<int> scriptTags)
        {
            var coordinates = font.Coordinates;
            int variation = Math.Max(
                layout.Substitutions?.FindVariation(coordinates) ?? -1,
                layout.Positions?.FindVariation(coordinates) ?? -1
            );

            var key = new Key(layout, script, languageTag, rightToLeft, variation);
            if (Plans.TryGetValue(key, out var plan))
                return plan;

            scriptTags.Clear();
            scriptTags.Add(Tag(UnicodeData.OpenTypeTag(script)));
            plan = Features(script, rightToLeft).Build(layout, scriptTags, languageTag, coordinates);
            Plans.Add(key, plan);
            return plan;
        }

        public static void Clear() => Plans.Clear();

        // HarfBuzz's feature order: rvrn, direction, the script's own features, then the
        // common and horizontal ones.
        private static ShapePlanBuilder Features(UnicodeScript script, bool rightToLeft)
        {
            var builder = new ShapePlanBuilder();
            builder.Add("rvrn");
            builder.Pause();
            if (rightToLeft)
            {
                builder.Add("rtla");
                builder.Add("rtlm");
            }
            else
            {
                builder.Add("ltra");
                builder.Add("ltrm");
            }

            if (ArabicJoining.UsesJoining(script))
            {
                builder.Add("stch");
                builder.Pause();
                builder.Add("ccmp", manualZwj: true);
                builder.Add("locl", manualZwj: true);
                builder.Pause();
                foreach (string feature in ArabicJoining.Features)
                {
                    builder.Add(feature, global: false, manualZwj: true);
                    builder.Pause();
                }
                builder.Pause();
                builder.Add("rlig", manualZwj: true);
                builder.Add("calt", manualZwj: true);
                builder.Pause();
                builder.Add("rclt", manualZwj: true);
                builder.Add("liga", manualZwj: true);
                builder.Add("clig", manualZwj: true);
                builder.Add("mset", manualZwj: true);
            }

            foreach (string feature in CommonFeatures)
                builder.Add(feature);
            foreach (string feature in HorizontalFeatures)
                builder.Add(feature);
            return builder;
        }

        private static readonly string[] CommonFeatures = { "abvm", "blwm", "ccmp", "locl", "mark", "mkmk", "rlig" };
        private static readonly string[] HorizontalFeatures = { "calt", "clig", "curs", "dist", "kern", "liga", "rclt" };
    }
}
