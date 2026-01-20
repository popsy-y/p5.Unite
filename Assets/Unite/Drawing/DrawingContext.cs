using System;
using System.Collections.Generic;
using Unite.Canvas;
using UnityEngine;
using UnityEngine.Rendering;
using DH = Unite.Drawing.DrawingHelper;

namespace Unite.Drawing
{
    public class DrawingContext
    {
        private Mesh _mesh;
        private Material _material;

        private Queue<DrawCommand> _commandQueue;

        public DrawingMode DrawingMode { get; private set; }

        public DrawingContext(DrawingMode mode, Queue<DrawCommand> commandQueue)
        {
            _commandQueue = commandQueue;

            _mesh = DH.UnitQuad;
            _mesh.RecalculateBounds();

            _material = new Material(Shader.Find("Hidden/Unite/Drawing"));

            DrawingMode = mode;
            switch (DrawingMode)
            {
                case DrawingMode.ScreenSpaceOverlay:
                    RenderPipelineManager.beginCameraRendering += LayoutMeshSsO;
                    break;
                case DrawingMode.ScreenSpace:
                    RenderPipelineManager.beginCameraRendering += LayoutMeshSs;
                    break;
                case DrawingMode.ViewSpaceOverlay:
                    RenderPipelineManager.beginCameraRendering += LayoutMeshVsO;
                    break;
                case DrawingMode.ViewSpace:
                    RenderPipelineManager.beginCameraRendering += LayoutMeshVs;
                    break;
                case DrawingMode.WorldSpace:
                    RenderPipelineManager.beginCameraRendering += LayoutMeshWs;
                    break;
            }
        }

        public void EndDrawing()
        {
            switch (DrawingMode)
            {
                case DrawingMode.ScreenSpaceOverlay:
                    RenderPipelineManager.beginCameraRendering -= LayoutMeshSsO;
                    break;
                case DrawingMode.ScreenSpace:
                    RenderPipelineManager.beginCameraRendering -= LayoutMeshSs;
                    break;
                case DrawingMode.ViewSpaceOverlay:
                    RenderPipelineManager.beginCameraRendering -= LayoutMeshVsO;
                    break;
                case DrawingMode.ViewSpace:
                    RenderPipelineManager.beginCameraRendering -= LayoutMeshVs;
                    break;
                case DrawingMode.WorldSpace:
                    RenderPipelineManager.beginCameraRendering -= LayoutMeshWs;
                    break;
            }
        }

        // UNIT VIEWPORT: (0, 0) TO (1, 1)

        private void LayoutMeshSsO(ScriptableRenderContext srpCtx, Camera cam)
        {
            throw new NotImplementedException();
        }

        private void LayoutMeshSs(ScriptableRenderContext srpCtx, Camera cam)
        {
            // implement this with RenderMeshIndirect in the future, but something dumb but work is important for now.

            var scr = cam.pixelRect;

            var rp = new RenderParams(_material);

            foreach (var cmd in _commandQueue)
            {
                // POSITION
                // first we have pixel coordinate on screen 
                var pos = cmd.Position;
                // convert pixel coordinate to view space position; push z onto near clip so it renders
                var vsPos = DH.V3DivV3(pos, new Vector3(scr.width, scr.height, 1f));
                if (vsPos.z <= 0f) vsPos.z = cam.nearClipPlane + 0.01f;
                Vector3 ws = DH.Vs2Ws(vsPos, cam);

                // ROTATION
                float zRot = cmd.Rotation;

                // SCALE
                // first we have pixel coordinate size of this shape
                var size = cmd.Size;
                // convert pixel size to view space size
                var vsSize = DH.V2DivV2(size, new Vector2(scr.width, scr.height));
                var wsSize = cam.ViewportToWorldPoint(new Vector3(vsPos.x + vsSize.x, vsPos.y + vsSize.y, vsPos.z)) -
                             cam.ViewportToWorldPoint(new Vector3(vsPos.x, vsPos.y, vsPos.z));

                var model = Matrix4x4.TRS(ws, Quaternion.Euler(0f, 0f, zRot), new Vector3(wsSize.x, wsSize.y, 1f));

                Graphics.RenderMesh(rp, _mesh, 0, model);
            }
        }

        private void LayoutMeshVsO(ScriptableRenderContext srpCtx, Camera cam)
        {
            throw new NotImplementedException();
        }

        private void LayoutMeshVs(ScriptableRenderContext srpCtx, Camera cam)
        {
            throw new NotImplementedException();
        }

        private void LayoutMeshWs(ScriptableRenderContext srpCtx, Camera cam)
        {
            throw new NotImplementedException();
        }
    }
}