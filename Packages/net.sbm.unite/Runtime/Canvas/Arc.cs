using UnityEngine;

namespace Unite.Canvas
{
    public partial class UCanvas
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float AngleEpsilon = 0.00001f;

        /// <summary>
        /// Draws an elliptical arc. Angles are expressed in radians.
        /// </summary>
        public void Arc(
            float x,
            float y,
            float width,
            float height,
            float start,
            float stop,
            ArcMode mode = ArcMode.Open
        ) => Arc(new Vector3(x, y, 0f), new Vector2(width, height), start, stop, mode);

        public void Arc(
            float x,
            float y,
            float z,
            float width,
            float height,
            float start,
            float stop,
            ArcMode mode = ArcMode.Open
        ) => Arc(new Vector3(x, y, z), new Vector2(width, height), start, stop, mode);

        public void Arc(
            Vector2 position,
            Vector2 size,
            float start,
            float stop,
            ArcMode mode = ArcMode.Open
        ) => Arc(new Vector3(position.x, position.y, 0f), size, start, stop, mode);

        public void Arc(
            Vector3 position,
            Vector2 size,
            float start,
            float stop,
            ArcMode mode = ArcMode.Open
        )
        {
            CheckDrawingContext();
            if (!isActiveAndEnabled)
                return;
            if (!_styleState.FillEnabled && !_styleState.StrokeEnabled)
                return;

            float rawSweep = stop - start;
            if (Mathf.Abs(rawSweep) < AngleEpsilon)
                return;

            var (center, resolvedSize) = ResolveShapeMode(position, size, EllipseMode);
            var shapeSize = Abs(resolvedSize);
            var radius = shapeSize * 0.5f;

            bool isFullEllipse = Mathf.Abs(Mathf.Repeat(rawSweep, TwoPi)) < AngleEpsilon;
            float correctedStart = CorrectEllipseAngle(start, shapeSize);
            float correctedSweep = isFullEllipse
                ? TwoPi
                : Mathf.Repeat(CorrectEllipseAngle(stop, shapeSize) - correctedStart, TwoPi);

            if (!isFullEllipse && correctedSweep < AngleEpsilon)
                return;

            CalculateArcBounds(
                radius,
                correctedStart,
                correctedSweep,
                mode,
                out var boundsMin,
                out var boundsMax
            );

            _commandQueue.Add(new DrawCommand
            {
                Transform = CurrentTransform * Matrix4x4.Translate(center),
                Size = shapeSize,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                HasCustomBounds = true,
                Parameters0 = new Vector4(correctedStart, correctedSweep, 0f, 0f),
                ShapeType = Shape.Arc,
                ArcMode = mode,
                FillColor = _styleState.FillColor,
                StrokeColor = _styleState.StrokeColor,
                StrokeWeight = Mathf.Max(0f, _styleState.StrokeWeight),
                FillEnabled = _styleState.FillEnabled,
                StrokeEnabled = _styleState.StrokeEnabled
            });
        }

        private static float CorrectEllipseAngle(float angle, Vector2 size)
        {
            float normalized = Mathf.Repeat(angle, TwoPi);
            return Mathf.Repeat(
                Mathf.Atan2(size.x * Mathf.Sin(normalized), size.y * Mathf.Cos(normalized)),
                TwoPi
            );
        }

        private static void CalculateArcBounds(
            Vector2 radius,
            float start,
            float sweep,
            ArcMode mode,
            out Vector2 boundsMin,
            out Vector2 boundsMax
        )
        {
            if (sweep >= TwoPi - AngleEpsilon)
            {
                boundsMin = -radius;
                boundsMax = radius;
                return;
            }

            var startPoint = EllipsePoint(radius, start);
            boundsMin = startPoint;
            boundsMax = startPoint;
            IncludeArcPoint(EllipsePoint(radius, start + sweep), ref boundsMin, ref boundsMax);

            for (int index = 0; index < 4; index++)
            {
                float cardinal = index * Mathf.PI * 0.5f;
                if (AngleIsInSweep(cardinal, start, sweep))
                    IncludeArcPoint(EllipsePoint(radius, cardinal), ref boundsMin, ref boundsMax);
            }

            if (mode == ArcMode.Pie)
                IncludeArcPoint(Vector2.zero, ref boundsMin, ref boundsMax);
        }

        private static Vector2 EllipsePoint(Vector2 radius, float angle)
            => new(radius.x * Mathf.Cos(angle), -radius.y * Mathf.Sin(angle));

        private static bool AngleIsInSweep(float angle, float start, float sweep)
            => Mathf.Repeat(angle - start, TwoPi) <= sweep + AngleEpsilon;

        private static void IncludeArcPoint(
            Vector2 point,
            ref Vector2 boundsMin,
            ref Vector2 boundsMax
        )
        {
            boundsMin = Vector2.Min(boundsMin, point);
            boundsMax = Vector2.Max(boundsMax, point);
        }
    }
}
