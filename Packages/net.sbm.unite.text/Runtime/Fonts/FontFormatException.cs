using System;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Thrown inside the font parsers when the data is malformed or uses an unsupported
    /// feature. <see cref="FontLoader"/> turns it into a failed load with the message.
    /// </summary>
    internal sealed class FontFormatException : Exception
    {
        public FontFormatException(string message) : base(message)
        {
        }
    }
}
