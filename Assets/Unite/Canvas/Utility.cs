using Unite.Drawing;
using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        // Return 0 to 1 linear depth calculated from world space depth from camera
        // Unite only expose human friendly depth value
        public float WorldToLinearDepth(float worldDistance)
        {
            var cam = _currentCamera;
            if (!cam) return worldDistance;
            
            // Debug.Log($"near: {near}, far: {far}, dist: {worldDistance}");

            // Unity uses Reverse-Z, but we need convert it to normal z depth value
            // (because we are following processing / p5.js)
            // return Mathf.Clamp01(1f - Mathf.InverseLerp(near, far, worldDistance));
            // var clipDepth = NonLinearDepth(cam, worldDistance);

            return Mathf.InverseLerp(cam.nearClipPlane, cam.farClipPlane, worldDistance);
        }
    }
}