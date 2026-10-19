using System;

namespace Unite.Text.Unicode
{
    // N0: paired brackets (BD16) take the direction of the text they enclose or surround.
    internal sealed partial class BidiParagraph
    {
        private const int MaxBracketDepth = 63;

        private readonly int[] _openerPositions = new int[MaxBracketDepth];
        private readonly int[] _openerClosers = new int[MaxBracketDepth];
        private int[] _pairOpen = Array.Empty<int>();
        private int[] _pairClose = Array.Empty<int>();

        private void ResolveBrackets(BidiClass[] types, int count, BidiClass sos, BidiClass embedding)
        {
            int pairs = FindBracketPairs(types, count);
            if (pairs == 0)
                return;

            var opposite = embedding == BidiClass.L ? BidiClass.R : BidiClass.L;
            for (int pair = 0; pair < pairs; pair++)
            {
                int open = _pairOpen[pair];
                int close = _pairClose[pair];

                bool foundEmbedding = false;
                bool foundOpposite = false;
                for (int position = open + 1; position < close; position++)
                {
                    var strong = BracketStrong(types[position]);
                    if (strong == embedding)
                    {
                        foundEmbedding = true;
                        break;
                    }
                    if (strong == opposite)
                        foundOpposite = true;
                }

                BidiClass resolved;
                if (foundEmbedding)
                {
                    resolved = embedding;
                }
                else if (foundOpposite)
                {
                    // N0 c: the context before the opening bracket decides.
                    var context = sos;
                    for (int position = open - 1; position >= 0; position--)
                    {
                        var strong = BracketStrong(types[position]);
                        if (strong != BidiClass.ON)
                        {
                            context = strong;
                            break;
                        }
                    }
                    resolved = context == opposite ? opposite : embedding;
                }
                else
                {
                    continue;
                }

                SetBracket(types, count, open, resolved);
                SetBracket(types, count, close, resolved);
            }
        }

        // A resolved bracket also resolves the original NSMs that follow it.
        private void SetBracket(BidiClass[] types, int count, int position, BidiClass resolved)
        {
            types[position] = resolved;
            for (int scan = position + 1; scan < count && _original[_sequence[scan]] == BidiClass.NSM; scan++)
                types[scan] = resolved;
        }

        private static BidiClass BracketStrong(BidiClass type) => type switch
        {
            BidiClass.L => BidiClass.L,
            BidiClass.R or BidiClass.EN or BidiClass.AN => BidiClass.R,
            _ => BidiClass.ON
        };

        // BD16: bracket pairs of the sequence, sorted by their opening position.
        private int FindBracketPairs(BidiClass[] types, int count)
        {
            if (_pairOpen.Length < count)
            {
                _pairOpen = new int[Math.Max(count, 16)];
                _pairClose = new int[Math.Max(count, 16)];
            }

            int depth = 0;
            int pairs = 0;
            for (int position = 0; position < count; position++)
            {
                if (types[position] != BidiClass.ON)
                    continue;

                int codePoint = _codePoints[_sequence[position]];
                var bracket = UnicodeData.GetBracket(codePoint, out int pair);
                if (bracket == BracketType.Open)
                {
                    if (depth == MaxBracketDepth)
                        break; // BD16: stop processing on overflow
                    _openerPositions[depth] = position;
                    _openerClosers[depth] = Canonical(pair);
                    depth++;
                }
                else if (bracket == BracketType.Close)
                {
                    int closer = Canonical(codePoint);
                    for (int entry = depth - 1; entry >= 0; entry--)
                    {
                        if (_openerClosers[entry] != closer)
                            continue;

                        _pairOpen[pairs] = _openerPositions[entry];
                        _pairClose[pairs] = position;
                        pairs++;
                        depth = entry;
                        break;
                    }
                }
            }

            // Insertion sort by opening position (pairs are few).
            for (int index = 1; index < pairs; index++)
            {
                int open = _pairOpen[index];
                int close = _pairClose[index];
                int scan = index - 1;
                while (scan >= 0 && _pairOpen[scan] > open)
                {
                    _pairOpen[scan + 1] = _pairOpen[scan];
                    _pairClose[scan + 1] = _pairClose[scan];
                    scan--;
                }
                _pairOpen[scan + 1] = open;
                _pairClose[scan + 1] = close;
            }
            return pairs;
        }

        // U+2329 / U+232A are canonically equivalent to U+3008 / U+3009.
        private static int Canonical(int codePoint) => codePoint switch
        {
            0x2329 => 0x3008,
            0x232A => 0x3009,
            _ => codePoint
        };
    }
}
