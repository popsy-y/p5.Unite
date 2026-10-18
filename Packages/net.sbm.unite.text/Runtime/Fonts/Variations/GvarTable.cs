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
        /// are the default coordinates of all points (phantom points included);
        /// <paramref name="endPoints"/> gives the contours for interpolation (null for
        /// composites, whose "points" are component offsets). Returns false when the
        /// glyph has no variation data for these coordinates.
        /// </summary>
        public bool TryGetDeltas(
            int glyphId,
            float[] coordinates,
            float[] x,
            float[] y,
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

            Array.Clear(deltaX, 0, deltaX.Length);
            Array.Clear(deltaY, 0, deltaY.Length);

            int glyphOffset = _tableOffset + _dataArrayOffset + start;
            var reader = new FontReader(_data, glyphOffset);
            int countField = reader.ReadU16();
            int tupleCount = countField & CountMask;
            int dataOffset = reader.ReadU16();

            var headers = new TupleHeader[tupleCount];
            for (int index = 0; index < tupleCount; index++)
            {
                int size = reader.ReadU16();
                int tupleIndex = reader.ReadU16();
                float[] peak = (tupleIndex & EmbeddedPeak) != 0
                    ? ReadTuple(reader, _axisCount)
                    : _sharedTuples[tupleIndex & TupleIndexMask];

                float[] intermediateStart = null;
                float[] intermediateEnd = null;
                if ((tupleIndex & IntermediateRegion) != 0)
                {
                    intermediateStart = ReadTuple(reader, _axisCount);
                    intermediateEnd = ReadTuple(reader, _axisCount);
                }

                headers[index] = new TupleHeader(size, tupleIndex, peak, intermediateStart, intermediateEnd);
            }

            reader.Seek(glyphOffset + dataOffset);
            int[] sharedPoints = null;
            bool hasSharedPoints = (countField & SharedPointNumbers) != 0;
            if (hasSharedPoints)
                sharedPoints = ReadPackedPoints(reader, x.Length);

            bool applied = false;
            var tupleDeltaX = new float[x.Length];
            var tupleDeltaY = new float[x.Length];
            var touched = new bool[x.Length];

            foreach (var header in headers)
            {
                int tupleStart = reader.Position;
                float scalar = header.Scalar(coordinates);
                if (scalar != 0f)
                {
                    int[] points = (header.TupleIndex & PrivatePointNumbers) != 0
                        ? ReadPackedPoints(reader, x.Length)
                        : sharedPoints;

                    ApplyTuple(
                        reader, points, scalar, x, y, endPoints,
                        tupleDeltaX, tupleDeltaY, touched, deltaX, deltaY
                    );
                    applied = true;
                }

                reader.Seek(tupleStart + header.Size);
            }

            return applied;
        }

        private static void ApplyTuple(
            FontReader reader,
            int[] points,
            float scalar,
            float[] x,
            float[] y,
            int[] endPoints,
            float[] tupleDeltaX,
            float[] tupleDeltaY,
            bool[] touched,
            float[] deltaX,
            float[] deltaY
        )
        {
            int pointCount = x.Length;
            int listed = points == null ? pointCount : points.Length;

            Array.Clear(tupleDeltaX, 0, pointCount);
            Array.Clear(tupleDeltaY, 0, pointCount);
            Array.Clear(touched, 0, pointCount);

            var packedX = ReadPackedDeltas(reader, listed);
            var packedY = ReadPackedDeltas(reader, listed);
            for (int index = 0; index < listed; index++)
            {
                int point = points == null ? index : points[index];
                if (point >= pointCount)
                    continue;

                tupleDeltaX[point] = packedX[index];
                tupleDeltaY[point] = packedY[index];
                touched[point] = true;
            }

            if (points != null && endPoints != null)
            {
                int contourStart = 0;
                foreach (int contourEnd in endPoints)
                {
                    InterpolateUntouched(x, touched, tupleDeltaX, contourStart, contourEnd);
                    InterpolateUntouched(y, touched, tupleDeltaY, contourStart, contourEnd);
                    contourStart = contourEnd + 1;
                }
            }

            for (int point = 0; point < pointCount; point++)
            {
                deltaX[point] += tupleDeltaX[point] * scalar;
                deltaY[point] += tupleDeltaY[point] * scalar;
            }
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
            for (int axis = 0; axis < axisCount; axis++)
                tuple[axis] = reader.ReadF2Dot14();
            return tuple;
        }

        // Packed point numbers; null means every point.
        private static int[] ReadPackedPoints(FontReader reader, int pointCount)
        {
            int count = reader.ReadU8();
            if ((count & 0x80) != 0)
                count = ((count & 0x7F) << 8) | reader.ReadU8();

            if (count == 0)
                return null;

            var points = new int[count];
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

            return points;
        }

        private static float[] ReadPackedDeltas(FontReader reader, int count)
        {
            var deltas = new float[count];
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

            return deltas;
        }

        private readonly struct TupleHeader
        {
            private readonly float[] _peak;
            private readonly float[] _start;
            private readonly float[] _end;

            public TupleHeader(int size, int tupleIndex, float[] peak, float[] start, float[] end)
            {
                Size = size;
                TupleIndex = tupleIndex;
                _peak = peak;
                _start = start;
                _end = end;
            }

            public int Size { get; }
            public int TupleIndex { get; }

            public float Scalar(float[] coordinates)
            {
                float scalar = 1f;
                for (int axis = 0; axis < _peak.Length && axis < coordinates.Length; axis++)
                {
                    float peak = _peak[axis];
                    float start = _start != null ? _start[axis] : Math.Min(0f, peak);
                    float end = _end != null ? _end[axis] : Math.Max(0f, peak);

                    scalar *= VariationMath.AxisScalar(coordinates[axis], start, peak, end);
                    if (scalar == 0f)
                        return 0f;
                }
                return scalar;
            }
        }
    }
}
