using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Managed Brotli decoder (RFC 7932) for WOFF2 table data: whole input in, whole
    /// output out, no streaming, no custom dictionaries. Written against the reference
    /// decoder (google/brotli, MIT license).
    /// </summary>
    internal sealed partial class BrotliDecoder
    {
        private const int WindowGap = 16;

        private readonly BrotliBitReader _bits;
        private byte[] _output;
        private int _length;
        private int _maxBackward;

        private BrotliDecoder(byte[] input, int offset, int length, int expectedSize)
        {
            _bits = new BrotliBitReader(input, offset, length);
            _output = new byte[Math.Max(expectedSize, 1024)];
        }

        /// <summary>Decompresses a complete Brotli stream. Throws <see cref="FontFormatException"/> on bad data.</summary>
        public static byte[] Decompress(byte[] input, int offset, int length, int expectedSize = 0)
            => new BrotliDecoder(input, offset, length, expectedSize).Run();

        private byte[] Run()
        {
            int windowBits = ReadWindowBits();
            _maxBackward = (1 << windowBits) - WindowGap;

            while (true)
            {
                bool isLast = _bits.ReadBits(1) == 1;
                if (isLast && _bits.ReadBits(1) == 1)
                    break; // last metablock is empty

                int nibbleCode = _bits.ReadBits(2);
                if (nibbleCode == 3)
                    SkipMetadata();
                else
                    ReadMetaBlock(isLast, nibbleCode + 4);

                if (isLast)
                    break;
            }

            var result = new byte[_length];
            Array.Copy(_output, result, _length);
            return result;
        }

        private int ReadWindowBits()
        {
            if (_bits.ReadBits(1) == 0)
                return 16;

            int value = _bits.ReadBits(3);
            if (value != 0)
                return 17 + value;

            value = _bits.ReadBits(3);
            if (value == 0)
                return 17;
            if (value == 1)
                throw new FontFormatException("Brotli: large windows are not supported.");
            return 8 + value;
        }

        private void SkipMetadata()
        {
            if (_bits.ReadBits(1) != 0)
                throw new FontFormatException("Brotli: reserved bit set.");

            int sizeBytes = _bits.ReadBits(2);
            int skip = 0;
            for (int index = 0; index < sizeBytes; index++)
            {
                int value = _bits.ReadBits(8);
                if (index + 1 == sizeBytes && sizeBytes > 1 && value == 0)
                    throw new FontFormatException("Brotli: malformed metadata length.");
                skip |= value << (8 * index);
            }

            if (sizeBytes > 0)
                skip++;

            _bits.AlignToByte();
            _bits.SkipBytes(skip);
        }

        private void ReadMetaBlock(bool isLast, int nibbles)
        {
            int length = 0;
            for (int index = 0; index < nibbles; index++)
            {
                int value = _bits.ReadBits(4);
                if (index + 1 == nibbles && nibbles > 4 && value == 0)
                    throw new FontFormatException("Brotli: malformed metablock length.");
                length |= value << (4 * index);
            }
            length++;

            bool uncompressed = !isLast && _bits.ReadBits(1) == 1;
            if (uncompressed)
            {
                _bits.AlignToByte();
                EnsureCapacity(_length + length);
                _bits.ReadBytes(_output, _length, length);
                _length += length;
                return;
            }

            ReadCompressedMetaBlock(length);
        }

        private void EnsureCapacity(int size)
        {
            if (size <= _output.Length)
                return;

            Array.Resize(ref _output, Math.Max(size, _output.Length * 2));
        }

        private int ReadVarLenUint8()
        {
            if (_bits.ReadBits(1) == 0)
                return 0;

            int bits = _bits.ReadBits(3);
            if (bits == 0)
                return 1;

            return (1 << bits) + _bits.ReadBits(bits);
        }
    }
}
