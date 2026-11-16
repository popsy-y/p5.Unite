# AGENTS.md

## Project Overview

p5.Unite is a Unity library that brings p5.js-style creative coding to Unity. It provides an immediate-mode drawing API
(`UCanvas`) for shapes, curves, custom paths and text, drawn through SDF shaders in several coordinate systems
(Screen, NormalizedScreen, World, CanvasLocal).

Built with Unity 6000.2.6f2 (Unity 6) and URP 17. Language version: C# 9.0.

The library ships as two UPM packages embedded in this repository:

| Package | Assembly / namespace | Contents |
| --- | --- | --- |
| `net.sbm.unite` (`Packages/net.sbm.unite`) | `Unite.Runtime` / `Unite.Canvas`, `Unite.Coordinate`, `Unite.Drawing` | Canvas API, transforms, rendering, shaders |
| `net.sbm.unite.text` (`Packages/net.sbm.unite.text`) | `Unite.Text` / `Unite.Text.*` | Optional text drawing: font loading, shaping, SDF glyph atlas. Depends on `net.sbm.unite` |

## Architecture

- **UCanvas**: The MonoBehaviour users attach to a GameObject. Provides the public drawing API (shapes, curves,
  `BeginShape()` paths), styles (Fill, Stroke, StrokeWeight, ...) and transforms (Translate, Rotate, Scale, Push,
  Pop, ...). A partial class split across `Runtime/Canvas/*.cs`.
- **DrawingContext**: Low-level rendering. Owns the mesh, materials and GPU buffers and submits all shapes of a canvas
  in one indirect instanced draw (`DrawingExecutionMode.Indirect`) to the canvas's target camera.
- **DrawCommand / DrawCommandBuffer**: One queued shape as a plain value, and the frame's commands plus the data they
  refer to by index (polygon points, glyph atlas textures).
- **ShapeDataBuilder / ShapeData**: A Burst-compiled kernel converts draw commands into per-instance GPU data.
  `ShapeData` must match the struct in `UniteCommon.hlsl`.
- **DrawingMode**: Coordinate system modes - ScreenOverlay, Screen, NormalizedScreenOverlay, NormalizedScreen, World
  and CanvasLocal.
- **CanvasTransformStack / MatrixMath**: The p5.js-style push()/pop() matrix stack, and closed-form matrix products
  used on every shape.
- **UniteShaderLibrary**: A ScriptableObject in `Runtime/Resources` that references the shaders so they are included
  in player builds (see [Shaders in player builds](#shaders-in-player-builds)).
- **Shaders**: ScreenDrawing, WorldDrawing and CanvasLocalDrawing render shapes with SDFs (`SDFs.hlsl`,
  `UniteCommon.hlsl`).
- **Text (`net.sbm.unite.text`)**: `UCanvas` extension methods (`TextCanvasExtensions`) for p5.js typography,
  `FontFace` / `FontInstance` for fonts (TrueType, CFF, WOFF/WOFF2, variable fonts), OpenType shaping, Unicode
  bidi/line breaking, and an SDF glyph atlas baked with Burst jobs. Glyphs are drawn through the core canvas pipeline.

## Project Structure

The repository root is a Unity project used for development. Folders directly under `Packages/` are embedded packages,
so they need no entry in `manifest.json` and are edited like normal assets.

```
/Packages/net.sbm.unite/            - Core package (shipped)
  Runtime/
    Canvas/        - UCanvas partial class: shapes, curves, paths, styles, transforms, camera
    Coordinate/    - CanvasTransformStack, MatrixMath
    Drawing/       - DrawingContext, draw commands, ShapeData(Builder), DrawingMode, UniteShaderLibrary
    Shaders/       - .shader and .hlsl files
    Resources/     - UniteShaderLibrary.asset (keeps the shaders in player builds)
  Tests/EditMode/  - EditMode tests (assembly Unite.Runtime.Tests)
  Samples~/BasicDrawing/ - Importable sample
  CHANGELOG.md
/Packages/net.sbm.unite.text/       - Text package (shipped; changelog lives in its README.md)
  Runtime/         - Atlas/, Canvas/, Fonts/, Paths/, Raster/, Shaping/, Unicode/
  Tests/EditMode/  - EditMode tests (assembly Unite.Text.Tests)
  Tools~/          - Python generators for the Unicode tables and shaping test data (not imported by Unity)
/Assets/Scripts/Dev/      - Development scripts: CanvasTester etc. (assembly Unite.Dev, not shipped)
/Assets/Scripts/Examples/ - Examples that are always compiled (assembly Unite.Examples, not shipped)
/Assets/Editor/           - Development-only editor tools: BuildShaderCheck, TextPerfBuild (not shipped)
/Assets/Scenes/           - Unite.unity (shapes), Unite_Text.unity (text)
```

Both test assemblies are exposed to the Test Runner through `testables` in `Packages/manifest.json`.

### Where to put things

| What | Where |
| --- | --- |
| Library implementation | `Packages/net.sbm.unite/Runtime`, `Packages/net.sbm.unite.text/Runtime` |
| Scripts for trying things locally | `Assets/Scripts/Dev` |
| Always-compiled examples | `Assets/Scripts/Examples` |
| Samples shipped with the package | `Packages/net.sbm.unite/Samples~` (see below) |

### Shaders in player builds

`Shader.Find` is not recorded as a build dependency, so the shaders would be stripped from player builds.
`Runtime/Resources/UniteShaderLibrary.asset` references all three shaders; being in `Resources`, it and the shaders it
references always make it into a build. `UCanvas` references it through `_shaderLibrary` (set automatically in
`Reset()` / `OnValidate()`) and falls back to `UniteShaderLibrary.Instance` (`Resources.Load`).

- When adding or replacing a shader, update the references in `UniteShaderLibrary.asset`. `ShaderLibraryTests`
  checks that every `DrawingMode` has a shader.
- `_UNITE_DEPTH_PREPASS` is `multi_compile_local`, so all variants are built. Switching it to `shader_feature` would
  need a ShaderVariantCollection.
- Shader `#include` paths are package paths (`Packages/net.sbm.unite/Runtime/Shaders/...`) and break when files move.

### Samples (`Samples~`)

Unity ignores folders ending in `~`, so nothing in `Samples~` is compiled until a user imports the sample (Package
Manager > Samples > Import copies it to `Assets/Samples/<name>/<version>/`, which is gitignored). A broken sample goes
unnoticed until then. When adding or changing a sample:

1. Get it working in an always-compiled place such as `Assets/Scripts/Examples`
2. Put it under `Samples~/<SampleName>` **together with its `.meta` files** (they keep the GUIDs on import)
3. Add `displayName` / `description` / `path` to the `samples` array in `package.json`
4. Compile check: copy the sample to a temporary folder such as `Assets/__SampleBuild`, recompile, confirm there are no
   errors, then delete the folder

## API Reference

A categorized p5.js compatibility reference is available. Start at the manifest in [`API_CHECKLIST.md`](API_CHECKLIST.md),
which links to the category documents under `references/api-checklist/`. When an API is added or its status changes,
update the category document and the manifest totals together.

## Build/Lint/Test Commands

### Assemblies and project files

Unity generates one `.csproj` per assembly at the repository root. They are gitignored and regenerated by Unity on
asset refresh, so they only exist after the project has been opened in the editor.

| csproj | Source |
| --- | --- |
| `Unite.Runtime.csproj` | `Packages/net.sbm.unite/Runtime` |
| `Unite.Runtime.Tests.csproj` | `Packages/net.sbm.unite/Tests/EditMode` |
| `Unite.Text.csproj` | `Packages/net.sbm.unite.text/Runtime` |
| `Unite.Text.Tests.csproj` | `Packages/net.sbm.unite.text/Tests/EditMode` |
| `Unite.Dev.csproj` | `Assets/Scripts/Dev` |
| `Unite.Examples.csproj` | `Assets/Scripts/Examples` |
| `Assembly-CSharp-Editor.csproj` | `Assets/Editor` |

`Samples~` has no csproj because it is never compiled in this project.

### Formatting

```pwsh
# Format the files you changed (preferred)
dotnet format Unite.Runtime.csproj --include Packages/net.sbm.unite/Runtime/Canvas/UCanvas.cs

# Format a whole assembly
dotnet format Unite.Text.csproj
```

Do not run `dotnet format` on `p5.Unite.sln`: it contains every Unity package in `Library/PackageCache`.

- Use 4 spaces for indentation (no tabs)
- Opening braces on new lines for classes, methods, and control structures
- Use expression-bodied members for simple one-line methods:

  ```csharp
  public void Fill(Color color) => _fillColor = color;
  ```

- Use traditional block-style namespaces (not file-scoped):

  ```csharp
  namespace Unite.Canvas
  {
      public class MyClass { }
  }
  ```

### Building

`dotnet build` works on the generated project files and is a quick C# check that needs no running editor:

```pwsh
dotnet build Unite.Runtime.csproj
dotnet build Unite.Text.csproj
```

It is not a replacement for Unity's compile:

- The csproj lists source files explicitly. A file you just added, renamed or deleted is not reflected until Unity
  regenerates the project files
- It does not compile shaders, run the asset pipeline or check `Samples~`

Unity's own compile (`unity command script recompile`, see below) is the source of truth.

### Testing

- EditMode tests: `Packages/net.sbm.unite/Tests/EditMode` (`Unite.Runtime.Tests`) and
  `Packages/net.sbm.unite.text/Tests/EditMode` (`Unite.Text.Tests`). Run them with `unity command test run` (below) or
  from the Test Runner window
- Two failures are pre-existing and not regressions:
  `Unite.Tests.CurveApiTests.BezierTangentMatchesFiniteDifference` (tolerance) and
  `Unite.Tests.TransformApiTests.ScaleOverloadsFollowP5DefaultsAndPreserveNegativeScale`
- Manual testing: run the scenes in `Assets/Scenes` (CanvasTester in `Assets/Scripts/Dev`) in Play mode
- Shaders are only compiled during a real import or a player build. Check `unity command console get --level error`
  (or `Logs\*.log` in batch mode) for `Shader error` entries after changing anything under `Runtime/Shaders`
- Shader inclusion in a player build: **Tools > p5.Unite > Verify Shader Inclusion In Build**
  (`Assets/Editor/BuildShaderCheck.cs`). It builds `Assets/Scenes/Unite.unity` and checks that the three shaders and
  `UniteShaderLibrary.asset` are in the build report. Headlessly (no editor may have the project open; find the
  editor path with `unity editors`):

  ```pwsh
  & '<Editor>\Unity.exe' -batchmode -nographics -quit -projectPath . `
    -executeMethod P5Unite.EditorTools.BuildShaderCheck.Verify -logFile Logs\shader-check.log
  ```

  On failure it logs `SHADERCHECK_MISSING=...` and exits with code 1

## Unity CLI

The Unity CLI (`unity`) drives the editor from the terminal. `com.unity.pipeline` exposes a local HTTP API that the CLI
talks to, so a running editor can be queried and controlled without touching its window.

The editor version is resolved from `ProjectSettings/ProjectVersion.txt`, so no command needs it spelled out. Commands
are grouped (`unity command` lists the groups, `unity command <group>` lists a group's commands and flags).

Every command takes `--json` for structured output. Exit codes: 0 success, 1 error, 6 test failure.

### Starting and stopping the editor

Work in batch mode by default. Launching a windowed editor is allowed when needed (e.g. visual checks).

```pwsh
unity open . --batchmode --until-ready   # no window; keeps running until closed
unity open .                             # windowed editor, only when needed
unity close .                            # exits without saving
```

`unity command` finds the editor from the current directory (subdirectories work too), so run it from inside the
project or worktree. Pass `--project-path <path>` only when the current directory is outside the project: there, the
CLI silently connects to whichever single editor is running.

### Checking your work

**`unity command script recompile` works while the editor is unfocused or minimized.**
Bringing Unity to the foreground is not required to compile-check a change.

```pwsh
unity command script recompile           # force a script recompile
unity command script recompile_status    # idle | triggered | compiling | completed | up_to_date
unity command console get --level error --tail 20
unity command console get --tail 50      # all levels, with stack traces
unity command console status             # compile-failure flag and error/warning counts
unity command test run --mode editor     # EditMode tests; --filter <name> to narrow down
```

`console get` reads the Player's output too, not just the editor's, and takes a cursor via `--since` to follow along.

### Inspecting and driving the project

Destructive commands require `--confirm true` and accept `--dry-run true`.

```pwsh
unity status                         # connected editors: port, project, state, PID
unity command asset find --type Shader --limit 20
unity command scene list_open
unity command scene open --path Assets/Scenes/Unite_Text.unity
unity command editor.playmode start  # also editor.playmode pause / stop
unity command capture game_view --output-path <path inside the project>
unity command package add --name <name@version> --confirm true --wait true
unity command package remove --name <name> --confirm true --wait true
```

`unity command script eval` runs C# inside the live editor through Roslyn — no project recompile, no domain reload. Use
it for one-off inspection; promote anything repeated to a `[CliCommand]` method so it lives in source control and can
be reviewed once instead of per-call.

```pwsh
unity command script eval --code 'return UnityEditor.AssetDatabase.FindAssets("t:Shader", new[] { "Packages/net.sbm.unite" }).Length;'
```

### Driving a build

`unity command --runtime <name>` connects to a running Player, so a build can be inspected and driven through the same
command interface. If no matching Player is found, the command fails; it does not fall back to the editor.

The runtime server is configured under **Project Settings > Pipeline > Runtime** (stored in
`ProjectSettings/Packages/com.unity.pipeline/RuntimePipelineConfig.json`, or set with
`unity command settings.runtime_pipeline ...`). It starts in a Player when both `autoStart` and `enableInBuilds` are
enabled.

- At build time the package bakes the configuration into a transient asset under `Assets/Settings/Pipeline/Resources/`
  and removes it afterwards. Do not commit it; a leftover from an interrupted build is safe to delete
- The runtime server is not gated on Development Build. Enabling `enableInBuilds` for a non-Development build logs a
  security warning, and in an interactive editor opens a modal dialog that blocks API-driven builds. For CLI-driven
  builds, use a Development Build or a batch-mode build
- `eval` and hot reload are only implemented under `UNITY_EDITOR || (UNITY_STANDALONE && DEBUG)`; elsewhere they return
  `Platform Not Supported`

### Caveats

- The CLI and `com.unity.pipeline` move together. After updating the CLI, an editor still on an older package rejects
  every command with "too old to parse command lines". Run `unity pipeline upgrade`; it edits `Packages/manifest.json`
  and `packages-lock.json`, so commit that separately
- When the project has compile errors at editor startup, the editor boots into Safe Mode, the Pipeline package does not
  load and `unity command` cannot connect. Fix the errors (`dotnet build` helps here) and restart the editor
- `capture game_view` / `capture scene_view` only write `--output-path` inside the project root; a scratchpad path is
  rejected
- Modal dialogs in the editor block the API — anything waiting on one hangs
- Editing an asset on disk while the editor holds it in memory gets overwritten on the editor's next save. Change those
  through the CLI or the Inspector

## Code Style Guidelines

### Naming Conventions

- **Classes/Structs/Enums**: PascalCase (e.g., `DrawingContext`, `DrawCommand`)
- **Methods** (public and private): PascalCase (e.g., `CreateCanvas()`, `CheckDrawingContext()`)
- **Public properties**: PascalCase (e.g., `Width`)
- **Private fields**: Prefix with underscore + camelCase (e.g., `_drawingContext`, `_commandQueue`)
- **Public fields**: PascalCase (avoid public fields; use properties instead)
- **Constants**: PascalCase
- **Namespaces**: PascalCase with structure `Unite.{Module}` (e.g., `Unite.Canvas`, `Unite.Drawing`, `Unite.Text.Fonts`)

### Class Design

- Add `sealed` to classes if they are not intended to be inherited
- Use partial classes to organize large classes across multiple files (e.g., `UCanvas`)
- Prefer get-only auto-properties over readonly fields with separate properties
- Use auto-properties wherever possible to reduce boilerplate
- Text features extend `UCanvas` from `net.sbm.unite.text` through extension methods; the core package must not
  depend on the text package

### Using Statements

Order and group as follows:

1. System namespaces (`System`, `System.Collections.Generic`)
2. Project namespaces (`Unite.Canvas`, `Unite.Drawing`)
3. Unity namespaces (`UnityEngine`, `UnityEngine.Rendering`)
4. Aliases at the end (`using DH = Unite.Drawing.DrawingHelper;`)

### Types and Variables

- Use `var` when type is obvious from right-hand side
- Use explicit types when clarity is needed
- Prefer `Vector2`/`Vector3` over custom point structs
- Use `float` for floating-point values (consistent with Unity)

### Error Handling

- Throw descriptive exceptions for invalid states, prefixed with `p5.Unite:`:

  ```csharp
  throw new NullReferenceException("p5.Unite: Canvas not initialized. Call CreateCanvas() first.");
  ```

- Use `Debug.LogWarning()` for recoverable issues (e.g., matrix stack underflow)
- Guard clauses at the start of methods (e.g., `CheckDrawingContext()`)

### Comments

- Use XML documentation comments for public APIs:

  ```csharp
  /// <summary>
  /// Converts a world-space depth to a 0..1 linear depth value.
  /// </summary>
  ```

- Inline comments in English for complex logic
- Use Japanese comments only for notes about intentional design decisions

### File Organization

- Consider splitting files when they exceed 300 lines
- Group related functionality in regions within partial classes
- One main class per file (except for closely-related small classes)

## Recommended workflow

1. `unity command script recompile`, then poll `unity command script recompile_status` until `completed` or
   `up_to_date`
2. `unity command console get --level error` — fix what it reports
3. If relevant, run the tests (`unity command test run --mode editor`, `--filter` to narrow down) and check the scenes
   in Play mode. After shader changes, also look for `Shader error` in the console
4. `dotnet format <Assembly>.csproj --include <changed files>`

## Release

1. Bump `version` in `package.json` of each changed package (Semantic Versioning). When the text package needs a newer
   core, raise its `net.sbm.unite` dependency too
2. Add the changes to `Packages/net.sbm.unite/CHANGELOG.md` / the version section of `Packages/net.sbm.unite.text/README.md`
3. Commit, tag `vX.Y.Z` and push
4. Users install from Git:

   ```
   https://github.com/popsy-y/p5.Unite.git?path=Packages/net.sbm.unite#vX.Y.Z
   https://github.com/popsy-y/p5.Unite.git?path=Packages/net.sbm.unite.text#vX.Y.Z
   ```

The repository is currently private, so the `https://` URL returns 404 without credentials. Use the SSH form
(`git@github.com:popsy-y/p5.Unite.git?path=...`) or a token. To try the working tree without pushing, reference it from
another project's `manifest.json` as `"net.sbm.unite": "file:<path to repo>/Packages/net.sbm.unite"`.
