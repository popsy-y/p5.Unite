# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-10-28

### Added

- `Samples~/BasicDrawing`: importable sample scene that draws circles, rounded rectangles and
  curves through the `UCanvas` API.
- Optional `net.sbm.unite.text` package: p5.js-style typography with an SDF glyph atlas,
  TTF/OTF/WOFF/WOFF2 and variable fonts, Unicode 16 analysis, OpenType shaping, font
  fallback and COLRv0 color glyphs.
- `UCanvas.SetCamera()` and the `targetCamera` argument of `CreateCanvas()`.

### Changed

- The per-frame shape data is built by a Burst-compiled kernel (new `com.unity.burst`
  dependency), once per frame instead of once per camera.
- `DrawCommand` holds no references: polygon points and glyph atlases are referred to by
  index (`PolygonPointStart` / `PolygonPointCount`, `GlyphAtlasIndex`). The
  `DrawingContext` constructor is internal.
- `Translate`, `Rotate` and `Scale` update the current matrix in closed form.
- A canvas draws to one target camera instead of every active camera. Passing `null` to
  `SetCamera()` follows `Camera.main`; a destroyed target falls back to it with a warning.
- `UCanvas` runs in Edit mode and draws to the focused Scene View until the Game view is
  focused.

### Fixed

- Declare the `com.unity.render-pipelines.universal` dependency. The drawing shaders include URP's
  shader library, so the package could not be compiled in a project that has no URP installed.
- World and CanvasLocal strokes are placed in bottom-left pixel space instead of being
  offset by half a screen.
- Transparent draws keep submission (painter's) order.

## [0.1.0] - 2026-10-12

### Added

- Package metadata (`package.json`) and UPM layout: `Runtime`, `Tests`, `CHANGELOG.md`, `README.md`.
- `UniteShaderLibrary` ScriptableObject that keeps serialized references to the drawing shaders so that they are not stripped from player builds.
- `ShaderLibraryTests` verifying that every drawing mode has a shader assigned.
- `UCanvas` assigns the default shader library automatically when the component is added.

### Changed

- Moved the library from `Assets/Unite` to `Packages/net.sbm.unite` so that the repository can be consumed as a Unity package while remaining editable in the Editor.
- `DrawingContext` resolves its shaders through `UniteShaderLibrary` instead of `Shader.Find`, and throws a descriptive exception when a shader cannot be resolved.
