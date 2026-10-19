using System;
using System.Collections.Generic;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Fonts.Layout
{
    /// <summary>One lookup of GSUB or GPOS with Extension subtables already resolved.</summary>
    internal sealed class LayoutLookup
    {
        public const int RightToLeft = 0x0001;
        public const int IgnoreBaseGlyphs = 0x0002;
        public const int IgnoreLigatures = 0x0004;
        public const int IgnoreMarks = 0x0008;
        public const int UseMarkFilteringSet = 0x0010;

        public int Type;
        public int Flag;
        public int MarkFilteringSet;
        public int[] Subtables;

        /// <summary>Glyphs the lookup can start at (see <see cref="GlyphDigest"/>).</summary>
        public GlyphDigest Digest;

        /// <summary>Mark attachment class filter (high byte of the flag); 0 when unused.</summary>
        public int MarkAttachmentType => (Flag >> 8) & 0xFF;
    }

    /// <summary>
    /// The common part of GSUB and GPOS: script / language systems, features, lookups and
    /// feature variations. Lookups are parsed on first use.
    /// </summary>
    internal sealed class LayoutTable
    {
        private readonly int _scriptList;
        private readonly int _featureList;
        private readonly int _lookupList;
        private readonly int _featureVariations;
        private readonly int _extensionType;
        private readonly LayoutLookup[] _lookups;
        private readonly Dictionary<int, int[]> _featureLookups = new();

        private LayoutTable(byte[] data, int offset, int extensionType)
        {
            Data = data;
            _extensionType = extensionType;
            int minor = U16(data, offset + 2);
            _scriptList = Offset16(data, offset, offset + 4);
            _featureList = Offset16(data, offset, offset + 6);
            _lookupList = Offset16(data, offset, offset + 8);
            if (minor >= 1)
            {
                int variations = U32(data, offset + 10);
                _featureVariations = variations == 0 ? 0 : offset + variations;
            }

            _lookups = new LayoutLookup[_lookupList == 0 ? 0 : U16(data, _lookupList)];
        }

        public byte[] Data { get; }

        public int LookupCount => _lookups.Length;

        public int FeatureCount => _featureList == 0 ? 0 : U16(Data, _featureList);

        /// <summary>Reads GSUB (extension lookup type 7) or GPOS (type 9); null when absent or unreadable.</summary>
        public static LayoutTable TryRead(byte[] data, SfntDirectory directory, string tag, int extensionType)
        {
            if (!directory.TryGet(tag, out var table) || table.Length < 10)
                return null;

            try
            {
                return new LayoutTable(data, table.Offset, extensionType);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        public int FeatureTag(int feature) => Tag(Data, _featureList + 2 + feature * 6);

        /// <summary>Lookup indices of a feature (cached).</summary>
        public int[] FeatureLookups(int feature) => FeatureLookupsAt(_featureList + U16(Data, _featureList + 2 + feature * 6 + 4));

        /// <summary>Lookup indices of the feature table at an absolute offset (cached).</summary>
        public int[] FeatureLookupsAt(int featureTable)
        {
            if (_featureLookups.TryGetValue(featureTable, out var lookups))
                return lookups;

            int count = U16(Data, featureTable + 2);
            lookups = new int[count];
            for (int index = 0; index < count; index++)
                lookups[index] = U16(Data, featureTable + 4 + index * 2);
            _featureLookups.Add(featureTable, lookups);
            return lookups;
        }

        public LayoutLookup GetLookup(int index)
        {
            if ((uint)index >= (uint)_lookups.Length)
                return null;

            var lookup = _lookups[index];
            if (lookup != null)
                return lookup;

            int table = _lookupList + U16(Data, _lookupList + 2 + index * 2);
            int declaredType = U16(Data, table);
            int type = declaredType;
            int flag = U16(Data, table + 2);
            int count = U16(Data, table + 4);
            var subtables = new int[count];
            for (int subtable = 0; subtable < count; subtable++)
            {
                int position = table + U16(Data, table + 6 + subtable * 2);
                if (declaredType == _extensionType)
                {
                    // ExtensionPosFormat1 / ExtensionSubstFormat1: real type + Offset32.
                    type = U16(Data, position + 2);
                    position += U32(Data, position + 4);
                }
                subtables[subtable] = position;
            }

            lookup = new LayoutLookup
            {
                Type = type,
                Flag = flag,
                MarkFilteringSet = (flag & LayoutLookup.UseMarkFilteringSet) != 0 ? U16(Data, table + 6 + count * 2) : -1,
                Subtables = subtables
            };
            lookup.Digest = GlyphDigest.ForLookup(Data, lookup, _extensionType == 9);
            _lookups[index] = lookup;
            return lookup;
        }

        /// <summary>
        /// The language system for a script and language: feature indices and the required
        /// feature (-1 when none). The first script tag found wins, then DFLT, dflt and latn;
        /// an unknown language uses the default language system.
        /// </summary>
        public bool TryGetLanguageSystem(IReadOnlyList<int> scriptTags, int languageTag, out int requiredFeature, out int[] features)
        {
            requiredFeature = -1;
            features = Array.Empty<int>();
            if (_scriptList == 0)
                return false;

            int script = FindScript(scriptTags);
            if (script == 0)
                return false;

            int langSys = Offset16(Data, script, script);
            int count = U16(Data, script + 2);
            for (int index = 0; index < count; index++)
            {
                int record = script + 4 + index * 6;
                if (Tag(Data, record) == languageTag)
                {
                    langSys = script + U16(Data, record + 4);
                    break;
                }
            }

            if (langSys == 0)
                return false;

            int required = U16(Data, langSys + 2);
            requiredFeature = required == 0xFFFF ? -1 : required;
            int featureCount = U16(Data, langSys + 4);
            features = new int[featureCount];
            for (int index = 0; index < featureCount; index++)
                features[index] = U16(Data, langSys + 6 + index * 2);
            return true;
        }

        private int FindScript(IReadOnlyList<int> scriptTags)
        {
            int count = U16(Data, _scriptList);
            for (int pass = 0; pass < scriptTags.Count + 3; pass++)
            {
                int wanted = pass < scriptTags.Count ? scriptTags[pass]
                    : pass == scriptTags.Count ? Tag("DFLT")
                    : pass == scriptTags.Count + 1 ? Tag("dflt")
                    : Tag("latn");
                for (int index = 0; index < count; index++)
                {
                    int record = _scriptList + 2 + index * 6;
                    if (Tag(Data, record) == wanted)
                        return _scriptList + U16(Data, record + 4);
                }
            }
            return 0;
        }

        /// <summary>
        /// FeatureVariations: the first record whose condition set matches the normalized
        /// coordinates, or -1 (default features). Only axis-range conditions exist.
        /// </summary>
        public int FindVariation(float[] coordinates)
        {
            if (_featureVariations == 0 || coordinates == null)
                return -1;

            int count = U32(Data, _featureVariations + 4);
            for (int record = 0; record < count; record++)
            {
                int position = _featureVariations + 8 + record * 8;
                int conditionSet = U32(Data, position);
                if (conditionSet == 0 || ConditionsMatch(_featureVariations + conditionSet, coordinates))
                    return record;
            }
            return -1;
        }

        /// <summary>The alternate feature table of a feature under a variation record, or 0 (keep the default).</summary>
        public int SubstituteFeature(int variation, int feature)
        {
            if (variation < 0)
                return 0;

            int substitution = U32(Data, _featureVariations + 8 + variation * 8 + 4);
            if (substitution == 0)
                return 0;

            int table = _featureVariations + substitution;
            int count = U16(Data, table + 4);
            for (int index = 0; index < count; index++)
            {
                int record = table + 6 + index * 6;
                if (U16(Data, record) == feature)
                    return table + U32(Data, record + 2);
            }
            return 0;
        }

        private bool ConditionsMatch(int set, float[] coordinates)
        {
            int count = U16(Data, set);
            for (int index = 0; index < count; index++)
            {
                int condition = set + U32(Data, set + 2 + index * 4);
                if (U16(Data, condition) != 1)
                    return false; // unknown condition formats never match

                int axis = U16(Data, condition + 2);
                float min = I16(Data, condition + 4) / 16384f;
                float max = I16(Data, condition + 6) / 16384f;
                float value = axis < coordinates.Length ? coordinates[axis] : 0f;
                if (value < min || value > max)
                    return false;
            }
            return true;
        }
    }
}
