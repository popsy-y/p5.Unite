using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Unite.Samples.BasicDrawing
{
    /// <summary>
    /// Minimal p5.Unite sketch. Draws a row of pulsing circles, rotating rounded rectangles and
    /// two curves every frame, in screen coordinates.
    /// </summary>
    [RequireComponent(typeof(UCanvas))]
    public sealed class BasicDrawing : MonoBehaviour
    {
        [Tooltip("Freezes the animation so a frame can be inspected.")]
        [SerializeField] private bool _animate = true;

        private UCanvas _canvas;

        private void Start()
        {
            _canvas = GetComponent<UCanvas>();

            // ScreenOverlay uses pixel coordinates, ignores depth and draws on top of the camera.
            // Try other DrawingMode values, but note that each mode uses its own coordinate space.
            _canvas.CreateCanvas(DrawingMode.ScreenOverlay);
        }

        private void Update()
        {
            float time = _animate ? Time.time : 0f;
            float width = _canvas.Width;
            float height = _canvas.Height;
            float unit = Mathf.Min(width, height);

            _canvas.ResetTransform();

            DrawCircles(time, width, height, unit);
            DrawRoundedRects(time, width, height, unit);
            DrawCurves(time, width, height, unit);
        }

        private void DrawCircles(float time, float width, float height, float unit)
        {
            const int count = 7;

            for (int index = 0; index < count; index++)
            {
                float t = (index + 0.5f) / count;
                float phase = time * 1.5f + index * 0.6f;
                float diameter = unit * (0.07f + 0.02f * Mathf.Sin(phase));
                Color color = Color.HSVToRGB(Mathf.Repeat(t + time * 0.05f, 1f), 0.55f, 0.95f);
                color.a = 0.9f;

                _canvas.NoStroke();
                _canvas.Fill(color);
                _canvas.Circle(new Vector3(t * width, height * 0.18f, 0f), diameter);
            }
        }

        private void DrawRoundedRects(float time, float width, float height, float unit)
        {
            const int count = 5;
            var size = new Vector2(unit * 0.16f, unit * 0.1f);

            for (int index = 0; index < count; index++)
            {
                float t = (index + 0.5f) / count;
                float direction = index % 2 == 0 ? 1f : -1f;
                float angle = time * (0.3f + index * 0.12f) * direction;

                _canvas.Push();
                _canvas.Translate(new Vector3(t * width, height * 0.5f, 0f));
                _canvas.Rotate(angle);
                _canvas.Fill(new Color(0.15f, 0.75f, 0.95f, 0.75f));
                _canvas.Stroke(new Color(0.04f, 0.07f, 0.12f, 1f));
                _canvas.StrokeWeight(Mathf.Max(1f, unit * 0.006f));
                _canvas.Rect(Vector3.zero, size, size.y * 0.25f);
                _canvas.Pop();
            }
        }

        private void DrawCurves(float time, float width, float height, float unit)
        {
            float amplitude = unit * (0.04f + 0.02f * Mathf.Sin(time * 0.9f));

            _canvas.Push();
            _canvas.Translate(new Vector3(0f, height * 0.8f, 0f));
            _canvas.NoFill();
            _canvas.StrokeWeight(Mathf.Max(2f, unit * 0.008f));

            _canvas.Stroke(new Color(0.55f, 1f, 0.4f, 0.95f));
            _canvas.Bezier(
                new Vector2(width * 0.1f, 0f),
                new Vector2(width * 0.25f, -amplitude),
                new Vector2(width * 0.35f, amplitude),
                new Vector2(width * 0.5f, 0f)
            );

            _canvas.Stroke(new Color(1f, 0.65f, 0.3f, 0.95f));
            _canvas.Spline(
                new Vector2(width * 0.55f, 0f),
                new Vector2(width * 0.65f, amplitude),
                new Vector2(width * 0.75f, -amplitude),
                new Vector2(width * 0.9f, 0f)
            );

            _canvas.Pop();
        }
    }
}
