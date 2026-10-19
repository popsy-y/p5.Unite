using System.Collections.Generic;
using UnityEngine;

namespace Unite.Coordinate
{
    public struct TRS
    {
        public Vector3 Translation;
        public float Rotation;
        public Vector3 Scale;
    }

    public class CanvasTransformStack
    {
        private Matrix4x4 _currentMatrix = Matrix4x4.identity;

        private readonly Stack<Matrix4x4> _stack = new();

        public Matrix4x4 CurrentMatrix => _currentMatrix;

        public CanvasTransformStack()
        {
            Reset();
        }

        public void Reset()
        {
            _currentMatrix = Matrix4x4.identity;
        }

        public void Clear()
        {
            Reset();
            _stack.Clear();
        }

        public void Push() => _stack.Push(_currentMatrix);

        public void Pop()
        {
            if (_stack.Count > 0)
            {
                _currentMatrix = _stack.Pop();
            }
            else
            {
                Debug.LogWarning("p5.Unite: Pop() was called without a matching Push().");
            }
        }

        public void Restore(Matrix4x4 matrix) => _currentMatrix = matrix;

        public void ApplyMatrix(Matrix4x4 matrix) => _currentMatrix *= matrix;

        public void Translate(Vector3 translation)
            => MatrixMath.Translate(ref _currentMatrix, translation.x, translation.y, translation.z);

        public void Translate(float x, float y, float z = 0f)
            => MatrixMath.Translate(ref _currentMatrix, x, y, z);

        public void Rotate(float radians)
            => MatrixMath.RotateZ(ref _currentMatrix, radians);

        public void Rotate(Vector3 anglesRadians)
            => _currentMatrix *= Matrix4x4.Rotate(Quaternion.Euler(anglesRadians * Mathf.Rad2Deg));

        public void Rotate(float radians, Vector3 axis)
        {
            if (axis.sqrMagnitude <= Mathf.Epsilon)
                return;

            _currentMatrix *= Matrix4x4.Rotate(
                Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis.normalized)
            );
        }

        public void RotateX(float radians) => Rotate(radians, Vector3.right);

        public void RotateY(float radians) => Rotate(radians, Vector3.up);

        public void RotateZ(float radians) => Rotate(radians, Vector3.forward);

        public void Scale(Vector3 scale)
            => MatrixMath.Scale(ref _currentMatrix, scale.x, scale.y, scale.z);

        public void Scale(float x, float y, float z)
            => MatrixMath.Scale(ref _currentMatrix, x, y, z);

        public void ShearX(float radians)
        {
            var matrix = Matrix4x4.identity;
            matrix.m01 = Mathf.Tan(radians);
            ApplyMatrix(matrix);
        }

        public void ShearY(float radians)
        {
            var matrix = Matrix4x4.identity;
            matrix.m10 = Mathf.Tan(radians);
            ApplyMatrix(matrix);
        }


        public Vector3 TransformPoint(Vector3 point)
            => _currentMatrix.MultiplyPoint(point);

        public (Vector3 angleDeg, Vector2 scale) RotationAndScale()
            => (
                _currentMatrix.rotation.eulerAngles,
                new Vector2(_currentMatrix.lossyScale.x, _currentMatrix.lossyScale.y)
            );
    }
}
