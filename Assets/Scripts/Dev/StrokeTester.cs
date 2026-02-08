using System;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;

namespace Dev
{
    public class StrokeTester : MonoBehaviour
    {
        private UCanvas _cnv;

        [Header("Test Settings")]
        [SerializeField] private DrawingMode _mode = DrawingMode.ScreenSpace;
        [SerializeField] private bool _autoCycleModes = false;
        [SerializeField] private float _cycleInterval = 5f;

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
                    int nextModeIndex = ((int)_mode + 1) % Enum.GetValues(typeof(DrawingMode)).Length;
                    _mode = (DrawingMode)nextModeIndex;
                }
            }

            // Check for mode change
            if (_mode != _currentMode)
            {
                _currentMode = _mode;
                _cnv.CreateCanvas(_mode);
            }

            // Draw stroke test patterns
            DrawStrokeTest();
        }

        private void DrawStrokeTest()
        {
            // Get layout metrics based on mode
            float unit;
            float spacing;
            Vector3 startPos;

            if (_currentMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
            {
                unit = 50f;
                spacing = 120f;
                startPos = new Vector3(100f, _cnv.Height - 150f, 0f);
            }
            else if (_currentMode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
            {
                unit = 0.05f;
                spacing = 0.15f;
                startPos = new Vector3(0.1f, 0.85f, 0f);
            }
            else // WorldSpace or ObjectSpace
            {
                unit = 1f;
                spacing = 3f;
                startPos = new Vector3(-6f, 4f, 10f);
            }

            float y = startPos.y;

            // Row 1: No Stroke (Fill only)
            _cnv.Fill(Color.red);
            _cnv.Stroke(Color.black);
            _cnv.StrokeWeight(0f);
            DrawShapeRow(startPos.x, y, unit, "No Stroke");

            // Row 2: Default Stroke (1)
            y -= spacing;
            _cnv.Fill(Color.green);
            _cnv.Stroke(Color.black);
            _cnv.StrokeWeight(1f);
            DrawShapeRow(startPos.x, y, unit, "Stroke: 1");

            // Row 3: Medium Stroke (3)
            y -= spacing;
            _cnv.Fill(Color.blue);
            _cnv.Stroke(Color.yellow);
            _cnv.StrokeWeight(3f);
            DrawShapeRow(startPos.x, y, unit, "Stroke: 3");

            // Row 4: Thick Stroke (5)
            y -= spacing;
            _cnv.Fill(Color.magenta);
            _cnv.Stroke(Color.cyan);
            _cnv.StrokeWeight(5f);
            DrawShapeRow(startPos.x, y, unit, "Stroke: 5");

            // Row 5: Very Thick Stroke (10)
            y -= spacing;
            _cnv.Fill(Color.white);
            _cnv.Stroke(Color.red);
            _cnv.StrokeWeight(10f);
            DrawShapeRow(startPos.x, y, unit, "Stroke: 10");

            // Row 6: Different stroke colors comparison
            y -= spacing * 1.2f;
            DrawColorComparisonRow(startPos.x, y, unit);

            // Row 7: Transparent fill with stroke
            y -= spacing * 1.2f;
            DrawTransparentFillRow(startPos.x, y, unit);
        }

        private void DrawShapeRow(float startX, float y, float unit, string label)
        {
            float x = startX;
            float spacing = unit * 3f;

            // Circle
            _cnv.Circle(x, y, unit);
            x += spacing;

            // Rectangle
            _cnv.Rect(x, y, unit * 2f, unit * 1.5f);
            x += spacing;

            // Square
            _cnv.Square(x, y, unit * 1.5f);
        }

        private void DrawColorComparisonRow(float startX, float y, float unit)
        {
            float x = startX;
            float spacing = unit * 3.5f;

            // Same fill, different stroke colors
            _cnv.Fill(Color.white);
            _cnv.StrokeWeight(3f);

            _cnv.Stroke(Color.red);
            _cnv.Circle(x, y, unit);
            x += spacing;

            _cnv.Stroke(Color.green);
            _cnv.Circle(x, y, unit);
            x += spacing;

            _cnv.Stroke(Color.blue);
            _cnv.Circle(x, y, unit);
            x += spacing;

            _cnv.Stroke(Color.yellow);
            _cnv.Circle(x, y, unit);
        }

        private void DrawTransparentFillRow(float startX, float y, float unit)
        {
            float x = startX;
            float spacing = unit * 3f;

            // Semi-transparent fill with solid stroke
            _cnv.Fill(new Color(1f, 0f, 0f, 0.3f));
            _cnv.Stroke(Color.black);
            _cnv.StrokeWeight(3f);
            _cnv.Circle(x, y, unit);
            x += spacing;

            _cnv.Fill(new Color(0f, 1f, 0f, 0.3f));
            _cnv.Rect(x, y, unit * 2f, unit * 1.5f);
            x += spacing;

            _cnv.Fill(new Color(0f, 0f, 1f, 0.3f));
            _cnv.Square(x, y, unit * 1.5f);
        }
    }
}
