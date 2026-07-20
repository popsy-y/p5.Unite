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
            Stress
        }

        private static readonly ShapeMode[] ShapeModes =
        {
            ShapeMode.Corner,
            ShapeMode.Center,
            ShapeMode.Corners,
            ShapeMode.Radius
        };

        [Header("Rendering")]
        [SerializeField] private DrawingMode _mode = DrawingMode.ScreenSpace;
        [SerializeField] private DrawingExecutionMode _executionMode = DrawingExecutionMode.Indirect;
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
        private bool _currentDebugShapeBounds;
        private float _modeTimer;
        private float _pageTimer;

        private bool IsScreenSpace => _mode is
            DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay;

        private bool IsClipSpace => _mode is
            DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay;

        private void Start()
        {
            _canvas = GetComponent<UCanvas>();
            _currentMode = _mode;
            _currentExecutionMode = _executionMode;
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
                default:
                    DrawPrimitives();
                    break;
            }
        }

        private void CreateCanvas()
        {
            _canvas.CreateCanvas(_mode, _executionMode);
            _canvas.DebugShapeBounds = _debugShapeBounds;
        }

        private void ApplyRenderingSettings()
        {
            if (_mode != _currentMode || _executionMode != _currentExecutionMode)
            {
                _currentMode = _mode;
                _currentExecutionMode = _executionMode;
                CreateCanvas();
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
            if (IsScreenSpace)
            {
                return new Vector3(
                    normalizedX * Mathf.Max(1, _canvas.Width),
                    normalizedY * Mathf.Max(1, _canvas.Height),
                    DrawingDepth()
                );
            }

            if (IsClipSpace)
                return new Vector3(normalizedX, normalizedY, DrawingDepth());

            return DrawingPlanePosition(normalizedX, normalizedY);
        }

        private float Unit()
        {
            if (IsScreenSpace)
                return Mathf.Max(1f, Mathf.Min(_canvas.Width, _canvas.Height));

            if (IsClipSpace)
                return 1f;

            Vector3 bottom = DrawingPlanePosition(0.5f, 0f);
            Vector3 top = DrawingPlanePosition(0.5f, 1f);
            return Mathf.Max(0.001f, Vector2.Distance(bottom, top) * 0.7f);
        }

        private Vector3 DrawingPlanePosition(float normalizedX, float normalizedY)
        {
            Camera cam = Camera.main;
            if (!cam)
                return new Vector3((normalizedX - 0.5f) * 8f, (normalizedY - 0.5f) * 8f);

            bool isObjectSpace = _mode == DrawingMode.ObjectSpace;
            var plane = isObjectSpace
                ? new Plane(transform.forward, transform.position)
                : new Plane(Vector3.forward, Vector3.zero);
            Ray ray = cam.ViewportPointToRay(new Vector3(normalizedX, normalizedY));

            if (!plane.Raycast(ray, out float distance))
                return Vector3.zero;

            Vector3 worldPosition = ray.GetPoint(distance);
            return isObjectSpace ? transform.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private float DrawingDepth()
        {
            if (_mode is DrawingMode.ScreenSpaceOverlay or DrawingMode.ClipSpaceOverlay)
                return 0f;

            return _canvas.WorldToLinearDepth(5f, Camera.main);
        }

        private float AntiAliasMinimum()
        {
            if (IsScreenSpace)
                return 2f;

            if (IsClipSpace)
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
