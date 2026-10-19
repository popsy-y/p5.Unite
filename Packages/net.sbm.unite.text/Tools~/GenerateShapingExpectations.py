#!/usr/bin/env python3
"""Generates Tests/EditMode/Data/ShapingExpectations.txt with HarfBuzz (uharfbuzz).

Usage: python GenerateShapingExpectations.py <output-file>

Each case shapes one single-script, single-direction run with HarfBuzz using fonts that
ship with Windows (the test skips cases whose font is missing). Default ignorables are
removed (as Unite.Text does). Output lines (tab separated):

    font  variations  direction  script  code points (hex)  glyphs

glyphs = "glyph:cluster:advance:xOffset:yOffset" entries separated by spaces, in logical
order (HarfBuzz's right-to-left output is reversed back), values in font units.
"""

import sys

import uharfbuzz as hb

FONTS = "C:/Windows/Fonts/"

CASES = [
    # Latin: kerning, ligatures, combining marks, contextual alternates.
    ("arial.ttf", "", "ltr", "Latn", "AVATAR Wavy To Yo LT"),
    ("arial.ttf", "", "ltr", "Latn", "e\u0301 a\u0308\u0323 n\u0303"),
    ("calibri.ttf", "", "ltr", "Latn", "office fit flow fft"),
    ("calibri.ttf", "", "ltr", "Latn", "AVATAR Wavy To"),
    ("calibri.ttf", "", "ltr", "Latn", "f\u200Ci f\u200Di"),
    ("segoeui.ttf", "", "ltr", "Latn", "Wavy AVA To fi"),
    ("segoeui.ttf", "", "ltr", "Latn", "a\u0301\u0302 o\u031B\u0323 i\u0307"),
    ("cambria.ttc", "", "ltr", "Latn", "office affluent fjord"),
    ("georgia.ttf", "", "ltr", "Latn", "0123456789 Typography"),
    ("constan.ttf", "", "ltr", "Latn", "Hamburgefonstiv 12345"),
    ("times.ttf", "", "ltr", "Latn", "AWAY Te Yo"),
    ("tahoma.ttf", "", "ltr", "Latn", "AVATAR Wavy"),
    ("times.ttf", "", "ltr", "Grek", "\u0391\u03b8\u03ae\u03bd\u03b1 \u1f00\u03b3\u03b1\u03b8\u03cc\u03c2"),
    ("arial.ttf", "", "ltr", "Cyrl", "\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440"),
    # Arabic: joining forms, lam-alef ligatures, marks, tatweel, ZWJ / ZWNJ.
    ("arial.ttf", "", "rtl", "Arab", "\u0627\u0644\u0633\u0644\u0627\u0645 \u0639\u0644\u064a\u0643\u0645"),
    ("arial.ttf", "", "rtl", "Arab", "\u0628\u0650\u0633\u0652\u0645\u0650 \u0627\u0644\u0644\u0651\u064e\u0647\u0650"),
    ("segoeui.ttf", "", "rtl", "Arab", "\u0645\u0631\u062d\u0628\u0627 \u0628\u0627\u0644\u0639\u0627\u0644\u0645"),
    ("segoeui.ttf", "", "rtl", "Arab", "\u0644\u0627 \u0644\u0623 \u0640\u0628\u0640 \u0628\u200d \u0628\u200c\u0628"),
    ("tahoma.ttf", "", "rtl", "Arab", "\u0641\u0627\u0631\u0633\u06cc \u06af\u0686\u067e\u0698"),
    ("times.ttf", "", "rtl", "Arab", "\u0627\u0644\u0639\u0631\u0628\u064a\u0629 \u0661\u0662\u0663"),
    ("segoeui.ttf", "", "rtl", "Arab", "\u0628\u064e\u0651 \u064a\u064f\u0648\u0652"),
    # Hebrew with points.
    ("arial.ttf", "", "rtl", "Hebr", "\u05e9\u05b8\u05c1\u05dc\u05d5\u05b9\u05dd \u05e2\u05b4\u05d1\u05b0\u05e8\u05b4\u05d9\u05ea"),
    ("segoeui.ttf", "", "rtl", "Hebr", "\u05e9\u05c1\u05bc\u05b8 \u05d1\u05bc\u05b0"),
    ("times.ttf", "", "rtl", "Hebr", "\u05e2\u05d1\u05e8\u05d9\u05ea"),
    # CJK and Hangul.
    ("YuGothM.ttc", "", "ltr", "Hani", "\u65e5\u672c\u8a9e\u306e\u30c6\u30ad\u30b9\u30c8\u3001\u300c\u62ec\u5f27\u300d\u3002"),
    ("malgun.ttf", "", "ltr", "Hang", "\ud55c\uad6d\uc5b4 \ud14d\uc2a4\ud2b8"),
    # More scripts: Armenian, Georgian, Ethiopic, N'Ko (joining), Cherokee, Canadian
    # syllabics, Mongolian (joining), Phags-pa, Yi, Thaana, historic scripts.
    ("sylfaen.ttf", "", "ltr", "Armn", "Հայերեն և"),
    ("sylfaen.ttf", "", "ltr", "Geor", "ქართული"),
    ("ebrima.ttf", "", "ltr", "Ethi", "አማርኛ ፊደል"),
    ("ebrima.ttf", "", "rtl", "Nkoo", "ߒߞߏ ߊ߲ߣ"),
    ("gadugi.ttf", "", "ltr", "Cher", "ᏣᎳᎩ ᎦᏬᏂᎯᏍᏗ"),
    ("gadugi.ttf", "", "ltr", "Cans", "ᐃᓄᒃᑎᑐᑦ"),
    ("monbaiti.ttf", "", "ltr", "Mong", "ᠮᠣᠩᠭᠤᠯ"),
    ("phagspa.ttf", "", "ltr", "Phag", "ꡀꡁꡂꡃ"),
    ("msyi.ttf", "", "ltr", "Yiii", "ꀀꀁꀂꀃ"),
    ("mvboli.ttf", "", "rtl", "Thaa", "ދިވެހި"),
    ("seguihis.ttf", "", "ltr", "Runr", "ᚠᚢᚦᚨᚱᚲ"),
    # Vietnamese stacked marks (precomposed and decomposed), Hangul jamo composition.
    ("arial.ttf", "", "ltr", "Latn", "Tiếng Việt tiếng việt"),
    ("malgun.ttf", "", "ltr", "Hang", "한글"),
    # Emoji: ZWJ sequences, flags, modifiers, keycaps (layout only; colour comes later).
    ("seguiemj.ttf", "", "ltr", "Zyyy", "\U0001f468‍\U0001f469‍\U0001f467 \U0001f1ef\U0001f1f5 \U0001f44d\U0001f3fd 1️⃣"),
    # Variable fonts: HVAR advances, GPOS deltas, FeatureVariations.
    ("SegUIVar.ttf", "wght=700", "ltr", "Latn", "AVATAR Wavy office"),
    ("SegUIVar.ttf", "wght=300", "ltr", "Latn", "Typography fi"),
    ("bahnschrift.ttf", "wdth=75,wght=600", "ltr", "Latn", "AVATAR Wavy"),
    ("SitkaVF.ttf", "opsz=36", "ltr", "Latn", "office affluent"),
]


def shape(font_file, variations, direction, script, text):
    blob = hb.Blob.from_file_path(FONTS + font_file)
    face = hb.Face(blob, 0)
    font = hb.Font(face)
    if variations:
        settings = {}
        for item in variations.split(","):
            tag, value = item.split("=")
            settings[tag] = float(value)
        font.set_variations(settings)

    buffer = hb.Buffer()
    buffer.add_codepoints([ord(char) for char in text])
    buffer.direction = direction
    buffer.script = script
    buffer.language = "dflt"
    buffer.flags = hb.BufferFlags.REMOVE_DEFAULT_IGNORABLES
    hb.shape(font, buffer, {})

    glyphs = list(zip(buffer.glyph_infos, buffer.glyph_positions))
    if direction == "rtl":
        glyphs.reverse()
    return " ".join(
        f"{info.codepoint}:{info.cluster}:{pos.x_advance}:{pos.x_offset}:{pos.y_offset}"
        for info, pos in glyphs
    )


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)

    lines = [
        "# Generated by Tools~/GenerateShapingExpectations.py with HarfBuzz " + hb.__version__ + ". Regenerate rather than edit.",
        "# font\tvariations\tdirection\tscript\tcode points\tglyph:cluster:advance:xOffset:yOffset ...",
    ]
    for font_file, variations, direction, script, text in CASES:
        code_points = " ".join(f"{ord(char):04X}" for char in text)
        glyphs = shape(font_file, variations, direction, script, text)
        lines.append("\t".join([font_file, variations, direction, script, code_points, glyphs]))

    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(len(CASES), "cases")


if __name__ == "__main__":
    main()
