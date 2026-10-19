using Unite.Canvas;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Unite.Drawing
{
    /// <summary>
    /// Converts draw commands into the per-instance GPU data (Burst compiled; tens of
    /// thousands of commands per frame are common).
    /// </summary>
    [BurstCompile]
    internal static unsafe class ShapeDataBuilder
    {
        /// <param name="pixelPadding">
        /// Anti-aliasing room in drawing units before the shape's own scale (the only
        /// camera-dependent input).
        /// </param>
        [BurstCompile]
        public static void Build(DrawCommand* commands, int count, ShapeData* output, float pixelPadding)
        {
            for (int index = 0; index < count; index++)
                Layout(ref commands[index], pixelPadding, ref output[index]);
        }

        private static void Layout(ref DrawCommand cmd, float pixelPadding, ref ShapeData data)
        {
            Matrix4x4 transform = cmd.Transform;
            // The zero matrix marks legacy Position / Rotation commands; every affine
            // transform keeps m33 at 1.
            if (transform.m33 == 0f && IsZero(transform))
            {
                var rotation = quaternion.EulerZXY(math.radians((float3)cmd.Rotation));
                transform = float4x4.TRS(cmd.Position, rotation, new float3(1f));
            }

            float sizeX = math.abs(cmd.Size.x);
            float sizeY = math.abs(cmd.Size.y);
            float strokeWeight = cmd.StrokeEnabled ? math.max(0f, cmd.StrokeWeight) : 0f;
            // Glyph quads sample a prebaked SDF that already has generous sprite padding,
            // so they keep their tight bounds (the analytic shapes round their bounds out
            // to give the anti-aliasing room).
            float padding = cmd.ShapeType == Shape.Text
                ? 0f
                : strokeWeight * 0.5f + AntiAliasPadding(pixelPadding, transform);
            int styleFlags = (cmd.FillEnabled ? 1 : 0) | (cmd.StrokeEnabled && strokeWeight > 0f ? 2 : 0);

            float minX = cmd.HasCustomBounds ? cmd.BoundsMin.x : -sizeX * 0.5f;
            float minY = cmd.HasCustomBounds ? cmd.BoundsMin.y : -sizeY * 0.5f;
            float maxX = cmd.HasCustomBounds ? cmd.BoundsMax.x : sizeX * 0.5f;
            float maxY = cmd.HasCustomBounds ? cmd.BoundsMax.y : sizeY * 0.5f;

            var point0 = new Vector4(cmd.Point0.x, cmd.Point0.y, 0f, 0f);
            var point1 = new Vector4(cmd.Point1.x, cmd.Point1.y, 0f, 0f);
            var parameters0 = cmd.Parameters0;

            if (cmd.ShapeType == Shape.Line3D)
            {
                point0 = new Vector4(cmd.LineStart.x, cmd.LineStart.y, cmd.LineStart.z, 0f);
                point1 = new Vector4(cmd.LineEnd.x, cmd.LineEnd.y, cmd.LineEnd.z, 0f);
            }

            if (cmd.ShapeType is Shape.Line or Shape.Line3D)
                parameters0 = new Vector4(cmd.LineCaps, cmd.LinePerspective ? 1f : 0f, 0f, 0f);

            // The points already sit in the frame's shared polygon buffer.
            if (cmd.ShapeType == Shape.PolygonFill && cmd.PolygonPointCount > 0)
                parameters0 = new Vector4(cmd.PolygonPointStart, cmd.PolygonPointCount, 0f, 0f);

            data.Transform0 = new Vector4(transform.m00, transform.m10, transform.m20, transform.m30);
            data.Transform1 = new Vector4(transform.m01, transform.m11, transform.m21, transform.m31);
            data.Transform2 = new Vector4(transform.m02, transform.m12, transform.m22, transform.m32);
            data.Transform3 = new Vector4(transform.m03, transform.m13, transform.m23, transform.m33);
            data.Bounds = new Vector4(minX - padding, minY - padding, maxX + padding, maxY + padding);
            data.ShapeParameters = new Vector4(sizeX, sizeY, 0f, 0f);
            data.Point0 = point0;
            data.Point1 = point1;
            data.Point2 = new Vector4(cmd.Point2.x, cmd.Point2.y, 0f, 0f);
            data.Point3 = new Vector4(cmd.Point3.x, cmd.Point3.y, 0f, 0f);
            data.Parameters0 = parameters0;
            data.Parameters1 = cmd.Parameters1;
            data.CornerRadiiX = cmd.CornerRadiiX;
            data.CornerRadiiY = cmd.CornerRadiiY;
            data.Style = new Vector4(GpuShapeType(cmd.ShapeType), (float)cmd.ArcMode, strokeWeight, styleFlags);
            data.FillColor = new Vector4(cmd.FillColor.r, cmd.FillColor.g, cmd.FillColor.b, cmd.FillColor.a);
            data.StrokeColor = new Vector4(cmd.StrokeColor.r, cmd.StrokeColor.g, cmd.StrokeColor.b, cmd.StrokeColor.a);
        }

        private static bool IsZero(in Matrix4x4 m)
        {
            float4x4 value = m;
            return math.all(value.c0 == 0f) && math.all(value.c1 == 0f)
                && math.all(value.c2 == 0f) && math.all(value.c3 == 0f);
        }

        // Padding divided by the transform's smallest scale (its smaller singular value),
        // so the anti-aliasing room stays constant on screen.
        private static float AntiAliasPadding(float drawingPadding, in Matrix4x4 transform)
        {
            var xAxis = new float3(transform.m00, transform.m10, transform.m20);
            var yAxis = new float3(transform.m01, transform.m11, transform.m21);
            float xx = math.dot(xAxis, xAxis);
            float yy = math.dot(yAxis, yAxis);
            float xy = math.dot(xAxis, yAxis);
            float discriminant = math.sqrt(math.max(0f, (xx - yy) * (xx - yy) + 4f * xy * xy));
            float minimumScale = math.sqrt(math.max(0f, (xx + yy - discriminant) * 0.5f));

            return drawingPadding / math.max(0.0001f, minimumScale);
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
                Shape.Text => 9f,
                _ => 0f
            };
        }
    }
}
