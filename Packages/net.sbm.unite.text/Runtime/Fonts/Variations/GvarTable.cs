using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// gvar: per-glyph TrueType point deltas. For normalized axis coordinates it sums the
    /// scaled deltas of every applicable tuple; points a tuple does not list are
    /// interpolated along their contour (IUP) when the glyph has contours.
    /// </summary>
    internal sealed class GvarTable
    {
        private const int SharedPointNumbers = 0x8000;
        private const int CountMask = 0x0FFF;
        private const int EmbeddedPeak = 0x8000;
        private const int IntermediateRegion = 0x4000;
        private const int PrivatePointNumbers = 0x2000;
        private const int TupleIndexMask = 0x0FFF;

        private readonly byte[] _data;
        private readonly int _tableOffset;
        private readonly int _axisCount;
        private readonly float[][] _sharedTuples;
        private readonly int[] _glyphOffsets;
        private readonly int _dataArrayOffset;

        // Scratch memory for TryGetDeltas (outlines are loaded on the main thread).
        private readonly FontReader _headerReader;
        private readonly FontReader _dataReader;
        private readonly float[] _peak;
        private readonly float[] _start;
        private readonly float[] _end;
        private float[] _tupleDeltaX = Array.Empty<float>();
        private float[] _tupleDeltaY = Array.Empty<float>();
        private bool[] _touched = Array.Empty<bool>();
        private float[] _packedX = Array.Empty<float>();
        private float[] _packedY = Array.Empty<float>();
        private int[] _sharedPoints = Array.Empty<int>();
        private int[] _privatePoints = Array.Empty<int>();

        private GvarTable(
            byte[] data,
            int tableOffset,
            int axisCount,
            float[][] sharedTuples,
            int[] glyphOffsets,
            int dataArrayOffset
        )
        {
            _data = data;
            _tableOffset = tableOffset;
            _axisCount = axisCount;
            _sharedTuples = sharedTuples;
            _glyphOffsets = glyphOffsets;
            _dataArrayOffset = dataArrayOffset;
            _headerReader = new FontReader(data);
            _dataReader = new FontReader(data);
            _peak = new float[axisCount];
            _start = new float[axisCount];
            _end = new float[axisCount];
        }

        public static GvarTable TryRead(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("gvar", out var table))
                return null;

            var reader = new FontReader(data, table.Offset);
            reader.Skip(4); // version
            int axisCount = reader.ReadU16();
            int sharedCount = reader.ReadU16();
            int sharedOffset = (int)reader.ReadU32();
            int glyphCount = reader.ReadU16();
            int flags = reader.ReadU16();
            int dataArrayOffset = (int)reader.ReadU32();

            bool longOffsets = (flags & 1) != 0;
            var offsets = new int[glyphCount + 1];
            for (int index = 0; index <= glyphCount; index++)
                offsets[index] = longOffsets ? (int)reader.ReadU32() : reader.ReadU16() * 2;

            reader.Seek(table.Offset + sharedOffset);
            var shared = new float[sharedCount][];
            for (int tuple = 0; tuple < sharedCount; tuple++)
                shared[tuple] = ReadTuple(reader, axisCount);

            return new GvarTable(data, table.Offset, axisCount, shared, offsets, dataArrayOffset);
        }

        /// <summary>
        /// Accumulates the point deltas of a glyph. <paramref name="x"/> / <paramref name="y"/>
        /// are the default coordinates of the first <paramref name="pointCount"/> points
        /// (phantom points included); <paramref name="endPoints"/> gives the contours for
        /// interpolation (null for composites, whose "points" are component offsets).
        /// Returns false when the glyph has no variation data for these coordinates.
        /// Reuses scratch memory, so it must not be called from several threads at once.
        /// </summary>
        public bool TryGetDeltas(
            int glyphId,
            float[] coordinates,
            float[] x,
            float[] y,
            int pointCount,
            int[] endPoints,
            float[] deltaX,
            float[] deltaY
        )
        {
            if (glyphId < 0 || glyphId + 1 >= _glyphOffsets.Length)
                return false;

            int start = _glyphOffsets[glyphId];
            int end = _glyphOffsets[glyphId + 1];
            if (end <= start)
                return false;

            Array.Clear(deltaX, 0, pointCount);
            Array.Clear(deltaY, 0, pointCount);
            EnsureScratch(pointCount);

            int glyphOffset = _tableOffset + _dataArrayOffset + start;
            var header = _headerReader;
            header.Position = glyphOffset;
            int countField = header.ReadU16();
            int tupleCount = countField & CountMask;
            int dataOffset = header.ReadU16();

            // Tuple headers and their serialized data are walked side by side.
            var data = _dataReader;
            data.Seek(glyphOffset + dataOffset);
            int sharedCount = -1;
            if ((countField & SharedPointNumbers) != 0)
                sharedCount = ReadPackedPoints(data, ref _sharedPoints);

            bool applied = false;
            for (int index = 0; index < tupleCount; index++)
            {
                int size = header.ReadU16();
                int tupleIndex = header.ReadU16();

                float[] peak;
                if ((tupleIndex & EmbeddedPeak) != 0)
                {
                    ReadTuple(header, _peak);
                    peak = _peak;
                }
                else
                {
                    peak = _sharedTuples[tupleIndex & TupleIndexMask];
                }

                bool intermediate = (tupleIndex & IntermediateRegion) != 0;
                if (intermediate)
                {
                    ReadTuple(header, _start);
                    ReadTuple(header, _end);
                }

                int tupleStart = data.Position;
                float scalar = Scalar(coordinates, peak, intermediate ? _start : null, intermediate ? _end : null);
                if (scalar != 0f)
                {
                    int[] points = _sharedPoints;
                    int listed = sharedCount;
                    if ((tupleIndex & PrivatePointNumbers) != 0)
                    {
                        points = _privatePoints;
                        listed = ReadPackedPoints(data, ref _privatePoints);
                    }

                    ApplyTuple(data, listed < 0 ? null : points, listed < 0 ? pointCount : listed, scalar, x, y, pointCount, endPoints, deltaX, deltaY);
                    applied = true;
                }

                data.Seek(tupleStart + size);
            }

            return applied;
        }

        private void EnsureScratch(int pointCount)
        {
            if (_tupleDeltaX.Length >= pointCount)
                return;

            int size = Math.Max(pointCount, _tupleDeltaX.Length * 2);
            _tupleDeltaX = new float[size];
            _tupleDeltaY = new float[size];
            _touched = new bool[size];
            _packedX = new float[size];
            _packedY = new float[size];
            _sharedPoints = new int[size];
            _privatePoints = new int[size];
        }

        private void ApplyTuple(
            FontReader reader,
            int[] points,
            int listed,
            float scalar,
            float[] x,
            float[] y,
            int pointCount,
            int[] endPoints,
            float[] deltaX,
            float[] deltaY
        )
        {
            Array.Clear(_tupleDeltaX, 0, pointCount);
            Array.Clear(_tupleDeltaY, 0, pointCount);
            Array.Clear(_touched, 0, pointCount);

            if (_packedX.Length < listed)
            {
                _packedX = new float[listed];
                _packedY = new float[listed];
            }

            ReadPackedDeltas(reader, _packedX, listed);
            ReadPackedDeltas(reader, _packedY, listed);
            for (int index = 0; index < listed; index++)
            {
                int point = points == null ? index : points[index];
                if (point >= pointCount)
                    continue;

                _tupleDeltaX[point] = _packedX[index];
                _tupleDeltaY[point] = _packedY[index];
                _touched[point] = true;
            }

            if (points != null && endPoints != null)
            {
                int contourStart = 0;
                foreach (int contourEnd in endPoints)
                {
                    InterpolateUntouched(x, _touched, _tupleDeltaX, contourStart, contourEnd);
                    InterpolateUntouched(y, _touched, _tupleDeltaY, contourStart, contourEnd);
                    contourStart = contourEnd + 1;
                }
            }

            for (int point = 0; point < pointCount; point++)
            {
                deltaX[point] += _tupleDeltaX[point] * scalar;
                deltaY[point] += _tupleDeltaY[point] * scalar;
            }
        }

        private static float Scalar(float[] coordinates, float[] peaks, float[] starts, float[] ends)
        {
            float scalar = 1f;
            for (int axis = 0; axis < peaks.Length && axis < coordinates.Length; axis++)
            {
                float peak = peaks[axis];
                float start = starts != null ? starts[axis] : Math.Min(0f, peak);
                float end = ends != null ? ends[axis] : Math.Max(0f, peak);

                scalar *= VariationMath.AxisScalar(coordinates[axis], start, peak, end);
                if (scalar == 0f)
                    return 0f;
            }
            return scalar;
        }

        // IUP: untouched points of a contour follow the nearest touched ones, linearly
        // between them along the axis and clamped outside their range.
        private static void InterpolateUntouched(float[] coordinate, bool[] touched, float[] delta, int start, int end)
        {
            int first = -1;
            int touchedCount = 0;
            for (int point = start; point <= end; point++)
            {
                if (!touched[point])
                    continue;
                if (first < 0)
                    first = point;
                touchedCount++;
            }

            if (touchedCount == 0)
                return;

            if (touchedCount == 1)
            {
                for (int point = start; point <= end; point++)
                    delta[point] = delta[first];
                return;
            }

            int previous = first;
            for (int point = first + 1; point <= end; point++)
            {
                if (!touched[point])
                    continue;
                InterpolateRange(coordinate, delta, previous, point, start, end);
                previous = point;
            }
            InterpolateRange(coordinate, delta, previous, first, start, end);
        }

        // Interpolates the untouched points strictly between two touched ones, walking the
        // contour (wrapping from end to start for the closing range).
        private static void InterpolateRange(float[] coordinate, float[] delta, int from, int to, int start, int end)
        {
            int point = from == end ? start : from + 1;
            while (point != to)
            {
                delta[point] = InterpolateValue(coordinate[point], coordinate[from], delta[from], coordinate[to], delta[to]);
                point = point == end ? start : point + 1;
            }
        }

        private static float InterpolateValue(float value, float coordinate1, float delta1, float coordinate2, float delta2)
        {
            if (coordinate1 == coordinate2)
                return delta1 == delta2 ? delta1 : 0f;

            if (coordinate1 > coordinate2)
            {
                (coordinate1, coordinate2) = (coordinate2, coordinate1);
                (delta1, delta2) = (delta2, delta1);
            }

            if (value <= coordinate1)
                return delta1;
            if (value >= coordinate2)
                return delta2;

            return delta1 + (delta2 - delta1) * (value - coordinate1) / (coordinate2 - coordinate1);
        }

        private static float[] ReadTuple(FontReader reader, int axisCount)
        {
            var tuple = new float[axisCount];
            ReadTuple(reader, tuple);
            return tuple;
        }

        private static void ReadTuple(FontReader reader, float[] tuple)
        {
            for (int axis = 0; axis < tuple.Length; axis++)
                tuple[axis] = reader.ReadF2Dot14();
        }

        // Packed point numbers into points (grown as needed); returns the count, or -1
        // for "every point".
        private static int ReadPackedPoints(FontReader reader, ref int[] points)
        {
            int count = reader.ReadU8();
            if ((count & 0x80) != 0)
                count = ((count & 0x7F) << 8) | reader.ReadU8();

            if (count == 0)
                return -1;

            if (points.Length < count)
                points = new int[count];

            int read = 0;
            int value = 0;
            while (read < count)
            {
                int control = reader.ReadU8();
                int run = (control & 0x7F) + 1;
                bool words = (control & 0x80) != 0;
                for (int index = 0; index < run && read < count; index++)
                {
                    value += words ? reader.ReadU16() : reader.ReadU8();
                    points[read++] = value;
                }
            }

            return count;
        }

        private static void ReadPackedDeltas(FontReader reader, float[] deltas, int count)
        {
            int read = 0;
            while (read < count)
            {
                int control = reader.ReadU8();
                int run = (control & 0x3F) + 1;
                bool zeros = (control & 0x80) != 0;
                bool words = (control & 0x40) != 0;
                for (int index = 0; index < run && read < count; index++)
                {
                    deltas[read++] = zeros ? 0f : (words ? reader.ReadI16() : reader.ReadI8());
                }
            }
        }
    }
}
