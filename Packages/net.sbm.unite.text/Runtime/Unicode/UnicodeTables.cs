using System;

namespace Unite.Text.Unicode
{
    // Views over the generated tables (UnicodeTables.*.Generated.cs).
    internal static partial class UnicodeTables
    {
        /// <summary>One two-stage property table: block numbers (2 bytes each) and the deduplicated blocks.</summary>
        internal readonly ref struct Table
        {
            public Table(int shift, ReadOnlySpan<byte> stage1, ReadOnlySpan<byte> stage2)
            {
                Shift = shift;
                Stage1 = stage1;
                Stage2 = stage2;
            }

            public int Shift { get; }
            public ReadOnlySpan<byte> Stage1 { get; }
            public ReadOnlySpan<byte> Stage2 { get; }
        }

        public static Table Bidi => new(BidiShift, BidiStage1, BidiStage2);
        public static Table LineBreak => new(LineBreakShift, LineBreakStage1, LineBreakStage2);
        public static Table Grapheme => new(GraphemeShift, GraphemeStage1, GraphemeStage2);
        public static Table Script => new(ScriptShift, ScriptStage1, ScriptStage2);
        public static Table Category => new(CategoryShift, CategoryStage1, CategoryStage2);
        public static Table Flags => new(FlagsShift, FlagsStage1, FlagsStage2);

        public static int[] MirrorSourceList => MirrorSources;
        public static int[] MirrorTargetList => MirrorTargets;
        public static int[] BracketSourceList => BracketSources;
        public static int[] BracketPairList => BracketPairs;
        public static byte[] BracketTypeList => BracketTypes;
    }
}
