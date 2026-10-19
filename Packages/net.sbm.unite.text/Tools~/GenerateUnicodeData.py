#!/usr/bin/env python3
"""Generates the Unicode property tables of Unite.Text (Runtime/Unicode/*.Generated.cs).

Usage: python GenerateUnicodeData.py <ucd-dir> <output-dir>

<ucd-dir> holds these files of one UCD release (https://www.unicode.org/Public/<ver>/ucd),
flattened into one directory: UnicodeData.txt PropertyValueAliases.txt BidiBrackets.txt
BidiMirroring.txt Scripts.txt DerivedCoreProperties.txt DerivedBidiClass.txt
DerivedJoiningType.txt DerivedGeneralCategory.txt DerivedLineBreak.txt EastAsianWidth.txt
GraphemeBreakProperty.txt emoji-data.txt DerivedNormalizationProps.txt

Every property becomes a two-stage table of one byte per code point: stage 1 maps
cp >> shift to a block number (two bytes, little endian), stage 2 holds the deduplicated
blocks. The block size is chosen per table to minimize its size. The tables are emitted
as ReadOnlySpan<byte> properties over constant arrays, which the C# compiler stores as
static data (no allocation at runtime).
"""

import os
import re
import sys

MAX_CP = 0x110000
UNICODE_VERSION = "16.0.0"

# Value orders are part of the generated C# enums; the runtime code depends on them.
BIDI_CLASSES = ["L", "R", "AL", "EN", "ES", "ET", "AN", "CS", "NSM", "BN", "B", "S", "WS", "ON",
                "LRE", "LRO", "RLE", "RLO", "PDF", "LRI", "RLI", "FSI", "PDI"]
LINE_BREAK_CLASSES = ["XX", "BK", "CR", "LF", "CM", "NL", "SG", "WJ", "ZW", "GL", "SP", "ZWJ", "B2",
                      "BA", "BB", "HY", "CB", "CL", "CP", "EX", "IN", "NS", "OP", "QU", "IS", "NU",
                      "PO", "PR", "SY", "AI", "AK", "AL", "AP", "AS", "CJ", "EB", "EM", "H2", "H3",
                      "HL", "ID", "JL", "JV", "JT", "RI", "SA", "VF", "VI"]
GRAPHEME_CLASSES = ["Other", "CR", "LF", "Control", "Extend", "ZWJ", "Regional_Indicator", "Prepend",
                    "SpacingMark", "L", "V", "T", "LV", "LVT"]
GENERAL_CATEGORIES = ["Cn", "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc",
                      "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp",
                      "Cc", "Cf", "Cs", "Co"]
JOINING_TYPES = ["U", "R", "D", "C", "L", "T"]
INCB_VALUES = {"None": 0, "Linker": 1, "Consonant": 2, "Extend": 3}

# ISO 15924 -> OpenType script tag, where they differ from the lowercased ISO code.
OPENTYPE_SCRIPT_TAGS = {
    "Zyyy": "DFLT", "Zinh": "DFLT", "Zzzz": "DFLT", "Hira": "kana", "Kana": "kana", "Hrkt": "kana",
    "Lao": "lao ", "Yiii": "yi  ", "Nkoo": "nko ", "Vaii": "vai ", "Hani": "hani", "Hang": "hang",
    "Laoo": "lao ", "Zmth": "math", "Brai": "brai", "Copt": "copt", "Qaac": "copt",
}


def ranges(path, field=1):
    """Yields (first, last, value) for every data line of a UCD range file."""
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            parts = [part.strip() for part in line.split(";")]
            span = parts[0].split("..")
            first = int(span[0], 16)
            last = int(span[-1], 16)
            yield first, last, parts[field] if field < len(parts) else ""


def missing_defaults(path):
    """Yields the @missing default lines of a derived file, in file order."""
    pattern = re.compile(r"#\s*@missing:\s*([0-9A-F]+)\.\.([0-9A-F]+);\s*([^;#]+)")
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            match = pattern.match(line)
            if match:
                yield int(match.group(1), 16), int(match.group(2), 16), match.group(3).strip()


def aliases(ucd, prop):
    """Long and short value names of one property -> short name."""
    result = {}
    with open(os.path.join(ucd, "PropertyValueAliases.txt"), encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            parts = [part.strip() for part in line.split(";")]
            if parts[0] != prop:
                continue
            short = parts[1]
            for name in parts[1:]:
                result[name] = short
    return result


def fill(values, first, last, value):
    for cp in range(first, last + 1):
        values[cp] = value


def derived(ucd, name, prop, order):
    alias = aliases(ucd, prop)
    index = {value: number for number, value in enumerate(order)}
    values = bytearray(MAX_CP)
    path = os.path.join(ucd, name)
    for first, last, value in missing_defaults(path):
        fill(values, first, last, index[alias.get(value, value)])
    for first, last, value in ranges(path):
        fill(values, first, last, index[alias.get(value, value)])
    return values


def flag_set(ucd, name, property_name):
    result = set()
    for first, last, value in ranges(os.path.join(ucd, name)):
        if value == property_name:
            result.update(range(first, last + 1))
    return result


def build_tables(ucd):
    tables = {}
    tables["Bidi"] = derived(ucd, "DerivedBidiClass.txt", "bc", BIDI_CLASSES)

    # Line break: 6-bit class | bit 6 East Asian (F/W/H) | bit 7 Extended_Pictographic & Cn.
    line_break = derived(ucd, "DerivedLineBreak.txt", "lb", LINE_BREAK_CLASSES)
    east_asian = bytearray(MAX_CP)
    for first, last, value in ranges(os.path.join(ucd, "EastAsianWidth.txt")):
        if value in ("F", "W", "H"):
            fill(east_asian, first, last, 1)

    general = derived(ucd, "DerivedGeneralCategory.txt", "gc", GENERAL_CATEGORIES)
    pictographic = flag_set(ucd, "emoji-data.txt", "Extended_Pictographic")
    unassigned = GENERAL_CATEGORIES.index("Cn")
    for cp in range(MAX_CP):
        if east_asian[cp]:
            line_break[cp] |= 0x40
        if cp in pictographic and general[cp] == unassigned:
            line_break[cp] |= 0x80
    tables["LineBreak"] = line_break

    # Grapheme: 4-bit Grapheme_Cluster_Break | bit 4 Extended_Pictographic | bits 5-6 InCB.
    grapheme = bytearray(MAX_CP)
    index = {value: number for number, value in enumerate(GRAPHEME_CLASSES)}
    for first, last, value in ranges(os.path.join(ucd, "GraphemeBreakProperty.txt")):
        fill(grapheme, first, last, index[value])
    for cp in pictographic:
        grapheme[cp] |= 0x10
    with open(os.path.join(ucd, "DerivedCoreProperties.txt"), encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            parts = [part.strip() for part in line.split(";")]
            if len(parts) == 3 and parts[1] == "InCB":
                span = parts[0].split("..")
                for cp in range(int(span[0], 16), int(span[-1], 16) + 1):
                    grapheme[cp] |= INCB_VALUES[parts[2]] << 5
    tables["Grapheme"] = grapheme

    # Script (enum order: Unknown, Common, Inherited, then the rest alphabetically).
    script_alias = aliases(ucd, "sc")
    shorts = sorted({short for short in script_alias.values()})
    scripts = ["Zzzz", "Zyyy", "Zinh"] + [short for short in shorts if short not in ("Zzzz", "Zyyy", "Zinh")]
    script_index = {value: number for number, value in enumerate(scripts)}
    script = bytearray(MAX_CP)
    for first, last, value in ranges(os.path.join(ucd, "Scripts.txt")):
        fill(script, first, last, script_index[script_alias[value]])
    tables["Script"] = script

    # General category (5 bits) | joining type << 5.
    joining = derived(ucd, "DerivedJoiningType.txt", "jt", JOINING_TYPES)
    category = bytearray(MAX_CP)
    for cp in range(MAX_CP):
        category[cp] = general[cp] | (joining[cp] << 5)
    tables["Category"] = category

    # Flags: bit 0 Default_Ignorable_Code_Point, bit 1 Emoji_Presentation, bit 2 Emoji,
    # bit 3 Bidi_Mirrored.
    flags = bytearray(MAX_CP)
    for cp in flag_set(ucd, "DerivedCoreProperties.txt", "Default_Ignorable_Code_Point"):
        flags[cp] |= 1
    for cp in flag_set(ucd, "emoji-data.txt", "Emoji_Presentation"):
        flags[cp] |= 2
    for cp in flag_set(ucd, "emoji-data.txt", "Emoji"):
        flags[cp] |= 4
    with open(os.path.join(ucd, "UnicodeData.txt"), encoding="utf-8") as handle:
        for line in handle:
            parts = line.split(";")
            if parts[9] == "Y":
                flags[int(parts[0], 16)] |= 8
    tables["Flags"] = flags

    # Canonical_Combining_Class.
    combining = bytearray(MAX_CP)
    with open(os.path.join(ucd, "UnicodeData.txt"), encoding="utf-8") as handle:
        previous = None
        for line in handle:
            parts = line.split(";")
            cp = int(parts[0], 16)
            ccc = int(parts[3])
            if parts[1].endswith(", Last>") and previous is not None:
                for value in range(previous, cp + 1):
                    combining[value] = ccc
            combining[cp] = ccc
            previous = cp if parts[1].endswith(", First>") else None
    tables["Combining"] = combining
    return tables, scripts


def two_stage(values):
    best = None
    for shift in (5, 6, 7, 8):
        size = 1 << shift
        blocks = {}
        stage1 = []
        stage2 = bytearray()
        for start in range(0, MAX_CP, size):
            block = bytes(values[start:start + size])
            number = blocks.get(block)
            if number is None:
                number = len(blocks)
                blocks[block] = number
                stage2 += block
            stage1.append(number)
        total = len(stage1) * 2 + len(stage2)
        if best is None or total < best[0]:
            best = (total, shift, stage1, stage2)
    _, shift, stage1, stage2 = best
    encoded = bytearray()
    for number in stage1:
        encoded += bytes((number & 0xFF, number >> 8))
    return shift, encoded, stage2


def byte_literal(data, indent="            "):
    lines = []
    for start in range(0, len(data), 32):
        lines.append(indent + ",".join(str(value) for value in data[start:start + 32]) + ",")
    return "\n".join(lines)


HEADER = """// <auto-generated>
// Generated by Packages/net.sbm.unite.text/Tools~/GenerateUnicodeData.py from the Unicode
// Character Database {version}. Do not edit; regenerate instead.
// </auto-generated>
"""


def write_table(output, name, values):
    shift, stage1, stage2 = two_stage(values)
    path = os.path.join(output, f"UnicodeTables.{name}.Generated.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(HEADER.format(version=UNICODE_VERSION))
        handle.write("using System;\n\nnamespace Unite.Text.Unicode\n{\n")
        handle.write("    internal static partial class UnicodeTables\n    {\n")
        handle.write(f"        private const int {name}Shift = {shift};\n\n")
        handle.write(f"        private static ReadOnlySpan<byte> {name}Stage1 => new byte[]\n        {{\n")
        handle.write(byte_literal(stage1) + "\n        };\n\n")
        handle.write(f"        private static ReadOnlySpan<byte> {name}Stage2 => new byte[]\n        {{\n")
        handle.write(byte_literal(stage2) + "\n        };\n")
        handle.write("    }\n}\n")
    return shift, len(stage1) + len(stage2)


def enum_body(names, indent="        "):
    return "\n".join(f"{indent}{name}," for name in names)


def write_enums(output, scripts, ucd):
    script_alias = aliases(ucd, "sc")
    long_names = {}
    with open(os.path.join(ucd, "PropertyValueAliases.txt"), encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            parts = [part.strip() for part in line.split(";")]
            if len(parts) >= 3 and parts[0] == "sc":
                long_names[parts[1]] = parts[2]

    tags = [OPENTYPE_SCRIPT_TAGS.get(short, short.lower()) for short in scripts]
    path = os.path.join(output, "UnicodeEnums.Generated.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(HEADER.format(version=UNICODE_VERSION))
        handle.write("namespace Unite.Text.Unicode\n{\n")
        handle.write("    /// <summary>Bidi_Class values (UAX #9).</summary>\n")
        handle.write("    internal enum BidiClass : byte\n    {\n" + enum_body(BIDI_CLASSES) + "\n    }\n\n")
        handle.write("    /// <summary>Line_Break classes (UAX #14).</summary>\n")
        handle.write("    internal enum LineBreakClass : byte\n    {\n" + enum_body(LINE_BREAK_CLASSES) + "\n    }\n\n")
        handle.write("    /// <summary>Grapheme_Cluster_Break values (UAX #29).</summary>\n")
        handle.write("    internal enum GraphemeClass : byte\n    {\n"
                     + enum_body(name.replace("_", "") for name in GRAPHEME_CLASSES) + "\n    }\n\n")
        handle.write("    /// <summary>General_Category values.</summary>\n")
        handle.write("    internal enum GeneralCategory : byte\n    {\n" + enum_body(GENERAL_CATEGORIES) + "\n    }\n\n")
        handle.write("    /// <summary>Joining_Type values (ArabicShaping.txt).</summary>\n")
        handle.write("    internal enum JoiningType : byte\n    {\n" + enum_body(JOINING_TYPES) + "\n    }\n\n")
        handle.write("    /// <summary>Script values: Unknown, Common, Inherited, then by ISO 15924 code.</summary>\n")
        handle.write("    internal enum UnicodeScript : byte\n    {\n")
        for short in scripts:
            handle.write(f"        /// <summary>{short} ({long_names.get(short, short)})</summary>\n")
            handle.write(f"        {long_names.get(short, short).replace('_', '')},\n")
        handle.write("    }\n\n")
        handle.write("    internal static partial class UnicodeTables\n    {\n")
        handle.write("        /// <summary>OpenType script tag of each <see cref=\"UnicodeScript\"/> value.</summary>\n")
        handle.write("        internal static readonly string[] OpenTypeScriptTags =\n        {\n")
        for start in range(0, len(tags), 12):
            handle.write("            " + " ".join(f'"{tag}",' for tag in tags[start:start + 12]) + "\n")
        handle.write("        };\n    }\n}\n")


def write_pairs(output, ucd):
    mirrors = []
    for first, _, value in ranges(os.path.join(ucd, "BidiMirroring.txt")):
        mirrors.append((first, int(value, 16)))
    brackets = []
    with open(os.path.join(ucd, "BidiBrackets.txt"), encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            if not line:
                continue
            parts = [part.strip() for part in line.split(";")]
            brackets.append((int(parts[0], 16), int(parts[1], 16), 1 if parts[2] == "o" else 2))

    path = os.path.join(output, "UnicodeTables.Bidi.Pairs.Generated.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(HEADER.format(version=UNICODE_VERSION))
        handle.write("namespace Unite.Text.Unicode\n{\n    internal static partial class UnicodeTables\n    {\n")
        handle.write("        // Bidi_Mirroring_Glyph pairs, sorted by code point.\n")
        handle.write("        private static readonly int[] MirrorSources =\n        {\n")
        handle.write(int_lines([source for source, _ in mirrors]) + "\n        };\n\n")
        handle.write("        private static readonly int[] MirrorTargets =\n        {\n")
        handle.write(int_lines([target for _, target in mirrors]) + "\n        };\n\n")
        handle.write("        // Bidi_Paired_Bracket and Bidi_Paired_Bracket_Type (1 open, 2 close), sorted.\n")
        handle.write("        private static readonly int[] BracketSources =\n        {\n")
        handle.write(int_lines([source for source, _, _ in brackets]) + "\n        };\n\n")
        handle.write("        private static readonly int[] BracketPairs =\n        {\n")
        handle.write(int_lines([pair for _, pair, _ in brackets]) + "\n        };\n\n")
        handle.write("        private static readonly byte[] BracketTypes =\n        {\n")
        handle.write(int_lines([kind for _, _, kind in brackets]) + "\n        };\n")
        handle.write("    }\n}\n")


def write_normalization(output, ucd):
    """Canonical decompositions (pairs / singletons) and the primary compositions."""
    decompositions = []
    with open(os.path.join(ucd, "UnicodeData.txt"), encoding="utf-8") as handle:
        for line in handle:
            parts = line.split(";")
            mapping = parts[5].strip()
            if not mapping or mapping.startswith("<"):
                continue
            pieces = [int(piece, 16) for piece in mapping.split()]
            decompositions.append((int(parts[0], 16), pieces[0], pieces[1] if len(pieces) > 1 else 0))

    excluded = flag_set(ucd, "DerivedNormalizationProps.txt", "Full_Composition_Exclusion")
    compositions = sorted(
        ((first << 21) | second, composite)
        for composite, first, second in decompositions
        if second != 0 and composite not in excluded
    )

    path = os.path.join(output, "UnicodeTables.Normalization.Generated.cs")
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(HEADER.format(version=UNICODE_VERSION))
        handle.write("namespace Unite.Text.Unicode\n{\n    internal static partial class UnicodeTables\n    {\n")
        handle.write("        // Canonical decompositions: code point -> first, second (0 for singletons), sorted.\n")
        handle.write("        private static readonly int[] DecompositionSources =\n        {\n")
        handle.write(int_lines([source for source, _, _ in decompositions]) + "\n        };\n\n")
        handle.write("        private static readonly int[] DecompositionFirsts =\n        {\n")
        handle.write(int_lines([first for _, first, _ in decompositions]) + "\n        };\n\n")
        handle.write("        private static readonly int[] DecompositionSeconds =\n        {\n")
        handle.write(int_lines([second for _, _, second in decompositions]) + "\n        };\n\n")
        handle.write("        // Primary compositions: (first << 21 | second) -> composite, sorted by key.\n")
        handle.write("        private static readonly long[] CompositionKeys =\n        {\n")
        handle.write(int_lines([key for key, _ in compositions]) + "\n        };\n\n")
        handle.write("        private static readonly int[] CompositionValues =\n        {\n")
        handle.write(int_lines([value for _, value in compositions]) + "\n        };\n")
        handle.write("    }\n}\n")


def int_lines(values, indent="            "):
    lines = []
    for start in range(0, len(values), 16):
        lines.append(indent + ", ".join(f"0x{value:04X}" for value in values[start:start + 16]) + ",")
    return "\n".join(lines)


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    ucd, output = sys.argv[1], sys.argv[2]
    tables, scripts = build_tables(ucd)
    for name, values in tables.items():
        shift, size = write_table(output, name, values)
        print(f"{name}: shift {shift}, {size} bytes")
    write_enums(output, scripts, ucd)
    write_pairs(output, ucd)
    write_normalization(output, ucd)


if __name__ == "__main__":
    main()
