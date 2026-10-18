namespace Unite.Text.Fonts
{
    // Compressed metablock body: block types, commands, literals, distances and
    // dictionary references.
    internal sealed partial class BrotliDecoder
    {
        private const int LiteralTree = 0;
        private const int CommandTree = 1;
        private const int DistanceTree = 2;
        private const int CommandSymbols = 704;
        private const int MinDictionaryWord = 4;
        private const int MaxDictionaryWord = 24;

        private static readonly int[] InsertLengthBase =
        {
            0, 1, 2, 3, 4, 5, 6, 8, 10, 14, 18, 26, 34, 50, 66, 98, 130, 194, 322, 578, 1090, 2114, 6210, 22594
        };

        private static readonly int[] InsertLengthExtraBits =
        {
            0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10, 12, 14, 24
        };

        private static readonly int[] CopyLengthBase =
        {
            2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 18, 22, 30, 38, 54, 70, 102, 134, 198, 326, 582, 1094, 2118
        };

        private static readonly int[] CopyLengthExtraBits =
        {
            0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 9, 10, 24
        };

        // Insert / copy length code groups of the 11 command cells (64 symbols each).
        private static readonly int[] CellInsertBase = { 0, 0, 0, 0, 8, 8, 0, 16, 8, 16, 16 };
        private static readonly int[] CellCopyBase = { 0, 8, 0, 8, 0, 8, 16, 0, 16, 8, 16 };

        // Most recent distances, newest first (RFC 7932 section 4).
        private int _distance0;
        private int _distance1;
        private int _distance2;
        private int _distance3;

        private readonly int[] _blockTypeCount = new int[3];
        private readonly int[] _blockLength = new int[3];
        private readonly int[] _blockTypeRing = new int[6]; // [second last, last] per tree
        private readonly BrotliHuffman[] _blockTypeCodes = new BrotliHuffman[3];
        private readonly BrotliHuffman[] _blockLengthCodes = new BrotliHuffman[3];

        private void ReadCompressedMetaBlock(int metaBlockLength)
        {
            for (int tree = 0; tree < 3; tree++)
            {
                _blockTypeCount[tree] = ReadVarLenUint8() + 1;
                _blockTypeRing[tree * 2] = 1;
                _blockTypeRing[tree * 2 + 1] = 0;
                _blockLength[tree] = 1 << 24;

                if (_blockTypeCount[tree] < 2)
                    continue;

                int typeAlphabet = _blockTypeCount[tree] + 2;
                _blockTypeCodes[tree] = ReadPrefixCode(typeAlphabet, typeAlphabet);
                _blockLengthCodes[tree] = ReadPrefixCode(BlockLengthSymbols, BlockLengthSymbols);
                _blockLength[tree] = ReadBlockLength(_blockLengthCodes[tree]);
            }

            int distanceParams = _bits.ReadBits(6);
            int postfixBits = distanceParams & 3;
            int directCodes = (distanceParams >> 2) << postfixBits;

            var contextModes = new byte[_blockTypeCount[LiteralTree]];
            for (int index = 0; index < contextModes.Length; index++)
                contextModes[index] = (byte)_bits.ReadBits(2);

            var literalMap = ReadContextMap(_blockTypeCount[LiteralTree] << 6, out int literalTreeCount);
            var distanceMap = ReadContextMap(_blockTypeCount[DistanceTree] << 2, out int distanceTreeCount);

            var literalTrees = ReadTreeGroup(literalTreeCount, 256);
            var commandTrees = ReadTreeGroup(_blockTypeCount[CommandTree], CommandSymbols);
            int distanceAlphabet = 16 + directCodes + (48 << postfixBits);
            var distanceTrees = ReadTreeGroup(distanceTreeCount, distanceAlphabet);

            _distance0 = 4;
            _distance1 = 11;
            _distance2 = 15;
            _distance3 = 16;

            int remaining = metaBlockLength;
            while (true)
            {
                if (_blockLength[CommandTree] == 0)
                    SwitchBlock(CommandTree);

                var commandCode = commandTrees[_blockTypeRing[CommandTree * 2 + 1]];
                int symbol = commandCode.ReadSymbol(_bits);
                _blockLength[CommandTree]--;

                DecodeCommand(symbol, out int insertLength, out int copyLength, out bool implicitDistance);

                remaining -= insertLength;
                ReadLiterals(insertLength, contextModes, literalMap, literalTrees);
                if (remaining <= 0)
                    break;

                // Only explicit codes other than 0 enter the ring of recent distances.
                int distance = _distance0;
                bool pushToRing = false;
                if (!implicitDistance)
                {
                    distance = ReadDistance(copyLength, postfixBits, directCodes, distanceMap, distanceTrees, out pushToRing);
                }

                remaining -= CopyOrDictionary(distance, copyLength, pushToRing);
                if (remaining <= 0)
                    break;
            }

            if (remaining < 0)
                throw new FontFormatException("Brotli: metablock overruns its declared length.");
        }

        private BrotliHuffman[] ReadTreeGroup(int count, int alphabetSize)
        {
            var trees = new BrotliHuffman[count];
            for (int index = 0; index < count; index++)
                trees[index] = ReadPrefixCode(alphabetSize, alphabetSize);
            return trees;
        }

        private void SwitchBlock(int tree)
        {
            if (_blockTypeCount[tree] <= 1)
                throw new FontFormatException("Brotli: block switch without block types.");

            int symbol = _blockTypeCodes[tree].ReadSymbol(_bits);
            _blockLength[tree] = ReadBlockLength(_blockLengthCodes[tree]);

            int type;
            if (symbol == 0)
                type = _blockTypeRing[tree * 2];
            else if (symbol == 1)
                type = _blockTypeRing[tree * 2 + 1] + 1;
            else
                type = symbol - 2;

            if (type >= _blockTypeCount[tree])
                type -= _blockTypeCount[tree];

            _blockTypeRing[tree * 2] = _blockTypeRing[tree * 2 + 1];
            _blockTypeRing[tree * 2 + 1] = type;
        }

        private void DecodeCommand(int symbol, out int insertLength, out int copyLength, out bool implicitDistance)
        {
            int cell = symbol >> 6;
            int insertCode = CellInsertBase[cell] + ((symbol >> 3) & 7);
            int copyCode = CellCopyBase[cell] + (symbol & 7);
            implicitDistance = cell < 2;

            insertLength = InsertLengthBase[insertCode] + _bits.ReadBits(InsertLengthExtraBits[insertCode]);
            copyLength = CopyLengthBase[copyCode] + _bits.ReadBits(CopyLengthExtraBits[copyCode]);
        }

        private void ReadLiterals(int count, byte[] contextModes, byte[] literalMap, BrotliHuffman[] trees)
        {
            EnsureCapacity(_length + count);
            for (int index = 0; index < count; index++)
            {
                if (_blockLength[LiteralTree] == 0)
                    SwitchBlock(LiteralTree);

                int blockType = _blockTypeRing[LiteralTree * 2 + 1];
                int mode = contextModes[blockType] << 9;
                int previous1 = _length > 0 ? _output[_length - 1] : 0;
                int previous2 = _length > 1 ? _output[_length - 2] : 0;
                int context = BrotliTables.ContextLookup[mode + previous1]
                    | BrotliTables.ContextLookup[mode + 256 + previous2];

                var tree = trees[literalMap[(blockType << 6) + context]];
                _output[_length++] = (byte)tree.ReadSymbol(_bits);
                _blockLength[LiteralTree]--;
            }
        }

        private int ReadDistance(
            int copyLength,
            int postfixBits,
            int directCodes,
            byte[] distanceMap,
            BrotliHuffman[] trees,
            out bool pushToRing
        )
        {
            if (_blockLength[DistanceTree] == 0)
                SwitchBlock(DistanceTree);

            int context = copyLength > 4 ? 3 : copyLength - 2;
            int blockType = _blockTypeRing[DistanceTree * 2 + 1];
            var tree = trees[distanceMap[(blockType << 2) + context]];
            int code = tree.ReadSymbol(_bits);
            _blockLength[DistanceTree]--;
            pushToRing = code != 0;

            if (code < 16)
                return ShortDistance(code);

            if (code < 16 + directCodes)
                return code - 15;

            int postfixMask = (1 << postfixBits) - 1;
            int distanceCode = code - directCodes - 16;
            int extraBits = 1 + (distanceCode >> (postfixBits + 1));
            int high = distanceCode >> postfixBits;
            int low = distanceCode & postfixMask;
            int offset = ((2 + (high & 1)) << extraBits) - 4;
            long distance = ((((long)offset + _bits.ReadBits(extraBits)) << postfixBits) + low) + directCodes + 1;
            if (distance > int.MaxValue)
                throw new FontFormatException("Brotli: distance out of range.");

            return (int)distance;
        }

        // Distance codes 0..15 refer to the ring of recent distances (RFC 7932 section 4).
        private int ShortDistance(int code)
        {
            switch (code)
            {
                case 0: return _distance0;
                case 1: return _distance1;
                case 2: return _distance2;
                case 3: return _distance3;
                case 4: return _distance0 - 1;
                case 5: return _distance0 + 1;
                case 6: return _distance0 - 2;
                case 7: return _distance0 + 2;
                case 8: return _distance0 - 3;
                case 9: return _distance0 + 3;
                case 10: return _distance1 - 1;
                case 11: return _distance1 + 1;
                case 12: return _distance1 - 2;
                case 13: return _distance1 + 2;
                case 14: return _distance1 - 3;
                default: return _distance1 + 3;
            }
        }

        // Returns the number of bytes appended.
        private int CopyOrDictionary(int distance, int copyLength, bool pushToRing)
        {
            if (distance <= 0)
                throw new FontFormatException("Brotli: invalid distance.");

            int maxDistance = _length < _maxBackward ? _length : _maxBackward;
            if (distance > maxDistance)
                return AppendDictionaryWord(distance - maxDistance - 1, copyLength);

            if (pushToRing)
                PushDistance(distance);

            EnsureCapacity(_length + copyLength);
            int source = _length - distance;
            for (int index = 0; index < copyLength; index++)
                _output[_length + index] = _output[source + index];

            _length += copyLength;
            return copyLength;
        }

        private void PushDistance(int distance)
        {
            _distance3 = _distance2;
            _distance2 = _distance1;
            _distance1 = _distance0;
            _distance0 = distance;
        }

        private int AppendDictionaryWord(int address, int length)
        {
            if (length < MinDictionaryWord || length > MaxDictionaryWord)
                throw new FontFormatException("Brotli: invalid dictionary reference length.");

            int sizeBits = BrotliTables.DictionarySizeBits[length];
            if (sizeBits == 0)
                throw new FontFormatException("Brotli: invalid dictionary reference.");

            int wordIndex = address & ((1 << sizeBits) - 1);
            int transformIndex = address >> sizeBits;
            if (transformIndex >= BrotliTables.Transforms.Length / 3)
                throw new FontFormatException("Brotli: invalid dictionary transform.");

            int offset = BrotliTables.DictionaryOffsets[length] + wordIndex * length;
            EnsureCapacity(_length + length + 64);
            int written = ApplyTransform(BrotliDictionary.Data, offset, length, transformIndex);
            _length += written;
            return written;
        }
    }
}
