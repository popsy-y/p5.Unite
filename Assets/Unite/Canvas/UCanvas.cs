using System;
using System.Collections.Generic;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Canvas
{
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

        public DrawingContext CreateCanvas(
            DrawingMode mode = DrawingMode.ScreenSpaceOverlay,
            DrawingExecutionMode executionMode = DrawingExecutionMode.Indirect
        )
        {
            _drawingContext?.Dispose();
            _drawingContext = new DrawingContext(mode, _commandQueue, transform, executionMode);
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

        private Color _fillColor = Color.white;
        private Color _strokeColor = Color.black;
        private float _strokeWeight = 1f;
        private bool _fillEnabled = true;
        private bool _strokeEnabled = true;

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

                if (m is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
                    return _currentCamera.pixelWidth;

                if (m is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                    return _currentCamera.pixelWidth;

                if (m is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
                    return -1; // there's no width in world space or object space

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

                if (m is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
                    return _currentCamera.pixelHeight;

                if (m is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                    return _currentCamera.pixelHeight;

                if (m is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
                    return -1; // there's no height in world space or object space

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
