using System;

namespace Unite.Text.Unicode
{
    // L1 and L2: per-line level adjustment and visual reordering.
    internal sealed partial class BidiParagraph
    {
        /// <summary>
        /// Copies the levels of the line [start, end) into <paramref name="lineLevels"/> with
        /// rule L1 applied: segment / paragraph separators and the whitespace (and isolate
        /// controls) before them or at the end of the line go back to the paragraph level.
        /// </summary>
        public void GetLineLevels(int start, int end, Span<byte> lineLevels)
        {
            bool trailing = true;
            for (int index = end - 1; index >= start; index--)
            {
                var type = _original[index];
                byte level = _levels[index];
                if (type is BidiClass.S or BidiClass.B)
                {
                    level = (byte)ParagraphLevel;
                    trailing = true;
                }
                else if (trailing && (type is BidiClass.WS or BidiClass.LRI or BidiClass.RLI or BidiClass.FSI or BidiClass.PDI || IsRemovedByX9(type)))
                {
                    level = (byte)ParagraphLevel;
                }
                else
                {
                    trailing = false;
                }
                lineLevels[index - start] = level;
            }
        }

        /// <summary>
        /// L2: fills <paramref name="visualToLogical"/> with the logical indices (0-based
        /// within the line) of the characters in left-to-right display order.
        /// </summary>
        public static void Reorder(ReadOnlySpan<byte> levels, Span<int> visualToLogical)
        {
            int length = levels.Length;
            int highest = 0;
            int lowestOdd = int.MaxValue;
            for (int index = 0; index < length; index++)
            {
                visualToLogical[index] = index;
                int level = levels[index];
                highest = Math.Max(highest, level);
                if ((level & 1) == 1)
                    lowestOdd = Math.Min(lowestOdd, level);
            }

            for (int level = highest; level >= lowestOdd && level > 0; level--)
            {
                for (int index = 0; index < length; index++)
                {
                    if (levels[visualToLogical[index]] < level)
                        continue;

                    int end = index;
                    while (end < length && levels[visualToLogical[end]] >= level)
                        end++;
                    visualToLogical.Slice(index, end - index).Reverse();
                    index = end;
                }
            }
        }
    }
}
