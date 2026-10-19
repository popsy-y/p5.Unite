using System;

namespace Unite.Text.Unicode
{
    /// <summary>Break opportunity before a code point (UAX #14).</summary>
    internal enum LineBreakOpportunity : byte
    {
        None,
        Allowed,
        Mandatory
    }

    /// <summary>
    /// Line break opportunities (UAX #14, Unicode 16 default rules LB1-LB31). For text of
    /// length n it fills n + 1 entries: entry i is the opportunity between code points
    /// i - 1 and i (entry 0 is never a break, entry n always a mandatory one).
    /// Combining marks belong to the character before them (LB9), so they never start a
    /// line. An instance keeps its buffers.
    /// </summary>
    internal sealed partial class LineBreaker
    {
        private const int DottedCircle = 0x25CC;

        private int[] _codePoints = Array.Empty<int>();
        private LineBreakClass[] _classes = Array.Empty<LineBreakClass>();
        private LineBreakClass[] _raw = Array.Empty<LineBreakClass>();
        private bool[] _attached = Array.Empty<bool>();
        private int _length;

        public void Analyze(ReadOnlySpan<int> codePoints, Span<LineBreakOpportunity> breaks)
        {
            Prepare(codePoints);

            breaks[0] = LineBreakOpportunity.None;
            for (int index = 1; index < _length; index++)
                breaks[index] = BreakBefore(index);
            if (_length > 0)
                breaks[_length] = LineBreakOpportunity.Mandatory;
        }

        private void Prepare(ReadOnlySpan<int> codePoints)
        {
            _length = codePoints.Length;
            if (_codePoints.Length < _length)
            {
                int size = Math.Max(_length, _codePoints.Length * 2);
                _codePoints = new int[size];
                _classes = new LineBreakClass[size];
                _raw = new LineBreakClass[size];
                _attached = new bool[size];
            }

            for (int index = 0; index < _length; index++)
            {
                int codePoint = codePoints[index];
                _codePoints[index] = codePoint;
                var type = UnicodeData.GetLineBreakClass(codePoint);

                // LB1: resolve ambiguous, surrogate, unknown and complex-context classes.
                switch (type)
                {
                    case LineBreakClass.AI:
                    case LineBreakClass.SG:
                    case LineBreakClass.XX:
                        type = LineBreakClass.AL;
                        break;
                    case LineBreakClass.SA:
                        var category = UnicodeData.GetGeneralCategory(codePoint);
                        type = category is GeneralCategory.Mn or GeneralCategory.Mc ? LineBreakClass.CM : LineBreakClass.AL;
                        break;
                    case LineBreakClass.CJ:
                        type = LineBreakClass.NS;
                        break;
                }
                _raw[index] = type;
            }

            // LB9 / LB10: a combining mark or ZWJ takes the class of the character it follows
            // (unless that is a break or space class); stray ones become AL.
            for (int index = 0; index < _length; index++)
            {
                var type = _raw[index];
                _attached[index] = false;
                if (type is LineBreakClass.CM or LineBreakClass.ZWJ)
                {
                    int previous = index - 1;
                    if (previous >= 0 && !(_raw[previous] is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
                            or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW))
                    {
                        // _classes[previous] already carries the base's class.
                        _classes[index] = _classes[previous];
                        _attached[index] = true;
                        continue;
                    }
                    type = LineBreakClass.AL;
                }
                _classes[index] = type;
            }
        }

        // Start of the unit (base character plus attached marks) that ends at index.
        private int UnitStart(int index)
        {
            while (index > 0 && _attached[index])
                index--;
            return index;
        }

        // Start of the unit before the unit that starts at unitStart; -1 at the text start.
        private int PreviousUnit(int unitStart) => unitStart <= 0 ? -1 : UnitStart(unitStart - 1);

        // Class of the unit starting at index (base class), or XX for "sot" / "eot".
        private LineBreakClass ClassAt(int index)
            => index < 0 || index >= _length ? LineBreakClass.XX : _classes[index];

        private bool IsEastAsian(int index) => index >= 0 && index < _length && UnicodeData.IsEastAsianWide(_codePoints[index]);

        private GeneralCategory CategoryAt(int index) => UnicodeData.GetGeneralCategory(_codePoints[index]);
    }
}
