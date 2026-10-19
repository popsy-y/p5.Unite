using System;

namespace Unite.Text.Unicode
{
    /// <summary>
    /// The Unicode Bidirectional Algorithm (UAX #9, Unicode 16) for one paragraph:
    /// paragraph level (P2/P3), explicit embeddings, overrides and isolates (X1-X10), weak
    /// and neutral types including paired brackets (W1-W7, N0-N2), implicit levels (I1/I2),
    /// and per line the whitespace reset (L1) and visual reordering (L2).
    /// An instance keeps its buffers, so analysing text allocates only while they grow.
    /// </summary>
    internal sealed partial class BidiParagraph
    {
        public const int MaxDepth = 125;

        /// <summary>Request for <see cref="Analyze"/>: let P2/P3 choose the paragraph level.</summary>
        public const int AutoLevel = -1;

        private int[] _codePoints = Array.Empty<int>();
        private BidiClass[] _original = Array.Empty<BidiClass>();
        private BidiClass[] _types = Array.Empty<BidiClass>();
        private byte[] _levels = Array.Empty<byte>();
        private int[] _matchingPdi = Array.Empty<int>();
        private int _length;

        /// <summary>Resolved paragraph embedding level (0 LTR, 1 RTL).</summary>
        public int ParagraphLevel { get; private set; }

        public int Length => _length;

        /// <summary>True when every character resolved to the paragraph level 0 (no reordering needed).</summary>
        public bool IsPureLeftToRight { get; private set; }

        /// <summary>Resolved embedding levels per code point (before the line rule L1).</summary>
        public ReadOnlySpan<byte> Levels => new(_levels, 0, _length);

        /// <summary>True for characters removed by rule X9 (embedding / override controls, BN).</summary>
        public bool IsRemoved(int index) => IsRemovedByX9(_original[index]);

        /// <summary>
        /// Resolves the levels of one paragraph. <paramref name="paragraphLevel"/> is 0, 1
        /// or <see cref="AutoLevel"/> (first strong character, default LTR).
        /// </summary>
        public void Analyze(ReadOnlySpan<int> codePoints, int paragraphLevel)
        {
            Prepare(codePoints);

            if (IsSimpleLeftToRight(paragraphLevel))
            {
                ParagraphLevel = 0;
                IsPureLeftToRight = true;
                Array.Clear(_levels, 0, _length);
                return;
            }

            FindMatchingPdis();
            ParagraphLevel = paragraphLevel == AutoLevel
                ? FirstStrongLevel(0, _length, 0)
                : paragraphLevel & 1;

            ResolveExplicit();
            ResolveSequences();
            AssignRemovedLevels();

            IsPureLeftToRight = true;
            for (int index = 0; index < _length; index++)
            {
                if (_levels[index] != 0)
                {
                    IsPureLeftToRight = false;
                    break;
                }
            }
        }

        private void Prepare(ReadOnlySpan<int> codePoints)
        {
            _length = codePoints.Length;
            if (_codePoints.Length < _length)
            {
                int size = Math.Max(_length, _codePoints.Length * 2);
                _codePoints = new int[size];
                _original = new BidiClass[size];
                _types = new BidiClass[size];
                _levels = new byte[size];
                _matchingPdi = new int[size];
            }

            for (int index = 0; index < _length; index++)
            {
                int codePoint = codePoints[index];
                _codePoints[index] = codePoint;
                var type = UnicodeData.GetBidiClass(codePoint);
                _original[index] = type;
                _types[index] = type;
            }
        }

        // Text without any right-to-left, explicit or number-sensitive characters resolves
        // to all zeros in an LTR (or auto) paragraph: skip the algorithm.
        private bool IsSimpleLeftToRight(int paragraphLevel)
        {
            if (paragraphLevel == 1)
                return false;

            for (int index = 0; index < _length; index++)
            {
                switch (_original[index])
                {
                    case BidiClass.R:
                    case BidiClass.AL:
                    case BidiClass.AN:
                    case BidiClass.LRE:
                    case BidiClass.LRO:
                    case BidiClass.RLE:
                    case BidiClass.RLO:
                    case BidiClass.PDF:
                    case BidiClass.LRI:
                    case BidiClass.RLI:
                    case BidiClass.FSI:
                    case BidiClass.PDI:
                        return false;
                }
            }
            return true;
        }

        private static bool IsRemovedByX9(BidiClass type)
            => type is BidiClass.LRE or BidiClass.RLE or BidiClass.LRO or BidiClass.RLO or BidiClass.PDF or BidiClass.BN;

        private static bool IsIsolateInitiator(BidiClass type)
            => type is BidiClass.LRI or BidiClass.RLI or BidiClass.FSI;

        // BD9: the PDI matching each isolate initiator (-1 when unmatched; also -1 for others).
        private void FindMatchingPdis()
        {
            for (int index = 0; index < _length; index++)
            {
                _matchingPdi[index] = -1;
                if (!IsIsolateInitiator(_original[index]))
                    continue;

                int depth = 1;
                for (int scan = index + 1; scan < _length; scan++)
                {
                    var type = _original[scan];
                    if (IsIsolateInitiator(type))
                    {
                        depth++;
                    }
                    else if (type == BidiClass.PDI && --depth == 0)
                    {
                        _matchingPdi[index] = scan;
                        break;
                    }
                    else if (type == BidiClass.B)
                    {
                        break;
                    }
                }
            }
        }

        // P2/P3: level of the first strong character in [start, end), skipping isolates;
        // fallback when there is none.
        private int FirstStrongLevel(int start, int end, int fallback)
        {
            for (int index = start; index < end; index++)
            {
                var type = _original[index];
                if (type == BidiClass.L)
                    return 0;
                if (type is BidiClass.R or BidiClass.AL)
                    return 1;
                if (IsIsolateInitiator(type))
                {
                    int pdi = _matchingPdi[index];
                    if (pdi < 0)
                        return fallback;
                    index = pdi;
                }
                else if (type == BidiClass.B)
                {
                    return fallback;
                }
            }
            return fallback;
        }

        // X9 removed characters take the level of the preceding character (or the
        // paragraph level), which keeps them inside their run for reordering.
        private void AssignRemovedLevels()
        {
            byte previous = (byte)ParagraphLevel;
            for (int index = 0; index < _length; index++)
            {
                if (IsRemovedByX9(_original[index]))
                    _levels[index] = previous;
                else
                    previous = _levels[index];
            }
        }
    }
}
