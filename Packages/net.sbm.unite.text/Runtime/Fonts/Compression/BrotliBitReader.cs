namespace Unite.Text.Fonts
{
    /// <summary>
    /// Least-significant-bit-first bit reader over a byte range (Brotli bit order).
    /// Peeking past the end yields zero bits; consuming them is an error.
    /// </summary>
    internal sealed class BrotliBitReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _position;
        private ulong _bits;
        private int _count;

        public BrotliBitReader(byte[] data, int offset, int length)
        {
            _data = data;
            _position = offset;
            _end = offset + length;
        }

        /// <summary>Reads up to 32 bits.</summary>
        public int ReadBits(int count)
        {
            if (count == 0)
                return 0;

            int value = Peek(count);
            Consume(count);
            return value;
        }

        public int Peek(int count)
        {
            Fill();
            return (int)(_bits & ((1UL << count) - 1UL));
        }

        public void Consume(int count)
        {
            if (count > _count)
                throw new FontFormatException("Brotli: unexpected end of input.");

            _bits >>= count;
            _count -= count;
        }

        /// <summary>Skips to the next byte boundary; the padding bits must be zero.</summary>
        public void AlignToByte()
        {
            int padding = _count & 7;
            if (padding == 0)
                return;

            if ((_bits & ((1UL << padding) - 1UL)) != 0)
                throw new FontFormatException("Brotli: non-zero padding bits.");

            _bits >>= padding;
            _count -= padding;
        }

        /// <summary>Reads whole bytes; the reader must be byte aligned.</summary>
        public void ReadBytes(byte[] destination, int destinationOffset, int length)
        {
            for (int index = 0; index < length; index++)
                destination[destinationOffset + index] = (byte)ReadBits(8);
        }

        public void SkipBytes(int length)
        {
            for (int index = 0; index < length; index++)
                ReadBits(8);
        }

        private void Fill()
        {
            while (_count <= 56 && _position < _end)
            {
                _bits |= (ulong)_data[_position++] << _count;
                _count += 8;
            }
        }
    }
}
