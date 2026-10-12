# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Samples~/BasicDrawing`: importable sample scene that draws circles, rounded rectangles and
  curves through the `UCanvas` API.

### Fixed

- Declare the `com.unity.render-pipelines.universal` dependency. The drawing shaders include URP's
  shader library, so the package could not be compiled in a project that has no URP installed.

## [0.1.0] - 2026-10-12

### Added

- Package metadata (`package.json`) and UPM layout: `Runtime`, `Tests`, `CHANGELOG.md`, `README.md`.
- `UniteShaderLibrary` ScriptableObject that keeps serialized references to the drawing shaders so that they are not stripped from player builds.
- `ShaderLibraryTests` verifying that every drawing mode has a shader assigned.
- `UCanvas` assigns the default shader library automatically when the component is added.

### Changed

- Moved the library from `Assets/Unite` to `Packages/net.sbm.unite` so that the repository can be consumed as a Unity package while remaining editable in the Editor.
- `DrawingContext` resolves its shaders through `UniteShaderLibrary` instead of `Shader.Find`, and throws a descriptive exception when a shader cannot be resolved.
