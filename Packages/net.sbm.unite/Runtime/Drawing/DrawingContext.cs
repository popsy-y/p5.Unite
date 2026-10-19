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

        // The canvas records into _commandQueue; CaptureCommands swaps it with _frame, the
        // commands being drawn. Both only ever grow, so steady frames allocate nothing.
        private readonly DrawCommandBuffer _commandQueue;
        private readonly DrawCommandBuffer _frame = new();
        private int _frameCommandCount;
        private ShapeData[] _shapeDataScratch = Array.Empty<ShapeData>();

        // The shape buffer depends on the camera only through the anti-aliasing padding, so
        // cameras with the same padding (Game and Scene view, stacked cameras) reuse the
        // buffer built for the first one this frame.
        private int _captureGeneration;
        private int _builtGeneration = -1;
        private float _builtPixelPadding = float.NaN;

        // Submission index of the draws of the current camera (sets their sort distance).
        private int _submitOrder;

        // Property blocks are recycled frame to frame instead of allocating one per draw
        // call. Within a frame every draw gets its own block, so it does not matter whether
        // a submitted draw still refers to its block until the camera renders.
        private readonly List<MaterialPropertyBlock> _propertyBlocks = new();
        private int _propertyBlocksInUse;

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

        // MaterialPropertyBlock textures do not reach RenderMeshIndirect reliably, so
        // every atlas page gets its own material variant that owns the texture binding.
        private readonly Dictionary<Texture, Material> _glyphMaterials = new();

        internal DrawingContext(
            DrawingMode mode,
            DrawCommandBuffer commandQueue,
            Transform parentTransform = null,
            DrawingExecutionMode executionMode = DrawingExecutionMode.Indirect,
            bool enableDepth = false,
            UniteShaderLibrary shaderLibrary = null
        )
        {
            DrawingMode = mode;
            ExecutionMode = executionMode;
            _enableDepth = enableDepth;

            _commandQueue = commandQueue;
            _parentTransform = parentTransform;

            _mesh = DH.UnitQuad;
            _mesh.RecalculateBounds();

            var shader = ResolveShader(shaderLibrary);
            if (shader == null)
            {
                throw new NullReferenceException(
                    $"p5.Unite: shader '{UniteShaderLibrary.ShaderNameFor(DrawingMode)}' was not found. "
                    + "The UniteShaderLibrary asset is missing or has no shader assigned."
                );
            }

            _material = new Material(shader);
            _material.SetFloat(PID.SrcBlend, (float)BlendMode.One);
            _material.SetFloat(PID.DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            _material.SetFloat(PID.ZWrite, 0f);

            _depthPrepassMaterial = new Material(shader);
            _depthPrepassMaterial.SetFloat(PID.SrcBlend, (float)BlendMode.Zero);
            _depthPrepassMaterial.SetFloat(PID.DstBlend, (float)BlendMode.One);
            _depthPrepassMaterial.SetFloat(PID.ZWrite, 1f);
            _depthPrepassMaterial.EnableKeyword("_UNITE_DEPTH_PREPASS");
            ApplyDepthState();

            _commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        }

        private Shader ResolveShader(UniteShaderLibrary shaderLibrary)
        {
            var library = shaderLibrary != null ? shaderLibrary : UniteShaderLibrary.Instance;
            var shader = library != null ? library.GetShader(DrawingMode) : null;

            // Shader.Find alone does not create a build dependency, so it is only a safety net
            // for shaders that are included in the build by other means.
            return shader != null ? shader : Shader.Find(UniteShaderLibrary.ShaderNameFor(DrawingMode));
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
            foreach (var glyphMaterial in _glyphMaterials.Values)
                Object.DestroyImmediate(glyphMaterial);
#else
            Object.Destroy(_material);
            Object.Destroy(_depthPrepassMaterial);
            Object.Destroy(_mesh);
            foreach (var glyphMaterial in _glyphMaterials.Values)
                Object.Destroy(glyphMaterial);
#endif
            _glyphMaterials.Clear();

            foreach (var buffer in _argsBufferPool)
                buffer?.Dispose();
            foreach (var buffer in _activeArgsBuffers)
                buffer?.Dispose();
            _argsBufferPool.Clear();
            _activeArgsBuffers.Clear();
        }

        private static readonly Unity.Profiling.ProfilerMarker CaptureMarker = new("Unite.Draw.Capture");
        private static readonly Unity.Profiling.ProfilerMarker LayoutMarker = new("Unite.Draw.Layout");
        private static readonly Unity.Profiling.ProfilerMarker UploadMarker = new("Unite.Draw.Upload");
        private static readonly Unity.Profiling.ProfilerMarker SubmitMarker = new("Unite.Draw.Submit");

        public void Draw(Camera cam)
        {
            if (_frameCommandCount == 0) return;

            LayoutShapes(cam);
        }

        public void CaptureCommands()
        {
            using var scope = CaptureMarker.Auto();
            _propertyBlocksInUse = 0;
            _frame.SwapWith(_commandQueue);
            _commandQueue.Clear();
            _frameCommandCount = _frame.Count;
            _captureGeneration++;
        }

        private void LayoutShapes(Camera cam)
        {
            int count = _frameCommandCount;
            float pixelPadding = PixelPadding(cam);
            if (_builtGeneration != _captureGeneration || _builtPixelPadding != pixelPadding)
            {
                BuildShapeData(pixelPadding);
                _builtGeneration = _captureGeneration;
                _builtPixelPadding = pixelPadding;
            }

            using var submitScope = SubmitMarker.Auto();
            _submitOrder = 0;
            if (ExecutionMode == DrawingExecutionMode.Sequential)
            {
                RenderSequential(cam, count);
                return;
            }

            RenderIndirect(cam, count);
        }

        private void BuildShapeData(float pixelPadding)
        {
            int count = _frameCommandCount;
            EnsureBuffers(count);

            if (_shapeDataScratch.Length < count)
                _shapeDataScratch = new ShapeData[Mathf.NextPowerOfTwo(Mathf.Max(count, 64))];

            var shapeDataArray = _shapeDataScratch;
            var layoutScope = LayoutMarker.Auto();
            unsafe
            {
                fixed (DrawCommand* commands = _frame.Items)
                fixed (ShapeData* output = shapeDataArray)
                    ShapeDataBuilder.Build(commands, count, output, pixelPadding);
            }

            layoutScope.Dispose();
            using (UploadMarker.Auto())
            {
                _shapeDataBuffer.SetData(shapeDataArray, 0, 0, count);
                var polygonPoints = _frame.PolygonPoints;
                EnsurePolygonPointBuffer(polygonPoints.Count);
                if (polygonPoints.Count > 0)
                    _polygonPointBuffer.SetData(polygonPoints);
            }
        }

        private void RenderIndirect(Camera cam, int count)
        {
            CollectAtlasRuns();
            bool singleRun = _runs.Count == 1 && _runs[0].Atlas == null;

            if (singleRun)
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
                return;
            }

            // Glyph commands reference different atlas textures; every run submits its
            // own indirect args buffer (a shared buffer would apply the LAST run's
            // instance count to every queued draw) with `startInstance` left at zero;
            // the run's offset into the shared shape buffer goes through the
            // _ShapeDataBase material property instead. Runs draw in submission order
            // so painter's order is preserved against the regular shapes.
            foreach (var run in _runs)
            {
                var argsBuffer = AcquireArgsBuffer();
                _commandData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
                _commandData[0].instanceCount = (uint)run.Count;
                argsBuffer.SetData(_commandData);

                if (DepthIsActive && run.Atlas == null)
                {
                    var prepassParams = CreateRenderParams(cam, 0, true, shapeDataOffset: run.Start);
                    Graphics.RenderMeshIndirect(prepassParams, _mesh, argsBuffer, 1);
                }

                var rp = CreateRenderParams(cam, 0, false, shapeDataOffset: run.Start, atlas: run.Atlas);
                Graphics.RenderMeshIndirect(rp, _mesh, argsBuffer, 1);
            }

            ReleaseArgsBuffers();
        }

        private void RenderSequential(Camera cam, int count)
        {
            if (DepthIsActive)
            {
                for (int index = 0; index < count; index++)
                {
                    // Glyph quads never write depth (like line strokes): the depth
                    // prepass material does not know the glyph's atlas texture.
                    if (_frame.Items[index].GlyphAtlasIndex != 0)
                        continue;

                    var prepassParams = CreateRenderParams(
                        cam,
                        index,
                        useDepthPrepass: true
                    );
                    Graphics.RenderMesh(prepassParams, _mesh, 0, Matrix4x4.identity);
                }
            }

            for (int index = 0; index < count; index++)
            {
                var rp = CreateRenderParams(cam, index, atlas: _frame.AtlasAt(_frame.Items[index].GlyphAtlasIndex));
                Graphics.RenderMesh(rp, _mesh, 0, Matrix4x4.identity);
            }
        }

        private struct AtlasRun
        {
            public int Start;
            public int Count;
            public Texture Atlas;
        }

        private readonly List<AtlasRun> _runs = new();
        private readonly List<GraphicsBuffer> _argsBufferPool = new();
        private readonly List<GraphicsBuffer> _activeArgsBuffers = new();

        private GraphicsBuffer AcquireArgsBuffer()
        {
            if (_argsBufferPool.Count > 0)
            {
                var reused = _argsBufferPool[_argsBufferPool.Count - 1];
                _argsBufferPool.RemoveAt(_argsBufferPool.Count - 1);
                _activeArgsBuffers.Add(reused);
                return reused;
            }

            var created = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );
            _activeArgsBuffers.Add(created);
            return created;
        }

        private void ReleaseArgsBuffers()
        {
            foreach (var buffer in _activeArgsBuffers)
                _argsBufferPool.Add(buffer);
            _activeArgsBuffers.Clear();
        }

        // Groups consecutive commands that share the same atlas texture into runs.
        private void CollectAtlasRuns()
        {
            _runs.Clear();
            int count = _frameCommandCount;
            var commands = _frame.Items;
            int current = 0;
            int start = 0;

            for (int index = 0; index < count; index++)
            {
                int atlas = commands[index].GlyphAtlasIndex;
                if (atlas == current) continue;

                if (index > start)
                    _runs.Add(new AtlasRun { Start = start, Count = index - start, Atlas = _frame.AtlasAt(current) });

                start = index;
                current = atlas;
            }

            if (count > start)
                _runs.Add(new AtlasRun { Start = start, Count = count - start, Atlas = _frame.AtlasAt(current) });
        }

        private RenderParams CreateRenderParams(
            Camera cam,
            int shapeIndex,
            bool useDepthPrepass = false,
            int shapeDataOffset = 0,
            Texture atlas = null
        )
        {
            var properties = RentPropertyBlock();
            properties.SetBuffer(PID.ShapeDataBuffer, _shapeDataBuffer);
            properties.SetBuffer(PID.PolygonPoints, _polygonPointBuffer);
            properties.SetInt(PID.DrawingMode, (int)DrawingMode);
            properties.SetInt(PID.ShapeIndex, shapeIndex);
            properties.SetInt(
                PID.UseShapeIndex,
                ExecutionMode == DrawingExecutionMode.Sequential ? 1 : 0
            );
            properties.SetInt(PID.DebugShapeBounds, DebugShapeBounds ? 1 : 0);

            if (shapeDataOffset != 0)
            {
                properties.SetInt(PID.ShapeDataBase, shapeDataOffset);
            }

            if (DrawingMode == DrawingMode.CanvasLocal && _parentTransform != null)
            {
                properties.SetMatrix(PID.ParentTransform, _parentTransform.localToWorldMatrix);
            }

            // The pipeline sorts transparent draws back to front by their bounds center
            // (URP's transparent sort ignores rendererPriority). Each later draw's center
            // sits a little closer to the camera, so the sort keeps submission (painter's)
            // order across draws: shapes and glyph atlas runs.
            var cameraTransform = cam.transform;
            float sortDistance = 1000f - 0.01f * _submitOrder++;
            var baseMaterial = atlas != null ? GetGlyphMaterial(atlas) : _material;
            return new RenderParams(useDepthPrepass ? _depthPrepassMaterial : baseMaterial)
            {
                worldBounds = new Bounds(cameraTransform.position + cameraTransform.forward * sortDistance, Vector3.one * 10000f),
                camera = cam,
                matProps = properties
            };
        }

        private MaterialPropertyBlock RentPropertyBlock()
        {
            if (_propertyBlocksInUse == _propertyBlocks.Count)
                _propertyBlocks.Add(new MaterialPropertyBlock());

            var block = _propertyBlocks[_propertyBlocksInUse++];
            block.Clear();
            return block;
        }

        private void ApplyDepthState()
        {
            var zTest = (float)(DepthIsActive ? CompareFunction.LessEqual : CompareFunction.Always);
            _material.SetFloat(PID.ZTest, zTest);
            _depthPrepassMaterial.SetFloat(PID.ZTest, (float)CompareFunction.LessEqual);

            foreach (var glyphMaterial in _glyphMaterials.Values)
                glyphMaterial.SetFloat(PID.ZTest, zTest);
        }

        // Clones the canvas material for one atlas page and owns its texture binding.
        // The binding is re-asserted on every acquire: the page texture can be replaced
        // (eviction + page reuse) or destroyed across play sessions, and a stale bind
        // makes every glyph quad sample the empty page (solid rectangles).
        // _GlyphAtlas is not a declared shader property, so it is only ever set:
        // Material.GetTexture logs an error for undeclared properties.
        private Material GetGlyphMaterial(Texture atlas)
        {
            if (_glyphMaterials.TryGetValue(atlas, out var material) && material != null)
            {
                material.SetTexture(PID.GlyphAtlas, atlas);
                return material;
            }

            material = new Material(_material);
            material.SetTexture(PID.GlyphAtlas, atlas);
            _glyphMaterials[atlas] = material;
            return material;
        }

        // Anti-aliasing room in drawing units before the shape's own scale: the only part
        // of the shape data that depends on the camera.
        private float PixelPadding(Camera cam)
        {
            if (DrawingMode is DrawingMode.Screen or DrawingMode.ScreenOverlay)
                return 1.5f;
            if (DrawingMode is DrawingMode.NormalizedScreen or DrawingMode.NormalizedScreenOverlay)
                return 1.5f / Mathf.Max(1f, Mathf.Min(cam.pixelWidth, cam.pixelHeight));
            return 0.001f;
        }

        private void EnsureBuffers(int count)
        {
            int stride = Marshal.SizeOf<ShapeData>();

            if (_shapeDataBuffer == null || _shapeDataBuffer.count < count)
            {
                // Grows geometrically: the glyph count of a text page changes every frame.
                int capacity = Mathf.NextPowerOfTwo(Mathf.Max(count, 64));
                _shapeDataBuffer?.Dispose();
                _shapeDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, stride);
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
