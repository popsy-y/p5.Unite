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
        private Material _depthPrepassMaterial;

        private Queue<DrawCommand> _commandQueue;
        private DrawCommand[] _frameCommands = Array.Empty<DrawCommand>();

        public DrawingMode DrawingMode { get; private set; }
        public DrawingExecutionMode ExecutionMode { get; set; }
        public bool DebugShapeBounds { get; set; }
        private Transform _parentTransform;

        private bool _enableDepth;

        /// <summary>
        /// Enables depth testing and depth writes for this canvas.
        /// When enabled, shapes are occluded by scene geometry and occlude each other.
        /// When disabled, shapes are drawn in draw order regardless of the depth buffer.
        /// Overlay drawing modes always ignore depth.
        /// </summary>
        public bool EnableDepth
        {
            get => _enableDepth;
            set
            {
                if (_enableDepth == value) return;
                _enableDepth = value;
                ApplyDepthState();
            }
        }

        private bool DepthIsActive =>
            _enableDepth &&
            DrawingMode is not (DrawingMode.ScreenOverlay or DrawingMode.NormalizedScreenOverlay);

        private GraphicsBuffer _shapeDataBuffer;
        private GraphicsBuffer _commandBuffer;
        private GraphicsBuffer _polygonPointBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
        private readonly List<Vector4> _polygonPoints = new();

        public DrawingContext(
            DrawingMode mode,
            Queue<DrawCommand> commandQueue,
            Transform parentTransform = null,
            DrawingExecutionMode executionMode = DrawingExecutionMode.Indirect,
            bool enableDepth = false
        )
        {
            DrawingMode = mode;
            ExecutionMode = executionMode;
            _enableDepth = enableDepth;

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
            _material.SetFloat(PID.SrcBlend, (float)BlendMode.One);
            _material.SetFloat(PID.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat(PID.ZWrite, 0f);

            _depthPrepassMaterial = new Material(Shader.Find(shaderName));
            _depthPrepassMaterial.SetFloat(PID.SrcBlend, (float)BlendMode.Zero);
            _depthPrepassMaterial.SetFloat(PID.DstBlend, (float)BlendMode.One);
            _depthPrepassMaterial.SetFloat(PID.ZWrite, 1f);
            _depthPrepassMaterial.EnableKeyword("_UNITE_DEPTH_PREPASS");
            ApplyDepthState();

            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        }

        public void Dispose()
        {
            _shapeDataBuffer?.Dispose();
            _commandBuffer?.Dispose();
            _polygonPointBuffer?.Dispose();
            _shapeDataBuffer = null;
            _commandBuffer = null;
            _polygonPointBuffer = null;

#if UNITY_EDITOR
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_depthPrepassMaterial);
            Object.DestroyImmediate(_mesh);
#else
            Object.Destroy(_material);
            Object.Destroy(_depthPrepassMaterial);
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
            _polygonPoints.Clear();
            int index = 0;

            foreach (var cmd in commands)
            {
                var transform = cmd.Transform == Matrix4x4.zero
                    ? Matrix4x4.TRS(cmd.Position, Quaternion.Euler(cmd.Rotation), Vector3.one)
                    : cmd.Transform;
                var shapeSize = new Vector2(Mathf.Abs(cmd.Size.x), Mathf.Abs(cmd.Size.y));
                var halfSize = shapeSize * 0.5f;
                float strokeWeight = cmd.StrokeEnabled ? Mathf.Max(0f, cmd.StrokeWeight) : 0f;
                float padding = strokeWeight * 0.5f + AntiAliasPadding(cam, transform);
                var padding2 = Vector2.one * padding;
                int styleFlags = (cmd.FillEnabled ? 1 : 0) |
                                 (cmd.StrokeEnabled && strokeWeight > 0f ? 2 : 0);
                Vector2 boundsMin = cmd.HasCustomBounds ? cmd.BoundsMin : -halfSize;
                Vector2 boundsMax = cmd.HasCustomBounds ? cmd.BoundsMax : halfSize;

                Vector4 point0 = cmd.Point0;
                Vector4 point1 = cmd.Point1;
                Vector4 parameters0 = cmd.Parameters0;

                if (cmd.ShapeType == Shape.Line3D)
                {
                    point0 = new Vector4(
                        cmd.LineStart.x,
                        cmd.LineStart.y,
                        cmd.LineStart.z,
                        0f
                    );
                    point1 = new Vector4(
                        cmd.LineEnd.x,
                        cmd.LineEnd.y,
                        cmd.LineEnd.z,
                        0f
                    );
                }

                if (cmd.ShapeType is Shape.Line or Shape.Line3D)
                {
                    parameters0 = new Vector4(
                        cmd.LineCaps,
                        cmd.LinePerspective ? 1f : 0f,
                        0f,
                        0f
                    );
                }

                if (cmd.ShapeType == Shape.PolygonFill && cmd.PolygonPoints != null)
                {
                    parameters0 = new Vector4(
                        _polygonPoints.Count,
                        cmd.PolygonPoints.Length,
                        0f,
                        0f
                    );
                    _polygonPoints.AddRange(cmd.PolygonPoints);
                }

                shapeDataArray[index] = new ShapeData
                {
                    Transform0 = transform.GetColumn(0),
                    Transform1 = transform.GetColumn(1),
                    Transform2 = transform.GetColumn(2),
                    Transform3 = transform.GetColumn(3),
                    Bounds = new Vector4(
                        boundsMin.x - padding2.x,
                        boundsMin.y - padding2.y,
                        boundsMax.x + padding2.x,
                        boundsMax.y + padding2.y
                    ),
                    ShapeParameters = new Vector4(shapeSize.x, shapeSize.y, 0f, 0f),
                    Point0 = point0,
                    Point1 = point1,
                    Point2 = cmd.Point2,
                    Point3 = cmd.Point3,
                    Parameters0 = parameters0,
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
            EnsurePolygonPointBuffer(_polygonPoints.Count);
            if (_polygonPoints.Count > 0)
                _polygonPointBuffer.SetData(_polygonPoints);
            if (ExecutionMode == DrawingExecutionMode.Sequential)
            {
                RenderSequential(cam, count);
                return;
            }

            RenderIndirect(cam, count);
        }

        private void RenderIndirect(Camera cam, int count)
        {
            _commandData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
            _commandData[0].instanceCount = (uint)count;
            _commandBuffer.SetData(_commandData);

            if (DepthIsActive)
            {
                var prepassParams = CreateRenderParams(cam, 0, useDepthPrepass: true);
                Graphics.RenderMeshIndirect(prepassParams, _mesh, _commandBuffer, 1);
            }

            var rp = CreateRenderParams(cam, 0);
            Graphics.RenderMeshIndirect(rp, _mesh, _commandBuffer, 1);
        }

        private void RenderSequential(Camera cam, int count)
        {
            if (DepthIsActive)
            {
                for (int index = 0; index < count; index++)
                {
                    var prepassParams = CreateRenderParams(cam, index, useDepthPrepass: true);
                    Graphics.RenderMesh(prepassParams, _mesh, 0, Matrix4x4.identity);
                }
            }

            for (int index = 0; index < count; index++)
            {
                var rp = CreateRenderParams(cam, index);
                Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
            }
        }

        private RenderParams CreateRenderParams(
            Camera cam,
            int shapeIndex,
            bool useDepthPrepass = false
        )
        {
            var properties = new MaterialPropertyBlock();
            properties.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);
            properties.SetBuffer(PID.PolygonPoints, _polygonPointBuffer);
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

            return new RenderParams(useDepthPrepass ? _depthPrepassMaterial : _material)
            {
                worldBounds = new Bounds(cam.transform.position, Vector3.one * 10000f),
                camera = cam,
                matProps = properties
            };
        }

        private void ApplyDepthState()
        {
            _material.SetFloat(
                PID.ZTest,
                (float)(DepthIsActive ? CompareFunction.LessEqual : CompareFunction.Always)
            );
            _depthPrepassMaterial.SetFloat(PID.ZTest, (float)CompareFunction.LessEqual);
        }

        private float AntiAliasPadding(Camera cam, Matrix4x4 transform)
        {
            float drawingPadding;
            if (DrawingMode is DrawingMode.Screen or DrawingMode.ScreenOverlay)
                drawingPadding = 1.5f;
            else if (DrawingMode is DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay)
                drawingPadding = 1.5f / Mathf.Max(1f, Mathf.Min(cam.pixelWidth, cam.pixelHeight));
            else
                drawingPadding = 0.001f;

            var xAxis = new Vector3(transform.m00, transform.m10, transform.m20);
            var yAxis = new Vector3(transform.m01, transform.m11, transform.m21);
            float xx = Vector3.Dot(xAxis, xAxis);
            float yy = Vector3.Dot(yAxis, yAxis);
            float xy = Vector3.Dot(xAxis, yAxis);
            float discriminant = Mathf.Sqrt(Mathf.Max(0f, (xx - yy) * (xx - yy) + 4f * xy * xy));
            float minimumScale = Mathf.Sqrt(Mathf.Max(0f, (xx + yy - discriminant) * 0.5f));

            return drawingPadding / Mathf.Max(0.0001f, minimumScale);
        }

        private static float GpuShapeType(Shape shape)
        {
            return shape switch
            {
                Shape.Circle or Shape.Ellipse => 0f,
                Shape.Rectangle or Shape.Square => 1f,
                Shape.Line => 2f,
                Shape.Line3D => 7f,
                Shape.Point => 3f,
                Shape.Triangle => 4f,
                Shape.Quad => 5f,
                Shape.Arc => 6f,
                Shape.PolygonFill => 8f,
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

        private void EnsurePolygonPointBuffer(int count)
        {
            count = Mathf.Max(1, count);
            if (_polygonPointBuffer != null && _polygonPointBuffer.count >= count)
                return;

            _polygonPointBuffer?.Dispose();
            _polygonPointBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                count,
                sizeof(float) * 4
            );
        }
    }
}
