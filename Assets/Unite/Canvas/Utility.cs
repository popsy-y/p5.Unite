using Unite.Drawing;
using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        public float WorldToClipDepth(Vector2 position, float worldSpaceDepth, DrawingMode mode = DrawingMode.ClipSpace)
        {
            if (
                _drawingContext.DrawingMode is
                    DrawingMode.ScreenSpace or
                    DrawingMode.ScreenSpaceOverlay)
            {
                // treat position as screen space coordinate.
                var ray = _currentCamera.ScreenPointToRay(new Vector3(position.x, position.y, 0f));
                var wPos = ray.GetPoint(worldSpaceDepth);
                // var NDC = 
            }
            else if (
                _drawingContext.DrawingMode is
                    DrawingMode.ClipSpace or
                    DrawingMode.ClipSpaceOverlay)
            {
                // treat position as clip space coordinate.
                
            }
            else
            {
                // drawing mode is world space. you don't have to convert depth.
                return worldSpaceDepth;
            }
        }
    }
}