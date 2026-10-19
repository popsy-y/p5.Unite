namespace Unite.Text.Fonts
{
    /// <summary>Region / tuple weighting shared by gvar, HVAR and MVAR.</summary>
    internal static class VariationMath
    {
        /// <summary>
        /// Contribution of one axis to a region: 1 at the peak, falling linearly to 0 at
        /// the region's start and end, 0 outside. A peak of 0 ignores the axis.
        /// </summary>
        public static float AxisScalar(float coordinate, float start, float peak, float end)
        {
            if (peak == 0f)
                return 1f;

            if (coordinate < start || coordinate > end)
                return 0f;

            if (coordinate == peak)
                return 1f;

            return coordinate < peak
                ? (coordinate - start) / (peak - start)
                : (end - coordinate) / (end - peak);
        }
    }

    /// <summary>
    /// OpenType ItemVariationStore: variation regions and delta sets addressed by
    /// (outer, inner) index, evaluated at normalized axis coordinates.
    /// </summary>
    internal sealed class ItemVariationStore
    {
        private readonly int _axisCount;
        private readonly float[][] _regions;  // per region: axisCount x (start, peak, end)
        private readonly int[][] _regionIndices;
        private readonly float[][][] _deltas; // [data][item][regionIndex]

        private ItemVariationStore(int axisCount, float[][] regions, int[][] regionIndices, float[][][] deltas)
        {
            _axisCount = axisCount;
            _regions = regions;
            _regionIndices = regionIndices;
            _deltas = deltas;
        }

        public static ItemVariationStore Read(byte[] data, int offset)
        {
            var reader = new FontReader(data, offset);
            if (reader.ReadU16() != 1)
                throw new FontFormatException("unsupported ItemVariationStore format.");

            int regionListOffset = (int)reader.ReadU32();
            int dataCount = reader.ReadU16();
            var dataOffsets = new int[dataCount];
            for (int index = 0; index < dataCount; index++)
                dataOffsets[index] = (int)reader.ReadU32();

            reader.Seek(offset + regionListOffset);
            int axisCount = reader.ReadU16();
            int regionCount = reader.ReadU16();
            var regions = new float[regionCount][];
            for (int region = 0; region < regionCount; region++)
            {
                regions[region] = new float[axisCount * 3];
                for (int value = 0; value < axisCount * 3; value++)
                    regions[region][value] = reader.ReadF2Dot14();
            }

            var regionIndices = new int[dataCount][];
            var deltas = new float[dataCount][][];
            for (int index = 0; index < dataCount; index++)
            {
                reader.Seek(offset + dataOffsets[index]);
                int itemCount = reader.ReadU16();
                int wordField = reader.ReadU16();
                bool longWords = (wordField & 0x8000) != 0;
                int wordCount = wordField & 0x7FFF;
                int indexCount = reader.ReadU16();

                // Out-of-range regions (seen in shipping fonts) contribute nothing.
                regionIndices[index] = new int[indexCount];
                for (int region = 0; region < indexCount; region++)
                {
                    int value = reader.ReadU16();
                    regionIndices[index][region] = value < regionCount ? value : -1;
                }

                deltas[index] = new float[itemCount][];
                for (int item = 0; item < itemCount; item++)
                {
                    var row = new float[indexCount];
                    for (int region = 0; region < indexCount; region++)
                    {
                        bool isWord = region < wordCount;
                        row[region] = isWord
                            ? (longWords ? reader.ReadI32() : reader.ReadI16())
                            : (longWords ? reader.ReadI16() : reader.ReadI8());
                    }
                    deltas[index][item] = row;
                }
            }

            return new ItemVariationStore(axisCount, regions, regionIndices, deltas);
        }

        /// <summary>Interpolated delta for one item at the normalized coordinates.</summary>
        public float GetDelta(int outer, int inner, float[] coordinates)
        {
            if (outer < 0 || outer >= _deltas.Length || inner < 0 || inner >= _deltas[outer].Length)
                return 0f;

            var row = _deltas[outer][inner];
            var indices = _regionIndices[outer];
            var scalars = ScalarsFor(coordinates);
            float total = 0f;
            for (int index = 0; index < indices.Length; index++)
            {
                if (indices[index] < 0)
                    continue;

                float scalar = scalars[indices[index]];
                if (scalar != 0f)
                    total += scalar * row[index];
            }
            return total;
        }

        // Region scalars depend only on the coordinates; instances keep their coordinate
        // array, so the scalars of the last few arrays seen are kept.
        private const int CachedScalarSets = 4;
        private readonly float[][] _scalarKeys = new float[CachedScalarSets][];
        private readonly float[][] _scalarValues = new float[CachedScalarSets][];
        private int _nextScalarSet;

        private float[] ScalarsFor(float[] coordinates)
        {
            for (int set = 0; set < CachedScalarSets; set++)
            {
                if (ReferenceEquals(_scalarKeys[set], coordinates))
                    return _scalarValues[set];
            }

            int slot = _nextScalarSet;
            _nextScalarSet = (_nextScalarSet + 1) % CachedScalarSets;
            var scalars = _scalarValues[slot] ??= new float[_regions.Length];
            for (int region = 0; region < _regions.Length; region++)
                scalars[region] = RegionScalar(_regions[region], coordinates);
            _scalarKeys[slot] = coordinates;
            return scalars;
        }

        private float RegionScalar(float[] region, float[] coordinates)
        {
            float scalar = 1f;
            int axes = coordinates.Length < _axisCount ? coordinates.Length : _axisCount;
            for (int axis = 0; axis < axes; axis++)
            {
                float start = region[axis * 3];
                float peak = region[axis * 3 + 1];
                float end = region[axis * 3 + 2];

                // Malformed or degenerate regions leave the axis out of the product.
                if (start > peak || peak > end || (start < 0f && end > 0f && peak != 0f))
                    continue;

                scalar *= VariationMath.AxisScalar(coordinates[axis], start, peak, end);
                if (scalar == 0f)
                    return 0f;
            }
            return scalar;
        }
    }

    /// <summary>OpenType DeltaSetIndexMap: glyph / item number → (outer, inner) delta-set index.</summary>
    internal sealed class DeltaSetIndexMap
    {
        private readonly int[] _entries;
        private readonly int _innerBits;

        private DeltaSetIndexMap(int[] entries, int innerBits)
        {
            _entries = entries;
            _innerBits = innerBits;
        }

        public static DeltaSetIndexMap Read(byte[] data, int offset)
        {
            var reader = new FontReader(data, offset);
            int format = reader.ReadU8();
            int entryFormat = reader.ReadU8();
            int count = format == 0 ? reader.ReadU16() : (int)reader.ReadU32();

            int innerBits = (entryFormat & 0x0F) + 1;
            int entrySize = ((entryFormat >> 4) & 3) + 1;
            var entries = new int[count];
            for (int index = 0; index < count; index++)
            {
                int value = 0;
                for (int part = 0; part < entrySize; part++)
                    value = (value << 8) | reader.ReadU8();
                entries[index] = value;
            }

            return new DeltaSetIndexMap(entries, innerBits);
        }

        public void Map(int index, out int outer, out int inner)
        {
            if (_entries.Length == 0)
            {
                outer = 0;
                inner = index;
                return;
            }

            // Indices past the end reuse the last entry.
            int entry = _entries[index < _entries.Length ? index : _entries.Length - 1];
            outer = entry >> _innerBits;
            inner = entry & ((1 << _innerBits) - 1);
        }
    }
}
