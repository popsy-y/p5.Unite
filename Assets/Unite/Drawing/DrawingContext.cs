using System;
using System.Collections.Generic;
using Unite.Canvas;
using UnityEngine;
using UnityEngine.Rendering;
using DH = Unite.Drawing.DrawingHelper;
using Object = UnityEngine.Object;

namespace Unite.Drawing
{
    public class DrawingContext : IDisposable
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
        }

        public void Dispose()
        {
            #if UNITY_EDITOR
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_mesh);
            #else
            Object.Destroy(_material);
            Object.Destroy(_mesh);
            #endif
        }

        public void Draw(ScriptableRenderContext srpCtx, Camera cam)
        {
            switch (DrawingMode)
            {
                case DrawingMode.ScreenSpaceOverlay:
                    LayoutHomogenousShape(srpCtx, cam);
                    break;
                case DrawingMode.ScreenSpace:
                    LayoutHomogenousShape(srpCtx, cam);
                    break;
                case DrawingMode.ClipSpaceOverlay:
                    LayoutHomogenousShape(srpCtx, cam);
                    break;
                case DrawingMode.ClipSpace:
                    LayoutHomogenousShape(srpCtx, cam);
                    break;
                case DrawingMode.WorldSpace:
                    LayoutMeshWs(srpCtx, cam);
                    break;
            }
        }

        private void LayoutHomogenousShape(ScriptableRenderContext srpCtx, Camera cam)
        {
            var rp = new RenderParams(_material);
            rp.worldBounds = new Bounds(cam.transform.position, Vector3.one);

            foreach (var cmd in _commandQueue)
            {
                var sPos = cmd.Position;
                var sSize = cmd.Size;
                var rot = cmd.Rotation;

                var clipPos = new Vector3(
                    sPos.x * 2f - 1f,
                    sPos.y * 2f - 1f,
                    DrawingMode is DrawingMode.ClipSpaceOverlay or DrawingMode.ScreenSpaceOverlay ? 1f : sPos.z
                );

                var clipScale = new Vector3(
                    sSize.x * 2f,
                    sSize.y * 2f,
                    1f
                );
                
                var clip = Matrix4x4.TRS(clipPos, Quaternion.AngleAxis(rot, Vector3.forward), clipScale);
                
                // objectToWorld is now OBJ -> CLIP
                Graphics.RenderMesh(rp, _mesh, 0, clip);
            }
        }
        
        private void LayoutMeshWs(ScriptableRenderContext srpCtx, Camera cam)
        {
            throw new NotImplementedException();
        }
    }
}