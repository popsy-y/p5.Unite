using System.Collections.Generic;
using UnityEngine;
using StrokeCapValue = Unite.Canvas.StrokeCap;

namespace Unite.Canvas
{
    /// <summary>
    /// Selects how the vertices collected by BeginShape() are connected.
    /// </summary>
    public enum ShapeKind
    {
        Path,
        Points,
        Lines,
        Triangles,
        TriangleFan,
        TriangleStrip,
        Quads,
        QuadStrip
    }

    /// <summary>
    /// Controls whether EndShape() connects the first and last path vertices.
    /// </summary>
    public enum ShapeCloseMode
    {
        Open,
        Close
    }

    public partial class UCanvas
    {
        internal enum PathCommandKind
        {
            Linear,
            Bezier,
            Spline
        }

        internal struct PathCommand
        {
            public Vector3 Point;
            public PathCommandKind Kind;
        }

        private ShapeKind _shapeKind = ShapeKind.Path;
        private bool _shapeActive;
        private readonly List<List<PathCommand>> _pathContours = new();

        /// <summary>
        /// Begins collecting vertices for a custom shape.
        /// </summary>
        public void BeginShape(ShapeKind kind = ShapeKind.Path)
        {
            CheckDrawingContext();
            _shapeKind = kind;
            _shapeActive = true;
            _pathContours.Clear();
            _pathContours.Add(new List<PathCommand>());
        }

        /// <summary>
        /// Adds a straight vertex to the shape started by BeginShape().
        /// </summary>
        public void Vertex(float x, float y) => Vertex(new Vector3(x, y, 0f));

        public void Vertex(float x, float y, float z) => Vertex(new Vector3(x, y, z));

        public void Vertex(Vector2 position)
            => Vertex(new Vector3(position.x, position.y, 0f));

        public void Vertex(Vector3 position)
            => AddPathCommand(position, PathCommandKind.Linear);

        /// <summary>
        /// Adds a Bezier vertex to the shape started by BeginShape().
        /// Each bezier span consumes <see cref="GetBezierOrder"/> vertices. The
        /// first call may set the anchor when the shape does not have one yet.
        /// </summary>
        public void BezierVertex(float x, float y) => BezierVertex(new Vector3(x, y, 0f));

        public void BezierVertex(float x, float y, float z)
            => BezierVertex(new Vector3(x, y, z));

        public void BezierVertex(Vector2 position)
            => BezierVertex(new Vector3(position.x, position.y, 0f));

        public void BezierVertex(Vector3 position)
            => AddPathCommand(position, PathCommandKind.Bezier);

        /// <summary>
        /// Adds a spline vertex to the shape started by BeginShape(). Consecutive
        /// spline vertices form one smooth curve.
        /// </summary>
        public void SplineVertex(float x, float y) => SplineVertex(new Vector3(x, y, 0f));

        public void SplineVertex(float x, float y, float z)
            => SplineVertex(new Vector3(x, y, z));

        public void SplineVertex(Vector2 position)
            => SplineVertex(new Vector3(position.x, position.y, 0f));

        public void SplineVertex(Vector3 position)
            => AddPathCommand(position, PathCommandKind.Spline);

        /// <summary>
        /// Starts a new contour. Contours can describe holes in a filled shape
        /// and are always stroked as separate paths.
        /// </summary>
        public void BeginContour()
        {
            if (!_shapeActive)
            {
                Debug.LogWarning(
                    "p5.Unite: BeginContour() must be called between BeginShape() and EndShape()."
                );
                return;
            }

            _pathContours.Add(new List<PathCommand>());
        }

        /// <summary>
        /// Stops collecting vertices for the current contour.
        /// </summary>
        public void EndContour() { }

        /// <summary>
        /// Finishes the custom shape started by BeginShape() and draws it.
        /// </summary>
        public void EndShape(ShapeCloseMode closeMode = ShapeCloseMode.Open)
        {
            if (!_shapeActive)
            {
                Debug.LogWarning(
                    "p5.Unite: EndShape() was called without a matching BeginShape()."
                );
                return;
            }

            _shapeActive = false;

            if (isActiveAndEnabled && (_styleState.FillEnabled || _styleState.StrokeEnabled))
                DrawPathShape(closeMode);

            _pathContours.Clear();
        }

        private void AddPathCommand(Vector3 point, PathCommandKind kind)
        {
            if (!_shapeActive)
            {
                Debug.LogWarning(
                    "p5.Unite: Vertex() must be called between BeginShape() and EndShape()."
                );
                return;
            }

            _pathContours[_pathContours.Count - 1].Add(new PathCommand
            {
                Point = point,
                Kind = kind
            });
        }

        private void DrawPathShape(ShapeCloseMode closeMode)
        {
            switch (_shapeKind)
            {
                case ShapeKind.Points:
                    DrawPathPoints();
                    break;
                case ShapeKind.Lines:
                    DrawPathLines();
                    break;
                case ShapeKind.Triangles:
                    DrawPathTriangles();
                    break;
                case ShapeKind.TriangleFan:
                    DrawPathTriangleFan();
                    break;
                case ShapeKind.TriangleStrip:
                    DrawPathTriangleStrip();
                    break;
                case ShapeKind.Quads:
                    DrawPathQuads();
                    break;
                case ShapeKind.QuadStrip:
                    DrawPathQuadStrip();
                    break;
                default:
                    DrawPathContours(closeMode);
                    break;
            }
        }

        private void DrawPathPoints()
        {
            foreach (var contour in _pathContours)
            {
                foreach (var command in contour)
                    Point(command.Point);
            }
        }

        private void DrawPathLines()
        {
            StrokeCap cap = _styleState.StrokeCap;
            foreach (var contour in _pathContours)
            {
                for (int index = 0; index + 1 < contour.Count; index += 2)
                {
                    EnqueueLineSegment(
                        contour[index].Point,
                        contour[index + 1].Point,
                        cap,
                        cap
                    );
                }
            }
        }

        private void DrawPathTriangles()
        {
            var points = CollectPathPoints();
            for (int index = 0; index + 2 < points.Count; index += 3)
                EmitFilledTriangle(points[index], points[index + 1], points[index + 2]);
        }

        private void DrawPathTriangleFan()
        {
            var points = CollectPathPoints();
            for (int index = 1; index + 1 < points.Count; index++)
                EmitFilledTriangle(points[0], points[index], points[index + 1]);
        }

        private void DrawPathTriangleStrip()
        {
            var points = CollectPathPoints();
            for (int index = 0; index + 2 < points.Count; index++)
                EmitFilledTriangle(points[index], points[index + 1], points[index + 2]);
        }

        private void DrawPathQuads()
        {
            var points = CollectPathPoints();
            for (int index = 0; index + 3 < points.Count; index += 4)
            {
                EmitFilledQuad(
                    points[index],
                    points[index + 1],
                    points[index + 2],
                    points[index + 3]
                );
            }
        }

        private void DrawPathQuadStrip()
        {
            var points = CollectPathPoints();
            for (int index = 0; index + 3 < points.Count; index += 2)
            {
                EmitFilledQuad(
                    points[index],
                    points[index + 1],
                    points[index + 3],
                    points[index + 2]
                );
            }
        }

        private void DrawPathContours(ShapeCloseMode closeMode)
        {
            bool closed = closeMode == ShapeCloseMode.Close;
            var fillContours = _styleState.FillEnabled ? new List<List<Vector3>>() : null;

            foreach (var contour in _pathContours)
            {
                if (contour.Count == 0)
                    continue;

                var polyline = BuildPathPolyline(contour);
                if (polyline.Count < 2)
                    continue;

                DrawPathStroke(polyline, closed);
                fillContours?.Add(polyline);
            }

            if (fillContours != null)
                EnqueuePathFill(fillContours);
        }

        private void DrawPathStroke(IReadOnlyList<Vector3> polyline, bool closed)
        {
            int segmentCount = closed ? polyline.Count : polyline.Count - 1;
            StrokeCap cap = _styleState.StrokeCap;

            for (int index = 0; index < segmentCount; index++)
            {
                Vector3 start = polyline[index];
                Vector3 end = polyline[(index + 1) % polyline.Count];
                if ((end - start).sqrMagnitude <= 1e-12f)
                    continue;

                StrokeCap startCap = !closed && index == 0 ? cap : StrokeCapValue.Round;
                StrokeCap endCap = !closed && index == segmentCount - 1
                    ? cap
                    : StrokeCapValue.Round;
                EnqueueLineSegment(start, end, startCap, endCap);
            }
        }

        /// <summary>
        /// Expands the collected path commands into a polyline. Linear vertices are
        /// used as is, Bezier vertices are grouped by the current Bezier order, and
        /// consecutive spline vertices are converted to a cardinal spline.
        /// </summary>
        internal List<Vector3> BuildPathPolyline(IReadOnlyList<PathCommand> commands)
        {
            var polyline = new List<Vector3>();
            int index = 0;
            Vector3 current = Vector3.zero;
            bool hasCurrent = false;

            while (index < commands.Count)
            {
                PathCommandKind kind = commands[index].Kind;

                if (kind == PathCommandKind.Linear)
                {
                    current = commands[index].Point;
                    polyline.Add(current);
                    hasCurrent = true;
                    index++;
                    continue;
                }

                int runEnd = index;
                while (runEnd < commands.Count && commands[runEnd].Kind == kind)
                    runEnd++;

                if (kind == PathCommandKind.Bezier)
                {
                    if (!hasCurrent)
                    {
                        current = commands[index].Point;
                        hasCurrent = true;
                        index++;
                    }

                    int order = Mathf.Clamp(_bezierOrder, 2, 3);
                    while (runEnd - index >= order)
                    {
                        Vector3 end;
                        if (order == 2)
                        {
                            Vector3 control = commands[index].Point;
                            end = commands[index + 1].Point;
                            Vector3 control1 = current + (control - current) * (2f / 3f);
                            Vector3 control2 = end + (control - end) * (2f / 3f);
                            EnsurePolylinePoint(polyline, current);
                            AppendCubic(polyline, current, control1, control2, end);
                        }
                        else
                        {
                            Vector3 control1 = commands[index].Point;
                            Vector3 control2 = commands[index + 1].Point;
                            end = commands[index + 2].Point;
                            EnsurePolylinePoint(polyline, current);
                            AppendCubic(polyline, current, control1, control2, end);
                        }

                        current = end;
                        index += order;
                    }

                    index = runEnd;
                    continue;
                }

                // Spline run: all commands are points on one smooth curve.
                var points = new List<Vector3>(runEnd - index);
                for (int offset = index; offset < runEnd; offset++)
                    points.Add(commands[offset].Point);

                if (!hasCurrent)
                {
                    current = points[0];
                    hasCurrent = true;
                }

                for (int span = 0; span < points.Count - 1; span++)
                {
                    if (
                        _splineEnds == SplineEndMode.Exclude &&
                        (span == 0 || span == points.Count - 2)
                    )
                    {
                        continue;
                    }

                    Vector3 a = span > 0 ? points[span - 1] : current;
                    Vector3 b = points[span];
                    Vector3 c = points[span + 1];
                    Vector3 d = span + 2 < points.Count ? points[span + 2] : points[span + 1];

                    EnsurePolylinePoint(polyline, b);
                    AppendCubic(
                        polyline,
                        b,
                        SplineControl1(a, b, c),
                        SplineControl2(b, c, d),
                        c
                    );
                }

                current = _splineEnds == SplineEndMode.Exclude && points.Count >= 2
                    ? points[points.Count - 2]
                    : points[points.Count - 1];
                index = runEnd;
            }

            return polyline;
        }

        private static void EnsurePolylinePoint(List<Vector3> polyline, Vector3 point)
        {
            if (polyline.Count > 0 && (polyline[polyline.Count - 1] - point).sqrMagnitude <= 1e-12f)
                return;

            polyline.Add(point);
        }

        private List<Vector3> CollectPathPoints()
        {
            var points = new List<Vector3>();
            foreach (var contour in _pathContours)
            {
                foreach (var command in contour)
                    points.Add(command.Point);
            }

            return points;
        }

        private void EmitFilledTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            float z = (a.z + b.z + c.z) / 3f;
            EnqueuePointShape(
                Shape.Triangle,
                z,
                V2(a),
                V2(b),
                V2(c),
                V2(c),
                3,
                true
            );
        }

        private void EmitFilledQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float z = (a.z + b.z + c.z + d.z) * 0.25f;
            EnqueuePointShape(Shape.Quad, z, V2(a), V2(b), V2(c), V2(d), 4, true);
        }

        #region Path Fill

        /// <summary>
        /// Fills all path contours with one winding number SDF instance. Contours
        /// are combined with the even-odd rule, so nested contours become holes or
        /// islands regardless of the direction they were wound in. A single
        /// instance keeps the fill free of the anti-aliasing seams that overlapping
        /// triangles would leave behind.
        /// </summary>
        private void EnqueuePathFill(IReadOnlyList<List<Vector3>> contours)
        {
            var points = new List<Vector4>();
            float zSum = 0f;
            int zCount = 0;

            foreach (var contour in contours)
            {
                var outline = SimplifyContour(contour);
                if (outline.Count < 3)
                    continue;

                foreach (var point in outline)
                {
                    points.Add(new Vector4(point.x, point.y, 0f, 0f));
                    zSum += point.z;
                    zCount++;
                }
            }

            if (points.Count < 3)
                return;

            var boundsMin = new Vector2(points[0].x, points[0].y);
            var boundsMax = boundsMin;
            for (int index = 1; index < points.Count; index++)
            {
                var point = new Vector2(points[index].x, points[index].y);
                boundsMin = Vector2.Min(boundsMin, point);
                boundsMax = Vector2.Max(boundsMax, point);
            }

            var center = (boundsMin + boundsMax) * 0.5f;
            var localPoints = new Vector4[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                localPoints[index] = new Vector4(
                    points[index].x - center.x,
                    points[index].y - center.y,
                    0f,
                    0f
                );
            }

            float centerZ = zSum / Mathf.Max(1, zCount);
            _commandQueue.Enqueue(new DrawCommand
            {
                Transform = CurrentTransform
                    * Matrix4x4.Translate(new Vector3(center.x, center.y, centerZ)),
                BoundsMin = boundsMin - center,
                BoundsMax = boundsMax - center,
                HasCustomBounds = true,
                ShapeType = Shape.PolygonFill,
                PolygonPoints = localPoints,
                FillColor = _styleState.FillColor,
                FillEnabled = true,
                StrokeEnabled = false
            });
        }

        private static List<Vector3> SimplifyContour(IReadOnlyList<Vector3> contour)
        {
            var result = new List<Vector3>(contour.Count);
            foreach (var point in contour)
            {
                if (
                    result.Count > 0 &&
                    (result[result.Count - 1] - point).sqrMagnitude <= 1e-10f
                )
                {
                    continue;
                }

                result.Add(point);
            }

            if (
                result.Count >= 2 &&
                (result[0] - result[result.Count - 1]).sqrMagnitude <= 1e-10f
            )
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        #endregion


        private static Vector2 V2(Vector3 value) => new(value.x, value.y);
    }
}
