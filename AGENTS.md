# AGENTS.md

## Project overview
p5.Unite is a Unity library that brings p5.js-style creative coding to Unity. It provides an immediate-mode drawing API that allows users to draw shapes (Circle, Rectangle, Square) with simple method calls, supporting multiple coordinate systems (ScreenSpace, ClipSpace, WorldSpace).

## Architecture
- **UCanvas**: The main MonoBehaviour component that users attach to a GameObject. It provides the public API for drawing shapes (Circle, Rectangle, Square), setting colors (Fill, Stroke), and managing coordinate transformations (Translate, Rotate, Scale, Push, Pop). Uses partial classes to organize functionality across multiple files.
- **DrawingContext**: Handles low-level rendering using Unity's SRP (Scriptable Render Pipeline). Manages mesh creation, material setup, and dispatches draw commands based on DrawingMode.
- **DrawCommand**: A struct that encapsulates drawing parameters (position, rotation, size, shape type, colors) for each shape to be drawn.
- **DrawingMode**: An enum defining coordinate system modes - ScreenSpaceOverlay, ScreenSpace, ClipSpaceOverlay, ClipSpace, and WorldSpace.
- **CanvasTransformStack**: Implements a matrix stack for hierarchical transformations, similar to p5.js's push()/pop() system.
- **Shaders**: Custom shaders (ClipSpaceDrawing, WorldSpaceDrawing) for rendering shapes in different coordinate spaces.

## Project structure
- `/Assets/Unite/Canvas/`: Core canvas API (UCanvas.cs, Shape.cs, Color.cs, Transform.cs, Screen.cs, Utility.cs)
- `/Assets/Unite/Coordinate/`: Coordinate transformation system (CanvasTransformStack.cs)
- `/Assets/Unite/Drawing/`: Rendering internals (DrawingContext.cs, DrawCommand.cs, DrawingHelper.cs, DrawingMode.cs)
- `/Assets/Unite/Shaders/`: Custom shaders for shape rendering (ClipSpaceDrawing.shader, WorldSpaceDrawing.shader)
- `/Assets/Scripts/Dev/`: Development and testing scripts (CanvasTester.cs, etc.)

## Development rules
- After work, format with `dotnet format Assembly-CSharp.csproj`.
- Use `--include` to specify files as needed.
- Add `sealed` to classes if possible.
- Use C# modern syntax:
  - Prefer get-only auto-properties over readonly fields with properties.
  - Use auto-properties wherever possible to reduce boilerplate code.
- When a file exceeds 300 lines, consider splitting related classes into separate files.
- Unity-specific constraints:
  - Cannot use `dotnet build` command.
  - Check compilation errors through Unity's asset refresh.

## Error checks
- After coding, use the `get_errors` tool to confirm there are no issues.
