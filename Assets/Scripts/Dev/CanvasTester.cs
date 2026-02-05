using System;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    public class CanvasTester : MonoBehaviour
    {
        private UCanvas _cnv;

        [Header("Test Settings")]
        [SerializeField] private DrawingMode _mode = DrawingMode.ScreenSpace;
        [SerializeField] private bool _autoCycleModes = true;
        [SerializeField] private float _cycleInterval = 3f;

        private DrawingMode _currentMode;
        private float _timer;

        private void Start()
        {
            _cnv = GetComponent<UCanvas>();
            _currentMode = _mode;
            _cnv.CreateCanvas(_mode);
        }

        private void Update()
        {
            // Auto cycle modes
            if (_autoCycleModes)
            {
                _timer += Time.deltaTime;
                if (_timer >= _cycleInterval)
                {
                    _timer = 0f;
                    int nextModeIndex = ((int)_mode + 1) % System.Enum.GetValues(typeof(DrawingMode)).Length;
                    _mode = (DrawingMode)nextModeIndex;
                }
            }

            // Check for mode change
            if (_mode != _currentMode)
            {
                _currentMode = _mode;
                _cnv.CreateCanvas(_mode);
            }

            // Determine layout metrics based on mode
            float unit;
            Vector3 center;

            if (_currentMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
            {
                unit = 100f;
                center = new Vector3(_cnv.Width / 2f, _cnv.Height / 2f, 0f);
            }
            else if (_currentMode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
            {
                unit = 0.1f;
                center = new Vector3(0.5f, 0.5f, 0f);
            }
            else // WorldSpace
            {
                unit = 2f;
                center = Vector3.zero;
            }

            // --- 1. Position & Size & Z Test ---
            // Draw shapes at specific positions with different sizes and Z depths
            _cnv.Fill(Color.red);
            _cnv.Square(center.x - 3 * unit, center.y, CorrectDepth(10f), unit); // Base (Z=10)

            _cnv.Fill(Color.blue);
            _cnv.Square(center.x - 2.5f * unit, center.y - 0.5f * unit, CorrectDepth(5f), unit * 0.8f); // Z=5 (Should be closer or further depending on projection)

            _cnv.Fill(Color.yellow);
            _cnv.Circle(center.x - 3 * unit, center.y + 1.2f * unit, CorrectDepth(0f), unit * 0.5f); // Z=0

            // --- 2. Canvas Transform Test (Push, Pop, Translate, Rotate, Scale) ---
            _cnv.Push();
            {
                _cnv.Translate(center.x, center.y, 0f);
                _cnv.Rotate(Time.time); // Rotate over time
                _cnv.Scale(Mathf.Sin(Time.time * 2f) * 0.5f + 1f, 1f, 1f); // Scale x oscillating

                _cnv.Fill(Color.green);
                _cnv.Rect(0f, 0f, unit, unit * 0.5f); // Drawn at (0,0) local, which is center screen
            }
            _cnv.Pop();

            // Verify Pop worked (draw something static nearby)
            _cnv.Fill(Color.white);
            _cnv.Circle(center.x, center.y - 1.5f * unit, unit * 0.2f); // Should stay static below rotating rect

            // --- 3. Color Test (Fill, Stroke) ---
            // Fill is tested above.
            // Test Stroke API (even if not rendered yet)
            _cnv.Stroke(Color.cyan);
            _cnv.Fill(Color.magenta);
            _cnv.Rect(center.x + 3 * unit, center.y, unit, unit);

            // Reset stroke for next frame/draws (optional, but good practice if state persists)
            _cnv.Stroke(Color.black);
        }
        
        private float CorrectDepth(float depth)
        {
            if (_cnv.DrawingMode is
                DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay or
                DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
            {
                return _cnv.WorldToLinearDepth(depth);
            }
            else
            {
                return depth;
            }
        }
    }
}
