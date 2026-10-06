# Shape API Checklist

[← API checklist manifest](../../API_CHECKLIST.md)

- Category: `Shape`
- Implemented: 30 / 65
- Partial support: 1
- Status source: p5.Unite public API only

## 2D Primitives

- [x] [arc()](https://p5js.org/reference/p5/arc/) — `UCanvas.Arc()`
- [x] [circle()](https://p5js.org/reference/p5/circle/) — `UCanvas.Circle()`
- [x] [ellipse()](https://p5js.org/reference/p5/ellipse/) — `UCanvas.Ellipse()`
- [x] [line()](https://p5js.org/reference/p5/line/) — `UCanvas.Line()`
- [x] [point()](https://p5js.org/reference/p5/point/) — `UCanvas.Point()`
- [x] [quad()](https://p5js.org/reference/p5/quad/) — `UCanvas.Quad()`
- [x] [rect()](https://p5js.org/reference/p5/rect/) — `UCanvas.Rect()`
- [x] [square()](https://p5js.org/reference/p5/square/) — `UCanvas.Square()`
- [x] [triangle()](https://p5js.org/reference/p5/triangle/) — `UCanvas.Triangle()`

## 3D Models

- [ ] [createModel()](https://p5js.org/reference/p5/createModel/)
- [ ] [loadModel()](https://p5js.org/reference/p5/loadModel/)
- [ ] [model()](https://p5js.org/reference/p5/model/)

## 3D Primitives

- [ ] [box()](https://p5js.org/reference/p5/box/)
- [ ] [buildGeometry()](https://p5js.org/reference/p5/buildGeometry/)
- [ ] [cone()](https://p5js.org/reference/p5/cone/)
- [x] [curveDetail()](https://p5js.org/reference/p5/curveDetail/) — `UCanvas.CurveDetail()` / `UCanvas.GetCurveDetail()`
- [ ] [cylinder()](https://p5js.org/reference/p5/cylinder/)
- [ ] [ellipsoid()](https://p5js.org/reference/p5/ellipsoid/)
- [ ] [freeGeometry()](https://p5js.org/reference/p5/freeGeometry/)
- [ ] [plane()](https://p5js.org/reference/p5/plane/)
- [ ] [saveObj()](https://p5js.org/reference/p5/saveObj/)
- [ ] [saveStl()](https://p5js.org/reference/p5/saveStl/)
- [ ] [sphere()](https://p5js.org/reference/p5/sphere/)
- [ ] [strokeMode()](https://p5js.org/reference/p5/strokeMode/)
- [ ] [torus()](https://p5js.org/reference/p5/torus/)

## Attributes

- [x] [ellipseMode()](https://p5js.org/reference/p5/ellipseMode/) — `UCanvas.EllipseMode`
- [ ] [noSmooth()](https://p5js.org/reference/p5/noSmooth/)
- [x] [rectMode()](https://p5js.org/reference/p5/rectMode/) — `UCanvas.RectMode`
- [ ] [smooth()](https://p5js.org/reference/p5/smooth/)
- [x] [strokeCap()](https://p5js.org/reference/p5/strokeCap/) — `UCanvas.StrokeCap()` / `UCanvas.GetStrokeCap()`
- [ ] [strokeJoin()](https://p5js.org/reference/p5/strokeJoin/) — 部分対応: `UCanvas.StrokeJoin()` / `UCanvas.GetStrokeJoin()` は値を保持するが、描画は常に ROUND 相当
- [x] [strokeWeight()](https://p5js.org/reference/p5/strokeWeight/) — `UCanvas.StrokeWeight()`

## Curves

- [x] [bezier()](https://p5js.org/reference/p5/bezier/) — `UCanvas.Bezier()`
- [x] [bezierPoint()](https://p5js.org/reference/p5/bezierPoint/) — `UCanvas.BezierPoint()`
- [x] [bezierTangent()](https://p5js.org/reference/p5/bezierTangent/) — `UCanvas.BezierTangent()`
- [x] [spline()](https://p5js.org/reference/p5/spline/) — `UCanvas.Spline()`
- [x] [splinePoint()](https://p5js.org/reference/p5/splinePoint/) — `UCanvas.SplinePoint()`
- [x] [splineTangent()](https://p5js.org/reference/p5/splineTangent/) — `UCanvas.SplineTangent()`

## Custom Shapes

- [x] [beginContour()](https://p5js.org/reference/p5/beginContour/) — `UCanvas.BeginContour()`
- [x] [beginShape()](https://p5js.org/reference/p5/beginShape/) — `UCanvas.BeginShape()`（PATH の塗りは輪郭をまとめて1インスタンスの even-odd 判定で描画）
- [x] [bezierOrder()](https://p5js.org/reference/p5/bezierOrder/) — `UCanvas.BezierOrder()` / `UCanvas.GetBezierOrder()`
- [x] [bezierVertex()](https://p5js.org/reference/p5/bezierVertex/) — `UCanvas.BezierVertex()`
- [x] [endContour()](https://p5js.org/reference/p5/endContour/) — `UCanvas.EndContour()`
- [x] [endShape()](https://p5js.org/reference/p5/endShape/) — `UCanvas.EndShape()`
- [ ] [normal()](https://p5js.org/reference/p5/normal/)
- [x] [splineProperties()](https://p5js.org/reference/p5/splineProperties/) — `UCanvas.SplineProperties()` / `UCanvas.GetSplineProperties()`
- [x] [splineProperty()](https://p5js.org/reference/p5/splineProperty/) — `UCanvas.SplineProperty()` / `UCanvas.GetSplineProperty()`
- [x] [splineVertex()](https://p5js.org/reference/p5/splineVertex/) — `UCanvas.SplineVertex()`
- [x] [vertex()](https://p5js.org/reference/p5/vertex/) — `UCanvas.Vertex()`
- [ ] [vertexProperty()](https://p5js.org/reference/p5/vertexProperty/)

## p5.Geometry

- [ ] [p5.Geometry](https://p5js.org/reference/p5/p5.Geometry/)
- [ ] [calculateBoundingBox()](https://p5js.org/reference/p5.Geometry/calculateBoundingBox/)
- [ ] [clearColors()](https://p5js.org/reference/p5.Geometry/clearColors/)
- [ ] [computeFaces()](https://p5js.org/reference/p5.Geometry/computeFaces/)
- [ ] [computeNormals()](https://p5js.org/reference/p5.Geometry/computeNormals/)
- [ ] [faces](https://p5js.org/reference/p5.Geometry/faces/)
- [ ] [flipU()](https://p5js.org/reference/p5.Geometry/flipU/)
- [ ] [flipV()](https://p5js.org/reference/p5.Geometry/flipV/)
- [ ] [gid](https://p5js.org/reference/p5.Geometry/gid/)
- [ ] [makeEdgesFromFaces()](https://p5js.org/reference/p5.Geometry/makeEdgesFromFaces/)
- [ ] [normalize()](https://p5js.org/reference/p5.Geometry/normalize/)
- [ ] [uvs](https://p5js.org/reference/p5.Geometry/uvs/)
- [ ] [vertexNormals](https://p5js.org/reference/p5.Geometry/vertexNormals/)
- [ ] [vertexProperty()](https://p5js.org/reference/p5.Geometry/vertexProperty/)
- [ ] [vertices](https://p5js.org/reference/p5.Geometry/vertices/)

