using System.Collections.Generic;
using NUnit.Framework;
using Unite.Canvas;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unite.Tests
{
    public sealed class CurveApiTests
    {
        private const float Tolerance = 0.0001f;
        private UCanvas _canvas;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("CurveApiTests");
            _canvas = _gameObject.AddComponent<UCanvas>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void BezierPointReturnsAnchorsAtTheEnds()
        {
            Assert.That(_canvas.BezierPoint(2f, 5f, 8f, 11f, 0f), Is.EqualTo(2f).Within(Tolerance));
            Assert.That(_canvas.BezierPoint(2f, 5f, 8f, 11f, 1f), Is.EqualTo(11f).Within(Tolerance));
        }

        [Test]
        public void BezierPointMatchesSymmetricCubic()
        {
            Assert.That(_canvas.BezierPoint(0f, 0f, 1f, 1f, 0.5f), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void BezierPointVectorOverloadMatchesPerAxisValues()
        {
            Vector3 point = _canvas.BezierPoint(
                new Vector3(0f, 1f, 2f),
                new Vector3(1f, 2f, 3f),
                new Vector3(2f, 3f, 4f),
                new Vector3(3f, 4f, 5f),
                0.25f
            );

            AssertVector(
                new Vector3(
                    _canvas.BezierPoint(0f, 1f, 2f, 3f, 0.25f),
                    _canvas.BezierPoint(1f, 2f, 3f, 4f, 0.25f),
                    _canvas.BezierPoint(2f, 3f, 4f, 5f, 0.25f)
                ),
                point
            );
        }

        [Test]
        public void BezierTangentMatchesFiniteDifference()
        {
            const float a = 1f;
            const float b = 4f;
            const float c = 2f;
            const float d = 6f;
            const float t = 0.3f;
            const float h = 0.0001f;

            float finiteDifference =
                (_canvas.BezierPoint(a, b, c, d, t + h)
                 - _canvas.BezierPoint(a, b, c, d, t - h)) / (2f * h);

            Assert.That(
                _canvas.BezierTangent(a, b, c, d, t),
                Is.EqualTo(finiteDifference).Within(0.001f)
            );
        }

        [Test]
        public void SplinePointMatchesCatmullRomMidpoint()
        {
            Assert.That(_canvas.SplinePoint(0f, 0f, 1f, 1f, 0.5f), Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void SplinePointUsesExplicitTightness()
        {
            float loose = _canvas.SplinePoint(0f, 0f, 1f, 1f, 0.5f, -1f);
            float tight = _canvas.SplinePoint(0f, 0f, 1f, 1f, 0.5f, 1f);

            Assert.That(loose, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(tight, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(
                _canvas.SplinePoint(0f, 0f, 1f, 2f, 0.35f, -1f),
                Is.Not.EqualTo(_canvas.SplinePoint(0f, 0f, 1f, 2f, 0.35f, 1f))
                    .Within(Tolerance)
            );
        }

        [Test]
        public void SplineTangentMatchesFiniteDifference()
        {
            const float a = 2f;
            const float b = 5f;
            const float c = 3f;
            const float d = 7f;
            const float t = 0.4f;
            const float h = 0.0001f;

            float finiteDifference =
                (_canvas.SplinePoint(a, b, c, d, t + h)
                 - _canvas.SplinePoint(a, b, c, d, t - h)) / (2f * h);

            Assert.That(
                _canvas.SplineTangent(a, b, c, d, t),
                Is.EqualTo(finiteDifference).Within(0.001f)
            );
        }

        [Test]
        public void SplinePropertyRoundTripsTypedAndStringValues()
        {
            _canvas.SplineProperties(1.25f, SplineEndMode.Exclude);

            var (tightness, ends) = _canvas.GetSplineProperties();
            Assert.That(tightness, Is.EqualTo(1.25f).Within(Tolerance));
            Assert.AreEqual(SplineEndMode.Exclude, ends);

            _canvas.SplineProperty("tightness", -2f);
            _canvas.SplineProperty("ends", SplineEndMode.Include);

            Assert.That((float)_canvas.GetSplineProperty("TIGHTNESS"), Is.EqualTo(-2f).Within(Tolerance));
            Assert.AreEqual(SplineEndMode.Include, _canvas.GetSplineProperty("ends"));
        }

        [Test]
        public void BezierOrderRejectsValuesOtherThanTwoAndThree()
        {
            LogAssert.Expect(
                LogType.Warning,
                "p5.Unite: BezierOrder() expects 2 or 3 but received 4."
            );
            _canvas.BezierOrder(4);

            Assert.AreEqual(3, _canvas.GetBezierOrder());
        }

        [Test]
        public void CurveDetailClampsToValidRange()
        {
            _canvas.CurveDetail(0);
            Assert.AreEqual(1, _canvas.GetCurveDetail());

            _canvas.CurveDetail(9999);
            Assert.AreEqual(512, _canvas.GetCurveDetail());
        }

        [Test]
        public void LineCapsPackIntoStartAndEndBits()
        {
            int caps = UCanvas.PackLineCaps(StrokeCap.Square, StrokeCap.Project);

            Assert.AreEqual((int)StrokeCap.Square, caps & 3);
            Assert.AreEqual((int)StrokeCap.Project, (caps >> 2) & 3);
        }

        [Test]
        public void BuildPathPolylineKeepsLinearVertices()
        {
            var commands = new List<UCanvas.PathCommand>
            {
                Linear(0f, 0f),
                Linear(1f, 0f),
                Linear(1f, 1f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            Assert.AreEqual(3, polyline.Count);
            AssertVector(new Vector3(0f, 0f, 0f), polyline[0]);
            AssertVector(new Vector3(1f, 0f, 0f), polyline[1]);
            AssertVector(new Vector3(1f, 1f, 0f), polyline[2]);
        }

        [Test]
        public void BuildPathPolylineBezierRunUsesFirstVertexAsAnchor()
        {
            _canvas.CurveDetail(8);
            var commands = new List<UCanvas.PathCommand>
            {
                Bezier(0f, 0f),
                Bezier(0f, 1f),
                Bezier(1f, 1f),
                Bezier(1f, 0f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            Assert.AreEqual(1 + 8, polyline.Count);
            AssertVector(new Vector3(0f, 0f, 0f), polyline[0]);
            AssertVector(new Vector3(1f, 0f, 0f), polyline[polyline.Count - 1]);
        }

        [Test]
        public void BuildPathPolylineQuadraticBezierUsesControlPoint()
        {
            _canvas.CurveDetail(10);
            _canvas.BezierOrder(2);
            var commands = new List<UCanvas.PathCommand>
            {
                Bezier(0f, 0f),
                Bezier(0f, 1f),
                Bezier(1f, 1f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            Assert.AreEqual(1 + 10, polyline.Count);
            AssertVector(new Vector3(0.25f, 0.75f, 0f), polyline[5]);
        }

        [Test]
        public void BuildPathPolylineSplineIncludePassesThroughEveryPoint()
        {
            _canvas.CurveDetail(10);
            _canvas.SplineEnds = SplineEndMode.Include;
            var commands = new List<UCanvas.PathCommand>
            {
                Spline(0f, 0f),
                Spline(1f, 0f),
                Spline(1f, 1f),
                Spline(2f, 1f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            Assert.AreEqual(1 + 30, polyline.Count);
            AssertVector(new Vector3(1f, 0f, 0f), polyline[10]);
            AssertVector(new Vector3(1f, 1f, 0f), polyline[20]);
            AssertVector(new Vector3(2f, 1f, 0f), polyline[30]);
        }

        [Test]
        public void BuildPathPolylineSplineExcludeDrawsOnlyTheMiddleSpan()
        {
            _canvas.CurveDetail(10);
            _canvas.SplineEnds = SplineEndMode.Exclude;
            var commands = new List<UCanvas.PathCommand>
            {
                Spline(0f, 0f),
                Spline(1f, 0f),
                Spline(1f, 1f),
                Spline(2f, 1f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            Assert.AreEqual(1 + 10, polyline.Count);
            AssertVector(new Vector3(1f, 0f, 0f), polyline[0]);
            AssertVector(new Vector3(1f, 1f, 0f), polyline[10]);
        }

        [Test]
        public void BuildPathPolylineMixesLinearAndSplineVertices()
        {
            _canvas.CurveDetail(4);
            _canvas.SplineEnds = SplineEndMode.Include;
            var commands = new List<UCanvas.PathCommand>
            {
                Linear(0f, 0f),
                Spline(1f, 0f),
                Spline(1f, 1f),
                Spline(2f, 1f),
                Linear(3f, 1f)
            };

            var polyline = _canvas.BuildPathPolyline(commands);

            // One linear vertex, the spline start vertex, two spline spans of
            // four samples each, and the closing linear vertex.
            Assert.AreEqual(11, polyline.Count);
            AssertVector(new Vector3(3f, 1f, 0f), polyline[polyline.Count - 1]);
        }

        private static UCanvas.PathCommand Linear(float x, float y)
            => new()
            {
                Point = new Vector3(x, y, 0f),
                Kind = UCanvas.PathCommandKind.Linear
            };

        private static UCanvas.PathCommand Bezier(float x, float y)
            => new()
            {
                Point = new Vector3(x, y, 0f),
                Kind = UCanvas.PathCommandKind.Bezier
            };

        private static UCanvas.PathCommand Spline(float x, float y)
            => new()
            {
                Point = new Vector3(x, y, 0f),
                Kind = UCanvas.PathCommandKind.Spline
            };

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
