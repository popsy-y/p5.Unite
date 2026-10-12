using System;
using System.Collections.Generic;
using Unite.Coordinate;
using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        private readonly CanvasTransformStack _transformStack = new();
        private readonly Stack<CanvasState> _stateStack = new();

        internal Matrix4x4 CurrentTransform => _transformStack.CurrentMatrix;

        /// <summary>
        /// Clears the current transformation matrix without discarding saved Push() states.
        /// </summary>
        public void ResetMatrix() => _transformStack.Reset();

        /// <summary>
        /// Backward-compatible alias for ResetMatrix().
        /// </summary>
        public void ResetTransform() => ResetMatrix();

        /// <summary>
        /// Begins a nested drawing group with its own transform and drawing style.
        /// </summary>
        public void Push() => _stateStack.Push(new CanvasState(CurrentTransform, _styleState));

        /// <summary>
        /// Restores the transform and drawing style saved by the matching Push().
        /// </summary>
        public void Pop()
        {
            if (_stateStack.Count == 0)
            {
                Debug.LogWarning("p5.Unite: Pop() was called without a matching Push().");
                return;
            }

            var state = _stateStack.Pop();
            _transformStack.Restore(state.Transform);
            _styleState = state.Style;
        }

        public void ApplyMatrix(Matrix4x4 matrix) => _transformStack.ApplyMatrix(matrix);

        public void ApplyMatrix(float[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Length == 6)
            {
                ApplyMatrix(values[0], values[1], values[2], values[3], values[4], values[5]);
                return;
            }

            if (values.Length == 16)
            {
                ApplyMatrix(
                    values[0],  values[1],  values[2],  values[3],
                    values[4],  values[5],  values[6],  values[7],
                    values[8],  values[9],  values[10], values[11],
                    values[12], values[13], values[14], values[15]
                );
                return;
            }

            throw new ArgumentException(
                "p5.Unite: ApplyMatrix() requires an array with 6 or 16 elements.",
                nameof(values)
            );
        }

        public void ApplyMatrix(float a, float b, float c, float d, float e, float f)
            => ApplyMatrix(new Matrix4x4(
                new Vector4(a,  b,  0f, 0f),
                new Vector4(c,  d,  0f, 0f),
                new Vector4(0f, 0f, 1f, 0f),
                new Vector4(e,  f,  0f, 1f)
            ));

        public void ApplyMatrix(
            float a, float b, float c, float d,
            float e, float f, float g, float h,
            float i, float j, float k, float l,
            float m, float n, float o, float p
        ) => ApplyMatrix(new Matrix4x4(
            new Vector4(a, b, c, d),
            new Vector4(e, f, g, h),
            new Vector4(i, j, k, l),
            new Vector4(m, n, o, p)
        ));

        public void Translate(Vector2 translation) => Translate(translation.x, translation.y);

        public void Translate(Vector3 translation) => _transformStack.Translate(translation);

        public void Translate(float x, float y, float z = 0f) => _transformStack.Translate(x, y, z);

        public void Rotate(float radians) => _transformStack.Rotate(radians);

        public void Rotate(float radians, Vector3 axis) => _transformStack.Rotate(radians, axis);

        public void Rotate(Vector3 anglesRadians) => _transformStack.Rotate(anglesRadians);

        public void RotateX(float radians) => _transformStack.RotateX(radians);

        public void RotateY(float radians) => _transformStack.RotateY(radians);

        public void RotateZ(float radians) => _transformStack.RotateZ(radians);

        public void Scale(float uniform) => Scale(uniform, uniform, uniform);

        public void Scale(float x, float y) => Scale(x, y, 1f);

        public void Scale(Vector2 scale) => Scale(scale.x, scale.y, 1f);

        public void Scale(Vector3 scale) => _transformStack.Scale(scale);

        public void Scale(float x, float y, float z) => _transformStack.Scale(x, y, z);

        public void ShearX(float radians) => _transformStack.ShearX(radians);

        public void ShearY(float radians) => _transformStack.ShearY(radians);

        public Vector3 TransformPoint(Vector3 point) => _transformStack.TransformPoint(point);

        public (Vector3 angleDeg, Vector2 scale) GetRotationAndScale() => _transformStack.RotationAndScale();
    }
}
