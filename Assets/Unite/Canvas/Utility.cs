using Unite.Drawing;
using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        // public float WorldToClipDepth(Vector2 position, float worldSpaceDepth)
        // {
        //     bool isScreenSpace =
        //         _drawingContext.DrawingMode is DrawingMode.ScreenSpace or DrawingMode.ScreenSpaceOverlay;
        //     bool isClipSpace =
        //         _drawingContext.DrawingMode is DrawingMode.ClipSpace or DrawingMode.ClipSpaceOverlay;
        //
        //     if (isScreenSpace || isClipSpace)
        //     {
        //         if (!_currentCamera) return worldSpaceDepth;
        //
        //         Ray ray;
        //         if (isScreenSpace)
        //             ray = _currentCamera.ScreenPointToRay(new Vector2(position.x, _currentCamera.pixelHeight - position.y));
        //         else
        //             ray = _currentCamera.ViewportPointToRay(new Vector2(position.x, 1f - position.y));
        //
        //         Vector3 worldPoint = ray.GetPoint(worldSpaceDepth);
        //         Vector3 screenPoint = _currentCamera.WorldToScreenPoint(worldPoint);
        //
        //         float zDist = screenPoint.z;
        //
        //         float near = _currentCamera.nearClipPlane;
        //         float far = _currentCamera.farClipPlane;
        //
        //         return (zDist - near) / (far - near);
        //     }
        //     else
        //     {
        //         // drawing mode is world space. you don't have to convert depth.
        //         return worldSpaceDepth;
        //     }
        // }
        
        public float WorldToClipDepth(float worldDistance)
        {
            if (!_currentCamera) return worldDistance;
            
            float near = _currentCamera.nearClipPlane;
            float far = _currentCamera.farClipPlane;
            
            Debug.Log($"near: {near}, far: {far}, dist: {worldDistance}");

            // Unity uses Reverse-Z, but we need convert it to normal z depth value
            // (because we are following processing / p5.js)
            return Mathf.Clamp01(1f - Mathf.InverseLerp(near, far, worldDistance));
        }
    }
}