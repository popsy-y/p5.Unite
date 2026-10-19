using System;
using Unite.Text.Unicode;

namespace Unite.Text.Shaping
{
    /// <summary>
    /// Cursive joining for Arabic-like scripts (UAX #9 / ArabicShaping.txt, HarfBuzz's state
    /// machine): picks isol / fina / fin2 / fin3 / medi / med2 / init per letter from the
    /// joining types of its neighbours, transparent marks skipped, ZWJ joining and ZWNJ
    /// breaking the chain.
    /// </summary>
    internal static class ArabicJoining
    {
        public static readonly string[] Features = { "isol", "fina", "fin2", "fin3", "medi", "med2", "init" };

        private const int None = -1;
        private const int Isol = 0;
        private const int Fina = 1;
        private const int Fin2 = 2;
        private const int Fin3 = 3;
        private const int Medi = 4;
        private const int Med2 = 5;
        private const int Init = 6;

        // Columns: U, L, R, D, ALAPH, DALATH_RISH; entries: previous action, current action, next state.
        private static readonly int[,,] States =
        {
            { { None, None, 0 }, { None, Isol, 2 }, { None, Isol, 1 }, { None, Isol, 2 }, { None, Isol, 1 }, { None, Isol, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { None, Isol, 1 }, { None, Isol, 2 }, { None, Fin2, 5 }, { None, Isol, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { Init, Fina, 1 }, { Init, Fina, 3 }, { Init, Fina, 4 }, { Init, Fina, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { Medi, Fina, 1 }, { Medi, Fina, 3 }, { Medi, Fina, 4 }, { Medi, Fina, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { Med2, Isol, 1 }, { Med2, Isol, 2 }, { Med2, Fin2, 5 }, { Med2, Isol, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { Isol, Isol, 1 }, { Isol, Isol, 2 }, { Isol, Fin2, 5 }, { Isol, Isol, 6 } },
            { { None, None, 0 }, { None, Isol, 2 }, { None, Isol, 1 }, { None, Isol, 2 }, { None, Fin3, 5 }, { None, Isol, 6 } },
        };

        private const int Transparent = -1;

        public static bool UsesJoining(UnicodeScript script) => script is UnicodeScript.Arabic or UnicodeScript.Syriac
            or UnicodeScript.Mongolian or UnicodeScript.Nko or UnicodeScript.PhagsPa or UnicodeScript.Mandaic
            or UnicodeScript.Manichaean or UnicodeScript.PsalterPahlavi or UnicodeScript.Adlam
            or UnicodeScript.HanifiRohingya or UnicodeScript.Sogdian or UnicodeScript.OldUyghur;

        /// <summary>
        /// Sets the positional feature masks of the buffer; the code points before
        /// <paramref name="start"/> and from <paramref name="end"/> on are the context.
        /// </summary>
        public static void Apply(ShapingBuffer buffer, ShapePlan plan, int[] codePoints, int start, int end, int length)
        {
            Span<uint> masks = stackalloc uint[Features.Length];
            for (int feature = 0; feature < Features.Length; feature++)
                masks[feature] = plan.MaskOf(Features[feature]);

            int state = 0;
            int previous = -1;

            // Pre-context: the state the text before the run leaves.
            for (int index = start - 1; index >= 0; index--)
            {
                int type = JoiningColumn(codePoints[index]);
                if (type == Transparent)
                    continue;
                state = States[0, type, 2];
                break;
            }

            for (int index = 0; index < buffer.Count; index++)
            {
                int type = JoiningColumn(buffer.Info[index].CodePoint);
                if (type == Transparent)
                    continue;

                int previousAction = States[state, type, 0];
                if (previousAction != None && previous >= 0)
                    SetAction(buffer, previous, previousAction, masks);
                SetAction(buffer, index, States[state, type, 1], masks);
                previous = index;
                state = States[state, type, 2];
            }

            // Post-context: the next joining character decides the last letter's form.
            for (int index = end; index < length; index++)
            {
                int type = JoiningColumn(codePoints[index]);
                if (type == Transparent)
                    continue;
                int previousAction = States[state, type, 0];
                if (previousAction != None && previous >= 0)
                    SetAction(buffer, previous, previousAction, masks);
                break;
            }
        }

        private static void SetAction(ShapingBuffer buffer, int index, int action, Span<uint> masks)
        {
            ref var info = ref buffer.Info[index];
            for (int feature = 0; feature < masks.Length; feature++)
                info.Mask &= ~masks[feature];
            if (action != None)
                info.Mask |= masks[action];
        }

        // Column of the state table, or Transparent.
        private static int JoiningColumn(int codePoint)
        {
            switch (codePoint)
            {
                case 0x0710: return 4;                       // SYRIAC LETTER ALAPH
                case 0x0715: case 0x0716: case 0x072A: case 0x072F: return 5; // DALATH / RISH group
            }

            return UnicodeData.GetJoiningType(codePoint) switch
            {
                JoiningType.T => Transparent,
                JoiningType.L => 1,
                JoiningType.R => 2,
                JoiningType.D or JoiningType.C => 3,
                _ => 0
            };
        }
    }
}
