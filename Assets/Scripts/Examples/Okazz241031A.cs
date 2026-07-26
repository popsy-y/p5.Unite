// Shoutout to Okazz for the original p5.js sketch "241031a".
// Original: https://openprocessing.org/@okazz/2421742
// License: Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported
// https://creativecommons.org/licenses/by-nc-sa/3.0/
// This Unity adaptation is distributed under the same CC BY-NC-SA 3.0 license.

using System.Collections.Generic;

using Unite.Canvas;
using Unite.Drawing;

using UnityEngine;

using Random = Unity.Mathematics.Random;

namespace Unite.Examples
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UCanvas))]
    public sealed class Okazz241031A : MonoBehaviour
    {
        private enum ExampleDrawingMode
        {
            ScreenOverlay,
            CanvasLocal
        }

        private const float ReferenceFrameRate = 60f;
        private const float MinimumCanvasSize = 0.01f;

        private static readonly Color[] _colors =
        {
            new Color32(247, 23, 53, 255),
            new Color32(247, 208, 2, 255),
            new Color32(26, 83, 192, 255),
            new Color32(35, 35, 35, 255)
        };

        [SerializeField] private UCanvas _canvas;
        [SerializeField] private ExampleDrawingMode _drawingMode = ExampleDrawingMode.ScreenOverlay;
        [SerializeField] private bool _drawBackground = true;
        [SerializeField] private Vector2 _canvasLocalSize = new(10f, 10f);
        [SerializeField] private uint _seed = 241031;

        private readonly List<DynamicShape> _shapes = new();

        private Random _random;
        private Vector2 _canvasCenter;
        private Vector2 _canvasDimensions;
        private Vector2 _canvasOrigin;
        private float _canvasSize;
        private float _spawnCountdown;

        private void OnEnable()
        {
            if (!_canvas)
                _canvas = GetComponent<UCanvas>();

            _canvas.CreateCanvas(getDrawingMode());
            _canvas.RectMode = ShapeMode.Center;

            _random = new Random(_seed == 0 ? 1u : _seed);
            _canvasDimensions = Vector2.zero;
            _spawnCountdown = nextSpawnDelay();
        }

        private void Update()
        {
            updateDrawingMode();

            if (!updateCanvasDimensions())
                return;

            if (_drawBackground)
                drawBackground();

            var deltaFrames = Time.deltaTime * ReferenceFrameRate;
            foreach (var shape in _shapes)
            {
                shape.Draw(_canvas);
                shape.Step(deltaFrames, _canvasSize, ref _random);
            }

            _spawnCountdown -= deltaFrames;
            if (_spawnCountdown <= 0f)
            {
                var spawnCount = _random.NextInt(1, 30);
                for (var i = 0; i < spawnCount; i++)
                    addShape();

                _spawnCountdown += nextSpawnDelay();
            }

            for (var i = _shapes.Count - 1; i >= 0; i--)
            {
                if (_shapes[i].IsDead)
                    _shapes.RemoveAt(i);
            }
        }

        private bool updateCanvasDimensions()
        {
            var dimensions = getCanvasDimensions();
            if (dimensions.x <= 0f || dimensions.y <= 0f)
                return false;

            if (dimensions == _canvasDimensions)
                return true;

            _canvasDimensions = dimensions;
            _canvasCenter = _drawingMode == ExampleDrawingMode.ScreenOverlay
                ? dimensions * 0.5f
                : Vector2.zero;
            _canvasSize = Mathf.Min(dimensions.x, dimensions.y);
            _canvasOrigin = _canvasCenter - Vector2.one * (_canvasSize * 0.5f);

            _shapes.Clear();
            addShape();
            return true;
        }

        private void drawBackground()
        {
            _canvas.Fill(Color.white);
            _canvas.NoStroke();
            _canvas.Rect(_canvasCenter, _canvasDimensions);
        }

        private void updateDrawingMode()
        {
            var drawingMode = getDrawingMode();
            if (_canvas.DrawingMode == drawingMode)
                return;

            _canvas.CreateCanvas(drawingMode);
            _canvas.RectMode = ShapeMode.Center;
            _canvasDimensions = Vector2.zero;
        }

        private DrawingMode getDrawingMode()
            => _drawingMode == ExampleDrawingMode.ScreenOverlay
                ? DrawingMode.ScreenOverlay
                : DrawingMode.CanvasLocal;

        private Vector2 getCanvasDimensions()
        {
            if (_drawingMode == ExampleDrawingMode.ScreenOverlay)
            {
                var width = _canvas.Width;
                var height = _canvas.Height;
                return width > 1 && height > 1
                    ? new Vector2(width, height)
                    : Vector2.zero;
            }

            return new Vector2(
                Mathf.Max(Mathf.Abs(_canvasLocalSize.x), MinimumCanvasSize),
                Mathf.Max(Mathf.Abs(_canvasLocalSize.y), MinimumCanvasSize)
            );
        }

        private void addShape()
        {
            var color = _colors[_random.NextInt(_colors.Length)];
            _shapes.Add(new DynamicShape(_canvasOrigin, _canvasSize, color, ref _random));
        }

        private float nextSpawnDelay()
            => _random.NextBool() ? 15f : 30f;

        private static float easeInOutExpo(float value)
        {
            if (value <= 0f)
                return 0f;
            if (value >= 1f)
                return 1f;

            return value < 0.5f
                ? Mathf.Pow(2f, 20f * value - 10f) * 0.5f
                : (2f - Mathf.Pow(2f, -20f * value + 10f)) * 0.5f;
        }

        private sealed class DynamicShape
        {
            private readonly Color _color;
            private readonly int _maxActionPoints;
            private readonly int _shapeType;

            private Vector2 _position;
            private Vector2 _fromPosition;
            private Vector2 _toPosition;
            private int _actionPoints;
            private int _animationType;
            private float _duration;
            private float _elapsed;
            private float _fromSize;
            private float _toSize;
            private float _size;
            private float _sizeMax;
            private float _reductionRatio = 1f;
            private float _lineStrokeWeight;

            public bool IsDead { get; private set; }

            public DynamicShape(
                Vector2 canvasOrigin,
                float canvasSize,
                Color color,
                ref Random random
            )
            {
                _position = canvasOrigin + new Vector2(
                    random.NextFloat(0.3f, 0.7f) * canvasSize,
                    random.NextFloat(0.3f, 0.7f) * canvasSize
                );
                _color = color;
                _shapeType = random.NextInt(4);
                _maxActionPoints = random.NextInt(2, 5);
                _actionPoints = _maxActionPoints;
                _sizeMax = canvasSize * random.NextFloat(0.01f, 0.05f);

                initializeAction(canvasSize, ref random);
            }

            public void Draw(UCanvas canvas)
            {
                drawShape(canvas);

                if (_lineStrokeWeight <= 0f)
                    return;

                canvas.Stroke(_color);
                canvas.StrokeWeight(_lineStrokeWeight);
                canvas.Line(_position, _fromPosition);
            }

            public void Step(float deltaFrames, float canvasSize, ref Random random)
            {
                var normalizedTime = Mathf.Clamp01(_elapsed / _duration);
                var easedTime = easeInOutExpo(normalizedTime);

                if (_elapsed > 0f && _elapsed < _duration)
                {
                    if (_actionPoints == _maxActionPoints)
                    {
                        _size = Mathf.Lerp(0f, _sizeMax, easedTime);
                    }
                    else if (_actionPoints > 0)
                    {
                        animateMiddleAction(easedTime);
                    }
                    else
                    {
                        _size = Mathf.Lerp(_fromSize, 0f, easedTime);
                    }
                }

                _elapsed += deltaFrames;
                if (_elapsed <= _duration)
                    return;

                _actionPoints--;
                initializeAction(canvasSize, ref random);
                if (_actionPoints < 0)
                    IsDead = true;
            }

            private void drawShape(UCanvas canvas)
            {
                var shapeSize = new Vector2(_size, _size);
                if (_animationType == 1)
                    shapeSize.y *= _reductionRatio;
                else if (_animationType == 2)
                    shapeSize.x *= _reductionRatio;

                canvas.StrokeWeight(_size * 0.05f);
                if (_shapeType is 0 or 2)
                {
                    canvas.Fill(_color);
                    canvas.NoStroke();
                }
                else
                {
                    canvas.NoFill();
                    canvas.Stroke(_color);
                }

                if (_shapeType < 2)
                    canvas.Ellipse(_position, shapeSize);
                else
                    canvas.Rect(_position, shapeSize * 0.9f);
            }

            private void animateMiddleAction(float easedTime)
            {
                if (_animationType == 0)
                {
                    _size = Mathf.Lerp(_fromSize, _toSize, easedTime);
                }
                else if (_animationType == 1)
                {
                    _position.x = Mathf.Lerp(_fromPosition.x, _toPosition.x, easedTime);
                    _lineStrokeWeight = Mathf.Lerp(
                        0f,
                        _size * 0.2f,
                        Mathf.Sin(easedTime * Mathf.PI)
                    );
                }
                else
                {
                    _position.y = Mathf.Lerp(_fromPosition.y, _toPosition.y, easedTime);
                    _lineStrokeWeight = Mathf.Lerp(
                        0f,
                        _size * 0.2f,
                        Mathf.Sin(easedTime * Mathf.PI)
                    );
                }

                _reductionRatio = Mathf.Lerp(
                    1f,
                    0.3f,
                    Mathf.Sin(easedTime * Mathf.PI)
                );
            }

            private void initializeAction(float canvasSize, ref Random random)
            {
                _elapsed = 0f;
                _fromSize = _size;
                _toSize = _sizeMax * random.NextFloat(0.5f, 1.5f);
                _fromPosition = _position;
                _toPosition = _fromPosition;
                _toPosition.x += canvasSize * 0.1f
                    * (random.NextBool() ? -1f : 1f)
                    * random.NextInt(1, 4);
                _toPosition.y += canvasSize * 0.1f
                    * (random.NextBool() ? -1f : 1f)
                    * random.NextInt(1, 4);
                _animationType = random.NextInt(3);
                _duration = random.NextFloat(20f, 50f);
                _reductionRatio = 1f;
                _lineStrokeWeight = 0f;
            }
        }
    }
}
