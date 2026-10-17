using System;
using System.Collections.Generic;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.Rendering;
using StrokeCapValue = Unite.Canvas.StrokeCap;
using StrokeJoinValue = Unite.Canvas.StrokeJoin;

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
        public StrokeCap StrokeCap;
        public StrokeJoin StrokeJoin;
        public bool LinePerspective;

        public static CanvasStyleState Default => new()
        {
            FillColor = Color.white,
            StrokeColor = Color.black,
            StrokeWeight = 1f,
            FillEnabled = true,
            StrokeEnabled = true,
            RectMode = ShapeMode.Corner,
            EllipseMode = ShapeMode.Center,
            StrokeCap = StrokeCapValue.Round,
            StrokeJoin = StrokeJoinValue.Round,
            LinePerspective = true
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

        /// <summary>
        /// Shaders used to render this canvas. When empty, the library shipped with the
        /// package (<see cref="UniteShaderLibrary"/>) is used instead.
        /// </summary>
        [SerializeField] private UniteShaderLibrary _shaderLibrary;

        /// <summary>
        /// Shaders used to render this canvas.
        /// </summary>
        public UniteShaderLibrary ShaderLibrary
        {
            get => _shaderLibrary;
            set => _shaderLibrary = value;
        }

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
        /// <remarks>
        /// Line strokes are depth tested but never write depth. This keeps the
        /// overlapping segments of curves and paths from occluding each other,
        /// which would otherwise cut visible seams at every joint. As a result,
        /// strokes are occluded by shapes and scene geometry, but they do not
        /// occlude other shapes themselves.
        /// </remarks>
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
            _drawingContext = new DrawingContext(
                mode,
                _commandQueue,
                transform,
                executionMode,
                enableDepth,
                _shaderLibrary
            );
            return _drawingContext;
        }

        private void CheckDrawingContext()
        {
            if (_drawingContext == null)
                throw new NullReferenceException("p5.Unite: Canvas not initialized. Call CreateCanvas() first.");
        }

        #region MonoBehaviour

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnCameraRendering;

            _transformStack.Reset();
            _commandQueue.Clear();
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


        #region Shader Library

#if UNITY_EDITOR
        private void Reset()
        {
            AssignDefaultShaderLibrary();
        }

        private void OnValidate()
        {
            AssignDefaultShaderLibrary();
        }

        /// <summary>
        /// Fills in the shader library shipped with the package, so that adding UCanvas to a
        /// GameObject never requires wiring the shaders by hand.
        /// </summary>
        private void AssignDefaultShaderLibrary()
        {
            if (_shaderLibrary != null)
                return;

            _shaderLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<UniteShaderLibrary>(
                UniteShaderLibrary.AssetPath
            );
        }
#endif

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

        /// <summary>
        /// The active drawing mode; throws the standard not-initialized error when the
        /// canvas has never been set up with CreateCanvas().
        /// </summary>
        internal DrawingMode CurrentDrawingMode
        {
            get
            {
                CheckDrawingContext();
                return _drawingContext.DrawingMode;
            }
        }

        /// <summary>
        /// Enqueues a fully-built draw command, bypassing the per-shape helpers. Used by
        /// optional packages (e.g., p5.Unite Text) that emit their own command batches
        /// through the same queue so draw order against shapes is preserved.
        /// </summary>
        internal void EnqueueRawCommand(DrawCommand command)
        {
            CheckDrawingContext();
            if (!isActiveAndEnabled)
                return;
            _commandQueue.Enqueue(command);
        }

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
