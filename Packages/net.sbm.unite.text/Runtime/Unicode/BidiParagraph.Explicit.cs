namespace Unite.Text.Unicode
{
    // X1-X8: explicit embedding levels and directional overrides, with isolates.
    internal sealed partial class BidiParagraph
    {
        private struct StackEntry
        {
            public byte Level;
            public BidiClass Override; // ON = neutral (no override), L or R
            public bool Isolate;
        }

        private readonly StackEntry[] _stack = new StackEntry[MaxDepth + 2];

        private void ResolveExplicit()
        {
            int depth = 0;
            _stack[0] = new StackEntry { Level = (byte)ParagraphLevel, Override = BidiClass.ON };
            int overflowIsolates = 0;
            int overflowEmbeddings = 0;
            int validIsolates = 0;

            for (int index = 0; index < _length; index++)
            {
                var type = _original[index];
                ref var top = ref _stack[depth];

                switch (type)
                {
                    case BidiClass.RLE:
                    case BidiClass.LRE:
                    case BidiClass.RLO:
                    case BidiClass.LRO:
                    {
                        // X2-X5
                        _levels[index] = top.Level;
                        bool rightToLeft = type is BidiClass.RLE or BidiClass.RLO;
                        int level = rightToLeft ? (top.Level + 1) | 1 : (top.Level + 2) & ~1;
                        if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                        {
                            _stack[++depth] = new StackEntry
                            {
                                Level = (byte)level,
                                Override = type == BidiClass.RLO ? BidiClass.R : type == BidiClass.LRO ? BidiClass.L : BidiClass.ON
                            };
                        }
                        else if (overflowIsolates == 0)
                        {
                            overflowEmbeddings++;
                        }
                        break;
                    }

                    case BidiClass.RLI:
                    case BidiClass.LRI:
                    case BidiClass.FSI:
                    {
                        // X5a-X5c
                        _levels[index] = top.Level;
                        if (top.Override != BidiClass.ON)
                            _types[index] = top.Override;

                        bool rightToLeft = type == BidiClass.RLI;
                        if (type == BidiClass.FSI)
                        {
                            int end = _matchingPdi[index] < 0 ? _length : _matchingPdi[index];
                            rightToLeft = FirstStrongLevel(index + 1, end, 0) == 1;
                        }

                        int level = rightToLeft ? (top.Level + 1) | 1 : (top.Level + 2) & ~1;
                        if (level <= MaxDepth && overflowIsolates == 0 && overflowEmbeddings == 0)
                        {
                            validIsolates++;
                            _stack[++depth] = new StackEntry { Level = (byte)level, Override = BidiClass.ON, Isolate = true };
                        }
                        else
                        {
                            overflowIsolates++;
                        }
                        break;
                    }

                    case BidiClass.PDI:
                    {
                        // X6a
                        if (overflowIsolates > 0)
                        {
                            overflowIsolates--;
                        }
                        else if (validIsolates > 0)
                        {
                            overflowEmbeddings = 0;
                            while (!_stack[depth].Isolate)
                                depth--;
                            depth--;
                            validIsolates--;
                        }

                        ref var current = ref _stack[depth];
                        _levels[index] = current.Level;
                        if (current.Override != BidiClass.ON)
                            _types[index] = current.Override;
                        break;
                    }

                    case BidiClass.PDF:
                        // X7
                        _levels[index] = top.Level;
                        if (overflowIsolates > 0)
                        {
                        }
                        else if (overflowEmbeddings > 0)
                        {
                            overflowEmbeddings--;
                        }
                        else if (!top.Isolate && depth >= 1)
                        {
                            depth--;
                        }
                        break;

                    case BidiClass.B:
                        // X8: a paragraph separator ends every embedding.
                        _levels[index] = (byte)ParagraphLevel;
                        depth = 0;
                        overflowIsolates = 0;
                        overflowEmbeddings = 0;
                        validIsolates = 0;
                        break;

                    case BidiClass.BN:
                        _levels[index] = top.Level;
                        break;

                    default:
                        // X6
                        _levels[index] = top.Level;
                        if (top.Override != BidiClass.ON)
                            _types[index] = top.Override;
                        break;
                }
            }
        }
    }
}
