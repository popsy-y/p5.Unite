using System;
using System.Collections.Generic;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Big-endian cursor over the raw font bytes. All reads are bounds-checked against
    /// the full data array; the table directory has already validated table offsets.
    /// </summary>
    internal sealed class TtfReader
    {
        private readonly byte[] _data;
        public int Position { get; set; }

        public TtfReader(byte[] data, int position = 0)
        {
            _data = data;
            Position = position;
        }

        public byte[] Data => _data;
        public int Length => _data.Length;

        public byte ReadU8() => _data[Position++];

        public ushort ReadU16()
        {
            var value = (ushort)((ReadU8() << 8) | ReadU8());
            return value;
        }

        public short ReadI16() => (short)ReadU16();

        public uint ReadU32()
        {
            uint value = 0;
            for (int index = 0; index < 4; index++)
            {
                value = (value << 8) | ReadU8();
            }
            return value;
        }

        public int ReadI32() => unchecked((int)ReadU32());

        /// <summary>Reads an unsigned byte array of the given length and advances.</summary>
        public byte[] ReadBytes(int length)
        {
            CheckRange(length);
            var result = new byte[length];
            Buffer.BlockCopy(_data, Position, result, 0, length);
            Position += length;
            return result;
        }

        public void Skip(int bytes)
        {
            CheckRange(bytes);
            Position += bytes;
        }

        public void Seek(int position)
        {
            if (position < 0 || position > _data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            Position = position;
        }

        public bool Has(int bytes) => Position + bytes <= _data.Length;

        public void CheckRange(int bytes)
        {
            if (!Has(bytes))
            {
                throw new IndexOutOfRangeException(
                    $"p5.Unite: font stream is truncated (needed {bytes} bytes at {Position})."
                );
            }
        }
    }
}
