using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Type 2 charstring interpreter: runs one glyph program and returns its outline.
    /// Hints are parsed only to skip them; the seac-style accented endchar is ignored.
    /// </summary>
    internal sealed class CffCharString
    {
        private const int MaxStack = 96;
        private const int MaxCallDepth = 10;

        private readonly byte[] _data;
        private readonly CffIndex _globalSubrs;
        private readonly CffIndex _localSubrs;
        private readonly float[] _stack = new float[MaxStack];
        private readonly float[] _transient = new float[32];
        private readonly OutlineBuilder _builder;

        private int _depth;
        private int _count;
        private Vector2 _position;
        private bool _widthHandled;
        private int _stemCount;
        private bool _finished;

        public CffCharString(byte[] data, CffIndex globalSubrs, CffIndex localSubrs, float flatness)
        {
            _builder = new OutlineBuilder(flatness);
            _data = data;
            _globalSubrs = globalSubrs;
            _localSubrs = localSubrs;
        }

        public GlyphOutline Run(int offset, int length)
        {
            Execute(offset, length);
            return _builder.ToOutline();
        }

        private void Execute(int offset, int length)
        {
            if (++_depth > MaxCallDepth)
                throw new FontFormatException("CFF subroutine nesting is too deep.");

            int position = offset;
            int end = offset + length;
            while (position < end && !_finished)
            {
                int b0 = _data[position++];

                if (b0 >= 32 || b0 == 28)
                {
                    position = ReadNumber(b0, position);
                    continue;
                }

                if (b0 == 11)
                    break; // return

                position = Operator(b0, position);
            }

            _depth--;
        }

        private int ReadNumber(int b0, int position)
        {
            float value;
            if (b0 == 28)
            {
                value = (short)((_data[position] << 8) | _data[position + 1]);
                position += 2;
            }
            else if (b0 <= 246)
            {
                value = b0 - 139;
            }
            else if (b0 <= 250)
            {
                value = (b0 - 247) * 256 + _data[position++] + 108;
            }
            else if (b0 <= 254)
            {
                value = -(b0 - 251) * 256 - _data[position++] - 108;
            }
            else
            {
                int fixedValue = (_data[position] << 24) | (_data[position + 1] << 16)
                    | (_data[position + 2] << 8) | _data[position + 3];
                value = fixedValue / 65536f;
                position += 4;
            }

            Push(value);
            return position;
        }

        private int Operator(int op, int position)
        {
            switch (op)
            {
                case 1: case 3: case 18: case 23: // hstem vstem hstemhm vstemhm
                    HandleWidth((_count & 1) == 1);
                    _stemCount += _count / 2;
                    _count = 0;
                    break;
                case 19: case 20: // hintmask cntrmask
                    HandleWidth((_count & 1) == 1);
                    _stemCount += _count / 2;
                    _count = 0;
                    position += (_stemCount + 7) / 8;
                    break;
                case 21: // rmoveto
                    HandleWidth(_count > 2);
                    MoveTo(_stack[0], _stack[1]);
                    break;
                case 22: // hmoveto
                    HandleWidth(_count > 1);
                    MoveTo(_stack[0], 0f);
                    break;
                case 4: // vmoveto
                    HandleWidth(_count > 1);
                    MoveTo(0f, _stack[0]);
                    break;
                case 5: // rlineto
                    for (int index = 0; index + 1 < _count; index += 2)
                        LineBy(_stack[index], _stack[index + 1]);
                    _count = 0;
                    break;
                case 6: case 7: // hlineto vlineto
                    AlternatingLines(op == 6);
                    break;
                case 8: // rrcurveto
                    for (int index = 0; index + 5 < _count; index += 6)
                        CurveBy(_stack[index], _stack[index + 1], _stack[index + 2], _stack[index + 3], _stack[index + 4], _stack[index + 5]);
                    _count = 0;
                    break;
                case 24: // rcurveline
                    CurveLine();
                    break;
                case 25: // rlinecurve
                    LineCurve();
                    break;
                case 26: // vvcurveto
                    VerticalCurves();
                    break;
                case 27: // hhcurveto
                    HorizontalCurves();
                    break;
                case 30: case 31: // vhcurveto hvcurveto
                    AlternatingCurves(op == 31);
                    break;
                case 10: // callsubr
                    CallSubroutine(_localSubrs);
                    break;
                case 29: // callgsubr
                    CallSubroutine(_globalSubrs);
                    break;
                case 14: // endchar
                    HandleWidth(_count == 1 || _count == 5);
                    _builder.Close();
                    _finished = true;
                    _count = 0;
                    break;
                case 12:
                    Escape(_data[position++]);
                    break;
                default:
                    // Reserved operators clear the stack, like unknown ones in FreeType.
                    _count = 0;
                    break;
            }

            return position;
        }

        private void Escape(int op)
        {
            switch (op)
            {
                case 34: Flex(HFlex); break;
                case 35: Flex(FullFlex); break;
                case 36: Flex(HFlex1); break;
                case 37: Flex(Flex1); break;
                case 3: { float b = Pop(), a = Pop(); Push(a != 0f && b != 0f ? 1f : 0f); break; }
                case 4: { float b = Pop(), a = Pop(); Push(a != 0f || b != 0f ? 1f : 0f); break; }
                case 5: Push(Pop() == 0f ? 1f : 0f); break;
                case 9: Push(Mathf.Abs(Pop())); break;
                case 10: { float b = Pop(), a = Pop(); Push(a + b); break; }
                case 11: { float b = Pop(), a = Pop(); Push(a - b); break; }
                case 12: { float b = Pop(), a = Pop(); Push(b != 0f ? a / b : 0f); break; }
                case 14: Push(-Pop()); break;
                case 15: { float b = Pop(), a = Pop(); Push(a == b ? 1f : 0f); break; }
                case 18: Pop(); break;
                case 20: { int index = (int)Pop(); float value = Pop(); if (index >= 0 && index < _transient.Length) _transient[index] = value; break; }
                case 21: { int index = (int)Pop(); Push(index >= 0 && index < _transient.Length ? _transient[index] : 0f); break; }
                case 22: { float v2 = Pop(), v1 = Pop(), s2 = Pop(), s1 = Pop(); Push(v1 <= v2 ? s1 : s2); break; }
                case 23: Push(0.5f); break;
                case 24: { float b = Pop(), a = Pop(); Push(a * b); break; }
                case 26: Push(Mathf.Sqrt(Mathf.Max(0f, Pop()))); break;
                case 27: { float value = Pop(); Push(value); Push(value); break; }
                case 28: { float b = Pop(), a = Pop(); Push(b); Push(a); break; }
                case 29: { int index = (int)Pop(); index = Mathf.Max(0, index); Push(_count > index ? _stack[_count - 1 - index] : 0f); break; }
                case 30: Roll(); break;
                default: _count = 0; break;
            }
        }

        private void Roll()
        {
            int shift = (int)Pop();
            int size = (int)Pop();
            if (size <= 0 || size > _count)
                return;

            var window = new float[size];
            int start = _count - size;
            for (int index = 0; index < size; index++)
                window[((index + shift) % size + size) % size] = _stack[start + index];
            for (int index = 0; index < size; index++)
                _stack[start + index] = window[index];
        }

        // The first stack-clearing operator may carry the glyph width as an extra argument.
        private void HandleWidth(bool hasWidth)
        {
            if (_widthHandled)
                return;

            _widthHandled = true;
            if (!hasWidth || _count == 0)
                return;

            for (int index = 1; index < _count; index++)
                _stack[index - 1] = _stack[index];
            _count--;
        }

        private void MoveTo(float dx, float dy)
        {
            _position += new Vector2(dx, dy);
            _builder.MoveTo(_position);
            _count = 0;
        }

        private void LineBy(float dx, float dy)
        {
            _position += new Vector2(dx, dy);
            _builder.LineTo(_position);
        }

        private void CurveBy(float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
        {
            var control1 = _position + new Vector2(dx1, dy1);
            var control2 = control1 + new Vector2(dx2, dy2);
            var end = control2 + new Vector2(dx3, dy3);
            _builder.CubicTo(control1, control2, end);
            _position = end;
        }

        private void AlternatingLines(bool horizontal)
        {
            for (int index = 0; index < _count; index++)
            {
                if (horizontal)
                    LineBy(_stack[index], 0f);
                else
                    LineBy(0f, _stack[index]);
                horizontal = !horizontal;
            }
            _count = 0;
        }

        private void CurveLine()
        {
            int index = 0;
            for (; index + 6 <= _count - 2; index += 6)
                CurveBy(_stack[index], _stack[index + 1], _stack[index + 2], _stack[index + 3], _stack[index + 4], _stack[index + 5]);
            if (index + 1 < _count)
                LineBy(_stack[index], _stack[index + 1]);
            _count = 0;
        }

        private void LineCurve()
        {
            int index = 0;
            for (; index + 2 <= _count - 6; index += 2)
                LineBy(_stack[index], _stack[index + 1]);
            if (index + 5 < _count)
                CurveBy(_stack[index], _stack[index + 1], _stack[index + 2], _stack[index + 3], _stack[index + 4], _stack[index + 5]);
            _count = 0;
        }

        private void HorizontalCurves()
        {
            int index = 0;
            float dy1 = 0f;
            if ((_count & 1) == 1)
                dy1 = _stack[index++];

            for (; index + 3 < _count; index += 4)
            {
                CurveBy(_stack[index], dy1, _stack[index + 1], _stack[index + 2], _stack[index + 3], 0f);
                dy1 = 0f;
            }
            _count = 0;
        }

        private void VerticalCurves()
        {
            int index = 0;
            float dx1 = 0f;
            if ((_count & 1) == 1)
                dx1 = _stack[index++];

            for (; index + 3 < _count; index += 4)
            {
                CurveBy(dx1, _stack[index], _stack[index + 1], _stack[index + 2], 0f, _stack[index + 3]);
                dx1 = 0f;
            }
            _count = 0;
        }

        // hvcurveto / vhcurveto: curves alternate between horizontal and vertical start
        // tangents; a trailing extra argument adjusts the final end point.
        private void AlternatingCurves(bool horizontal)
        {
            int index = 0;
            while (index + 4 <= _count)
            {
                bool last = _count - index == 5;
                float extra = last ? _stack[index + 4] : 0f;
                if (horizontal)
                    CurveBy(_stack[index], 0f, _stack[index + 1], _stack[index + 2], extra, _stack[index + 3]);
                else
                    CurveBy(0f, _stack[index], _stack[index + 1], _stack[index + 2], _stack[index + 3], extra);

                horizontal = !horizontal;
                index += 4;
            }
            _count = 0;
        }

        private void CallSubroutine(CffIndex subroutines)
        {
            int bias = subroutines.Count < 1240 ? 107 : (subroutines.Count < 33900 ? 1131 : 32768);
            int index = (int)Pop() + bias;
            if (index < 0 || index >= subroutines.Count)
                throw new FontFormatException("CFF subroutine index is out of range.");

            Execute(subroutines.Offset(index), subroutines.Length(index));
        }

        // Flex operators expand into two curves given as 12 relative coordinates.
        private delegate float[] FlexShape(float[] stack);

        private void Flex(FlexShape shape)
        {
            var curves = shape(_stack);
            for (int index = 0; index < 12; index += 6)
                CurveBy(curves[index], curves[index + 1], curves[index + 2], curves[index + 3], curves[index + 4], curves[index + 5]);
            _count = 0;
        }

        private static float[] FullFlex(float[] s)
            => new[] { s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7], s[8], s[9], s[10], s[11] };

        private static float[] HFlex(float[] s)
            => new[] { s[0], 0f, s[1], s[2], s[3], 0f, s[4], 0f, s[5], -s[2], s[6], 0f };

        private static float[] HFlex1(float[] s)
            => new[] { s[0], s[1], s[2], s[3], s[4], 0f, s[5], 0f, s[6], s[7], s[8], -(s[1] + s[3] + s[7]) };

        private static float[] Flex1(float[] s)
        {
            float dx = s[0] + s[2] + s[4] + s[6] + s[8];
            float dy = s[1] + s[3] + s[5] + s[7] + s[9];
            bool horizontalEnd = Mathf.Abs(dx) > Mathf.Abs(dy);
            return new[]
            {
                s[0], s[1], s[2], s[3], s[4], s[5], s[6], s[7], s[8], s[9],
                horizontalEnd ? s[10] : -dx,
                horizontalEnd ? -dy : s[10]
            };
        }

        private void Push(float value)
        {
            if (_count >= MaxStack)
                throw new FontFormatException("CFF charstring stack overflow.");

            _stack[_count++] = value;
        }

        private float Pop()
        {
            if (_count == 0)
                throw new FontFormatException("CFF charstring stack underflow.");

            return _stack[--_count];
        }
    }
}
