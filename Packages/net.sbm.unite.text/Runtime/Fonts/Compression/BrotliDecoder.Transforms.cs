namespace Unite.Text.Fonts
{
    // Static dictionary word transforms (RFC 7932 appendix B).
    internal sealed partial class BrotliDecoder
    {
        private const int OmitLastMax = 9;
        private const int UppercaseFirst = 10;
        private const int UppercaseAll = 11;
        private const int OmitFirstMin = 12;
        private const int OmitFirstMax = 20;

        /// <summary>Writes prefix + transformed word + suffix at the end of the output; returns the byte count.</summary>
        private int ApplyTransform(byte[] dictionary, int wordOffset, int length, int transformIndex)
        {
            int prefixId = BrotliTables.Transforms[transformIndex * 3];
            int type = BrotliTables.Transforms[transformIndex * 3 + 1];
            int suffixId = BrotliTables.Transforms[transformIndex * 3 + 2];

            int index = _length;
            index = CopyAffix(prefixId, index);

            if (type <= OmitLastMax)
            {
                length -= type;
            }
            else if (type >= OmitFirstMin && type <= OmitFirstMax)
            {
                int skip = type - (OmitFirstMin - 1);
                wordOffset += skip;
                length -= skip;
            }

            int wordStart = index;
            for (int position = 0; position < length; position++)
                _output[index++] = dictionary[wordOffset + position];

            if (type == UppercaseFirst)
            {
                ToUpperCase(wordStart);
            }
            else if (type == UppercaseAll)
            {
                int cursor = wordStart;
                int remaining = length;
                while (remaining > 0)
                {
                    int step = ToUpperCase(cursor);
                    cursor += step;
                    remaining -= step;
                }
            }

            index = CopyAffix(suffixId, index);
            return index - _length;
        }

        private int CopyAffix(int affixId, int index)
        {
            int start = BrotliTables.PrefixSuffixMap[affixId];
            int count = BrotliTables.PrefixSuffix[start];
            for (int position = 1; position <= count; position++)
                _output[index++] = BrotliTables.PrefixSuffix[start + position];
            return index;
        }

        // Overly simplified UTF-8 uppercasing, exactly as the format defines it.
        private int ToUpperCase(int index)
        {
            if (_output[index] < 0xC0)
            {
                if (_output[index] >= 'a' && _output[index] <= 'z')
                    _output[index] ^= 32;
                return 1;
            }

            if (_output[index] < 0xE0)
            {
                _output[index + 1] ^= 32;
                return 2;
            }

            _output[index + 2] ^= 5;
            return 3;
        }
    }
}
