using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unite.Canvas;
using Unite.Drawing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unite.Tests
{
    public sealed class TransformApiTests
    {
        private const float Tolerance = 0.0001f;
        private UCanvas _canvas;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TransformApiTests");
            _canvas = _gameObject.AddComponent<UCanvas>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ApplyMatrixSixValuesUsesP5Layout()
        {
            _canvas.ApplyMatrix(2f, 3f, 4f, 5f, 6f, 7f);

            AssertVector(new Vector3(22f, 28f, 0f), _canvas.TransformPoint(new Vector3(2f, 3f)));
        }

        [Test]
        public void ApplyMatrixSixElementArrayUsesP5Layout()
        {
            _canvas.ApplyMatrix(new[] { 2f, 3f, 4f, 5f, 6f, 7f });

            AssertVector(new Vector3(22f, 28f, 0f), _canvas.TransformPoint(new Vector3(2f, 3f)));
        }

        [Test]
        public void ApplyMatrixSixteenValuesUsesP5ColumnOrder()
        {
            _canvas.ApplyMatrix(
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                4f, 5f, 6f, 1f
            );

            AssertVector(new Vector3(5f, 7f, 9f), _canvas.TransformPoint(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void ApplyMatrixSixteenElementArrayUsesP5ColumnOrder()
        {
            _canvas.ApplyMatrix(new[]
            {
                1f, 0f, 0f, 0f,
                0f, 1f, 0f, 0f,
                0f, 0f, 1f, 0f,
                4f, 5f, 6f, 1f
            });

            AssertVector(new Vector3(5f, 7f, 9f), _canvas.TransformPoint(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void ApplyMatrixAcceptsUnityMatrix()
        {
            _canvas.ApplyMatrix(Matrix4x4.Translate(new Vector3(3f, 4f, 5f)));

            AssertVector(new Vector3(3f, 4f, 5f), _canvas.TransformPoint(Vector3.zero));
        }

        [Test]
        public void ApplyMatrixRejectsNullAndInvalidArrayLengths()
        {
            Assert.Throws<ArgumentNullException>(() => _canvas.ApplyMatrix((float[])null));
            Assert.Throws<ArgumentException>(() => _canvas.ApplyMatrix(new float[5]));
            Assert.Throws<ArgumentException>(() => _canvas.ApplyMatrix(new float[7]));
        }

        [Test]
        public void TransformCallsAccumulateInCallOrder()
        {
            _canvas.Translate(10f, 20f);
            _canvas.RotateZ(Mathf.PI * 0.5f);

            AssertVector(new Vector3(10f, 21f, 0f), _canvas.TransformPoint(Vector3.right));
        }

        [Test]
        public void AxisRotationsRotateAboutExpectedAxes()
        {
            _canvas.RotateX(Mathf.PI * 0.5f);
            AssertVector(Vector3.forward, _canvas.TransformPoint(Vector3.up));

            _canvas.ResetMatrix();
            _canvas.RotateY(Mathf.PI * 0.5f);
            AssertVector(Vector3.back, _canvas.TransformPoint(Vector3.right));

            _canvas.ResetMatrix();
            _canvas.Rotate(Mathf.PI * 0.5f, Vector3.forward);
            AssertVector(Vector3.up, _canvas.TransformPoint(Vector3.right));
        }

        [Test]
        public void ShearFunctionsUseP5Formulas()
        {
            float angle = Mathf.Atan(2f);
            _canvas.ShearX(angle);
            AssertVector(new Vector3(7f, 3f, 0f), _canvas.TransformPoint(new Vector3(1f, 3f)));

            _canvas.ResetMatrix();
            _canvas.ShearY(angle);
            AssertVector(new Vector3(3f, 7f, 0f), _canvas.TransformPoint(new Vector3(3f, 1f)));
        }

        [Test]
        public void ScaleOverloadsFollowP5DefaultsAndPreserveNegativeScale()
        {
            _canvas.Scale(-2f);
            AssertVector(new Vector3(-2f, -4f, -6f), _canvas.TransformPoint(new Vector3(1f, 2f, 3f)));

            _canvas.ResetMatrix();
            _canvas.Scale(2f, 3f);
            AssertVector(new Vector3(2f, 6f, 9f), _canvas.TransformPoint(new Vector3(1f, 2f, 3f)));

            _canvas.ResetMatrix();
            _canvas.Scale(new Vector2(4f, 5f));
            AssertVector(new Vector3(4f, 10f, 15f), _canvas.TransformPoint(new Vector3(1f, 2f, 3f)));
        }

        [Test]
        public void PushPopRestoresTransformAndAllCurrentP5Styles()
        {
            _canvas.Translate(10f, 20f);
            _canvas.Fill(Color.red);
            _canvas.Stroke(Color.green);
            _canvas.StrokeWeight(3f);
            _canvas.RectMode = ShapeMode.Center;
            _canvas.EllipseMode = ShapeMode.Radius;
            var expectedTransform = _canvas.CurrentTransform;
            var expectedStyle = _canvas.StyleState;

            _canvas.Push();
            _canvas.ResetMatrix();
            _canvas.NoFill();
            _canvas.NoStroke();
            _canvas.StrokeWeight(9f);
            _canvas.RectMode = ShapeMode.Corners;
            _canvas.EllipseMode = ShapeMode.Corner;
            _canvas.Pop();

            AssertMatrix(expectedTransform, _canvas.CurrentTransform);
            AssertStyle(expectedStyle, _canvas.StyleState);
        }

        [Test]
        public void PushPopSupportsNestedDrawingGroups()
        {
            _canvas.Fill(Color.red);
            _canvas.Push();
            _canvas.Fill(Color.green);
            _canvas.Push();
            _canvas.Fill(Color.blue);

            _canvas.Pop();
            Assert.AreEqual(Color.green, _canvas.StyleState.FillColor);

            _canvas.Pop();
            Assert.AreEqual(Color.red, _canvas.StyleState.FillColor);
        }

        [Test]
        public void PopWithoutPushWarnsAndLeavesStateUnchanged()
        {
            _canvas.Translate(2f, 3f);
            _canvas.Fill(Color.magenta);
            var expectedTransform = _canvas.CurrentTransform;
            var expectedStyle = _canvas.StyleState;

            LogAssert.Expect(LogType.Warning, "p5.Unite: Pop() was called without a matching Push().");
            _canvas.Pop();

            AssertMatrix(expectedTransform, _canvas.CurrentTransform);
            AssertStyle(expectedStyle, _canvas.StyleState);
        }

        [Test]
        public void ShapeDataLayoutMatchesHlslContract()
        {
            Assert.AreEqual(17 * Marshal.SizeOf<Vector4>(), Marshal.SizeOf<ShapeData>());
        }

        private static void AssertStyle(CanvasStyleState expected, CanvasStyleState actual)
        {
            Assert.AreEqual(expected.FillColor, actual.FillColor);
            Assert.AreEqual(expected.StrokeColor, actual.StrokeColor);
            Assert.AreEqual(expected.StrokeWeight, actual.StrokeWeight);
            Assert.AreEqual(expected.FillEnabled, actual.FillEnabled);
            Assert.AreEqual(expected.StrokeEnabled, actual.StrokeEnabled);
            Assert.AreEqual(expected.RectMode, actual.RectMode);
            Assert.AreEqual(expected.EllipseMode, actual.EllipseMode);
        }

        private static void AssertMatrix(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(Tolerance));
            }
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
        }
    }
}
