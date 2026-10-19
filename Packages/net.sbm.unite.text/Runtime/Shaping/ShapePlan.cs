using System;
using System.Collections.Generic;
using Unite.Text.Fonts.Layout;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Shaping
{
    /// <summary>One lookup to apply: which glyphs it may touch and how ZWJ / ZWNJ are matched.</summary>
    internal struct PlannedLookup
    {
        public int Index;
        public uint Mask;
        public int FeatureValue;
        public bool AutoZwj;
        public bool AutoZwnj;
    }

    /// <summary>
    /// The lookups of a font for one script, language, direction, feature settings and
    /// variation instance, in application order: GSUB stages (lookups sorted by index
    /// inside a stage, as OpenType requires) and the GPOS lookups. Built once and cached.
    /// </summary>
    internal sealed class ShapePlan
    {
        public const uint GlobalMask = 1u;

        public readonly List<PlannedLookup[]> SubstitutionStages = new();
        public PlannedLookup[] PositionLookups = Array.Empty<PlannedLookup>();

        /// <summary>Mask bit of each feature tag that got one (Arabic positional forms, user features).</summary>
        public readonly Dictionary<int, uint> FeatureMasks = new();

        /// <summary>GPOS has a kern feature for this script: the legacy 'kern' table is not used.</summary>
        public bool HasPositionedKerning;

        /// <summary>GPOS positions marks for this script (Hebrew falls back to presentation forms otherwise).</summary>
        public bool HasMarkPositioning;

        public bool KerningEnabled = true;

        public uint MaskOf(string tag) => FeatureMasks.TryGetValue(Tag(tag), out uint mask) ? mask : 0u;
    }

    /// <summary>A requested feature: tag, value (0 = off) and whether it applies to every glyph.</summary>
    internal struct FeatureRequest
    {
        public int Tag;
        public int Value;
        public bool Global;
        public bool ManualZwj;
        public int Stage;
    }

    /// <summary>Collects feature requests per stage and resolves them against GSUB / GPOS.</summary>
    internal sealed class ShapePlanBuilder
    {
        private readonly List<FeatureRequest> _features = new();
        private int _stage;

        public void Add(string tag, bool global = true, bool manualZwj = false, int value = 1)
            => Add(Tag(tag), global, manualZwj, value);

        public void Add(int tag, bool global = true, bool manualZwj = false, int value = 1)
        {
            // A later request for the same tag (user settings) replaces the earlier one.
            for (int index = 0; index < _features.Count; index++)
            {
                if (_features[index].Tag != tag)
                    continue;
                var existing = _features[index];
                existing.Value = value;
                existing.Global = global && existing.Global;
                _features[index] = existing;
                return;
            }
            _features.Add(new FeatureRequest { Tag = tag, Value = value, Global = global, ManualZwj = manualZwj, Stage = _stage });
        }

        /// <summary>GSUB lookups of later features run after every lookup of the earlier ones.</summary>
        public void Pause() => _stage++;

        public ShapePlan Build(OpenTypeLayout layout, IReadOnlyList<int> scriptTags, int languageTag, float[] coordinates)
        {
            var plan = new ShapePlan();
            uint nextBit = 2u;
            foreach (var feature in _features)
            {
                if (feature.Value != 0 && !feature.Global && !plan.FeatureMasks.ContainsKey(feature.Tag) && nextBit != 0)
                {
                    plan.FeatureMasks[feature.Tag] = nextBit;
                    nextBit <<= 1;
                }
            }

            if (layout.Substitutions != null)
            {
                var stages = Collect(plan, layout.Substitutions, scriptTags, languageTag, coordinates, _stage + 1);
                foreach (var stage in stages)
                    plan.SubstitutionStages.Add(stage);
            }

            if (layout.Positions != null)
            {
                var stages = Collect(plan, layout.Positions, scriptTags, languageTag, coordinates, 1);
                plan.PositionLookups = stages[0];
                plan.HasPositionedKerning = HasFeature(layout.Positions, scriptTags, languageTag, Tag("kern"));
                plan.HasMarkPositioning = HasFeature(layout.Positions, scriptTags, languageTag, Tag("mark"));
            }

            plan.KerningEnabled = IsEnabled(Tag("kern"));
            return plan;
        }

        private bool IsEnabled(int tag)
        {
            foreach (var feature in _features)
            {
                if (feature.Tag == tag)
                    return feature.Value != 0;
            }
            return false;
        }

        private static bool HasFeature(LayoutTable table, IReadOnlyList<int> scriptTags, int languageTag, int tag)
        {
            if (!table.TryGetLanguageSystem(scriptTags, languageTag, out _, out var features))
                return false;
            foreach (int feature in features)
            {
                if (table.FeatureTag(feature) == tag && table.FeatureLookups(feature).Length > 0)
                    return true;
            }
            return false;
        }

        private PlannedLookup[][] Collect(
            ShapePlan plan,
            LayoutTable table,
            IReadOnlyList<int> scriptTags,
            int languageTag,
            float[] coordinates,
            int stageCount
        )
        {
            var perStage = new Dictionary<int, PlannedLookup>[stageCount];
            for (int stage = 0; stage < stageCount; stage++)
                perStage[stage] = new Dictionary<int, PlannedLookup>();

            if (table.TryGetLanguageSystem(scriptTags, languageTag, out int required, out var features))
            {
                int variation = table.FindVariation(coordinates);

                if (required >= 0)
                    AddLookups(table, variation, required, perStage[0], ShapePlan.GlobalMask, 1, autoZwj: true);

                foreach (var request in _features)
                {
                    if (request.Value == 0)
                        continue;

                    uint mask = request.Global ? ShapePlan.GlobalMask : plan.FeatureMasks.TryGetValue(request.Tag, out uint bit) ? bit : 0u;
                    if (mask == 0u)
                        continue;

                    int stage = Math.Min(request.Stage, stageCount - 1);
                    foreach (int feature in features)
                    {
                        if (table.FeatureTag(feature) == request.Tag)
                            AddLookups(table, variation, feature, perStage[stage], mask, request.Value, !request.ManualZwj);
                    }
                }
            }

            var result = new PlannedLookup[stageCount][];
            for (int stage = 0; stage < stageCount; stage++)
            {
                var lookups = new List<PlannedLookup>(perStage[stage].Values);
                lookups.Sort((left, right) => left.Index.CompareTo(right.Index));
                result[stage] = lookups.ToArray();
            }
            return result;
        }

        private static void AddLookups(LayoutTable table, int variation, int feature, Dictionary<int, PlannedLookup> stage, uint mask, int value, bool autoZwj)
        {
            int substitute = table.SubstituteFeature(variation, feature);
            var lookups = substitute != 0 ? table.FeatureLookupsAt(substitute) : table.FeatureLookups(feature);
            foreach (int lookup in lookups)
            {
                if (lookup >= table.LookupCount)
                    continue;

                if (stage.TryGetValue(lookup, out var existing))
                {
                    existing.Mask |= mask;
                    existing.AutoZwj &= autoZwj;
                    stage[lookup] = existing;
                }
                else
                {
                    stage[lookup] = new PlannedLookup { Index = lookup, Mask = mask, FeatureValue = value, AutoZwj = autoZwj, AutoZwnj = true };
                }
            }
        }
    }
}
