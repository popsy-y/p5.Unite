using System;
using System.Collections.Generic;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Canvas
{
    public partial class UCanvas : MonoBehaviour
    {
        [SerializeField] [HideInInspector] private DrawingContext _drawingContext;

        public DrawingContext CreateCanvas(DrawingMode mode)
        {
            _drawingContext =  new DrawingContext(mode, _commandQueue);
            return _drawingContext;
        }

        private void CheckDrawingContext()
        {
            if (_drawingContext == null)
                    throw new NullReferenceException("p5.Unite: Canvas not initialized. Call CreateCanvas() first.");
        }
        
        # region MonoBehaviour
        
        private void Start()
        {
            RenderPipelineManager.beginCameraRendering += OnCameraRendering;
            RenderPipelineManager.endCameraRendering += AfterCameraRendering;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnCameraRendering;
            RenderPipelineManager.endCameraRendering -= AfterCameraRendering;
            
            _drawingContext.Dispose();
        }

        private void OnCameraRendering(ScriptableRenderContext srpCtx, Camera cam)
        {
            CheckCurrentCamera(srpCtx, cam);
            
            if (!_currentCamera) return;
            _drawingContext.Draw(srpCtx, cam);
        }

        private void AfterCameraRendering(ScriptableRenderContext srpCtx, Camera cam)
        {
            ClearCommandQueue(srpCtx, cam);
        }
        
        #endregion
        
        
        #region Camera Management
        
        private Camera _currentCamera;
        private void CheckCurrentCamera(ScriptableRenderContext srpCtx, Camera cam)
        {
            if (!Equals(_currentCamera, cam))
            {
                _currentCamera = cam;
            }
        }
        
        #endregion


        #region Draw Command

        private Queue<DrawCommand> _commandQueue = new ();
        private void ClearCommandQueue(ScriptableRenderContext srpCtx, Camera cam) => _commandQueue.Clear();

        #endregion
        
        
        #region Color
        
        private Color _fillColor = Color.white;
        private Color _strokeColor = Color.black;
        
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
                };
                
                var m = _drawingContext.DrawingMode;

                if (m is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
                    return _currentCamera.pixelWidth;

                if (m is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                    return _currentCamera.pixelWidth;

                if (m == DrawingMode.WorldSpace)
                    return -1; // there's no width in world space
                
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
                };
        
                var m = _drawingContext.DrawingMode;
        
                if (m is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
                    return _currentCamera.pixelHeight;

                if (m is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                    return _currentCamera.pixelHeight;
        
                if (m == DrawingMode.WorldSpace)
                    return -1; // there's no height in world space
        
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
