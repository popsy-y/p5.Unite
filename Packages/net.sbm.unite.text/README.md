# p5.Unite Text

Experimental text drawing for [p5.Unite](https://github.com/popsy-y/p5.Unite):

```csharp
// Create a font asset: Create > p5.Unite > Unite Font, then assign a TextAsset
// holding the raw font bytes (a .ttf renamed to .bytes).
canvas.TextFont(unityFont);
canvas.TextSize(24f);
canvas.Text("p5.Unite", 0f, 0f);
float width = canvas.TextWidth("width");
```

Glyphs are rasterized on demand into an SDF atlas cache (per font asset,
fixed-size R8 pages, pow2 em-size buckets) and drawn as quads through the
p5.Unite canvas pipeline, so text participates in fill/stroke styling, depth and
draw ordering like any other shape.

## Current limitations (v1)

- No shaping: one glyph per code point via cmap (formats 4/12/0); kerning, GSUB/GPOS
  and variable-font axes are not applied yet.
- TrueType-outline fonts only (`glyf` tables); CFF-flavored OTF is rejected.
- Horizontal text alignment only; the vertical anchor is always the baseline.
- First use of a glyph rasterizes synchronously; deferred/async baking is a
  follow-up item from the planning docs (`.temporal-text-implementation-docs/`).
