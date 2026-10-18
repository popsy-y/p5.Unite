namespace Unite.Text.Fonts
{
    /// <summary>Variable-length integer encodings used by the WOFF2 directory and glyf streams.</summary>
    internal static class Woff2Streams
    {
        /// <summary>UIntBase128: big-endian, 7 bits per byte, high bit = more bytes follow.</summary>
        public static uint ReadBase128(FontReader reader)
        {
            uint result = 0;
            for (int index = 0; index < 5; index++)
            {
                byte code = reader.ReadU8();
                if (index == 0 && code == 0x80)
                    throw new FontFormatException("WOFF2: base-128 value has leading zeros.");

                if ((result & 0xFE000000u) != 0)
                    throw new FontFormatException("WOFF2: base-128 value overflows.");

                result = (result << 7) | (uint)(code & 0x7F);
                if ((code & 0x80) == 0)
                    return result;
            }

            throw new FontFormatException("WOFF2: base-128 value is too long.");
        }

        /// <summary>255UInt16: one to three bytes holding a 16-bit value.</summary>
        public static int Read255UShort(FontReader reader)
        {
            const int wordCode = 253;
            const int oneMoreByteCode2 = 254;
            const int oneMoreByteCode1 = 255;
            const int lowestCode = 253;

            int code = reader.ReadU8();
            switch (code)
            {
                case wordCode:
                    return reader.ReadU16();
                case oneMoreByteCode1:
                    return reader.ReadU8() + lowestCode;
                case oneMoreByteCode2:
                    return reader.ReadU8() + lowestCode * 2;
                default:
                    return code;
            }
        }
    }
}
