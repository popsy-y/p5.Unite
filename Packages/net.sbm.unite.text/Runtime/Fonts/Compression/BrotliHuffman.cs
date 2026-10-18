namespace Unite.Text.Fonts
{
    /// <summary>
    /// Canonical prefix code (Brotli): shorter codes first, equal lengths by symbol value,
    /// the first bit read is the most significant bit of the code. Codes up to
    /// <see cref="FastBits"/> bits decode through a table, longer ones bit by bit.
    /// A code with a single symbol takes zero bits.
    /// </summary>
    internal sealed class BrotliHuffman
    {
        public const int MaxLength = 15;
        private const int FastBits = 8;

        private readonly int _singleSymbol;
        private readonly int[] _fast;    // (length << 16) | symbol; 0 when the code is longer than FastBits
        private readonly int[] _counts;  // codes per length
        private readonly int[] _symbols; // symbols ordered by (length, value)

        private BrotliHuffman(int singleSymbol, int[] fast, int[] counts, int[] symbols)
        {
            _singleSymbol = singleSymbol;
            _fast = fast;
            _counts = counts;
            _symbols = symbols;
        }

        /// <summary>
        /// Builds the code from per-symbol code lengths (0 = unused). Throws when the
        /// lengths do not describe a complete prefix code.
        /// </summary>
        public static BrotliHuffman FromLengths(int[] lengths, int symbolCount)
        {
            var counts = new int[MaxLength + 1];
            int used = 0;
            int lastUsed = 0;
            for (int symbol = 0; symbol < symbolCount; symbol++)
            {
                if (lengths[symbol] == 0)
                    continue;

                counts[lengths[symbol]]++;
                used++;
                lastUsed = symbol;
            }

            if (used == 0)
                throw new FontFormatException("Brotli: empty prefix code.");

            if (used == 1)
                return new BrotliHuffman(lastUsed, null, null, null);

            int space = 1 << MaxLength;
            for (int length = 1; length <= MaxLength; length++)
                space -= counts[length] << (MaxLength - length);

            if (space != 0)
                throw new FontFormatException("Brotli: prefix code is not complete.");

            // Index of the first symbol of each length in the ordered symbol list, and
            // the canonical first code of each length.
            var offsets = new int[MaxLength + 1];
            var firstCodes = new int[MaxLength + 1];
            int code = 0;
            int total = 0;
            for (int length = 1; length <= MaxLength; length++)
            {
                firstCodes[length] = code;
                offsets[length] = total;
                code = (code + counts[length]) << 1;
                total += counts[length];
            }

            var symbols = new int[used];
            var fast = new int[1 << FastBits];
            var placed = new int[MaxLength + 1];
            for (int symbol = 0; symbol < symbolCount; symbol++)
            {
                int length = lengths[symbol];
                if (length == 0)
                    continue;

                symbols[offsets[length] + placed[length]] = symbol;
                int symbolCode = firstCodes[length] + placed[length];
                placed[length]++;

                if (length > FastBits)
                    continue;

                int entry = (length << 16) | symbol;
                for (int slot = Reverse(symbolCode, length); slot < fast.Length; slot += 1 << length)
                    fast[slot] = entry;
            }

            return new BrotliHuffman(-1, fast, counts, symbols);
        }

        public int ReadSymbol(BrotliBitReader reader)
        {
            if (_fast == null)
                return _singleSymbol;

            int peeked = reader.Peek(MaxLength);
            int entry = _fast[peeked & ((1 << FastBits) - 1)];
            if (entry != 0)
            {
                reader.Consume(entry >> 16);
                return entry & 0xFFFF;
            }

            // Canonical bit-by-bit decode for the long codes.
            int code = 0;
            int firstCode = 0;
            int index = 0;
            for (int length = 1; length <= MaxLength; length++)
            {
                code |= (peeked >> (length - 1)) & 1;
                int count = _counts[length];
                if (code - count < firstCode)
                {
                    reader.Consume(length);
                    return _symbols[index + (code - firstCode)];
                }

                index += count;
                firstCode = (firstCode + count) << 1;
                code <<= 1;
            }

            throw new FontFormatException("Brotli: invalid prefix code in the stream.");
        }

        private static int Reverse(int value, int length)
        {
            int result = 0;
            for (int bit = 0; bit < length; bit++)
            {
                result = (result << 1) | (value & 1);
                value >>= 1;
            }
            return result;
        }
    }
}
