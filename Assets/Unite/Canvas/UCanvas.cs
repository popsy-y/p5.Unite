using System;
using System.Collections.Generic;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unite.Canvas
{
    public partial class UCanvas : MonoBehaviour
    {
        private DrawingContext _drawingContext;

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
        
        private Queue<DrawCommand> _commandQueue = new ();
        
        private void ClearCommandQueue(ScriptableRenderContext srpCtx, Camera cam) => _commandQueue.Clear();
        
        private Color _fillColor = Color.white;
        private Color _strokeColor = Color.black;


        private void Start()
        {
            RenderPipelineManager.endCameraRendering += ClearCommandQueue;
        }

        private void OnDestroy()
        {
            _drawingContext?.EndDrawing();
            RenderPipelineManager.endCameraRendering -= ClearCommandQueue;
        }
    }
}
