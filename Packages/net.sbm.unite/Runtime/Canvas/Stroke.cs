using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        /// <summary>
        /// Sets the style used to render the ends of strokes.
        /// </summary>
        public void StrokeCap(StrokeCap cap) => _styleState.StrokeCap = cap;

        /// <summary>
        /// Gets the current stroke cap style.
        /// </summary>
        public StrokeCap GetStrokeCap() => _styleState.StrokeCap;

        /// <summary>
        /// Sets the style used to render joints between stroke segments.
        /// </summary>
        /// <remarks>
        /// Stroke rendering currently always produces rounded joints, so
        /// <see cref="StrokeJoin.Miter"/> and <see cref="StrokeJoin.Bevel"/>
        /// are stored but not yet rendered.
        /// </remarks>
        public void StrokeJoin(StrokeJoin join) => _styleState.StrokeJoin = join;

        /// <summary>
        /// Gets the current stroke join style.
        /// </summary>
        public StrokeJoin GetStrokeJoin() => _styleState.StrokeJoin;

        /// <summary>
        /// Enables or disables perspective scaling for lines drawn in world space.
        /// When enabled, the stroke weight is treated as world units and lines get
        /// thinner as they move away from the camera. When disabled, the stroke
        /// weight is treated as screen pixels and stays constant with distance.
        /// </summary>
        public void LinePerspective(bool enable) => _styleState.LinePerspective = enable;

        /// <summary>
        /// Gets whether perspective scaling is enabled for lines drawn in world space.
        /// </summary>
        public bool GetLinePerspective() => _styleState.LinePerspective;

        /// <summary>
        /// Enqueues a single stroked line segment using the current stroke style.
        /// </summary>
        private void EnqueueLineSegment(
            Vector3 start,
            Vector3 end,
            StrokeCap startCap,
            StrokeCap endCap
        )
        {
            CheckDrawingContext();
            if (!isActiveAndEnabled)
                return;

            if (!_styleState.StrokeEnabled)
                return;

            float strokeWeight = Mathf.Max(0f, _styleState.StrokeWeight);
            if (strokeWeight <= 0f)
                return;

            var midpoint = (start + end) * 0.5f;

            _commandQueue.Add(new DrawCommand
            {
                Transform = CurrentTransform * Matrix4x4.Translate(midpoint),
                LineStart = start - midpoint,
                LineEnd = end - midpoint,
                ShapeType = Shape.Line3D,
                StrokeColor = _styleState.StrokeColor,
                StrokeWeight = strokeWeight,
                FillEnabled = false,
                StrokeEnabled = true,
                LineCaps = PackLineCaps(startCap, endCap),
                LinePerspective = _styleState.LinePerspective
            });
        }
    }
}
