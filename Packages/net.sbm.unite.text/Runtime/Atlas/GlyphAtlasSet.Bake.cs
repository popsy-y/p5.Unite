using System;
using System.Collections.Generic;
using Unite.Text.Fonts;
using Unite.Text.Raster;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Unite.Text
{
    // Placement of new glyphs and the deferred, parallel encoding of their pixels.
    internal sealed partial class GlyphAtlasSet
    {
        /// <summary>The pixels of a glyph still to be encoded: an outline, or ready-made bytes.</summary>
        private struct PendingBake
        {
            public Page Page;
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public float Range;
            public GlyphOutline Outline;
            public GlyphPoints Points;
            public float Scale;
            public float OriginX;
            public float OriginY;
            public byte[] Field;
        }

        private readonly List<PendingBake> _pending = new();
        private readonly List<Page> _dirtyPages = new();
        private readonly List<Page> _batchPages = new();
        private float4[] _edges = new float4[1024];
        private SdfBakeTask[] _tasks = new SdfBakeTask[32];

        /// <summary>
        /// Reserves a rect for a glyph outline and queues its encoding; the pixels exist
        /// after <see cref="Flush"/>. (offsetX, offsetY) is the pen-relative sprite rect
        /// origin in baked pixels (y-up). Outline points map to texels as
        /// <c>point * scale - origin</c>.
        /// </summary>
        public CachedGlyph Allocate(
            GlyphKey key,
            int frame,
            bool isVolatile,
            int width,
            int height,
            float range,
            float offsetX,
            float offsetY,
            GlyphBakeSource source,
            float scale,
            float originX,
            float originY
        )
        {
            var entry = Place(key, frame, isVolatile, width, height, range, offsetX, offsetY);
            _pending.Add(new PendingBake
            {
                Page = entry.Page,
                X = entry.X,
                Y = entry.Y,
                Width = width,
                Height = height,
                Range = range,
                Outline = source.Outline,
                Points = source.Points,
                Scale = scale,
                OriginX = originX,
                OriginY = originY
            });
            return ToCachedGlyph(entry);
        }

        /// <summary>
        /// Reserves a rect and writes ready-made SDF bytes (width*height, row 0 = bottom)
        /// into it right away.
        /// </summary>
        public CachedGlyph Reserve(
            GlyphKey key,
            int frame,
            int width,
            int height,
            float range,
            byte[] field,
            float offsetX,
            float offsetY
        )
        {
            if (field == null || field.Length != width * height)
            {
                throw new ArgumentException(
                    "p5.Unite: glyph field buffer must hold width * height bytes."
                );
            }

            var entry = Place(key, frame, false, width, height, range, offsetX, offsetY);
            _pending.Add(new PendingBake
            {
                Page = entry.Page,
                X = entry.X,
                Y = entry.Y,
                Width = width,
                Height = height,
                Range = range,
                Field = field
            });
            Flush();
            return ToCachedGlyph(entry);
        }

        /// <summary>
        /// Encodes every queued glyph (in parallel, Burst compiled) straight into the page
        /// memory and uploads the touched pages. Called once per frame before the canvas
        /// draws; cheap when nothing is pending.
        /// </summary>
        public void Flush()
        {
            using var flushScope = TextProfiling.Flush.Auto();
            if (_pending.Count > 0)
            {
                _batchPages.Clear();
                foreach (var job in _pending)
                {
                    if (!_batchPages.Contains(job.Page))
                        _batchPages.Add(job.Page);
                }

                foreach (var page in _batchPages)
                    BakePage(page);

                _pending.Clear();
            }

            // Expiry and promotion touch page memory, so they run before the upload.
            using (TextProfiling.Maintain.Auto())
                Maintain();

            using (TextProfiling.Upload.Auto())
            {
                foreach (var page in _dirtyPages)
                {
                    if (page.Texture != null)
                        page.Texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    page.Dirty = false;
                }
                _dirtyPages.Clear();
            }
        }

        private void BakePage(Page page)
        {
            if (page.Texture == null)
                return;

            var raw = page.Texture.GetRawTextureData<byte>();
            int edgeTotal = 0;
            int distanceTotal = 0;
            int crossingTotal = 0;
            int taskCount = 0;

            foreach (var job in _pending)
            {
                if (job.Page != page)
                    continue;

                if (job.Field != null)
                {
                    for (int row = 0; row < job.Height; row++)
                    {
                        NativeArray<byte>.Copy(
                            job.Field, row * job.Width, raw, (job.Y + row) * page.Span + job.X, job.Width
                        );
                    }
                    continue;
                }

                if (job.Points != null)
                    continue; // flattened and encoded by the outline job

                int edgeStart = edgeTotal;
                using (TextProfiling.Edges.Auto())
                {
                    edgeTotal = EdgeBuilder.Append(
                        ref _edges, edgeTotal, job.Outline.Contours, job.Scale, job.OriginX, job.OriginY
                    );
                }
                int edgeCount = edgeTotal - edgeStart;

                if (_tasks.Length <= taskCount)
                    System.Array.Resize(ref _tasks, System.Math.Max(8, _tasks.Length * 2));

                _tasks[taskCount++] = new SdfBakeTask
                {
                    EdgeStart = edgeStart,
                    EdgeCount = edgeCount,
                    Width = job.Width,
                    Height = job.Height,
                    Range = job.Range,
                    DistanceStart = distanceTotal,
                    CrossingStart = crossingTotal,
                    OutputStart = job.Y * page.Span + job.X
                };
                distanceTotal += job.Width * job.Height;
                crossingTotal += edgeCount;
            }

            var outlineHandle = ScheduleOutlineBake(page, raw);

            if (taskCount > 0)
            {
                using var edges = new NativeArray<float4>(System.Math.Max(1, edgeTotal), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                using var tasks = new NativeArray<SdfBakeTask>(taskCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                using var distance = new NativeArray<float>(distanceTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                using var crossingX = new NativeArray<float>(System.Math.Max(1, crossingTotal), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                using var crossingWinding = new NativeArray<int>(System.Math.Max(1, crossingTotal), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                NativeArray<float4>.Copy(_edges, edges, edgeTotal);
                NativeArray<SdfBakeTask>.Copy(_tasks, tasks, taskCount);

                using var jobScope = TextProfiling.Job.Auto();
                new SdfBakeJob
                {
                    Edges = edges,
                    Tasks = tasks,
                    Distance = distance,
                    CrossingX = crossingX,
                    CrossingWinding = crossingWinding,
                    Output = raw,
                    OutputStride = page.Span
                }.Schedule(taskCount, 1, outlineHandle).Complete();
            }
            else
            {
                using (TextProfiling.Job.Auto())
                    outlineHandle.Complete();
            }

            MarkDirty(page);
        }

        // Glyphs given as contour points: one job flattens their curves and encodes them.
        // The native arrays are disposed by jobs that depend on the bake, so they outlive it
        // and the caller only has to complete the returned handle.
        private JobHandle ScheduleOutlineBake(Page page, NativeArray<byte> raw)
        {
            int glyphs = 0;
            int pointTotal = 0;
            int contourTotal = 0;
            int edgeTotal = 0;
            int distanceTotal = 0;
            foreach (var job in _pending)
            {
                if (job.Page != page || job.Points == null)
                    continue;

                glyphs++;
                pointTotal += job.Points.X.Length;
                contourTotal += job.Points.EndPoints.Length;
                edgeTotal += job.Points.X.Length * OutlineBakeJob.MaxSubdivisions;
                distanceTotal += job.Width * job.Height;
            }

            if (glyphs == 0)
                return default;

            var points = new NativeArray<float2>(pointTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var onCurve = new NativeArray<byte>(pointTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var endPoints = new NativeArray<int>(contourTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var tasks = new NativeArray<OutlineBakeTask>(glyphs, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var edges = new NativeArray<float4>(edgeTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var distance = new NativeArray<float>(distanceTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var crossingX = new NativeArray<float>(edgeTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var crossingWinding = new NativeArray<int>(edgeTotal, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            int pointStart = 0;
            int contourStart = 0;
            int edgeStart = 0;
            int distanceStart = 0;
            int task = 0;
            foreach (var job in _pending)
            {
                if (job.Page != page || job.Points == null)
                    continue;

                var source = job.Points;
                for (int index = 0; index < source.X.Length; index++)
                {
                    points[pointStart + index] = new float2(source.X[index], source.Y[index]);
                    onCurve[pointStart + index] = source.OnCurve[index] ? (byte)1 : (byte)0;
                }
                for (int index = 0; index < source.EndPoints.Length; index++)
                    endPoints[contourStart + index] = source.EndPoints[index];

                int edgeCapacity = source.X.Length * OutlineBakeJob.MaxSubdivisions;
                tasks[task++] = new OutlineBakeTask
                {
                    PointStart = pointStart,
                    ContourStart = contourStart,
                    ContourCount = source.EndPoints.Length,
                    Scale = job.Scale,
                    OriginX = job.OriginX,
                    OriginY = job.OriginY,
                    Flatness = source.Flatness,
                    EdgeStart = edgeStart,
                    EdgeCapacity = edgeCapacity,
                    Width = job.Width,
                    Height = job.Height,
                    Range = job.Range,
                    DistanceStart = distanceStart,
                    OutputStart = job.Y * page.Span + job.X
                };

                pointStart += source.X.Length;
                contourStart += source.EndPoints.Length;
                edgeStart += edgeCapacity;
                distanceStart += job.Width * job.Height;
            }

            var handle = new OutlineBakeJob
            {
                Points = points,
                OnCurve = onCurve,
                EndPoints = endPoints,
                Tasks = tasks,
                Edges = edges,
                Distance = distance,
                CrossingX = crossingX,
                CrossingWinding = crossingWinding,
                Output = raw,
                OutputStride = page.Span
            }.Schedule(glyphs, 1);

            var disposal = JobHandle.CombineDependencies(points.Dispose(handle), onCurve.Dispose(handle), endPoints.Dispose(handle));
            disposal = JobHandle.CombineDependencies(disposal, tasks.Dispose(handle), edges.Dispose(handle));
            disposal = JobHandle.CombineDependencies(disposal, distance.Dispose(handle), crossingX.Dispose(handle));
            return JobHandle.CombineDependencies(disposal, crossingWinding.Dispose(handle));
        }

        // Moves a long-lived volatile glyph to the stable pages by copying its pixels
        // between the pages' CPU memory (uploaded with the next Apply). The old rect stays
        // intact until its page is recycled, so commands captured this frame still draw.
        private void Promote(GlyphKey key, Entry entry)
        {
            var source = entry.Page;
            if (source.Texture == null)
                return;

            Page target = null;
            int x = 0;
            int y = 0;
            foreach (var candidate in _pages)
            {
                if (candidate.InUse && !candidate.Volatile && candidate.Span == source.Span
                    && candidate.Allocator.TryPlace(entry.W, entry.H, out x, out y))
                {
                    target = candidate;
                    break;
                }
            }

            if (target == null)
            {
                target = AcquirePage(false, source.Span);
                if (!target.Allocator.TryPlace(entry.W, entry.H, out x, out y))
                    return;
            }

            var from = source.Texture.GetRawTextureData<byte>();
            var to = target.Texture.GetRawTextureData<byte>();
            for (int row = 0; row < entry.H; row++)
            {
                NativeArray<byte>.Copy(
                    from, (entry.Y + row) * source.Span + entry.X, to, (y + row) * target.Span + x, entry.W
                );
            }
            MarkDirty(target);

            source.Live--;
            target.Live++;
            entry.Page = target;
            entry.X = x;
            entry.Y = y;
            entry.Volatile = false;
            _volatileEntries.Remove(key);
        }

        private void MarkDirty(Page page)
        {
            if (page.Dirty)
                return;

            page.Dirty = true;
            _dirtyPages.Add(page);
        }

        private void ClearPending()
        {
            _pending.Clear();
            _dirtyPages.Clear();
        }

        private Entry Place(
            GlyphKey key,
            int frame,
            bool isVolatile,
            int width,
            int height,
            float range,
            float offsetX,
            float offsetY
        )
        {
            _currentFrame = frame;
            int span = SpanFor(width, height);

            Page page = null;
            int x = 0;
            int y = 0;
            foreach (var candidate in _pages)
            {
                if (candidate.InUse && candidate.Volatile == isVolatile && candidate.Span == span
                    && candidate.Allocator.TryPlace(width, height, out x, out y))
                {
                    page = candidate;
                    break;
                }
            }

            if (page == null)
            {
                page = AcquirePage(isVolatile, span);

                // Glyph rects are bounded by LargePageSpan through the layout bucket clamps.
                if (!page.Allocator.TryPlace(width, height, out x, out y))
                {
                    throw new ArgumentException(
                        $"p5.Unite: glyph rect {width}x{height} does not fit an atlas page."
                    );
                }
            }

            var entry = RentEntry();
            entry.Page = page;
            entry.X = x;
            entry.Y = y;
            entry.W = width;
            entry.H = height;
            entry.Range = range;
            entry.OffsetX = offsetX;
            entry.OffsetY = offsetY;
            entry.CreatedFrame = frame;
            entry.LastUsedFrame = frame;
            entry.Volatile = isVolatile;
            _entries.Add(key, entry);
            if (isVolatile)
                _volatileEntries.Add(key, entry);

            page.Live++;
            return entry;
        }
    }
}
