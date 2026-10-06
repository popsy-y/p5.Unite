using System;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    [RequireComponent(typeof(UCanvas))]
    public sealed class CanvasTester : MonoBehaviour
    {
        public enum TestPage
        {
            Primitives,
            ModesAndStyles,
            Stress,
            Transforms,
            Depth,
            Curves
        }

        private static readonly ShapeMode[] ShapeModes =
        {
            ShapeMode.Corner,
            ShapeMode.Center,
            ShapeMode.Corners,
            ShapeMode.Radius
        };

        private const float PlanarExtent = 6f;

        [Header("Rendering")]
        [SerializeField] private DrawingMode _mode = DrawingMode.Screen;
        [SerializeField] private DrawingExecutionMode _executionMode = DrawingExecutionMode.Indirect;
        [SerializeField] private bool _enableDepth = true;
        [SerializeField] private bool _debugShapeBounds;
        [SerializeField] private bool _autoCycleModes;
        [SerializeField] private float _cycleInterval = 3f;

        [Header("Test Page")]
        [SerializeField] private TestPage _page = TestPage.Primitives;
        [SerializeField] private bool _autoCyclePages;
        [SerializeField] private float _pageCycleInterval = 5f;
        [SerializeField] private bool _animateTransforms = true;

        [Header("Stress Test")]
        [SerializeField][Min(1)] private int _stressShapeCount = 1000;

        private UCanvas _canvas;
        private DrawingMode _currentMode;
        private DrawingExecutionMode _currentExecutionMode;
        private bool _currentEnableDepth;
        private bool _currentDebugShapeBounds;
        private float _modeTimer;
        private float _pageTimer;

        private bool IsScreen => _mode is
            DrawingMode.Screen or DrawingMode.ScreenOverlay;

        private bool IsNormalizedScreen => _mode is
            DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay;

        private void Start()
        {
            _canvas = GetComponent<UCanvas>();
            _currentMode = _mode;
            _currentExecutionMode = _executionMode;
            _currentEnableDepth = _enableDepth;
            _currentDebugShapeBounds = _debugShapeBounds;
            CreateCanvas();
        }

        private void Update()
        {
            UpdateAutomaticCycling();
            ApplyRenderingSettings();
            _canvas.ResetTransform();

            switch (_page)
            {
                case TestPage.ModesAndStyles:
                    DrawModesAndStyles();
                    break;
                case TestPage.Stress:
                    DrawStressTest();
                    break;
                case TestPage.Transforms:
                    DrawTransforms();
                    break;
                case TestPage.Depth:
                    DrawDepthTest();
                    break;
                case TestPage.Curves:
                    DrawCurves();
                    break;
                default:
                    DrawPrimitives();
                    break;
            }
        }

        private void CreateCanvas()
        {
            _canvas.CreateCanvas(_mode, _executionMode);
            _canvas.DebugShapeBounds = _debugShapeBounds;
            _canvas.EnableDepth = _enableDepth;
        }

        private void ApplyRenderingSettings()
        {
            if (_mode != _currentMode || _executionMode != _currentExecutionMode)
            {
                _currentMode = _mode;
                _currentExecutionMode = _executionMode;
                CreateCanvas();
            }

            if (_enableDepth != _currentEnableDepth)
            {
                _currentEnableDepth = _enableDepth;
                _canvas.EnableDepth = _enableDepth;
            }

            if (_debugShapeBounds == _currentDebugShapeBounds)
                return;

            _currentDebugShapeBounds = _debugShapeBounds;
            _canvas.DebugShapeBounds = _debugShapeBounds;
        }

        private void UpdateAutomaticCycling()
        {
            if (_autoCycleModes)
            {
                _modeTimer += Time.unscaledDeltaTime;
                if (_modeTimer >= Mathf.Max(0.1f, _cycleInterval))
                {
                    _modeTimer = 0f;
                    int modeCount = Enum.GetValues(typeof(DrawingMode)).Length;
                    _mode = (DrawingMode)(((int)_mode + 1) % modeCount);
                }
            }

            if (!_autoCyclePages)
                return;

            _pageTimer += Time.unscaledDeltaTime;
            if (_pageTimer < Mathf.Max(0.1f, _pageCycleInterval))
                return;

            _pageTimer = 0f;
            int pageCount = Enum.GetValues(typeof(TestPage)).Length;
            _page = (TestPage)(((int)_page + 1) % pageCount);
        }

        private void DrawPrimitives()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.012f);
            float angle = _animateTransforms ? Mathf.Sin(Time.time) * 0.35f : 0.2f;

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
            SetStyle(new Color(0.15f, 0.75f, 0.95f, 0.8f), DarkColor(), strokeWeight);

            DrawArc(Position(0.125f, 0.82f), unit, ArcMode.Open);
            DrawArc(Position(0.375f, 0.82f), unit, ArcMode.Chord);
            DrawArc(Position(0.625f, 0.82f), unit, ArcMode.Pie);

            _canvas.Push();
            _canvas.Translate(Position(0.875f, 0.82f));
            _canvas.Rotate(angle);
            _canvas.Scale(1.45f, 0.65f, 1f);
            _canvas.Ellipse(Vector3.zero, Vector2.one * unit * 0.14f);
            _canvas.Pop();

            _canvas.Fill(new Color(0.95f, 0.45f, 0.25f, 0.8f));
            _canvas.Circle(Position(0.125f, 0.5f), unit * 0.15f);
            _canvas.Ellipse(Position(0.375f, 0.5f), new Vector2(unit * 0.19f, unit * 0.12f));
            _canvas.Rect(
                Position(0.625f, 0.5f),
                new Vector2(unit * 0.19f, unit * 0.13f),
                unit * 0.03f
            );

            _canvas.NoStroke();
            _canvas.Square(Position(0.875f, 0.5f), unit * 0.14f, unit * 0.025f);

            _canvas.Stroke(DarkColor());
            _canvas.StrokeWeight(strokeWeight);
            _canvas.NoFill();
            DrawLine(Position(0.055f, 0.14f), Position(0.195f, 0.28f));

            _canvas.StrokeWeight(Mathf.Max(strokeWeight, unit * 0.025f));
            _canvas.Point(Position(0.31f, 0.21f));

            _canvas.StrokeWeight(strokeWeight);
            _canvas.Fill(new Color(0.65f, 0.85f, 0.25f, 0.8f));
            DrawTriangle(
                Position(0.43f, 0.13f),
                Position(0.57f, 0.13f),
                Position(0.5f, 0.31f)
            );
            DrawQuad(
                Position(0.68f, 0.13f),
                Position(0.82f, 0.16f),
                Position(0.79f, 0.31f),
                Position(0.65f, 0.27f)
            );
        }

        private void DrawModesAndStyles()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.01f);
            SetStyle(new Color(0.25f, 0.7f, 1f, 0.75f), DarkColor(), strokeWeight);
            for (int index = 0; index < ShapeModes.Length; index++)
            {
                float x = 0.125f + index * 0.25f;
                DrawEllipseMode(
                    ShapeModes[index],
                    Position(x, 0.78f),
                    new Vector2(unit * 0.16f, unit * 0.1f)
                );
            }

            _canvas.Fill(new Color(1f, 0.55f, 0.25f, 0.75f));
            for (int index = 0; index < ShapeModes.Length; index++)
            {
                float x = 0.125f + index * 0.25f;
                DrawRectMode(
                    ShapeModes[index],
                    Position(x, 0.5f),
                    new Vector2(unit * 0.16f, unit * 0.11f)
                );
            }

            _canvas.EllipseMode = ShapeMode.Center;
            _canvas.RectMode = ShapeMode.Center;

            _canvas.NoFill();
            _canvas.Stroke(new Color(0.2f, 1f, 0.65f));
            _canvas.StrokeWeight(strokeWeight * 1.5f);
            _canvas.Circle(Position(0.125f, 0.2f), unit * 0.14f);

            _canvas.NoStroke();
            _canvas.Fill(new Color(1f, 0.3f, 0.65f, 0.8f));
            _canvas.Square(Position(0.375f, 0.2f), unit * 0.14f, unit * 0.035f);

            _canvas.Stroke(DarkColor());
            _canvas.StrokeWeight(strokeWeight);
            _canvas.Fill(new Color(0.75f, 0.9f, 0.25f, 0.8f));
            _canvas.Push();
            _canvas.Translate(Position(0.625f, 0.2f));
            _canvas.Rotate(_animateTransforms ? Time.time * 0.6f : 0.4f);
            _canvas.Scale(1.35f, 0.7f, 1f);
            _canvas.Rect(Vector3.zero, new Vector2(unit * 0.13f, unit * 0.1f), unit * 0.02f);
            _canvas.Pop();

            _canvas.Fill(new Color(0.65f, 0.45f, 1f, 0.8f));
            _canvas.Rect(
                Position(0.875f, 0.2f),
                new Vector2(unit * 0.18f, unit * 0.12f),
                new Vector4(unit * 0.01f, unit * 0.025f, unit * 0.045f, 0f)
            );
        }

        private void DrawStressTest()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.002f);
            int shapeCount = Mathf.Max(1, _stressShapeCount);

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
            _canvas.Stroke(new Color(0.02f, 0.04f, 0.08f, 0.65f));
            _canvas.StrokeWeight(strokeWeight);

            for (int index = 0; index < shapeCount; index++)
            {
                float x = 0.03f + Mathf.Repeat((index + 1) * 0.61803398875f, 1f) * 0.94f;
                float y = 0.04f + Mathf.Repeat((index + 1) * 0.754877666f, 1f) * 0.92f;
                float variation = Mathf.Repeat((index + 1) * 0.414213562f, 1f);
                float size = Mathf.Lerp(unit * 0.006f, unit * 0.022f, variation);
                Color color = Color.HSVToRGB(Mathf.Repeat(index * 0.071f, 1f), 0.65f, 0.95f);
                color.a = 0.75f;
                _canvas.Fill(color);

                Vector3 position = Position(x, y);
                switch (index % 4)
                {
                    case 0:
                        _canvas.Circle(position, size);
                        break;
                    case 1:
                        _canvas.Ellipse(position, new Vector2(size * 1.5f, size));
                        break;
                    case 2:
                        _canvas.Rect(position, new Vector2(size * 1.6f, size), size * 0.2f);
                        break;
                    default:
                        _canvas.Square(position, size, size * 0.15f);
                        break;
                }
            }
        }

        private void DrawDepthTest()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.01f);
            Vector2 shapeSize = new Vector2(unit * 0.17f, unit * 0.17f);
            var nearColor = new Color(0.15f, 0.75f, 0.95f, 1f);
            var farColor = new Color(0.95f, 0.3f, 0.35f, 1f);
            bool screenDepth = IsScreen || IsNormalizedScreen;
            float nearDepth = screenDepth ? 4f : -unit * 0.04f;
            float farDepth = screenDepth ? 6f : unit * 0.04f;

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
            _canvas.StrokeWeight(strokeWeight);

            // Same depth: the last drawn color must win regardless of depth testing.
            const int tieCount = 12;
            Vector3 tiePosition = Position(0.18f, 0.62f);
            for (int index = 0; index < tieCount; index++)
            {
                _canvas.NoStroke();
                _canvas.Fill(Color.HSVToRGB(Mathf.Repeat(index * 0.083f, 1f), 0.7f, 0.95f));
                _canvas.Rect(tiePosition, shapeSize, shapeSize.x * 0.12f);
            }

            // Near drawn first, far drawn second.
            // With depth on the near color must survive; with depth off the far color wins.
            _canvas.Fill(nearColor);
            _canvas.Stroke(DarkColor());
            _canvas.Rect(PositionWithDepth(0.5f, 0.62f, nearDepth), shapeSize, shapeSize.x * 0.12f);
            _canvas.Fill(farColor);
            _canvas.Rect(PositionWithDepth(0.56f, 0.62f, farDepth), shapeSize, shapeSize.x * 0.12f);

            // Far drawn first, near drawn second: the near color wins in both settings.
            _canvas.Fill(farColor);
            _canvas.Rect(PositionWithDepth(0.8f, 0.55f, farDepth), shapeSize, shapeSize.x * 0.12f);
            _canvas.Fill(nearColor);
            _canvas.Rect(PositionWithDepth(0.86f, 0.55f, nearDepth), shapeSize, shapeSize.x * 0.12f);
        }

        private Vector3 PositionWithDepth(float normalizedX, float normalizedY, float depth)
        {
            Vector3 position = Position(normalizedX, normalizedY);
            position.z = IsScreen || IsNormalizedScreen
                ? _canvas.WorldToLinearDepth(depth, Camera.main)
                : depth;
            return position;
        }

        private void DrawCurves()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.012f);

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
            _canvas.NoFill();
            _canvas.StrokeWeight(strokeWeight);

            // Curves on the top row: Bezier, spline INCLUDE and spline EXCLUDE.
            _canvas.Stroke(new Color(0.2f, 0.85f, 1f, 0.95f));
            _canvas.Bezier(
                Position(0.08f, 0.9f),
                Position(0.2f, 0.62f),
                Position(0.32f, 0.62f),
                Position(0.44f, 0.9f)
            );

            _canvas.Stroke(new Color(0.55f, 1f, 0.4f, 0.95f));
            _canvas.SplineEnds = SplineEndMode.Include;
            _canvas.Spline(
                Position(0.56f, 0.9f),
                Position(0.66f, 0.66f),
                Position(0.78f, 0.94f),
                Position(0.9f, 0.7f)
            );

            _canvas.Stroke(new Color(1f, 0.7f, 0.3f, 0.95f));
            _canvas.SplineEnds = SplineEndMode.Exclude;
            _canvas.Spline(
                Position(0.56f, 0.52f),
                Position(0.66f, 0.28f),
                Position(0.78f, 0.56f),
                Position(0.9f, 0.32f)
            );
            _canvas.SplineEnds = SplineEndMode.Include;

            // Stroke caps.
            float capY = 0.36f;
            _canvas.StrokeWeight(Mathf.Max(strokeWeight, unit * 0.03f));
            DrawCapSample(
                StrokeCap.Round,
                Position(0.1f, capY),
                Position(0.3f, capY),
                new Color(0.95f, 0.35f, 0.45f)
            );
            DrawCapSample(
                StrokeCap.Square,
                Position(0.4f, capY),
                Position(0.6f, capY),
                new Color(0.45f, 0.95f, 0.6f)
            );
            DrawCapSample(
                StrokeCap.Project,
                Position(0.7f, capY),
                Position(0.9f, capY),
                new Color(0.4f, 0.6f, 1f)
            );

            // Custom path with fill and stroke.
            _canvas.StrokeWeight(Mathf.Max(strokeWeight, unit * 0.008f));
            _canvas.Fill(new Color(0.9f, 0.55f, 1f, 0.65f));
            _canvas.Stroke(new Color(0.1f, 0.1f, 0.2f));
            _canvas.BeginShape();
            _canvas.Vertex(Position(0.1f, 0.15f));
            _canvas.Vertex(Position(0.3f, 0.05f));
            _canvas.Vertex(Position(0.42f, 0.22f));
            _canvas.Vertex(Position(0.25f, 0.3f));
            _canvas.BeginContour();
            _canvas.Vertex(Position(0.19f, 0.14f));
            _canvas.Vertex(Position(0.27f, 0.11f));
            _canvas.Vertex(Position(0.3f, 0.18f));
            _canvas.Vertex(Position(0.21f, 0.21f));
            _canvas.EndContour();
            _canvas.EndShape(ShapeCloseMode.Close);

            // Custom path built from spline vertices.
            _canvas.NoFill();
            _canvas.Stroke(new Color(0.9f, 0.9f, 0.3f, 0.95f));
            _canvas.BeginShape();
            _canvas.SplineVertex(Position(0.55f, 0.12f));
            _canvas.SplineVertex(Position(0.65f, 0.3f));
            _canvas.SplineVertex(Position(0.78f, 0.1f));
            _canvas.SplineVertex(Position(0.9f, 0.28f));
            _canvas.EndShape();

            // 3D line segments: z is depth in screen modes and world height in
            // world modes.
            _canvas.StrokeWeight(Mathf.Max(strokeWeight, unit * 0.006f));
            _canvas.Stroke(new Color(0.4f, 1f, 1f, 0.9f));
            float depthSpan = IsScreen || IsNormalizedScreen ? 0.12f : unit * 0.5f;
            _canvas.Line(
                WithDepth(Position(0.08f, 0.46f), 0f),
                WithDepth(Position(0.3f, 0.46f), depthSpan)
            );
            _canvas.Line(
                WithDepth(Position(0.3f, 0.46f), depthSpan),
                WithDepth(Position(0.5f, 0.38f), 0f)
            );
            _canvas.Line(
                WithDepth(Position(0.5f, 0.38f), 0f),
                WithDepth(Position(0.72f, 0.46f), depthSpan)
            );
            _canvas.Line(
                WithDepth(Position(0.72f, 0.46f), depthSpan),
                WithDepth(Position(0.9f, 0.38f), 0f)
            );
        }

        private void DrawCapSample(
            StrokeCap cap,
            Vector3 start,
            Vector3 end,
            Color color
        )
        {
            _canvas.Stroke(color);
            _canvas.StrokeCap(cap);
            _canvas.Line(start.x, start.y, start.z, end.x, end.y, end.z);
        }

        private static Vector3 WithDepth(Vector3 position, float depthOffset)
        {
            position.z += depthOffset;
            return position;
        }

        private void DrawArc(Vector3 center, float unit, ArcMode mode)
        {
            _canvas.Arc(
                center,
                new Vector2(unit * 0.19f, unit * 0.14f),
                -Mathf.PI * 0.15f,
                Mathf.PI * 1.35f,
                mode
            );
        }

        private void DrawEllipseMode(ShapeMode mode, Vector3 center, Vector2 size)
        {
            _canvas.EllipseMode = mode;
            Vector2 halfSize = size * 0.5f;

            switch (mode)
            {
                case ShapeMode.Corner:
                    _canvas.Ellipse(
                        new Vector3(center.x - halfSize.x, center.y - halfSize.y, center.z),
                        size
                    );
                    break;
                case ShapeMode.Corners:
                    _canvas.Ellipse(
                        new Vector3(center.x - halfSize.x, center.y - halfSize.y, center.z),
                        new Vector2(center.x + halfSize.x, center.y + halfSize.y)
                    );
                    break;
                case ShapeMode.Radius:
                    _canvas.Ellipse(center, halfSize);
                    break;
                default:
                    _canvas.Ellipse(center, size);
                    break;
            }
        }

        private void DrawRectMode(ShapeMode mode, Vector3 center, Vector2 size)
        {
            _canvas.RectMode = mode;
            Vector2 halfSize = size * 0.5f;

            switch (mode)
            {
                case ShapeMode.Corner:
                    _canvas.Rect(
                        new Vector3(center.x - halfSize.x, center.y - halfSize.y, center.z),
                        size
                    );
                    break;
                case ShapeMode.Corners:
                    _canvas.Rect(
                        new Vector3(center.x - halfSize.x, center.y - halfSize.y, center.z),
                        new Vector2(center.x + halfSize.x, center.y + halfSize.y)
                    );
                    break;
                case ShapeMode.Radius:
                    _canvas.Rect(center, halfSize);
                    break;
                default:
                    _canvas.Rect(center, size);
                    break;
            }
        }

        private void SetStyle(Color fill, Color stroke, float strokeWeight)
        {
            _canvas.Fill(fill);
            _canvas.Stroke(stroke);
            _canvas.StrokeWeight(strokeWeight);
        }

        private Vector3 Position(float normalizedX, float normalizedY)
        {
            if (IsScreen)
            {
                return new Vector3(
                    normalizedX * Mathf.Max(1, _canvas.Width),
                    normalizedY * Mathf.Max(1, _canvas.Height),
                    DrawingDepth()
                );
            }

            if (IsNormalizedScreen)
                return new Vector3(normalizedX, normalizedY, DrawingDepth());

            return new Vector3(
                (normalizedX - 0.5f) * PlanarExtent,
                (normalizedY - 0.5f) * PlanarExtent,
                0f
            );
        }

        private void DrawTransforms()
        {
            float unit = Unit();
            float strokeWeight = Mathf.Max(AntiAliasMinimum(), unit * 0.01f);
            float angle = _animateTransforms ? Mathf.Sin(Time.time) * 0.45f : 0.35f;
            var shapeSize = new Vector2(unit * 0.13f, unit * 0.09f);

            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
            SetStyle(new Color(0.2f, 0.8f, 1f, 0.8f), DarkColor(), strokeWeight);

            // moving x low -> x high, y high -> y low

            // Blue Rectangles
            // Shear X through ApplyMatrix -> Shear X through ShearX() -> Shear Y through ShearY() -> Negative Scale 
            DrawTransformedRect(Position(0.125f, 0.75f), shapeSize, () =>
                _canvas.ApplyMatrix(1f, 0f, Mathf.Tan(angle), 1f, 0f, 0f)
            );
            DrawTransformedRect(Position(0.375f, 0.75f), shapeSize, () => _canvas.ShearX(angle));
            DrawTransformedRect(Position(0.625f, 0.75f), shapeSize, () => _canvas.ShearY(angle));
            DrawTransformedRect(Position(0.875f, 0.75f), shapeSize, () => _canvas.Scale(-1f, 1f, 1f));


            // Orange Rectangles
            // Rotate X -> Rotate Y -> Rotate Z
            _canvas.Fill(new Color(1f, 0.55f, 0.2f, 0.8f));
            DrawTransformedRect(Position(0.125f, 0.35f), shapeSize, () => _canvas.RotateX(angle));
            DrawTransformedRect(Position(0.375f, 0.35f), shapeSize, () => _canvas.RotateY(angle));
            DrawTransformedRect(Position(0.625f, 0.35f), shapeSize, () => _canvas.RotateZ(angle));


            // Pink Rectangle
            // Shear X
            _canvas.Fill(new Color(0.45f, 1f, 0.55f, 0.8f));
            _canvas.Stroke(DarkColor());
            _canvas.Push();
            _canvas.Translate(Position(0.875f, 0.35f));
            _canvas.Fill(new Color(1f, 0.25f, 0.65f, 0.8f));
            _canvas.NoStroke();
            _canvas.ShearX(angle);
            _canvas.Rect(Vector3.zero, shapeSize);
            _canvas.Pop();

            // The outline proves that Pop() restored both transform and style.
            _canvas.NoFill();
            _canvas.Circle(Position(0.875f, 0.35f), unit * 0.15f);
        }

        private void DrawTransformedRect(Vector3 position, Vector2 size, Action transform)
        {
            _canvas.Push();
            _canvas.Translate(position);
            transform();
            _canvas.Rect(Vector3.zero, size, size.y * 0.2f);
            _canvas.Pop();
        }

        private float Unit()
        {
            if (IsScreen)
                return Mathf.Max(1f, Mathf.Min(_canvas.Width, _canvas.Height));

            if (IsNormalizedScreen)
                return 1f;

            return PlanarExtent;
        }

        private float DrawingDepth()
        {
            if (_mode is DrawingMode.ScreenOverlay or DrawingMode.NormalizedScreenOverlay)
                return 0f;

            return _canvas.WorldToLinearDepth(5f, Camera.main);
        }

        private float AntiAliasMinimum()
        {
            if (IsScreen)
                return 2f;

            if (IsNormalizedScreen)
                return 0.002f;

            return 0.02f;
        }

        private static Color DarkColor() => new(0.04f, 0.07f, 0.12f, 1f);

        private void DrawLine(Vector3 start, Vector3 end)
            => _canvas.Line(start.x, start.y, end.x, end.y, start.z);

        private void DrawTriangle(Vector3 point1, Vector3 point2, Vector3 point3)
            => _canvas.Triangle(
                point1.x,
                point1.y,
                point2.x,
                point2.y,
                point3.x,
                point3.y,
                point1.z
            );

        private void DrawQuad(Vector3 point1, Vector3 point2, Vector3 point3, Vector3 point4)
            => _canvas.Quad(
                point1.x,
                point1.y,
                point2.x,
                point2.y,
                point3.x,
                point3.y,
                point4.x,
                point4.y,
                point1.z
            );
    }
}
