using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        public void Draw(Camera cam)
        {
            if (_commandQueue.Count == 0) return;

            LayoutShapeIndirect(cam);
        }

        private void LayoutShapeIndirect(Camera cam)
        {
            int count = _commandQueue.Count;
            EnsureBuffers(count);

            var shapeDataArray = new ShapeData[count];
            int index = 0;

            foreach (var cmd in _commandQueue)
            {
                var q = Quaternion.Euler(cmd.Rotation);
                var rot = new Vector4(q.x, q.y, q.z, q.w);
                var shapeSize = new Vector2(Mathf.Abs(cmd.Size.x), Mathf.Abs(cmd.Size.y));
                var halfSize = shapeSize * 0.5f;
                float strokeWeight = Mathf.Max(0f, cmd.StrokeWeight);
                float padding = strokeWeight * 0.5f + AntiAliasPadding(cam);
                var padding2 = Vector2.one * padding;

                shapeDataArray[index] = new ShapeData
                {
                    Position = new Vector4(cmd.Position.x, cmd.Position.y, cmd.Position.z, 0f),
                    Rotation = rot,
                    Bounds = new Vector4(
                        -halfSize.x - padding2.x,
                        -halfSize.y - padding2.y,
                        halfSize.x + padding2.x,
                        halfSize.y + padding2.y
                    ),
                    ShapeParameters = new Vector4(shapeSize.x, shapeSize.y, 0f, 0f),
                    Style = new Vector4(
                        cmd.ShapeType == Shape.Circle ? 0f : 1f,
                        0f,
                        strokeWeight,
                        strokeWeight > 0f ? 3f : 1f
                    ),
                    FillColor = cmd.FillColor,
                    StrokeColor = cmd.StrokeColor
                };

                index++;
            }

            _shapeDataBuffer.SetData(shapeDataArray);
            _material.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);

            _material.SetInt(PID.DrawingMode, (int)DrawingMode);

            if (DrawingMode == DrawingMode.ObjectSpace && _parentTransform != null)
            {
                _material.SetMatrix(PID.ParentTransform, _parentTransform.localToWorldMatrix);
            }

            _commandData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
            _commandData[0].instanceCount = (uint)count;
            _commandBuffer.SetData(_commandData);

            var rp = new RenderParams(_material)
            {
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 10000f),
                matProps = new MaterialPropertyBlock()
            };

            Graphics.RenderMeshIndirect(rp, _mesh, _commandBuffer, 1);
        }

        private float AntiAliasPadding(Camera cam)
        {
            if (DrawingMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                return 1.5f;

            if (DrawingMode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay)
                return 1.5f / Mathf.Max(1f, Mathf.Min(cam.pixelWidth, cam.pixelHeight));

            return 0.001f;
        }

        private void EnsureBuffers(int count)
        {
            int stride = Marshal.SizeOf<ShapeData>();

            if (_shapeDataBuffer == null || _shapeDataBuffer.count < count)
            {
                _shapeDataBuffer?.Dispose();
                _shapeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            }

            if (_commandBuffer == null)
            {
                _commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            }
        }
    }
}
