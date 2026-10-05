using System;
using System.Collections.Generic;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Canvas
{
    internal struct CanvasStyleState
    {
        public Color FillColor;
        public Color StrokeColor;
        public float StrokeWeight;
        public bool FillEnabled;
        public bool StrokeEnabled;
        public ShapeMode RectMode;
        public ShapeMode EllipseMode;

        public static CanvasStyleState Default => new()
        {
            FillColor = Color.white,
            StrokeColor = Color.black,
            StrokeWeight = 1f,
            FillEnabled = true,
            StrokeEnabled = true,
            RectMode = ShapeMode.Corner,
            EllipseMode = ShapeMode.Center
        };
    }

    internal readonly struct CanvasState
    {
        public CanvasState(Matrix4x4 transform, CanvasStyleState style)
        {
            Transform = transform;
            Style = style;
        }

        public Matrix4x4 Transform { get; }
        public CanvasStyleState Style { get; }
    }

    public partial class UCanvas : MonoBehaviour
    {
        [SerializeField][HideInInspector] private DrawingContext _drawingContext;

        public DrawingMode DrawingMode => _drawingContext.DrawingMode;

        /// <summary>
        /// Selects indirect rendering or the diagnostic sequential rendering path.
        /// </summary>
        public DrawingExecutionMode ExecutionMode
        {
            get => _drawingContext.ExecutionMode;
            set => _drawingContext.ExecutionMode = value;
        }

        /// <summary>
        /// Displays the expanded quad bounds instead of evaluating the shape SDF.
        /// </summary>
        public bool DebugShapeBounds
        {
            get => _drawingContext.DebugShapeBounds;
            set => _drawingContext.DebugShapeBounds = value;
        }

        /// <summary>
        /// Enables depth testing and depth writes for this canvas.
        /// When enabled, shapes are occluded by scene geometry and occlude each other.
        /// When disabled, shapes are drawn in draw order regardless of the depth buffer.
        /// Overlay drawing modes always ignore depth.
        /// </summary>
        public bool EnableDepth
        {
            get => _drawingContext.EnableDepth;
            set => _drawingContext.EnableDepth = value;
        }

        /// <summary>
        /// Creates (or recreates) the drawing context for this canvas.
        /// </summary>
        /// <param name="enableDepth">
        /// Enables depth testing and depth writes. Overlay drawing modes ignore depth.
        /// </param>
        public DrawingContext CreateCanvas(
            DrawingMode mode = DrawingMode.ScreenOverlay,
            DrawingExecutionMode executionMode = DrawingExecutionMode.Indirect,
            bool enableDepth = false
        )
        {
            _drawingContext?.Dispose();
            _drawingContext = new DrawingContext(mode, _commandQueue, transform, executionMode, enableDepth);
            return _drawingContext;
        }

        private void CheckDrawingContext()
        {
            if (_drawingContext == null)
                throw new NullReferenceException("p5.Unite: Canvas not initialized. Call CreateCanvas() first.");
        }

        #region MonoBehaviour

        private void Start()
        {
            RenderPipelineManager.beginCameraRendering += OnCameraRendering;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnCameraRendering;

            _drawingContext?.Dispose();
        }

        private void LateUpdate()
        {
            _drawingContext?.CaptureCommands();

            if (_drawingContext?.ExecutionMode != DrawingExecutionMode.Sequential)
                return;

            foreach (var cam in Camera.allCameras)
            {
                if (cam && cam.isActiveAndEnabled)
                    _drawingContext.Draw(cam);
            }
        }

        private void OnCameraRendering(ScriptableRenderContext srpCtx, Camera cam)
        {
            CheckCurrentCamera(cam);

            if (!_currentCamera) return;
            if (_drawingContext.ExecutionMode == DrawingExecutionMode.Indirect)
                _drawingContext.Draw(cam);

            _transformStack.Reset();
        }

        #endregion


        #region Camera Management

        private Camera _currentCamera;
        private void CheckCurrentCamera(Camera cam)
        {
            if (!Equals(_currentCamera, cam))
            {
                _currentCamera = cam;
            }
        }

        #endregion


        #region Draw Command

        private Queue<DrawCommand> _commandQueue = new();

        #endregion


        #region Color

        private CanvasStyleState _styleState = CanvasStyleState.Default;

        internal CanvasStyleState StyleState => _styleState;

        #endregion


        #region Canvas Size

        public int Width
        {
            get
            {
                if (!_currentCamera)
                {
                    var mainCam = Camera.main;
                    if (mainCam)// for very first frame
                    {
                        return mainCam.pixelWidth;
                    }

                    return 1;// no active camera
                }
                ;

                var m = _drawingContext.DrawingMode;

                if (m is DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay)
                    return _currentCamera.pixelWidth;

                if (m is DrawingMode.Screen or DrawingMode.ScreenOverlay)
                    return _currentCamera.pixelWidth;

                if (m is DrawingMode.World or DrawingMode.CanvasLocal)
                    return -1; // there's no width in world or canvas-local coordinates

                return 0;
            }
        }

        public int Height
        {
            get
            {
                if (!_currentCamera)
                {
                    var mainCam = Camera.main;
                    if (mainCam)// for very first frame
                    {
                        return mainCam.pixelHeight;
                    }

                    return 1;// no active camera
                }
                ;

                var m = _drawingContext.DrawingMode;

                if (m is DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay)
                    return _currentCamera.pixelHeight;

                if (m is DrawingMode.Screen or DrawingMode.ScreenOverlay)
                    return _currentCamera.pixelHeight;

                if (m is DrawingMode.World or DrawingMode.CanvasLocal)
                    return -1; // there's no height in world or canvas-local coordinates

                return 0;
            }
        }

        #endregion


        #region Debug

        private void OnDrawGizmos()
        {
            // var camFront = _currentCamera.transform.TransformPoint(Vector3.forward);

            // Gizmos.DrawWireCube(camFront, Vector3.one * .1f);
        }

        #endregion
    }
}
