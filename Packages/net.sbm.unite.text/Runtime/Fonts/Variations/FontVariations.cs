using System;
using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>A variation axis of a variable font (fvar): tag and user-space range.</summary>
    public readonly struct VariationAxis
    {
        public VariationAxis(string tag, float minimum, float defaultValue, float maximum)
        {
            Tag = tag;
            Minimum = minimum;
            Default = defaultValue;
            Maximum = maximum;
        }

        /// <summary>Four-character axis tag such as "wght", "wdth", "ital", "slnt" or "opsz".</summary>
        public string Tag { get; }

        public float Minimum { get; }
        public float Default { get; }
        public float Maximum { get; }

        public float Clamp(float value) => Math.Min(Maximum, Math.Max(Minimum, value));

        public override string ToString() => $"{Tag} {Minimum}..{Default}..{Maximum}";
    }

    /// <summary>A requested axis value, in the axis's user-space units (e.g. wght 700).</summary>
    public readonly struct AxisSetting
    {
        public AxisSetting(string tag, float value)
        {
            Tag = tag;
            Value = value;
        }

        public string Tag { get; }
        public float Value { get; }

        public override string ToString() => $"{Tag} {Value}";
    }

    /// <summary>
    /// The variation tables of a variable font that are independent of the outline
    /// format: fvar axes, avar segment maps, HVAR advance deltas and MVAR metric deltas.
    /// </summary>
    internal sealed class FontVariations
    {
        private readonly float[][] _avarFrom; // per axis: piecewise-linear map (null = identity)
        private readonly float[][] _avarTo;

        private ItemVariationStore _horizontalStore;
        private DeltaSetIndexMap _advanceMap;
        private ItemVariationStore _metricsStore;
        private Dictionary<string, (int Outer, int Inner)> _metricRecords;

        private FontVariations(VariationAxis[] axes, float[][] avarFrom, float[][] avarTo)
        {
            Axes = axes;
            _avarFrom = avarFrom;
            _avarTo = avarTo;
        }

        public VariationAxis[] Axes { get; }

        public bool HasAdvanceVariations => _horizontalStore != null;

        /// <summary>Reads fvar (required) and the optional avar / HVAR / MVAR tables; null when the font has no fvar.</summary>
        public static FontVariations TryRead(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("fvar", out var fvar))
                return null;

            var reader = new FontReader(data, fvar.Offset);
            reader.Skip(4); // version
            int axesOffset = reader.ReadU16();
            reader.Skip(2); // reserved
            int axisCount = reader.ReadU16();
            int axisSize = reader.ReadU16();
            if (axisCount == 0)
                return null;

            var axes = new VariationAxis[axisCount];
            for (int index = 0; index < axisCount; index++)
            {
                reader.Seek(fvar.Offset + axesOffset + index * axisSize);
                string tag = reader.ReadTag();
                float minimum = reader.ReadFixed();
                float defaultValue = reader.ReadFixed();
                float maximum = reader.ReadFixed();
                axes[index] = new VariationAxis(tag, minimum, defaultValue, maximum);
            }

            var avarFrom = new float[axisCount][];
            var avarTo = new float[axisCount][];
            if (directory.TryGet("avar", out var avar))
                ReadAvar(data, avar, axisCount, avarFrom, avarTo);

            var variations = new FontVariations(axes, avarFrom, avarTo);
            variations.ReadHvar(data, directory);
            variations.ReadMvar(data, directory);
            return variations;
        }

        public int IndexOfAxis(string tag)
        {
            for (int index = 0; index < Axes.Length; index++)
            {
                if (Axes[index].Tag == tag)
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// Normalized coordinates (-1..1, default 0) for the requested settings; axes not
        /// mentioned stay at their default. Values are clamped to the axis range.
        /// </summary>
        public float[] Normalize(IReadOnlyList<AxisSetting> settings)
        {
            var coordinates = new float[Axes.Length];
            Normalize(settings, coordinates);
            return coordinates;
        }

        /// <summary>Writes the normalized coordinates (one per axis) into <paramref name="coordinates"/>.</summary>
        public void Normalize(IReadOnlyList<AxisSetting> settings, float[] coordinates)
        {
            Array.Clear(coordinates, 0, coordinates.Length);
            if (settings == null)
                return;

            for (int index = 0; index < settings.Count; index++)
            {
                var setting = settings[index];
                int axis = IndexOfAxis(setting.Tag);
                if (axis < 0)
                    continue;

                var definition = Axes[axis];
                float value = definition.Clamp(setting.Value);
                float normalized = 0f;
                if (value < definition.Default)
                    normalized = (value - definition.Default) / (definition.Default - definition.Minimum);
                else if (value > definition.Default)
                    normalized = (value - definition.Default) / (definition.Maximum - definition.Default);

                coordinates[axis] = MapAvar(axis, normalized);
            }
        }

        public static bool IsDefault(float[] coordinates)
        {
            foreach (float value in coordinates)
            {
                if (value != 0f)
                    return false;
            }
            return true;
        }

        /// <summary>Advance width delta from HVAR (0 when the font has none).</summary>
        public float AdvanceDelta(int glyphId, float[] coordinates)
        {
            if (_horizontalStore == null)
                return 0f;

            int outer = 0;
            int inner = glyphId;
            _advanceMap?.Map(glyphId, out outer, out inner);
            return _horizontalStore.GetDelta(outer, inner, coordinates);
        }

        /// <summary>MVAR delta of a metric tag such as "hasc" / "hdsc" (0 when absent).</summary>
        public float MetricDelta(string tag, float[] coordinates)
        {
            if (_metricsStore == null || !_metricRecords.TryGetValue(tag, out var record))
                return 0f;

            return _metricsStore.GetDelta(record.Outer, record.Inner, coordinates);
        }

        private float MapAvar(int axis, float value)
        {
            var from = _avarFrom[axis];
            if (from == null || from.Length == 0)
                return value;

            var to = _avarTo[axis];
            if (value <= from[0])
                return to[0] + (value - from[0]);

            for (int index = 1; index < from.Length; index++)
            {
                if (value > from[index])
                    continue;

                float span = from[index] - from[index - 1];
                return span <= 0f
                    ? to[index]
                    : to[index - 1] + (to[index] - to[index - 1]) * (value - from[index - 1]) / span;
            }

            int last = from.Length - 1;
            return to[last] + (value - from[last]);
        }

        private static void ReadAvar(byte[] data, TableRecord avar, int axisCount, float[][] from, float[][] to)
        {
            var reader = new FontReader(data, avar.Offset);
            reader.Skip(4); // version
            reader.Skip(2); // reserved
            int count = Math.Min(reader.ReadU16(), axisCount);
            for (int axis = 0; axis < count; axis++)
            {
                int mapCount = reader.ReadU16();
                from[axis] = new float[mapCount];
                to[axis] = new float[mapCount];
                for (int index = 0; index < mapCount; index++)
                {
                    from[axis][index] = reader.ReadF2Dot14();
                    to[axis][index] = reader.ReadF2Dot14();
                }
            }
        }

        private void ReadHvar(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("HVAR", out var hvar))
                return;

            var reader = new FontReader(data, hvar.Offset);
            reader.Skip(4); // version
            int storeOffset = (int)reader.ReadU32();
            int advanceMapOffset = (int)reader.ReadU32();

            _horizontalStore = ItemVariationStore.Read(data, hvar.Offset + storeOffset);
            if (advanceMapOffset != 0)
                _advanceMap = DeltaSetIndexMap.Read(data, hvar.Offset + advanceMapOffset);
        }

        private void ReadMvar(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("MVAR", out var mvar))
                return;

            var reader = new FontReader(data, mvar.Offset);
            reader.Skip(4); // version
            reader.Skip(2); // reserved
            int recordSize = reader.ReadU16();
            int recordCount = reader.ReadU16();
            int storeOffset = reader.ReadU16();
            if (storeOffset == 0)
                return;

            _metricRecords = new Dictionary<string, (int, int)>(recordCount);
            for (int index = 0; index < recordCount; index++)
            {
                reader.Seek(mvar.Offset + 12 + index * recordSize);
                string tag = reader.ReadTag();
                int outer = reader.ReadU16();
                int inner = reader.ReadU16();
                _metricRecords[tag] = (outer, inner);
            }

            _metricsStore = ItemVariationStore.Read(data, mvar.Offset + storeOffset);
        }
    }
}
