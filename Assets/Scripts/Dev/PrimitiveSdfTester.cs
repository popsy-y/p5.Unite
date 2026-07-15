using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    [RequireComponent(typeof(UCanvas))]
    public sealed class PrimitiveSdfTester : MonoBehaviour
    {
        [SerializeField] private DrawingExecutionMode _executionMode = DrawingExecutionMode.Sequential;
        [SerializeField] private bool _debugShapeBounds;

        private UCanvas _canvas;

        private void Start()
        {
            _canvas = GetComponent<UCanvas>();
            _canvas.CreateCanvas(DrawingMode.ScreenSpaceOverlay, _executionMode);
            _canvas.DebugShapeBounds = _debugShapeBounds;
            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Center;
        }

        private void Update()
        {
            float cell = Mathf.Min(_canvas.Width / 4.5f, _canvas.Height / 3.5f);
            float width = _canvas.Width;
            float height = _canvas.Height;

            _canvas.Stroke(new Color(0.05f, 0.08f, 0.12f));
            _canvas.StrokeWeight(Mathf.Max(3f, cell * 0.035f));
            _canvas.Fill(new Color(0.15f, 0.75f, 0.95f, 0.8f));

            DrawArc(width * 0.125f, height * 0.82f, cell, ArcMode.Open);
            DrawArc(width * 0.375f, height * 0.82f, cell, ArcMode.Chord);
            DrawArc(width * 0.625f, height * 0.82f, cell, ArcMode.Pie);

            _canvas.Push();
            _canvas.Translate(width * 0.875f, height * 0.82f);
            _canvas.Scale(1.45f, 0.65f, 1f);
            _canvas.Ellipse(0f, 0f, cell * 0.55f, cell * 0.55f);
            _canvas.Pop();

            _canvas.Fill(new Color(0.95f, 0.45f, 0.25f, 0.8f));
            _canvas.Circle(width * 0.125f, height * 0.5f, cell * 0.58f);
            _canvas.Ellipse(width * 0.375f, height * 0.5f, cell * 0.72f, cell * 0.48f);
            _canvas.Rect(width * 0.625f, height * 0.5f, cell * 0.72f, cell * 0.52f, cell * 0.12f);

            _canvas.NoStroke();
            _canvas.Square(width * 0.875f, height * 0.5f, cell * 0.55f, cell * 0.08f);

            _canvas.Stroke(new Color(0.05f, 0.08f, 0.12f));
            _canvas.NoFill();
            _canvas.Line(
                width * 0.06f,
                height * 0.17f,
                width * 0.19f,
                height * 0.29f
            );
            _canvas.Point(width * 0.31f, height * 0.23f);

            _canvas.Fill(new Color(0.65f, 0.85f, 0.25f, 0.8f));
            _canvas.Triangle(
                width * 0.43f,
                height * 0.14f,
                width * 0.57f,
                height * 0.14f,
                width * 0.5f,
                height * 0.31f
            );
            _canvas.Quad(
                width * 0.68f,
                height * 0.14f,
                width * 0.82f,
                height * 0.17f,
                width * 0.79f,
                height * 0.31f,
                width * 0.65f,
                height * 0.27f
            );
        }

        private void DrawArc(float x, float y, float cell, ArcMode mode)
        {
            _canvas.Arc(
                x,
                y,
                cell * 0.72f,
                cell * 0.56f,
                -Mathf.PI * 0.15f,
                Mathf.PI * 1.35f,
                mode
            );
        }
    }
}
