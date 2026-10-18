using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Entry point for font data: recognizes the file format, unwraps WOFF / WOFF2
    /// containers into plain sfnt bytes and builds a <see cref="FontFace"/> from them.
    /// TrueType, OpenType/CFF and collections (first font) are read directly.
    /// </summary>
    internal static class FontLoader
    {
        public static bool TryLoad(byte[] data, out FontFace face, out string error)
        {
            face = null;
            error = null;

            if (data == null || data.Length < 12)
            {
                error = "the font data is empty or too short.";
                return false;
            }

            try
            {
                face = Load(data);
                return true;
            }
            catch (FontFormatException exception)
            {
                error = exception.Message;
            }
            catch (Exception exception) when (
                exception is IndexOutOfRangeException or ArgumentException or InvalidOperationException
                    or OverflowException or NullReferenceException or System.IO.IOException
            )
            {
                // Corrupt data surfaces as arithmetic / indexing faults deep inside the parsers.
                error = $"the font data is malformed ({exception.GetType().Name}).";
            }

            return false;
        }

        public static FontFace Load(byte[] data) => FontFace.Create(ToSfnt(data));

        /// <summary>The sfnt bytes of a font file in any supported container.</summary>
        public static byte[] ToSfnt(byte[] data)
        {
            var reader = new FontReader(data);
            switch (reader.ReadU32())
            {
                case WoffContainer.Signature:
                    return WoffContainer.ToSfnt(data);
                case Woff2Container.Signature:
                    return Woff2Container.ToSfnt(data);
                default:
                    return data;
            }
        }
    }
}
