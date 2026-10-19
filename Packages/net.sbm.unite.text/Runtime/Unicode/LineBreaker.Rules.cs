namespace Unite.Text.Unicode
{
    // The pair rules of UAX #14, evaluated in order for the boundary before one code point.
    internal sealed partial class LineBreaker
    {
        private LineBreakOpportunity BreakBefore(int index)
        {
            var previousRaw = _raw[index - 1];
            var currentRaw = _raw[index];

            // LB4, LB5: hard line breaks.
            if (previousRaw == LineBreakClass.BK)
                return LineBreakOpportunity.Mandatory;
            if (previousRaw == LineBreakClass.CR && currentRaw == LineBreakClass.LF)
                return LineBreakOpportunity.None;
            if (previousRaw is LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
                return LineBreakOpportunity.Mandatory;

            // LB6, LB7
            if (currentRaw is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
                return LineBreakOpportunity.None;
            if (currentRaw is LineBreakClass.SP or LineBreakClass.ZW)
                return LineBreakOpportunity.None;

            // LB8: ZW SP* ÷
            int scan = index - 1;
            while (scan > 0 && _raw[scan] == LineBreakClass.SP)
                scan--;
            if (_raw[scan] == LineBreakClass.ZW)
                return LineBreakOpportunity.Allowed;

            // LB8a, LB9
            if (previousRaw == LineBreakClass.ZWJ || _attached[index])
                return LineBreakOpportunity.None;

            int start = UnitStart(index - 1);
            var before = _classes[start];
            var current = _classes[index];
            return PairRules(index, start, before, current) ? LineBreakOpportunity.None : LineBreakOpportunity.Allowed;
        }

        // LB11-LB30b; true means "do not break" (LB31 breaks everywhere else).
        private bool PairRules(int index, int start, LineBreakClass before, LineBreakClass current)
        {
            // LB11, LB12, LB12a
            if (before == LineBreakClass.WJ || current == LineBreakClass.WJ)
                return true;
            if (before == LineBreakClass.GL)
                return true;
            if (current == LineBreakClass.GL && !(before is LineBreakClass.SP or LineBreakClass.BA or LineBreakClass.HY))
                return true;

            // LB13
            if (current is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.SY)
                return true;

            // LB14: OP SP* ×
            if (ClassAt(SkipSpacesBack(start)) == LineBreakClass.OP)
                return true;

            // LB15a: (sot | BK | CR | LF | NL | OP | QU | GL | SP | ZW) [Pi & QU] SP* ×
            int quote = SkipSpacesBack(start);
            if (quote >= 0 && ClassAt(quote) == LineBreakClass.QU && CategoryAt(quote) == GeneralCategory.Pi)
            {
                int ahead = PreviousUnit(quote);
                if (ahead < 0 || ClassAt(ahead) is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                        or LineBreakClass.OP or LineBreakClass.QU or LineBreakClass.GL or LineBreakClass.SP or LineBreakClass.ZW)
                    return true;
            }

            // LB15b: × [Pf & QU] (SP | GL | WJ | CL | QU | CP | EX | IS | SY | BK | CR | LF | NL | ZW | eot)
            if (current == LineBreakClass.QU && CategoryAt(index) == GeneralCategory.Pf)
            {
                int next = NextUnit(index);
                if (next >= _length || ClassAt(next) is LineBreakClass.SP or LineBreakClass.GL or LineBreakClass.WJ or LineBreakClass.CL
                        or LineBreakClass.QU or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.IS or LineBreakClass.SY
                        or LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL or LineBreakClass.ZW)
                    return true;
            }

            // LB15c: SP ÷ IS NU, LB15d: × IS
            if (before == LineBreakClass.SP && current == LineBreakClass.IS && ClassAt(NextUnit(index)) == LineBreakClass.NU)
                return false;
            if (current == LineBreakClass.IS)
                return true;

            // LB16: (CL | CP) SP* × NS, LB17: B2 SP* × B2
            if (current == LineBreakClass.NS && ClassAt(SkipSpacesBack(start)) is LineBreakClass.CL or LineBreakClass.CP)
                return true;
            if (current == LineBreakClass.B2 && ClassAt(SkipSpacesBack(start)) == LineBreakClass.B2)
                return true;

            // LB18: SP ÷
            if (before == LineBreakClass.SP)
                return false;

            // LB19, LB19a: quotation marks.
            if (current == LineBreakClass.QU && CategoryAt(index) != GeneralCategory.Pi)
                return true;
            if (before == LineBreakClass.QU && CategoryAt(start) != GeneralCategory.Pf)
                return true;
            if (current == LineBreakClass.QU)
            {
                int next = NextUnit(index);
                if (!IsEastAsian(start) || next >= _length || !IsEastAsian(next))
                    return true;
            }
            if (before == LineBreakClass.QU)
            {
                int ahead = PreviousUnit(start);
                if (!IsEastAsian(index) || ahead < 0 || !IsEastAsian(ahead))
                    return true;
            }

            // LB20: ÷ CB, CB ÷
            if (current == LineBreakClass.CB || before == LineBreakClass.CB)
                return false;

            // LB20a: (sot | BK | CR | LF | NL | SP | ZW | CB | GL) (HY | U+2010) × AL
            if ((before == LineBreakClass.HY || _codePoints[start] == 0x2010) && current == LineBreakClass.AL)
            {
                int ahead = PreviousUnit(start);
                if (ahead < 0 || ClassAt(ahead) is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                        or LineBreakClass.SP or LineBreakClass.ZW or LineBreakClass.CB or LineBreakClass.GL)
                    return true;
            }

            // LB21: × BA, × HY, × NS, BB ×
            if (current is LineBreakClass.BA or LineBreakClass.HY or LineBreakClass.NS || before == LineBreakClass.BB)
                return true;

            // LB21a: HL (HY | [BA - $EastAsian]) × [^HL]
            if ((before == LineBreakClass.HY || (before == LineBreakClass.BA && !IsEastAsian(start)))
                && current != LineBreakClass.HL && ClassAt(PreviousUnit(start)) == LineBreakClass.HL)
                return true;

            // LB21b, LB22
            if (before == LineBreakClass.SY && current == LineBreakClass.HL)
                return true;
            if (current == LineBreakClass.IN)
                return true;

            return NumberAndLetterRules(index, start, before, current);
        }

        private int NextUnit(int index)
        {
            int next = index + 1;
            while (next < _length && _attached[next])
                next++;
            return next;
        }

        // The unit before any spaces that end at the unit starting at start (start itself
        // when it is not a space); -1 at the text start.
        private int SkipSpacesBack(int start)
        {
            int unit = start;
            while (unit >= 0 && _classes[unit] == LineBreakClass.SP)
                unit = PreviousUnit(unit);
            return unit;
        }
    }
}
