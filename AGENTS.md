# AGENTS.md

## Project Overview

p5.Unite is a Unity library that brings p5.js-style creative coding to Unity. It provides an immediate-mode drawing API that allows users to draw shapes (Circle, Rectangle, Square) with simple method calls, supporting multiple coordinate systems (Screen, NormalizedScreen, World, CanvasLocal).

## Architecture

- **UCanvas**: The main MonoBehaviour component that users attach to a GameObject. Provides the public API for drawing shapes (Circle, Rect, Square), setting colors (Fill, Stroke), and managing coordinate transformations (Translate, Rotate, Scale, Push, Pop). Uses partial classes to organize functionality across multiple files.
- **DrawingContext**: Handles low-level rendering using Unity's SRP (Scriptable Render Pipeline). Manages mesh creation, material setup, and dispatches draw commands via GPU instancing.
- **DrawCommand**: A struct that encapsulates drawing parameters (position, rotation, size, shape type, colors) for each shape to be drawn.
- **DrawingMode**: An enum defining coordinate system modes - ScreenOverlay, Screen, NormalizedScreenOverlay, NormalizedScreen, World, and CanvasLocal.
- **CanvasTransformStack**: Implements a matrix stack for hierarchical transformations, similar to p5.js's push()/pop() system.
- **ShapeData**: GPU-friendly struct with GenerateHLSL attribute for shader data passing.
- **Shaders**: Custom shaders (ScreenDrawing, WorldDrawing, CanvasLocalDrawing) for rendering shapes in different coordinate spaces using SDFs.

## Project Structure

```
/Packages/net.sbm.unite/          - The library itself (embedded UPM package, net.sbm.unite)
  package.json
  Runtime/      - Core canvas API, coordinate system, rendering internals and shaders
    Canvas/       (UCanvas.cs, Shape.cs, Color.cs, Transform.cs, Screen.cs, Utility.cs)
    Coordinate/   (CanvasTransformStack.cs)
    Drawing/      (DrawingContext.cs, DrawCommand.cs, DrawingHelper.cs, DrawingMode.cs, ShapeData.cs, ShaderPropertyIds.cs, UniteShaderLibrary.cs)
    Shaders/      (Custom shaders and HLSL includes: .shader, .hlsl files)
    Resources/    (UniteShaderLibrary.asset - keeps the shaders in player builds)
  Tests/EditMode/ - EditMode tests (assembly Unite.Runtime.Tests)
/Assets/Scripts/Dev/ - Development and testing scripts (CanvasTester.cs, etc. Not shipped)
/Assets/Scripts/Examples/ - Samples that are compiled at all times (Not shipped)
```

The repository root is a Unity project used for development. See
[`PACKAGE_MIGRATION.md`](PACKAGE_MIGRATION.md) for the package layout, the shader
inclusion mechanism and the release steps.

## API Reference

A categorized p5.js compatibility reference is available. Start at the manifest in [`API_CHECKLIST.md`](API_CHECKLIST.md), which links to the category documents under `references/api-checklist/`.

## Build/Lint/Test Commands

### Formatting

```bash
# Format all files in the main project
dotnet format Assembly-CSharp.csproj

# Format specific files only
dotnet format Unite.Runtime.csproj --include Packages/net.sbm.unite/Runtime/Canvas/UCanvas.cs
```

### Building

- **Cannot use `dotnet build`** - Unity projects must be built through Unity Editor
- Compilation errors are checked through Unity's asset refresh system
- Unity version: 6000.2.6f2 (Unity 6)
- Language version: C# 9.0

### Testing

- EditMode tests live in `Packages/net.sbm.unite/Tests/EditMode` (assembly `Unite.Runtime.Tests`). The package is exposed to the Test Runner through `testables` in `Packages/manifest.json`.
- Run them from the Test Runner window, or headlessly:

  ```powershell
  & '<UnityEditor>\Unity.exe' -batchmode -nographics -projectPath '<project>' `
    -runTests -testPlatform EditMode -testResults Logs\TestResults-EditMode.xml `
    -logFile Logs\editmode-tests.log
  ```

- Manual testing via scripts in `/Assets/Scripts/Dev/` (run CanvasTester.cs in Play mode)
- Shaders are only compiled during a real import or a player build. Check `Logs\*.log` for
  `Shader error` entries after moving or renaming anything under `Runtime/Shaders`.
- Shader inclusion in a player build: **Tools > p5.Unite > Verify Shader Inclusion In Build**
  (`Assets/Editor/BuildShaderCheck.cs`, development-only). It also runs headlessly through
  `-executeMethod P5Unite.EditorTools.BuildShaderCheck.Verify` and exits with code 1 on failure.

## Code Style Guidelines

### Naming Conventions

- **Classes/Structs/Enums**: PascalCase (e.g., `DrawingContext`, `DrawCommand`)
- **Public methods/properties**: PascalCase (e.g., `CreateCanvas()`, `DrawingMode`)
- **Private methods**: camelCase (e.g., `checkDrawingContext()`, `finalPos()`)
- **Private fields**: Prefix with underscore + camelCase (e.g., `_drawingContext`, `_commandQueue`)
- **Public fields**: PascalCase (avoid public fields; use properties instead)
- **Constants**: PascalCase
- **Namespaces**: PascalCase with structure `Unite.{Module}` (e.g., `Unite.Canvas`, `Unite.Drawing`)

### Class Design

- Add `sealed` to classes if they are not intended to be inherited
- Use partial classes to organize large classes across multiple files (e.g., `UCanvas`)
- Prefer get-only auto-properties over readonly fields with separate properties
- Use auto-properties wherever possible to reduce boilerplate

### Using Statements

Order and group as follows:

1. System namespaces (`System`, `System.Collections.Generic`)
2. Project namespaces (`Unite.Canvas`, `Unite.Drawing`)
3. Unity namespaces (`UnityEngine`, `UnityEngine.Rendering`)
4. Aliases at the end (`using DH = Unite.Drawing.DrawingHelper;`)

### Formatting

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

### Types and Variables

- Use `var` when type is obvious from right-hand side
- Use explicit types when clarity is needed
- Prefer `Vector2`/`Vector3` over custom point structs
- Use `float` for floating-point values (consistent with Unity)

### Error Handling

- Throw descriptive exceptions for invalid states:

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

- Split files when they exceed 300 lines
- Group related functionality in regions within partial classes
- One main class per file (except for closely-related small classes)

## Post-Development Checklist

- [ ] Run `dotnet format Assembly-CSharp.csproj` to format code
- [ ] Check for compilation errors in Unity Console
- [ ] Verify no missing `meta` files for new assets
- [ ] Test in Play mode with CanvasTester if relevant
