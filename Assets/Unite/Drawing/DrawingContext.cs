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
            DrawingMode = mode;
            
            _commandQueue = commandQueue;

            _mesh = DH.UnitQuad;
            _mesh.RecalculateBounds();
            
            _material = new Material(Shader.Find((
                DrawingMode is DrawingMode.WorldSpace ? 
                "Hidden/Unite/WorldSpaceDrawing" :
                "Hidden/Unite/ClipSpaceDrawing"
            )));
            
            Debug.Log(_material.shader.name);
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
                    LayoutClipSpaceShape(srpCtx, cam);
                    break;
                case DrawingMode.ScreenSpace:
                    LayoutClipSpaceShape(srpCtx, cam);
                    break;
                case DrawingMode.ClipSpaceOverlay:
                    LayoutClipSpaceShape(srpCtx, cam);
                    break;
                case DrawingMode.ClipSpace:
                    LayoutClipSpaceShape(srpCtx, cam);
                    break;
                case DrawingMode.WorldSpace:
                    LayoutMeshWs(srpCtx, cam);
                    break;
            }
        }

        private void LayoutClipSpaceShape(ScriptableRenderContext srpCtx, Camera cam)
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
                    DrawingMode is DrawingMode.ClipSpaceOverlay or DrawingMode.ScreenSpaceOverlay ? 0f : sPos.z
                );

                var clipScale = new Vector3(
                    sSize.x * 2f,
                    sSize.y * 2f,
                    1f
                );
                
                var obj2Clip = Matrix4x4.TRS(clipPos, Quaternion.Euler(rot), clipScale);
                
                // objectToWorld is now OBJ -> CLIP
                Graphics.RenderMesh(rp, _mesh, 0, obj2Clip);
            }
        }
        
        private void LayoutMeshWs(ScriptableRenderContext srpCtx, Camera cam)
        {
            var rp = new RenderParams(_material);

            foreach (var cmd in _commandQueue)
            {
                var obj2World = Matrix4x4.TRS(
                    cmd.Position,
                    Quaternion.Euler(cmd.Rotation),
                    new Vector3(cmd.Size.x / 2, cmd.Size.y / 2, 1f)// divide by 2, because "Size" means full width or height but unit quad is -1 to 1
                );

                rp.worldBounds = new Bounds(cmd.Position, new Vector3(cmd.Size.x, cmd.Size.y, 0.1f));
                Graphics.RenderMesh(rp, _mesh, 0, obj2World);
            }
        }
    }
}