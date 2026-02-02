using System.Collections.Generic;
using Unity.Mathematics;
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

        private Stack<Matrix4x4> _stack = new();

        public CanvasTransformStack()
        {
            Reset();
        }

        public void Reset()
        {
            _currentMatrix = Matrix4x4.identity;
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
                Debug.LogWarning("Matrix stack underflow!");
                _currentMatrix = Matrix4x4.identity;
            }
        }

        public void Translate(Vector3 translation)
            => _currentMatrix *= Matrix4x4.Translate(translation);

        public void Translate(float x, float y, float z = 0f)
            => Translate(new Vector3(x, y, z));


        public void Rotate(float radians)
            => _currentMatrix *= Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, radians * Mathf.Rad2Deg));

        public void Rotate(Vector3 anglesRadians)
            => _currentMatrix *= Matrix4x4.Rotate(Quaternion.Euler(anglesRadians * Mathf.Rad2Deg));


        public void Scale(Vector3 scale)
            => _currentMatrix *= Matrix4x4.Scale(scale);

        public void Scale(float x, float y, float z)
            => Scale(new Vector3(x, y, z));


        public Vector3 TransformPoint(Vector3 point)
            => _currentMatrix.MultiplyPoint3x4(point);

        // public (float angleDeg, Vector2 scale) RotationAndScale()
        //     => (
        //         _currentMatrix.rotation.eulerAngles.z * Mathf.Deg2Rad,
        //         new Vector2(_currentMatrix.lossyScale.x, _currentMatrix.lossyScale.y)
        //     );

        public (Vector3 angleDeg, Vector2 scale) RotationAndScale()
            => (
                _currentMatrix.rotation.eulerAngles,
                new Vector2(_currentMatrix.lossyScale.x, _currentMatrix.lossyScale.y)
            );
    }
}