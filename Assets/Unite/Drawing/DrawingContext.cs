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

            bool isWorldOrObjectSpace = DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace;
            bool isOverlay = DrawingMode is DrawingMode.ClipSpaceOverlay or DrawingMode.ScreenSpaceOverlay;

            foreach (var cmd in _commandQueue)
            {
                Vector3 pos;
                Vector2 shapeSize;

                var q = Quaternion.Euler(cmd.Rotation);
                var rot = new Vector4(q.x, q.y, q.z, q.w);

                if (isWorldOrObjectSpace)
                {
                    pos = cmd.Position;
                    // Mesh spans [-1,1], so half-size = cmd.Size / 2
                    shapeSize = cmd.Size / 2f;
                }
                else
                {
                    // Clip/Screen space: remap [0,1] position to NDC [-1,1]
                    var sPos = cmd.Position;
                    pos = new Vector3(
                        sPos.x * 2f - 1f,
                        sPos.y * 2f - 1f,
                        isOverlay ? 0f : sPos.z
                    );
                    shapeSize = cmd.Size;
                }

                // Expand mesh to accommodate stroke outside the shape boundary
                // float sw = cmd.StrokeWeight;
                
                Vector2 sw;// stroke width in drawing mode unit

                if (DrawingMode is DrawingMode.WorldSpace or DrawingMode.ObjectSpace)
                {
                    sw = Vector2.one * cmd.StrokeWeight;
                }
                else if (DrawingMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay)
                {
                    // convert strokeWeight, pixel space -> mesh uv space
                    // uv空間で指定したstrokeWeightが欲しい
                    // strokeWeight is in pixel space...
                    
                }
                
                var size = shapeSize + Vector2.one * (sw * 2f);

                // Per-axis stroke weight in UV space: ratio of stroke to expanded mesh
                var strokeWeightUV = Vector2.zero;
                if (sw > 0f)
                {
                    if (size.x > 0f) strokeWeightUV.x = 0.5f * (1f - shapeSize.x / size.x);
                    if (size.y > 0f) strokeWeightUV.y = 0.5f * (1f - shapeSize.y / size.y);
                }

                shapeDataArray[index] = new ShapeData
                {
                    Position = pos,
                    Rotation = rot,
                    Size = size,
                    ShapeType = (int)cmd.ShapeType,
                    FillColor = cmd.FillColor,
                    StrokeColor = cmd.StrokeColor,
                    StrokeWeight = strokeWeightUV
                };

                index++;
            }

            _shapeDataBuffer.SetData(shapeDataArray);
            _material.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);
            
            _material.SetInt(PID.DrawingMode, (int) DrawingMode);

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
