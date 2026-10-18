# p5.Unite Text

Experimental text drawing for [p5.Unite](https://github.com/popsy-y/p5.Unite):

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

## Current limitations (v1)

- No shaping: one glyph per code point via cmap (formats 4/12/0); kerning, GSUB/GPOS
  and bidi are not applied. Right-to-left only reverses the glyph order per line.
- Variable OpenType/CFF2 fonts are not supported (variable TrueType fonts are).
- `TextStyle()` and `TextWeight()` are synthesized (slant and outline offset) only for
  fonts without matching variation axes; they do not affect the p5.Font outline
  functions.
- Glyphs are baked once per frame just before the canvases draw (Burst jobs, all cores);
  a frame that needs many new glyphs at once (first draw of a page of text) still takes
  a few milliseconds.
