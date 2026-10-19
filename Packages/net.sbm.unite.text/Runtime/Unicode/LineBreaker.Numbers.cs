namespace Unite.Text.Unicode
{
    // LB23-LB30b: letters and numbers, Korean syllables, Brahmic aksaras, regional
    // indicators and emoji modifiers.
    internal sealed partial class LineBreaker
    {
        private bool NumberAndLetterRules(int index, int start, LineBreakClass before, LineBreakClass current)
        {
            // LB23, LB23a, LB24
            if (IsLetter(before) && current == LineBreakClass.NU || before == LineBreakClass.NU && IsLetter(current))
                return true;
            if (before == LineBreakClass.PR && current is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM)
                return true;
            if (before is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM && current == LineBreakClass.PO)
                return true;
            if (before is LineBreakClass.PR or LineBreakClass.PO && IsLetter(current))
                return true;
            if (IsLetter(before) && current is LineBreakClass.PR or LineBreakClass.PO)
                return true;

            if (NumberRules(index, start, before, current))
                return true;

            // LB26, LB27: Korean syllable blocks.
            if (before == LineBreakClass.JL && current is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3)
                return true;
            if (before is LineBreakClass.JV or LineBreakClass.H2 && current is LineBreakClass.JV or LineBreakClass.JT)
                return true;
            if (before is LineBreakClass.JT or LineBreakClass.H3 && current == LineBreakClass.JT)
                return true;
            if (IsHangul(before) && current == LineBreakClass.PO || before == LineBreakClass.PR && IsHangul(current))
                return true;

            // LB28
            if (IsLetter(before) && IsLetter(current))
                return true;

            // LB28a: Brahmic orthographic syllables.
            if (before == LineBreakClass.AP && IsAksara(index))
                return true;
            if (IsAksara(start) && current is LineBreakClass.VF or LineBreakClass.VI)
                return true;
            if (before == LineBreakClass.VI && IsAksara(PreviousUnit(start))
                && (current == LineBreakClass.AK || _codePoints[index] == DottedCircle))
                return true;
            if (IsAksara(start) && IsAksara(index) && ClassAt(NextUnit(index)) == LineBreakClass.VF)
                return true;

            // LB29, LB30
            if (before == LineBreakClass.IS && IsLetter(current))
                return true;
            if ((IsLetter(before) || before == LineBreakClass.NU) && current == LineBreakClass.OP && !IsEastAsian(index))
                return true;
            if (before == LineBreakClass.CP && !IsEastAsian(start) && (IsLetter(current) || current == LineBreakClass.NU))
                return true;

            // LB30a: regional indicators pair up.
            if (before == LineBreakClass.RI && current == LineBreakClass.RI)
            {
                int count = 0;
                for (int unit = start; unit >= 0 && ClassAt(unit) == LineBreakClass.RI; unit = PreviousUnit(unit))
                    count++;
                if (count % 2 == 1)
                    return true;
            }

            // LB30b
            if (current == LineBreakClass.EM && (before == LineBreakClass.EB || UnicodeData.IsUnassignedPictographic(_codePoints[start])))
                return true;

            return false;
        }

        // LB25 (Unicode 16): numeric expressions such as "$(12.5)%".
        private bool NumberRules(int index, int start, LineBreakClass before, LineBreakClass current)
        {
            if (current is LineBreakClass.PO or LineBreakClass.PR)
            {
                // NU (SY | IS)* (CL | CP)? × (PO | PR)
                int unit = start;
                if (ClassAt(unit) is LineBreakClass.CL or LineBreakClass.CP)
                    unit = PreviousUnit(unit);
                if (ClassAt(SkipNumericSeparators(unit)) == LineBreakClass.NU)
                    return true;
            }

            if (before is LineBreakClass.PO or LineBreakClass.PR)
            {
                // (PO | PR) × OP NU, (PO | PR) × OP IS NU, (PO | PR) × NU
                if (current == LineBreakClass.NU)
                    return true;
                if (current == LineBreakClass.OP)
                {
                    int next = NextUnit(index);
                    if (ClassAt(next) == LineBreakClass.NU)
                        return true;
                    if (ClassAt(next) == LineBreakClass.IS && ClassAt(NextUnit(next)) == LineBreakClass.NU)
                        return true;
                }
            }

            if (current == LineBreakClass.NU)
            {
                // HY × NU, IS × NU, NU (SY | IS)* × NU
                if (before is LineBreakClass.HY or LineBreakClass.IS)
                    return true;
                if (ClassAt(SkipNumericSeparators(start)) == LineBreakClass.NU)
                    return true;
            }
            return false;
        }

        private int SkipNumericSeparators(int unit)
        {
            while (unit >= 0 && ClassAt(unit) is LineBreakClass.SY or LineBreakClass.IS)
                unit = PreviousUnit(unit);
            return unit;
        }

        private static bool IsLetter(LineBreakClass type) => type is LineBreakClass.AL or LineBreakClass.HL;

        private static bool IsHangul(LineBreakClass type)
            => type is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3;

        private bool IsAksara(int index)
            => index >= 0 && index < _length
                && (_classes[index] is LineBreakClass.AK or LineBreakClass.AS || _codePoints[index] == DottedCircle);
    }
}
