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
        private DrawCommand[] _frameCommands = Array.Empty<DrawCommand>();

        public DrawingMode DrawingMode { get; private set; }
        public DrawingExecutionMode ExecutionMode { get; set; }
        public bool DebugShapeBounds { get; set; }
        private Transform _parentTransform;

        private GraphicsBuffer _shapeDataBuffer;
        private GraphicsBuffer _commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;

        public DrawingContext(
            DrawingMode mode,
            Queue<DrawCommand> commandQueue,
            Transform parentTransform = null,
            DrawingExecutionMode executionMode = DrawingExecutionMode.Indirect
        )
        {
            DrawingMode = mode;
            ExecutionMode = executionMode;

            _commandQueue = commandQueue;
            _parentTransform = parentTransform;

            _mesh = DH.UnitQuad;
            _mesh.RecalculateBounds();

            string shaderName = DrawingMode switch
            {
                DrawingMode.World => "Hidden/Unite/WorldDrawing",
                DrawingMode.CanvasLocal => "Hidden/Unite/CanvasLocalDrawing",
                _ => "Hidden/Unite/ScreenDrawing"
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
            if (_frameCommands.Length == 0) return;

            LayoutShapes(cam, _frameCommands);
        }

        public void CaptureCommands()
        {
            if (_commandQueue.Count == 0)
            {
                _frameCommands = Array.Empty<DrawCommand>();
                return;
            }

            _frameCommands = _commandQueue.ToArray();
            _commandQueue.Clear();
        }

        private void LayoutShapes(Camera cam, IReadOnlyList<DrawCommand> commands)
        {
            int count = commands.Count;
            EnsureBuffers(count);

            var shapeDataArray = new ShapeData[count];
            int index = 0;

            foreach (var cmd in commands)
            {
                var q = Quaternion.Euler(cmd.Rotation);
                var rot = new Vector4(q.x, q.y, q.z, q.w);
                var shapeSize = new Vector2(Mathf.Abs(cmd.Size.x), Mathf.Abs(cmd.Size.y));
                var halfSize = shapeSize * 0.5f;
                float strokeWeight = cmd.StrokeEnabled ? Mathf.Max(0f, cmd.StrokeWeight) : 0f;
                float padding = strokeWeight * 0.5f + AntiAliasPadding(cam);
                var padding2 = Vector2.one * padding;
                int styleFlags = (cmd.FillEnabled ? 1 : 0) |
                                 (cmd.StrokeEnabled && strokeWeight > 0f ? 2 : 0);
                Vector2 boundsMin = cmd.HasCustomBounds ? cmd.BoundsMin : -halfSize;
                Vector2 boundsMax = cmd.HasCustomBounds ? cmd.BoundsMax : halfSize;

                shapeDataArray[index] = new ShapeData
                {
                    Position = new Vector4(cmd.Position.x, cmd.Position.y, cmd.Position.z, 0f),
                    Rotation = rot,
                    Bounds = new Vector4(
                        boundsMin.x - padding2.x,
                        boundsMin.y - padding2.y,
                        boundsMax.x + padding2.x,
                        boundsMax.y + padding2.y
                    ),
                    ShapeParameters = new Vector4(shapeSize.x, shapeSize.y, 0f, 0f),
                    Point0 = cmd.Point0,
                    Point1 = cmd.Point1,
                    Point2 = cmd.Point2,
                    Point3 = cmd.Point3,
                    Parameters0 = cmd.Parameters0,
                    Parameters1 = cmd.Parameters1,
                    CornerRadiiX = cmd.CornerRadiiX,
                    CornerRadiiY = cmd.CornerRadiiY,
                    Style = new Vector4(
                        GpuShapeType(cmd.ShapeType),
                        (float)cmd.ArcMode,
                        strokeWeight,
                        styleFlags
                    ),
                    FillColor = cmd.FillColor,
                    StrokeColor = cmd.StrokeColor
                };

                index++;
            }

            _shapeDataBuffer.SetData(shapeDataArray);
            if (ExecutionMode == DrawingExecutionMode.Sequential)
            {
                RenderSequential(cam, count);
                return;
            }

            RenderIndirect(cam, count);
        }

        private void RenderIndirect(Camera cam, int count)
        {
            var rp = CreateRenderParams(cam, 0);

            _commandData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
            _commandData[0].instanceCount = (uint)count;
            _commandBuffer.SetData(_commandData);

            Graphics.RenderMeshIndirect(rp, _mesh, _commandBuffer, 1);
        }

        private void RenderSequential(Camera cam, int count)
        {
            for (int index = 0; index < count; index++)
            {
                var rp = CreateRenderParams(cam, index);
                Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
            }
        }

        private RenderParams CreateRenderParams(Camera cam, int shapeIndex)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);
            properties.SetInt(PID.DrawingMode, (int)DrawingMode);
            properties.SetInt(PID.ShapeIndex, shapeIndex);
            properties.SetInt(
                PID.UseShapeIndex,
                ExecutionMode == DrawingExecutionMode.Sequential ? 1 : 0
            );
            properties.SetInt(PID.DebugShapeBounds, DebugShapeBounds ? 1 : 0);

            if (DrawingMode == DrawingMode.CanvasLocal && _parentTransform != null)
            {
                properties.SetMatrix(PID.ParentTransform, _parentTransform.localToWorldMatrix);
            }

            return new RenderParams(_material)
            {
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 10000f),
                camera = cam,
                matProps = properties
            };
        }

        private float AntiAliasPadding(Camera cam)
        {
            if (DrawingMode is DrawingMode.Screen or DrawingMode.ScreenOverlay)
                return 1.5f;

            if (DrawingMode is DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay)
                return 1.5f / Mathf.Max(1f, Mathf.Min(cam.pixelWidth, cam.pixelHeight));

            return 0.001f;
        }

        private static float GpuShapeType(Shape shape)
        {
            return shape switch
            {
                Shape.Circle or Shape.Ellipse => 0f,
                Shape.Rectangle or Shape.Square => 1f,
                Shape.Line => 2f,
                Shape.Point => 3f,
                Shape.Triangle => 4f,
                Shape.Quad => 5f,
                Shape.Arc => 6f,
                _ => 0f
            };
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
