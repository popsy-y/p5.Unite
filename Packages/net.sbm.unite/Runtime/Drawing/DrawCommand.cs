using UnityEngine;

namespace Unite.Canvas
{
    /// <summary>
    /// One queued shape. Commands are plain values (no references) so the frame's shape
    /// data can be built from them in Burst; polygon points and atlas textures live in
    /// the frame's command buffer and are referred to by index.
    /// </summary>
    public struct DrawCommand
    {
        public Matrix4x4 Transform;

        // Legacy fields retained for source compatibility with existing callers.
        // DrawingContext uses them only when Transform is the zero matrix.
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector2 Size;

        public Vector2 Point0;
        public Vector2 Point1;
        public Vector2 Point2;
        public Vector2 Point3;

        // Line3D only: shape-local endpoints of a line segment in 3D space.
        public Vector3 LineStart;
        public Vector3 LineEnd;

        // Packed StrokeCap values: start cap in bits 0..1, end cap in bits 2..3.
        public int LineCaps;

        // Line3D only: scales the stroke weight by the perspective projection.
        public bool LinePerspective;

        // PolygonFill only: shape-local points of the outline, in order, in the frame's
        // shared polygon point buffer. Holes are stored as additional contours wound
        // opposite to the outer contour.
        public int PolygonPointStart;
        public int PolygonPointCount;

        public Vector4 Parameters0;
        public Vector4 Parameters1;
        public Vector4 CornerRadiiX;
        public Vector4 CornerRadiiY;
        public Vector2 BoundsMin;
        public Vector2 BoundsMax;
        public bool HasCustomBounds;

        public Shape ShapeType;
        public ArcMode ArcMode;

        public Color FillColor;
        public Color StrokeColor;
        public float StrokeWeight;
        public bool FillEnabled;
        public bool StrokeEnabled;

        // Text only: atlas page holding this glyph's SDF, as a 1-based index into the
        // frame's atlas list. 0 for every shape.
        public int GlyphAtlasIndex;
    }
}
