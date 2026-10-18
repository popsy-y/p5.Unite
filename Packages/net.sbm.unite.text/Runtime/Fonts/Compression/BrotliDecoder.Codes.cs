namespace Unite.Text.Fonts
{
    // Prefix codes, context maps and block lengths: the metablock header machinery.
    internal sealed partial class BrotliDecoder
    {
        private const int CodeLengthCodes = 18;
        private const int BlockLengthSymbols = 26;

        private static readonly int[] CodeLengthOrder =
            { 1, 2, 3, 4, 0, 5, 17, 6, 16, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        // Static prefix code of the code length code lengths, indexed by 4 peeked bits.
        private static readonly int[] CodeLengthPrefixLength =
            { 2, 2, 2, 3, 2, 2, 2, 4, 2, 2, 2, 3, 2, 2, 2, 4 };

        private static readonly int[] CodeLengthPrefixValue =
            { 0, 4, 3, 2, 0, 4, 3, 1, 0, 4, 3, 2, 0, 4, 3, 5 };

        private static readonly int[] BlockLengthBase =
        {
            1, 5, 9, 13, 17, 25, 33, 41, 49, 65, 81, 97, 113, 145, 177, 209, 241, 305, 369, 497,
            753, 1265, 2289, 4337, 8433, 16625
        };

        private static readonly int[] BlockLengthExtraBits =
        {
            2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 7, 8, 9, 10, 11, 12, 13, 24
        };

        private BrotliHuffman ReadPrefixCode(int alphabetSizeMax, int alphabetSizeLimit)
        {
            int skip = _bits.ReadBits(2);
            return skip == 1
                ? ReadSimplePrefixCode(alphabetSizeMax, alphabetSizeLimit)
                : ReadComplexPrefixCode(skip, alphabetSizeLimit);
        }

        // 1..4 symbols stored directly; the code lengths follow from their count.
        private BrotliHuffman ReadSimplePrefixCode(int alphabetSizeMax, int alphabetSizeLimit)
        {
            int symbolCount = _bits.ReadBits(2) + 1;
            int symbolBits = BitLength(alphabetSizeMax - 1);
            var symbols = new int[symbolCount];
            for (int index = 0; index < symbolCount; index++)
            {
                symbols[index] = _bits.ReadBits(symbolBits);
                if (symbols[index] >= alphabetSizeLimit)
                    throw new FontFormatException("Brotli: simple prefix code symbol out of range.");
            }

            for (int index = 0; index < symbolCount; index++)
            {
                for (int other = index + 1; other < symbolCount; other++)
                {
                    if (symbols[index] == symbols[other])
                        throw new FontFormatException("Brotli: duplicate symbol in a simple prefix code.");
                }
            }

            int[] lengthsInOrder;
            switch (symbolCount)
            {
                case 1:
                    lengthsInOrder = new[] { 1 };
                    break;
                case 2:
                    lengthsInOrder = new[] { 1, 1 };
                    break;
                case 3:
                    lengthsInOrder = new[] { 1, 2, 2 };
                    break;
                default:
                    lengthsInOrder = _bits.ReadBits(1) == 1 ? new[] { 1, 2, 3, 3 } : new[] { 2, 2, 2, 2 };
                    break;
            }

            var lengths = new int[alphabetSizeLimit];
            for (int index = 0; index < symbolCount; index++)
                lengths[symbols[index]] = lengthsInOrder[index];

            return BrotliHuffman.FromLengths(lengths, alphabetSizeLimit);
        }

        private BrotliHuffman ReadComplexPrefixCode(int skip, int alphabetSize)
        {
            var codeLengthCodeLengths = new int[CodeLengthCodes];
            int space = 32;
            int codeCount = 0;
            for (int index = skip; index < CodeLengthCodes; index++)
            {
                int peeked = _bits.Peek(4);
                _bits.Consume(CodeLengthPrefixLength[peeked]);
                int value = CodeLengthPrefixValue[peeked];
                codeLengthCodeLengths[CodeLengthOrder[index]] = value;
                if (value == 0)
                    continue;

                space -= 32 >> value;
                codeCount++;
                if (space <= 0)
                    break;
            }

            if (codeCount != 1 && space != 0)
                throw new FontFormatException("Brotli: invalid code length code.");

            var codeLengthCode = BrotliHuffman.FromLengths(codeLengthCodeLengths, CodeLengthCodes);
            var lengths = new int[alphabetSize];
            int symbol = 0;
            int previousLength = 8;
            int repeat = 0;
            int repeatLength = 0;
            space = 32768;

            while (symbol < alphabetSize && space > 0)
            {
                int codeLength = codeLengthCode.ReadSymbol(_bits);
                if (codeLength < 16)
                {
                    repeat = 0;
                    if (codeLength != 0)
                    {
                        lengths[symbol] = codeLength;
                        previousLength = codeLength;
                        space -= 32768 >> codeLength;
                    }
                    symbol++;
                    continue;
                }

                int extraBits = codeLength == 16 ? 2 : 3;
                int newLength = codeLength == 16 ? previousLength : 0;
                int delta = _bits.ReadBits(extraBits);

                if (repeatLength != newLength)
                {
                    repeat = 0;
                    repeatLength = newLength;
                }

                int oldRepeat = repeat;
                if (repeat > 0)
                {
                    repeat -= 2;
                    repeat <<= extraBits;
                }
                repeat += delta + 3;
                int count = repeat - oldRepeat;

                if (symbol + count > alphabetSize)
                    throw new FontFormatException("Brotli: code length repeat overflows the alphabet.");

                if (repeatLength != 0)
                {
                    for (int index = 0; index < count; index++)
                        lengths[symbol++] = repeatLength;
                    space -= count << (15 - repeatLength);
                }
                else
                {
                    symbol += count;
                }
            }

            if (space != 0)
                throw new FontFormatException("Brotli: prefix code lengths do not fill the code space.");

            return BrotliHuffman.FromLengths(lengths, alphabetSize);
        }

        private byte[] ReadContextMap(int size, out int treeCount)
        {
            treeCount = ReadVarLenUint8() + 1;
            var map = new byte[size];
            if (treeCount <= 1)
                return map;

            int maxRunPrefix = 0;
            if (_bits.ReadBits(1) == 1)
                maxRunPrefix = _bits.ReadBits(4) + 1;

            var code = ReadPrefixCode(treeCount + maxRunPrefix, treeCount + maxRunPrefix);
            int index = 0;
            while (index < size)
            {
                int symbol = code.ReadSymbol(_bits);
                if (symbol == 0)
                {
                    map[index++] = 0;
                }
                else if (symbol <= maxRunPrefix)
                {
                    int repeats = (1 << symbol) + _bits.ReadBits(symbol);
                    if (index + repeats > size)
                        throw new FontFormatException("Brotli: context map run overflows.");

                    // Zero entries; the array is already cleared.
                    index += repeats;
                }
                else
                {
                    map[index++] = (byte)(symbol - maxRunPrefix);
                }
            }

            if (_bits.ReadBits(1) == 1)
                InverseMoveToFront(map);

            return map;
        }

        private static void InverseMoveToFront(byte[] values)
        {
            var list = new byte[256];
            for (int index = 0; index < 256; index++)
                list[index] = (byte)index;

            for (int index = 0; index < values.Length; index++)
            {
                int position = values[index];
                byte value = list[position];
                values[index] = value;
                for (; position > 0; position--)
                    list[position] = list[position - 1];
                list[0] = value;
            }
        }

        private int ReadBlockLength(BrotliHuffman code)
        {
            int symbol = code.ReadSymbol(_bits);
            return BlockLengthBase[symbol] + _bits.ReadBits(BlockLengthExtraBits[symbol]);
        }

        private static int BitLength(int value)
        {
            int length = 0;
            while (value != 0)
            {
                length++;
                value >>= 1;
            }
            return length;
        }
    }
}
