using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Big-endian cursor over raw font bytes. All reads are bounds-checked against the
    /// whole data array.
    /// </summary>
    internal sealed class FontReader
    {
        private readonly byte[] _data;
        public int Position { get; set; }

        public FontReader(byte[] data, int position = 0)
        {
            _data = data;
            Position = position;
        }

        public byte[] Data => _data;
        public int Length => _data.Length;

        public byte ReadU8()
        {
            CheckRange(1);
            return _data[Position++];
        }

        public sbyte ReadI8() => (sbyte)ReadU8();

        public ushort ReadU16()
        {
            CheckRange(2);
            var value = (ushort)((_data[Position] << 8) | _data[Position + 1]);
            Position += 2;
            return value;
        }

        public short ReadI16() => (short)ReadU16();

        public uint ReadU24()
        {
            CheckRange(3);
            uint value = ((uint)_data[Position] << 16) | ((uint)_data[Position + 1] << 8) | _data[Position + 2];
            Position += 3;
            return value;
        }

        public uint ReadU32()
        {
            CheckRange(4);
            uint value = ((uint)_data[Position] << 24)
                | ((uint)_data[Position + 1] << 16)
                | ((uint)_data[Position + 2] << 8)
                | _data[Position + 3];
            Position += 4;
            return value;
        }

        public int ReadI32() => unchecked((int)ReadU32());

        /// <summary>F2Dot14 fixed point: signed 2.14.</summary>
        public float ReadF2Dot14() => ReadI16() / 16384f;

        /// <summary>16.16 fixed point.</summary>
        public float ReadFixed() => ReadI32() / 65536f;

        /// <summary>Four ASCII characters, e.g. a table or axis tag.</summary>
        public string ReadTag()
        {
            CheckRange(4);
            var characters = new char[4];
            for (int index = 0; index < 4; index++)
                characters[index] = (char)_data[Position++];
            return new string(characters);
        }

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
                throw new FontFormatException("font offset is outside the data.");

            Position = position;
        }

        public bool Has(int bytes) => bytes >= 0 && Position + (long)bytes <= _data.Length;

        public void CheckRange(int bytes)
        {
            if (!Has(bytes))
                throw new FontFormatException($"font data is truncated (needed {bytes} bytes at {Position}).");
        }
    }
}
