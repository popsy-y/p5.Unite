using System.Collections.Generic;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Dev
{
    public sealed class MassiveShapeTester : MonoBehaviour
    {
        public enum TestMode
        {
            Random,
            Animated
        }

        private struct ShapeData
        {
            public float X;
            public float Y;
            public int ShapeType;
            public float CurrentSize;
            public float TargetSize;
            public float SecondSize;
            public Color CurrentColor;
            public Color TargetColor;
            public float AnimationSpeed;
            public float AnimationOffset;
        }

        [Header("Settings")]
        [SerializeField] private DrawingMode _mode = DrawingMode.ScreenSpace;
        [SerializeField] private TestMode _testMode = TestMode.Animated;
        [SerializeField] private int _shapeCount = 500;
        [SerializeField] private float _minSize = 10f;
        [SerializeField] private float _maxSize = 50f;

        [Header("Clip Space Settings")]
        [SerializeField] private float _minSizeClip = 0.01f;
        [SerializeField] private float _maxSizeClip = 0.05f;

        [Header("Animation Settings")]
        [SerializeField] private float _animationDuration = 2f;
        [SerializeField] private float _sizeVariation = 0.3f;

        private UCanvas _cnv;
        private List<ShapeData> _shapes = new();
        private int _lastShapeCount;
        private TestMode _lastTestMode;
        private DrawingMode _lastMode;

        private void Start()
        {
            _cnv = GetComponent<UCanvas>();
            _cnv.CreateCanvas(_mode);
            _lastShapeCount = _shapeCount;
            _lastTestMode = _testMode;
            _lastMode = _mode;

            if (_testMode == TestMode.Animated)
            {
                InitializeShapes();
            }
        }

        private void Update()
        {
            if (!_cnv)
                return;

            // Check if we need to reinitialize
            if (_shapeCount != _lastShapeCount || _testMode != _lastTestMode || _mode != _lastMode)
            {
                _lastShapeCount = _shapeCount;
                _lastTestMode = _testMode;
                _lastMode = _mode;

                if (_mode != _lastMode)
                {
                    _cnv.CreateCanvas(_mode);
                }

                if (_testMode == TestMode.Animated)
                {
                    InitializeShapes();
                }
            }

            // Determine coordinate system
            bool isNormalized = _mode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay;

            // Get bounds
            float width = isNormalized ? 1f : _cnv.Width;
            float height = isNormalized ? 1f : _cnv.Height;

            // Get size range based on mode
            float minSize = isNormalized ? _minSizeClip : _minSize;
            float maxSize = isNormalized ? _maxSizeClip : _maxSize;

            if (_testMode == TestMode.Random)
            {
                RenderRandomMode(width, height, minSize, maxSize);
            }
            else
            {
                RenderAnimatedMode(width, height, minSize, maxSize);
            }
        }

        private void InitializeShapes()
        {
            _shapes.Clear();

            bool isNormalized = _mode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay;
            float width = isNormalized ? 1f : _cnv.Width;
            float height = isNormalized ? 1f : _cnv.Height;
            float maxSize = isNormalized ? _maxSizeClip : _maxSize;
            float margin = isNormalized ? 0.05f : maxSize;

            for (int i = 0; i < _shapeCount; i++)
            {
                float baseSize = Random.Range(isNormalized ? _minSizeClip : _minSize, maxSize);

                _shapes.Add(new ShapeData
                {
                    X = Random.Range(margin, width - margin),
                    Y = Random.Range(margin, height - margin),
                    ShapeType = Random.Range(0, 3),
                    CurrentSize = baseSize,
                    TargetSize = baseSize * Random.Range(1f - _sizeVariation, 1f + _sizeVariation),
                    SecondSize = baseSize * Random.Range(0.5f, 1.5f),
                    CurrentColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f),
                    TargetColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f),
                    AnimationSpeed = Random.Range(0.5f, 2f),
                    AnimationOffset = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        private void RenderRandomMode(float width, float height, float minSize, float maxSize)
        {
            float margin = _mode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay ? 0.05f : maxSize;

            for (int i = 0; i < _shapeCount; i++)
            {
                float x = Random.Range(margin, width - margin);
                float y = Random.Range(margin, height - margin);

                _cnv.Fill(Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f));

                int shapeType = Random.Range(0, 3);

                switch (shapeType)
                {
                    case 0:
                        _cnv.Circle(x, y, Random.Range(minSize, maxSize));
                        break;
                    case 1:
                        _cnv.Square(x, y, Random.Range(minSize, maxSize));
                        break;
                    case 2:
                        _cnv.Rect(x, y, Random.Range(minSize, maxSize), Random.Range(minSize, maxSize));
                        break;
                }
            }
        }

        private void RenderAnimatedMode(float width, float height, float minSize, float maxSize)
        {
            float time = Time.time;

            for (int i = 0; i < _shapes.Count; i++)
            {
                ShapeData shape = _shapes[i];

                // Update size with ping-pong animation
                float t = Mathf.PingPong((time * shape.AnimationSpeed + shape.AnimationOffset) / _animationDuration, 1f);
                float currentSize = Mathf.Lerp(shape.CurrentSize, shape.TargetSize, t);

                // Update color with smooth transition
                float colorT = Mathf.PingPong((time * shape.AnimationSpeed * 0.5f + shape.AnimationOffset) / (_animationDuration * 2f), 1f);
                Color currentColor = Color.Lerp(shape.CurrentColor, shape.TargetColor, colorT);

                _cnv.Fill(currentColor);

                switch (shape.ShapeType)
                {
                    case 0:
                        _cnv.Circle(shape.X, shape.Y, currentSize);
                        break;
                    case 1:
                        _cnv.Square(shape.X, shape.X, currentSize);
                        break;
                    case 2:
                        float w = currentSize;
                        float h = Mathf.Lerp(shape.SecondSize, shape.SecondSize * 0.5f, t);
                        _cnv.Rect(shape.X, shape.Y, w, h);
                        break;
                }
            }
        }
    }
}
