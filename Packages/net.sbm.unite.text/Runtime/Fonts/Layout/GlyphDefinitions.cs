using System;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Fonts.Layout
{
    /// <summary>GDEF glyph classes.</summary>
    internal enum GlyphClass : byte
    {
        Unclassified,
        Base,
        Ligature,
        Mark,
        Component
    }

    /// <summary>
    /// GDEF: glyph classes, mark attachment classes, mark glyph sets and the item variation
    /// store that variable fonts use for GPOS value deltas.
    /// </summary>
    internal sealed class GlyphDefinitions
    {
        private readonly byte[] _data;
        private readonly int _glyphClasses;
        private readonly int _markAttachClasses;
        private readonly int _markGlyphSets;

        private GlyphDefinitions(byte[] data, int offset)
        {
            _data = data;
            int minor = U16(data, offset + 2);
            _glyphClasses = Offset16(data, offset, offset + 4);
            _markAttachClasses = Offset16(data, offset, offset + 10);
            if (minor >= 2)
                _markGlyphSets = Offset16(data, offset, offset + 12);
            if (minor >= 3)
            {
                int store = U32(data, offset + 14);
                if (store != 0)
                {
                    try
                    {
                        VariationStore = ItemVariationStore.Read(data, offset + store);
                    }
                    catch (FontFormatException)
                    {
                        VariationStore = null;
                    }
                }
            }
        }

        public bool HasGlyphClasses => _glyphClasses != 0;

        /// <summary>Deltas for GPOS VariationIndex device tables (null for static fonts).</summary>
        public ItemVariationStore VariationStore { get; }

        public static GlyphDefinitions TryRead(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("GDEF", out var table) || table.Length < 12)
                return null;

            try
            {
                return new GlyphDefinitions(data, table.Offset);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        public GlyphClass GetGlyphClass(int glyph) => (GlyphClass)ClassOf(_data, _glyphClasses, glyph);

        public int GetMarkAttachClass(int glyph) => ClassOf(_data, _markAttachClasses, glyph);

        public bool IsInMarkGlyphSet(int set, int glyph)
        {
            if (_markGlyphSets == 0 || set < 0 || set >= U16(_data, _markGlyphSets + 2))
                return false;

            int coverage = _markGlyphSets + U32(_data, _markGlyphSets + 4 + set * 4);
            return Coverage(_data, coverage, glyph) >= 0;
        }
    }

    /// <summary>The OpenType layout tables of one face (each may be null).</summary>
    internal sealed class OpenTypeLayout
    {
        public OpenTypeLayout(byte[] data, SfntDirectory directory)
        {
            Data = data;
            Definitions = GlyphDefinitions.TryRead(data, directory);
            Substitutions = LayoutTable.TryRead(data, directory, "GSUB", 7);
            Positions = LayoutTable.TryRead(data, directory, "GPOS", 9);
            Kerning = KerningTable.TryRead(data, directory);
        }

        public byte[] Data { get; }
        public GlyphDefinitions Definitions { get; }
        public LayoutTable Substitutions { get; }
        public LayoutTable Positions { get; }

        /// <summary>The legacy 'kern' table, used only when there is no GPOS.</summary>
        public KerningTable Kerning { get; }

        public bool IsEmpty => Substitutions == null && Positions == null && Kerning == null;
    }

    /// <summary>The legacy 'kern' table: horizontal format 0 pair lists.</summary>
    internal sealed class KerningTable
    {
        private readonly byte[] _data;
        private readonly int[] _subtables;

        private KerningTable(byte[] data, int[] subtables)
        {
            _data = data;
            _subtables = subtables;
        }

        public static KerningTable TryRead(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("kern", out var table) || table.Length < 4)
                return null;

            try
            {
                int version = U16(data, table.Offset);
                if (version != 0)
                    return null; // Apple's version 1 'kern' is not supported

                int count = U16(data, table.Offset + 2);
                var subtables = new System.Collections.Generic.List<int>();
                int position = table.Offset + 4;
                for (int index = 0; index < count; index++)
                {
                    int length = U16(data, position + 2);
                    int coverage = U16(data, position + 4);
                    bool horizontal = (coverage & 1) != 0;
                    bool minimum = (coverage & 2) != 0;
                    bool crossStream = (coverage & 4) != 0;
                    int format = coverage >> 8;
                    if (format == 0 && horizontal && !minimum && !crossStream)
                        subtables.Add(position + 6);
                    position += length;
                }
                return subtables.Count == 0 ? null : new KerningTable(data, subtables.ToArray());
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>Summed kerning (font units) between two glyphs.</summary>
        public int GetKerning(int left, int right)
        {
            int key = (left << 16) | right;
            int total = 0;
            foreach (int subtable in _subtables)
            {
                int count = U16(_data, subtable);
                int low = 0;
                int high = count - 1;
                while (low <= high)
                {
                    int middle = (low + high) >> 1;
                    int record = subtable + 8 + middle * 6;
                    int pair = (U16(_data, record) << 16) | U16(_data, record + 2);
                    if (pair == key)
                    {
                        total += I16(_data, record + 4);
                        break;
                    }
                    if ((uint)pair < (uint)key)
                        low = middle + 1;
                    else
                        high = middle - 1;
                }
            }
            return total;
        }
    }
}
