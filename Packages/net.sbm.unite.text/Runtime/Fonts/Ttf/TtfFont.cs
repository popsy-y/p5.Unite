using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// A flattened glyph outline: closed polygon contours in glyph units (y up) and the
    /// union bounding box of every contour point. Composites are resolved into plain
    /// contours so downstream code never sees component relationships.
    /// </summary>
    public sealed class GlyphOutline
    {
        public GlyphOutline(Vector2[][] contours, Vector2 boundsMin, Vector2 boundsMax)
        {
            Contours = contours;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
        }

        public Vector2[][] Contours { get; }
        public Vector2 BoundsMin { get; }
        public Vector2 BoundsMax { get; }
    }

    /// <summary>
    /// A parsed TrueType font: head/hhea/hmtx metrics, cmap character mapping and lazily
    /// parsed glyf outlines. TrueType outlines only for v1 — OTTO (CFF) files are rejected
    /// by <see cref="TryParse"/>, hinting and the kern table are ignored (documented
    /// limitation in the package README).
    /// </summary>
    public sealed class TtfFont
    {
        private const uint TrueTypeSignature = 0x00010000u;
        private const uint OpenTypeCffSignature = 0x4F54544Fu; // 'OTTO'

        private readonly byte[] _data;
        private readonly int[] _glyphEndOffsets; // loca offsets; length GlyphCount + 1
        private readonly int _glyfStart;
        private readonly int _cmapOffset;
        private readonly int _cmapLength;
        private readonly ushort[] _advances;
        private readonly Dictionary<int, GlyphOutline> _outlineCache = new();
        private readonly HashSet<int> _outlinesInFlight = new();

        public int UnitsPerEm { get; }
        public int Ascender { get; }
        public int Descender { get; }
        public int GlyphCount { get; }

        private TtfFont(
            byte[] data,
            int glyfStart,
            int cmapOffset,
            int cmapLength,
            int[] glyphEndOffsets,
            ushort[] advances,
            int unitsPerEm,
            int ascender,
            int descender,
            int glyphCount
        )
        {
            _data = data;
            _glyfStart = glyfStart;
            _cmapOffset = cmapOffset;
            _cmapLength = cmapLength;
            _glyphEndOffsets = glyphEndOffsets;
            _advances = advances;
            UnitsPerEm = unitsPerEm;
            Ascender = ascender;
            Descender = descender;
            GlyphCount = glyphCount;
        }

        /// <summary>
        /// Parses a raw font byte stream. Returns false instead of throwing for anything
        /// that is not a structurally sound TrueType-outline font (missing requirement
        /// tables, truncation, CFF signatures, etc.).
        /// </summary>
        public static bool TryParse(byte[] fontData, out TtfFont font)
        {
            font = null;

            if (fontData == null || fontData.Length < 16)
                return false;

            try
            {
                var tables = ReadTableDirectory(fontData);

                foreach (string required in new[] { "head", "maxp", "hhea", "hmtx", "cmap", "loca", "glyf" })
                {
                    if (!tables.ContainsKey(required))
                        return false;
                }

                var head = tables["head"];
                int unitsPerEm = ReadHeadUnitsPerEm(fontData, head);
                int indexToLocFormat = ReadHeadIndexToLocFormat(fontData, head);

                var maxp = tables["maxp"];
                var maxpReader = new TtfReader(fontData, maxp.Offset + 4);
                int glyphCount = maxpReader.ReadU16();
                if (glyphCount == 0)
                    return false;

                var hhea = tables["hhea"];
                var metrics = ReadHhea(fontData, hhea);

                var locaOffsets = tables["loca"];
                var glyphEnds = ParseLoca(fontData, locaOffsets, glyphCount, indexToLocFormat);

                var hmtx = tables["hmtx"];
                var advances = ParseHmtx(fontData, hmtx, glyphCount, metrics.NumberOfHMetrics);

                var cmap = tables["cmap"];
                if (!CmapTables.HasSupportedSubtable(fontData, cmap.Offset, cmap.Length))
                    return false;

                font = new TtfFont(
                    fontData,
                    tables["glyf"].Offset,
                    cmap.Offset,
                    cmap.Length,
                    glyphEnds,
                    advances,
                    unitsPerEm,
                    metrics.Ascender,
                    metrics.Descender,
                    glyphCount
                );
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Character-to-glyph lookup (cmap only, no shaping). Returns 0 (.notdef) when
        /// the code point is unmapped.
        /// </summary>
        public int GetGlyphId(int codePoint)
        {
            return LookupGlyphId(codePoint);
        }

        /// <summary>
        /// Advance width of one glyph in glyph units; out-of-range ids clamp to the
        /// nearest glyph (0 when the font itself is empty).
        /// </summary>
        public float Advance(int glyphId)
        {
            int index = Mathf.Clamp(glyphId, 0, GlyphCount - 1);
            return _advances[index];
        }

        /// <summary>
        /// Flattened outline of one glyph. Returns false for blank glyphs (space,
        /// .notdef, unresolvable composites); advance widths still apply to those.
        /// </summary>
        public bool TryGetOutline(int glyphId, out GlyphOutline outline)
        {
            outline = null;

            if (glyphId < 0 || glyphId >= GlyphCount)
                return false;

            if (_outlineCache.TryGetValue(glyphId, out outline))
                return outline != null;

            // Guards against composite cycles in malformed fonts: re-entrant parses
            // behave like blank glyphs instead of recursing forever.
            if (!_outlinesInFlight.Add(glyphId))
                return false;

            GlyphOutline parsed;
            try
            {
                int start = _glyphEndOffsets[glyphId];
                int end = _glyphEndOffsets[glyphId + 1];
                parsed = end <= start ? null : ParseGlyphRecord(glyphId, start, end);
            }
            finally
            {
                _outlinesInFlight.Remove(glyphId);
            }

            _outlineCache[glyphId] = parsed;
            outline = parsed;
            return parsed != null;
        }

        private int LookupGlyphId(int codePoint)
        {
            var reader = new TtfReader(_data, 0);
            return CmapTables.Lookup(reader, _cmapOffset, _cmapLength, codePoint);
        }

        private GlyphOutline ParseGlyphRecord(int glyphId, int start, int end)
        {
            var reader = new TtfReader(_data);
            reader.Seek(_glyfStart + start);

            return GlyfTables.ParseOutline(reader, end - start, this);
        }

        private static Dictionary<string, (int Offset, int Length)> ReadTableDirectory(byte[] fontData)
        {
            var reader = new TtfReader(fontData);
            uint version = reader.ReadU32();

            if (version != TrueTypeSignature)
            {
                // CFF-flavored OpenType fonts carry vector outlines we do not parse yet.
                if (version != OpenTypeCffSignature)
                {
                    throw new InvalidDataException(
                        "unknown font signature (neither TrueType nor OpenType/CFF)."
                    );
                }
                throw new InvalidDataException("CFF-flavored OpenType fonts are not supported yet.");
            }

            int numTables = reader.ReadU16();
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            var tables = new Dictionary<string, (int, int)>(numTables);
            for (int index = 0; index < numTables && reader.Has(16); index++)
            {
                string tag = TtfTag(reader);
                reader.ReadU32(); // checksum
                int offset = (int)reader.ReadU32();
                int length = (int)reader.ReadU32();

                if (offset + length > fontData.Length)
                {
                    throw new InvalidDataException($"table '{tag}' extends past the file end.");
                }

                tables[tag] = (offset, length);
            }
            return tables;
        }

        private static string TtfTag(TtfReader reader)
        {
            var bytes = reader.ReadBytes(4);
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        private static int ReadHeadUnitsPerEm(byte[] fontData, (int Offset, int Length) head)
        {
            var reader = new TtfReader(fontData, head.Offset + 18);
            int value = reader.ReadU16();
            if (value < 1)
            {
                throw new InvalidDataException("head unitsPerEm must be >= 1.");
            }
            return value;
        }

        private static int ReadHeadIndexToLocFormat(byte[] fontData, (int Offset, int Length) head)
        {
            if (head.Length < 52)
            {
                throw new InvalidDataException("head table is truncated.");
            }

            var reader = new TtfReader(fontData, head.Offset + 50);
            int format = reader.ReadI16();
            if (format != 0 && format != 1)
            {
                throw new InvalidDataException("unsupported loca indexToLocFormat.");
            }
            return format;
        }

        private static (int Ascender, int Descender, int NumberOfHMetrics) ReadHhea(
            byte[] fontData,
            (int Offset, int Length) hhea
        )
        {
            if (hhea.Length < 36)
            {
                throw new InvalidDataException("hhea table is truncated.");
            }

            var reader = new TtfReader(fontData, hhea.Offset + 4);
            int ascender = reader.ReadI16();
            int descender = reader.ReadI16();
            reader.Seek(hhea.Offset + 34);
            int numberOfHMetrics = reader.ReadU16();
            return (ascender, descender, numberOfHMetrics);
        }

        private static int[] ParseLoca(
            byte[] fontData,
            (int Offset, int Length) loca,
            int glyphCount,
            int format
        )
        {
            int expected = glyphCount + 1;
            long needed = (long)(format == 0 ? 2 : 4) * expected;
            if (loca.Offset + needed > fontData.Length)
            {
                throw new InvalidDataException("loca table is truncated.");
            }

            var reader = new TtfReader(fontData, loca.Offset);
            var ends = new int[expected];
            for (int index = 0; index < expected; index++)
            {
                ends[index] = format == 0
                    ? reader.ReadU16() * 2
                    : (int)reader.ReadU32();
            }
            return ends;
        }

        private static ushort[] ParseHmtx(
            byte[] fontData,
            (int Offset, int Length) hmtx,
            int glyphCount,
            int numberOfHMetrics
        )
        {
            if (numberOfHMetrics <= 0 || numberOfHMetrics > glyphCount)
            {
                throw new InvalidDataException("hhea.numberOfHMetrics is out of range.");
            }

            long needed = (long)(numberOfHMetrics - 1) * 4 + 2;
            if (hmtx.Offset + needed > fontData.Length)
            {
                throw new InvalidDataException("hmtx table is truncated.");
            }

            var reader = new TtfReader(fontData, hmtx.Offset);
            var advances = new ushort[glyphCount];

            for (int index = 0; index < glyphCount; index++)
            {
                // Trailing glyphs share the last tabulated advance; lsbs are ignored.
                advances[index] = index < numberOfHMetrics ? reader.ReadU16() : advances[numberOfHMetrics - 1];
                if (index < numberOfHMetrics - 1)
                    reader.Skip(2);
            }
            return advances;
        }

        private sealed class InvalidDataException : Exception
        {
            public InvalidDataException(string message) : base($"p5.Unite: {message}") { }
        }
    }
}
