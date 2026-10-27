using System.Diagnostics;
using Unite.Drawing;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        // Return 0 to 1 linear depth calculated from camera-relative world depth.
        // Unite only expose human friendly(0-1, near-far) depth value

        /// <summary>
        /// Converts a world-space depth to a 0..1 linear depth value as seen by the current camera when DrawingMode is Screen or NormalizedScreen.
        /// </summary>
        /// <param name="worldDistance"></param>
        /// <returns>Depth value</returns>
        public float WorldToLinearDepth(float worldDistance)
        {
            return WorldToLinearDepth(worldDistance, ActiveCamera);
        }

        public float WorldToLinearDepth(float worldDistance, Camera cam)
        {
            if (!cam) return worldDistance;

            // Unity uses Reverse-Z, but we need convert it to normal z depth value
            // (because we are following processing / p5.js)
            return Mathf.InverseLerp(cam.nearClipPlane, cam.farClipPlane, worldDistance);
        }
    }
}
