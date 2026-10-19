using System;
using UnityEngine;
using static Unite.Text.Fonts.Layout.OpenTypeData;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// COLR version 0 color glyphs with their CPAL palette: a base glyph is drawn as a stack
    /// of ordinary glyph layers, each in one palette color (or in the text's fill color for
    /// palette index 0xFFFF). COLRv1 paint graphs and bitmap emoji are not read; such
    /// glyphs draw their monochrome outline when the font has one.
    /// </summary>
    internal sealed class ColorGlyphs
    {
        public const int ForegroundPalette = 0xFFFF;

        private readonly byte[] _data;
        private readonly int _baseRecords;
        private readonly int _baseCount;
        private readonly int _layerRecords;
        private readonly int _layerCount;
        private readonly Color[] _palette;

        private ColorGlyphs(byte[] data, int baseRecords, int baseCount, int layerRecords, int layerCount, Color[] palette)
        {
            _data = data;
            _baseRecords = baseRecords;
            _baseCount = baseCount;
            _layerRecords = layerRecords;
            _layerCount = layerCount;
            _palette = palette;
        }

        public static ColorGlyphs TryRead(byte[] data, SfntDirectory directory)
        {
            if (!directory.TryGet("COLR", out var colr) || !directory.TryGet("CPAL", out var cpal) || colr.Length < 14 || cpal.Length < 12)
                return null;

            try
            {
                int baseCount = U16(data, colr.Offset + 2);
                int baseRecords = colr.Offset + U32(data, colr.Offset + 4);
                int layerRecords = colr.Offset + U32(data, colr.Offset + 8);
                int layerCount = U16(data, colr.Offset + 12);
                if (baseCount == 0 || layerCount == 0)
                    return null;

                // Palette 0 (the default one).
                int entries = U16(data, cpal.Offset + 2);
                int colorRecords = cpal.Offset + U32(data, cpal.Offset + 8);
                int firstColor = U16(data, cpal.Offset + 12);
                var palette = new Color[entries];
                for (int entry = 0; entry < entries; entry++)
                {
                    int record = colorRecords + (firstColor + entry) * 4;
                    palette[entry] = new Color32(data[record + 2], data[record + 1], data[record], data[record + 3]);
                }

                return new ColorGlyphs(data, baseRecords, baseCount, layerRecords, layerCount, palette);
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>The layers of a color glyph (first layer index and count); false for plain glyphs.</summary>
        public bool TryGetLayers(int glyph, out int first, out int count)
        {
            int low = 0;
            int high = _baseCount - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                int record = _baseRecords + middle * 6;
                int value = U16(_data, record);
                if (value == glyph)
                {
                    first = U16(_data, record + 2);
                    count = U16(_data, record + 4);
                    if (first + count > _layerCount)
                        count = Math.Max(0, _layerCount - first);
                    return count > 0;
                }
                if (value < glyph)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            first = 0;
            count = 0;
            return false;
        }

        public void GetLayer(int index, out int glyph, out int paletteIndex)
        {
            int record = _layerRecords + index * 4;
            glyph = U16(_data, record);
            paletteIndex = U16(_data, record + 2);
        }

        /// <summary>The palette color of a layer, or <paramref name="foreground"/> for 0xFFFF; the foreground alpha applies to both.</summary>
        public Color LayerColor(int paletteIndex, Color foreground)
        {
            if (paletteIndex == ForegroundPalette || paletteIndex >= _palette.Length)
                return foreground;

            var color = _palette[paletteIndex];
            color.a *= foreground.a;
            return color;
        }
    }
}
