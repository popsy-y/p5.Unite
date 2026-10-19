using System;

namespace Unite.Text.Unicode
{
    // X10 isolating run sequences and the per-sequence rules W1-W7, N0-N2, I1-I2.
    internal sealed partial class BidiParagraph
    {
        private int[] _runChars = Array.Empty<int>();   // non-removed character indices, in order
        private int[] _runStart = Array.Empty<int>();   // run -> offset into _runChars
        private int[] _runCount = Array.Empty<int>();
        private int[] _runOfChar = Array.Empty<int>();  // character -> run (-1 for removed)
        private bool[] _runUsed = Array.Empty<bool>();
        private int[] _sequence = Array.Empty<int>();
        private BidiClass[] _sequenceTypes = Array.Empty<BidiClass>();
        private byte[] _explicitLevels = Array.Empty<byte>(); // levels after X1-X8, read for sos / eos

        private void ResolveSequences()
        {
            int runs = BuildLevelRuns();
            Array.Clear(_runUsed, 0, runs);
            Array.Copy(_levels, _explicitLevels, _length);

            for (int run = 0; run < runs; run++)
            {
                if (_runUsed[run])
                    continue;

                // Chain level runs across matched isolates into one sequence.
                int count = 0;
                int current = run;
                while (true)
                {
                    _runUsed[current] = true;
                    Array.Copy(_runChars, _runStart[current], _sequence, count, _runCount[current]);
                    count += _runCount[current];

                    int last = _sequence[count - 1];
                    int pdi = IsIsolateInitiator(_original[last]) ? _matchingPdi[last] : -1;
                    if (pdi < 0 || _runOfChar[pdi] < 0)
                        break;
                    current = _runOfChar[pdi];
                }

                ResolveSequence(count);
            }
        }

        private int BuildLevelRuns()
        {
            if (_runChars.Length < _length)
            {
                int size = Math.Max(_length, _runChars.Length * 2);
                _runChars = new int[size];
                _runStart = new int[size];
                _runCount = new int[size];
                _runOfChar = new int[size];
                _runUsed = new bool[size];
                _sequence = new int[size];
                _sequenceTypes = new BidiClass[size];
                _explicitLevels = new byte[size];
            }

            int chars = 0;
            int runs = 0;
            int previousLevel = -1;
            for (int index = 0; index < _length; index++)
            {
                if (IsRemovedByX9(_original[index]))
                {
                    _runOfChar[index] = -1;
                    continue;
                }

                if (_levels[index] != previousLevel)
                {
                    _runStart[runs] = chars;
                    _runCount[runs] = 0;
                    runs++;
                    previousLevel = _levels[index];
                }

                _runChars[chars++] = index;
                _runCount[runs - 1]++;
                _runOfChar[index] = runs - 1;
            }
            return runs;
        }

        private void ResolveSequence(int count)
        {
            var types = _sequenceTypes;
            for (int position = 0; position < count; position++)
                types[position] = _types[_sequence[position]];

            int level = _explicitLevels[_sequence[0]];
            var embedding = (level & 1) == 0 ? BidiClass.L : BidiClass.R;
            var sos = Direction(Math.Max(level, NeighborLevel(_sequence[0], -1)));
            int last = _sequence[count - 1];
            int after = IsIsolateInitiator(_original[last]) ? ParagraphLevel : NeighborLevel(last, +1);
            var eos = Direction(Math.Max(level, after));

            ResolveWeak(types, count, sos);
            ResolveBrackets(types, count, sos, embedding);
            ResolveNeutral(types, count, sos, eos, embedding);

            // I1, I2
            for (int position = 0; position < count; position++)
            {
                int index = _sequence[position];
                var type = types[position];
                _types[index] = type;
                if ((level & 1) == 0)
                {
                    if (type == BidiClass.R)
                        _levels[index] = (byte)(level + 1);
                    else if (type is BidiClass.AN or BidiClass.EN)
                        _levels[index] = (byte)(level + 2);
                }
                else if (type is BidiClass.L or BidiClass.EN or BidiClass.AN)
                {
                    _levels[index] = (byte)(level + 1);
                }
            }
        }

        private static BidiClass Direction(int level) => (level & 1) == 0 ? BidiClass.L : BidiClass.R;

        // Level of the nearest non-removed character before (-1) or after (+1) an index,
        // or the paragraph level at the paragraph edge.
        private int NeighborLevel(int index, int step)
        {
            for (int scan = index + step; scan >= 0 && scan < _length; scan += step)
            {
                if (!IsRemovedByX9(_original[scan]))
                    return _explicitLevels[scan];
            }
            return ParagraphLevel;
        }

        private void ResolveWeak(BidiClass[] types, int count, BidiClass sos)
        {
            // W1: NSM takes the type of the previous character (ON after isolates).
            for (int position = 0; position < count; position++)
            {
                if (types[position] != BidiClass.NSM)
                    continue;

                if (position == 0)
                    types[position] = sos;
                else
                {
                    var previous = types[position - 1];
                    types[position] = previous is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI
                        ? BidiClass.ON
                        : previous;
                }
            }

            // W2: EN after AL becomes AN. W3: AL becomes R.
            var lastStrong = sos;
            for (int position = 0; position < count; position++)
            {
                var type = types[position];
                if (type is BidiClass.L or BidiClass.R or BidiClass.AL)
                    lastStrong = type;
                else if (type == BidiClass.EN && lastStrong == BidiClass.AL)
                    types[position] = BidiClass.AN;
            }
            for (int position = 0; position < count; position++)
            {
                if (types[position] == BidiClass.AL)
                    types[position] = BidiClass.R;
            }

            // W4: a single separator between two numbers of the same kind.
            for (int position = 1; position < count - 1; position++)
            {
                var type = types[position];
                var before = types[position - 1];
                var next = types[position + 1];
                if (type == BidiClass.ES && before == BidiClass.EN && next == BidiClass.EN)
                    types[position] = BidiClass.EN;
                else if (type == BidiClass.CS && before == BidiClass.EN && next == BidiClass.EN)
                    types[position] = BidiClass.EN;
                else if (type == BidiClass.CS && before == BidiClass.AN && next == BidiClass.AN)
                    types[position] = BidiClass.AN;
            }

            // W5: terminators next to European numbers become numbers.
            for (int position = 0; position < count; position++)
            {
                if (types[position] != BidiClass.ET)
                    continue;

                int end = position;
                while (end < count && types[end] == BidiClass.ET)
                    end++;

                bool adjacent = (position > 0 && types[position - 1] == BidiClass.EN) || (end < count && types[end] == BidiClass.EN);
                if (adjacent)
                {
                    for (int scan = position; scan < end; scan++)
                        types[scan] = BidiClass.EN;
                }
                position = end - 1;
            }

            // W6: remaining separators and terminators become neutral.
            for (int position = 0; position < count; position++)
            {
                if (types[position] is BidiClass.ES or BidiClass.ET or BidiClass.CS)
                    types[position] = BidiClass.ON;
            }

            // W7: European numbers in a left-to-right context become L.
            lastStrong = sos;
            for (int position = 0; position < count; position++)
            {
                var type = types[position];
                if (type is BidiClass.L or BidiClass.R)
                    lastStrong = type;
                else if (type == BidiClass.EN && lastStrong == BidiClass.L)
                    types[position] = BidiClass.L;
            }
        }

        // N1, N2: runs of neutrals between same-direction text take that direction,
        // otherwise the embedding direction. Numbers count as R.
        private static void ResolveNeutral(BidiClass[] types, int count, BidiClass sos, BidiClass eos, BidiClass embedding)
        {
            for (int position = 0; position < count; position++)
            {
                if (!IsNeutral(types[position]))
                    continue;

                int end = position;
                while (end < count && IsNeutral(types[end]))
                    end++;

                var leading = position == 0 ? sos : StrongDirection(types[position - 1]);
                var trailing = end == count ? eos : StrongDirection(types[end]);
                var resolved = leading == trailing ? leading : embedding;
                for (int scan = position; scan < end; scan++)
                    types[scan] = resolved;

                position = end - 1;
            }
        }

        private static bool IsNeutral(BidiClass type)
            => type is BidiClass.B or BidiClass.S or BidiClass.WS or BidiClass.ON
                or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI;

        private static BidiClass StrongDirection(BidiClass type)
            => type == BidiClass.L ? BidiClass.L : BidiClass.R;
    }
}
