# p5.Unite Text

Experimental text drawing for [p5.Unite](https://github.com/popsy-y/p5.Unite):

## 0.2.0

- First release of the optional text package, shipped with p5.Unite 0.2.0.
- p5.js typography API: `Text`, `TextFont`, `TextSize`, `TextLeading`, `TextAlign`,
  `TextWrap`, `TextDirection`, `TextStyle`, `TextWeight`, `TextProperty`, `TextWidth`,
  `TextAscent` / `TextDescent`, `TextBounds`, and the p5.Font outline functions.
- TTF/OTF/WOFF/WOFF2 and variable fonts, with Unicode 16 analysis, OpenType shaping
  (including Arabic joining, GSUB/GPOS and language/feature settings), font stacks with
  system-font fallback, and COLRv0 color glyphs.
- On-demand SDF atlas cache with Burst-compiled, batched glyph baking and cached shaping.

```csharp
// Load a .ttf / .otf / .woff / .woff2 file (StreamingAssets, an absolute path or a URL),
// or parse bytes you already have with FontFace.LoadFont(byte[]):
FontFace font = await FontFace.LoadFontAsync("Fonts/MyFont.woff2");

canvas.TextFont(font, 24f);
canvas.TextAlign(TextAlignMode.Center, TextVerticalAlignMode.Top);
canvas.Text("p5.Unite", 0f, 0f);
canvas.Text("Wrapped inside a box", 0f, 40f, 200f, 100f);

float width = canvas.TextWidth("width");      // tight ink width
Rect bounds = canvas.TextBounds("bounds", 0f, 0f);
List<TextPoint> points = font.TextToPoints("p5", 0f, 0f, TextPathOptions.FromCanvas(canvas));

// Variable fonts: weight and style map onto the font's axes, other axes by tag.
canvas.TextWeight(650f);
canvas.TextVariation("wdth", 80f);

// Fallback fonts for characters the first font lacks (like a CSS font-family list),
// OpenType features and language as in p5.js 2.x textProperty():
canvas.TextFont(new[] { latinFont, japaneseFont, emojiFont }, 24f);
canvas.TextProperty("fontFeatureSettings", "'smcp' 1, 'liga' 0");
canvas.TextProperty("lang", "tr");
canvas.TextDirection(TextDirectionMode.Auto);
```

Glyphs are rasterized on demand into an SDF atlas cache (per font asset,
fixed-size R8 pages, pow2 em-size buckets) and drawn as quads through the
p5.Unite canvas pipeline, so text participates in fill/stroke styling, depth and
draw ordering like any other shape. Text attributes are saved and restored by
`Push()` / `Pop()`.

The API mirrors the p5.js typography reference: `Text`, `TextFont`, `TextSize`,
`TextLeading`, `TextAlign`, `TextWrap`, `TextDirection`, `TextStyle`, `TextWeight`,
`TextProperty` / `TextProperties`, `TextWidth` / `FontWidth`, `TextAscent` /
`TextDescent`, `FontAscent` / `FontDescent`, `TextBounds` / `FontBounds`, and on the
font (p5.Font) `TextToPoints`, `TextToContours`, `TextToPaths` and `TextToModel`.
See `references/api-checklist/typography.md` in the repository for the mapping.

Screen modes use p5's y-down canvas space; in the World and CanvasLocal modes text
stays upright and lines advance toward -y.

## Writing systems

Text goes through the same steps as in a browser:

- **Unicode 16 analysis**: extended grapheme clusters (UAX #29), the bidirectional
  algorithm with bracket pairs and mirroring (UAX #9), line breaking (UAX #14: CJK
  breaks between characters with the usual kinsoku rules, no-break space, zero-width
  space, soft hyphen) and script runs (UAX #24). Each part passes the Unicode
  conformance test files.
- **OpenType shaping** modeled on HarfBuzz: GSUB lookups 1-8 and GPOS lookups 1-9
  (kerning, cursive attachment, mark-to-base / ligature / mark), lookup flags and mark
  filtering sets, `FeatureVariations` (`rvrn`), device / variation deltas, and the
  legacy `kern` table. Arabic, Syriac, N'Ko, Mongolian, Adlam and the other joining
  scripts get their positional forms; text is normalized (composed or decomposed) to what the font covers. The
  results are checked against HarfBuzz on Windows system fonts.
- **Font fallback**: a cluster is drawn with the first font in `TextFont(...)` that has
  all its characters. Past those, the system fonts (Windows, macOS and Linux font
  folders) are indexed on a worker thread the first time a character is missing and the
  matching font is loaded in the background. Until it arrives the text shows tofu (the
  primary font's .notdef). Set `FontFace.SystemFontFallback = false` to turn this off.
- **Color glyphs**: COLRv0 / CPAL layers (such as Segoe UI Emoji) are drawn in their
  palette colors; the foreground entry uses the fill color, whose alpha applies to all
  layers. ZWJ sequences and flags come from the font's ligatures.

Shaping results are cached per string, font stack, direction, features and language
(about a few microseconds per glyph for new strings), so steady text costs only the
lookup and placement.

## Font formats

Every format ends up as the same structure, so everything above the file reader is
format independent:

| Layer | Implementations |
| --- | --- |
| Container | sfnt (TrueType, OpenType, collections: first font), WOFF (zlib), WOFF2 (Brotli + glyf / hmtx transforms) |
| Outlines | `glyf` (TrueType quadratics, composites, `gvar`), `CFF ` (Type 2 charstrings: cubic curves, subroutines, CID-keyed fonts) |
| Variations | `fvar` axes, `avar`, `gvar` deltas (with IUP), `HVAR` / `MVAR` (phantom points when HVAR is missing) |

`FontFace` is the font you load and pass to `TextFont` (p5.Font); `FontInstance` is the
face at one set of axis values (the default instance for static fonts).
`FontFace.GetInstance` returns it and `FontFace.Axes` lists the variation axes.
`FontFace.Dispose()` releases the atlas textures baked for the font.

Axis values are used exactly as given, so animating an axis is smooth. Every distinct
value needs its own baked glyphs: those of non-default instances are *volatile*, living on
separate atlas pages and expiring two frames after their last use (a glyph that stays in use
moves to the long-lived pages), so the atlas does not grow while an axis animates. The cost
is that glyphs of an animated axis are re-baked every frame (Burst-compiled and parallel,
a fraction of a millisecond per glyph). `FontFace.AtlasPages` exposes the baked atlas textures
for inspection. World and CanvasLocal text is baked at the size it covers on screen (up to
1024 px per em; the largest sizes use 2048 px pages), so it stays sharp when the camera moves closer.

## Current limitations

- Scripts that need reordering or syllable shaping beyond the default OpenType
  features are not shaped correctly: Indic scripts (Devanagari, Bengali, Tamil, ...),
  Thai, Lao, Khmer, Myanmar, Tibetan and similar. They are drawn with the default
  features only, without reordering.
- Only COLRv0 color glyphs. COLRv1 gradients and bitmap emoji fonts (sbix, CBDT: Apple
  Color Emoji, Noto Color Emoji) are not drawn; such fonts give blank glyphs.
- Variation selectors (cmap format 14) are not used to choose glyphs; VS15 / VS16 are
  ignored when picking fonts.
- System font fallback only covers desktop platforms (it needs the font folders). On
  other platforms missing characters stay tofu unless a fallback font is given to
  `TextFont`.
- Variable OpenType/CFF2 fonts are not supported (variable TrueType fonts are).
- `TextStyle()` and `TextWeight()` are synthesized (slant and outline offset) only for
  fonts without matching variation axes; they do not affect the p5.Font outline
  functions.
- Glyphs start baking in Burst jobs while text is laid out and are finished just before
  the canvases draw; a frame that needs many new glyphs at once (first draw of a page of
  text) still takes a few milliseconds.

## Third-party data

The Unicode tables are generated from the Unicode Character Database; see
`Third Party Notices.md`.
