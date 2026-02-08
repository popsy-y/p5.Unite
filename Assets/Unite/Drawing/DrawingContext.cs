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

                // Original shape size (before stroke expansion)
                Vector2 shapeSize;

                if (isWorldOrObjectSpace)
                {
                    pos = cmd.Position;
                    // Mesh width is 2. We want to draw 'cmd.Size'. Scale = cmd.Size / 2.
                    shapeSize = cmd.Size / 2f;
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

                    shapeSize = cmd.Size;
                }

                // Expand mesh size by stroke weight so stroke is visible outside the shape.
                // cmd.StrokeWeight is in the same coordinate space as shapeSize.
                // Mesh vertices span [-1, 1], so scale = half-size.
                // We need to add strokeWeight to each side, so add strokeWeight to the scale.
                float sw = cmd.StrokeWeight;
                if (isWorldOrObjectSpace)
                {
                    // shapeSize is already cmd.Size / 2. StrokeWeight is in world units (full).
                    // We need to add sw/2 to each half-extent (sw to diameter -> sw/2 to radius).
                    size = shapeSize + Vector2.one * (sw / 2f);
                }
                else
                {
                    // shapeSize = cmd.Size (normalized). StrokeWeight is normalized.
                    // Add sw to each side of the shape (sw is full width, half per side).
                    size = shapeSize + Vector2.one * (sw / 2f);
                }

                // Compute stroke weight in UV space for the expanded mesh.
                // strokeWeightUV = portion of UV [0, 0.5] that the stroke occupies on each side.
                // Shape edge in UV from center = (shapeSize / expandedSize) * 0.5
                // strokeWeightUV = 0.5 - shapeEdge = 0.5 * (1 - shapeSize / expandedSize)
                float strokeWeightUV = 0f;
                if (sw > 0f)
                {
                    float maxExpandedSize = Mathf.Max(size.x, size.y);
                    if (maxExpandedSize > 0f)
                    {
                        float maxShapeSize = Mathf.Max(shapeSize.x, shapeSize.y);
                        strokeWeightUV = 0.5f * (1f - maxShapeSize / maxExpandedSize);
                    }
                }

                shapeDataArray[index] = new ShapeData
                {
                    Position = pos,
                    Rotation = rot,
                    Size = size,
                    ShapeType = (int)cmd.ShapeType, // Circle=0, Rect=1, Square=2
                    FillColor = cmd.FillColor,
                    StrokeColor = cmd.StrokeColor,
                    StrokeWeight = strokeWeightUV
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
            // Stride: Vector3(12) + Vector4(16) + Vector2(8) + int(4) + Vector4(16) + Vector4(16) + float(4) = 76 bytes
            int stride = 76;

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
