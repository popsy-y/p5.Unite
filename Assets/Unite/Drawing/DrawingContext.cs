using System;
using System.Collections.Generic;
using Unite.Canvas;
using UnityEngine;
using UnityEngine.Rendering;
using DH = Unite.Drawing.DrawingHelper;
using Object = UnityEngine.Object;
using PID = Unite.Drawing.ShaderPropertyIds;

namespace Unite.Drawing
{
    public sealed class DrawingContext : IDisposable
    {
        private Mesh _mesh;
        private Material _material;

        private Queue<DrawCommand> _commandQueue;

        public DrawingMode DrawingMode { get; private set; }
        private Transform _parentTransform;

        private GraphicsBuffer _shapeDataBuffer;
        private GraphicsBuffer _commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;

        public DrawingContext(DrawingMode mode, Queue<DrawCommand> commandQueue, Transform parentTransform = null)
        {
            DrawingMode = mode;

            _commandQueue = commandQueue;
            _parentTransform = parentTransform;

            _mesh = DH.UnitQuad;
            _mesh.RecalculateBounds();

            string shaderName = DrawingMode switch
            {
                DrawingMode.WorldSpace => "Hidden/Unite/WorldSpaceDrawing",
                DrawingMode.ObjectSpace => "Hidden/Unite/ObjectSpaceDrawing",
                _ => "Hidden/Unite/ClipSpaceDrawing"
            };

            _material = new Material(Shader.Find(shaderName));

            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        }

        public void Dispose()
        {
            _shapeDataBuffer?.Dispose();
            _commandBuffer?.Dispose();
            _shapeDataBuffer = null;
            _commandBuffer = null;

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
            if (_commandQueue.Count == 0) return;

            LayoutShapeIndirect(srpCtx, cam);
        }

        private void LayoutShapeIndirect(ScriptableRenderContext srpCtx, Camera cam)
        {
            int count = _commandQueue.Count;
            EnsureBuffers(count);

            var shapeDataArray = new ShapeData[count];
            int index = 0;

            bool isWorldOrObjectSpace = DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace;
            bool isOverlay = DrawingMode is DrawingMode.ClipSpaceOverlay or DrawingMode.ScreenSpaceOverlay;

            foreach (var cmd in _commandQueue)
            {
                Vector3 pos;
                Vector2 size;
                Vector4 rot = new Vector4(0, 0, 0, 1); // Identity quaternion

                // Quaternion from Euler
                Quaternion q = Quaternion.Euler(cmd.Rotation);
                rot = new Vector4(q.x, q.y, q.z, q.w);

                if (isWorldOrObjectSpace)
                {
                    pos = cmd.Position;
                    // Mesh width is 2. We want to draw 'cmd.Size'. Scale = cmd.Size / 2.
                    size = cmd.Size / 2f;
                }
                else
                {
                    // Clip Space / Screen Space

                    var sPos = cmd.Position;
                    pos = new Vector3(
                        sPos.x * 2f - 1f,
                        sPos.y * 2f - 1f,
                        isOverlay ? 0f : sPos.z
                    );

                    // Size
                    // cmd.Size is Normalized (0-1) relative to screen.
                    // Clip Space Width is 2.
                    // Scale * MeshWidth(2) = NormalizedSize * ClipSpaceWidth(2)?
                    // No. Normalized 1.0 = Width 2.0 (Clip).
                    // Scale * 2 = 1.0 * 2 -> Scale = 1.0.
                    // So Scale = cmd.Size.
                    size = cmd.Size;

                    // Previous code used * 2f. 
                    // "var clipScale = new Vector3(sSize.x * 2f, sSize.y * 2f, 1f);"
                    // If I am wrong and previous code was correct:
                    // Scale = cmd.Size * 2f means Normalized 0.5 fills the screen.
                    // I will stick to Scale = cmd.Size for now as it makes more sense logically.
                    // If shapes appear half size, I will multiply by 2.
                }

                shapeDataArray[index] = new ShapeData
                {
                    Position = pos,
                    Rotation = rot,
                    Size = size,
                    ShapeType = (int)cmd.ShapeType, // Circle=0, Rect=1, Square=2
                    FillColor = cmd.FillColor,
                    StrokeColor = cmd.StrokeColor
                };

                index++;
            }

            _shapeDataBuffer.SetData(shapeDataArray);
            _material.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);

            // For ObjectSpace mode, set the parent transform matrix
            if (DrawingMode == DrawingMode.ObjectSpace && _parentTransform != null)
            {
                _material.SetMatrix(PID.ParentTransform, _parentTransform.localToWorldMatrix);
            }

            // Indirect Args
            _commandData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
            _commandData[0].instanceCount = (uint)count;
            _commandBuffer.SetData(_commandData);

            RenderParams rp = new RenderParams(_material);
            // Use Camera Bounds as requested
            rp.worldBounds = new Bounds(cam.transform.position, Vector3.one * 10000f);
            rp.matProps = new MaterialPropertyBlock();

            // NOTE: Matrix is handled in shader via StructuredBuffer, 
            // but RenderMeshIndirect might need a matrix for culling if not using worldBounds properly?
            // We set worldBounds large, so culling shouldn't be an issue.
            // We don't need _ObjectToWorld in props because we don't use it in shader for positioning instances.

            Graphics.RenderMeshIndirect(rp, _mesh, _commandBuffer, 1);
        }

        private void EnsureBuffers(int count)
        {
            // ShapeData Buffer
            // Stride: Vector3(12) + Vector4(16) + Vector2(8) + int(4) + Vector4(16) + Vector4(16) = 72 bytes
            int stride = 72;

            if (_shapeDataBuffer == null || _shapeDataBuffer.count < count)
            {
                _shapeDataBuffer?.Dispose();
                _shapeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            }

            // Command Buffer
            if (_commandBuffer == null)
            {
                _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            }
        }
    }
}
