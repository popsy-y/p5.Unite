using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Collects the path of one glyph and produces both views of it: the flattened
    /// polygon contours and the unflattened path commands. Both outline formats feed it
    /// move / line / curve calls.
    /// </summary>
    internal sealed class OutlineBuilder
    {
        private const int MinSubdivisions = 2;
        private const int MaxSubdivisions = 48;

        private readonly List<Vector2[]> _contours = new();
        private readonly List<PathCommand> _commands = new();
        private readonly List<Vector2> _current = new();
        private readonly float _flatness;
        private Vector2 _start;
        private Vector2 _position;
        private bool _open;

        /// <param name="flatness">Largest distance, in glyph units, between a curve and its polyline.</param>
        public OutlineBuilder(float flatness)
        {
            _flatness = flatness;
        }

        public int ContourCount => _contours.Count;

        public void MoveTo(Vector2 point)
        {
            Close();
            _start = point;
            _position = point;
            _current.Add(point);
            _commands.Add(new PathCommand(PathCommandType.MoveTo, point, point));
            _open = true;
        }

        public void LineTo(Vector2 point)
        {
            _current.Add(point);
            _commands.Add(new PathCommand(PathCommandType.LineTo, point, point));
            _position = point;
        }

        public void QuadraticTo(Vector2 control, Vector2 point)
        {
            // Polyline error is at most |B''| / (8 n^2) with |B''| = 2 |P0 - 2 P1 + P2|.
            float curvature = (_position - 2f * control + point).magnitude;
            int steps = Subdivisions(0.25f * curvature);
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                float u = 1f - t;
                _current.Add(u * u * _position + 2f * u * t * control + t * t * point);
            }

            _commands.Add(new PathCommand(PathCommandType.QuadraticTo, control, point));
            _position = point;
        }

        public void CubicTo(Vector2 control1, Vector2 control2, Vector2 point)
        {
            // |B''| <= 6 max(|P0 - 2 P1 + P2|, |P1 - 2 P2 + P3|), so the error is at most 0.75 d / n^2.
            float curvature = Mathf.Max(
                (_position - 2f * control1 + control2).magnitude,
                (control1 - 2f * control2 + point).magnitude
            );
            int steps = Subdivisions(0.75f * curvature);
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                float u = 1f - t;
                _current.Add(
                    u * u * u * _position
                    + 3f * u * u * t * control1
                    + 3f * u * t * t * control2
                    + t * t * t * point
                );
            }

            _commands.Add(new PathCommand(PathCommandType.CubicTo, control1, control2, point));
            _position = point;
        }

        /// <summary>Closes the current contour (a new MoveTo closes implicitly).</summary>
        public void Close()
        {
            if (!_open)
                return;

            _open = false;

            // A contour that returns to its start point does not repeat it.
            if (_current.Count > 1 && (_current[_current.Count - 1] - _current[0]).sqrMagnitude < 1e-6f)
                _current.RemoveAt(_current.Count - 1);

            if (_current.Count >= 2)
            {
                _contours.Add(_current.ToArray());
                _commands.Add(new PathCommand(PathCommandType.Close, _start, _start));
            }
            else
            {
                // Degenerate contour: drop its commands too.
                int move = LastMoveIndex();
                _commands.RemoveRange(move, _commands.Count - move);
            }

            _current.Clear();
        }

        /// <summary>Appends an already built outline, transformed by x' = a x + c y + dx, y' = b x + d y + dy.</summary>
        public void AddTransformed(GlyphOutline outline, float a, float b, float c, float d, float dx, float dy)
        {
            Close();
            foreach (var contour in outline.Contours)
            {
                var transformed = new Vector2[contour.Length];
                for (int index = 0; index < contour.Length; index++)
                    transformed[index] = Transform(contour[index], a, b, c, d, dx, dy);
                _contours.Add(transformed);
            }

            foreach (var command in outline.Commands)
            {
                _commands.Add(new PathCommand(
                    command.Type,
                    Transform(command.Control, a, b, c, d, dx, dy),
                    Transform(command.Control2, a, b, c, d, dx, dy),
                    Transform(command.Point, a, b, c, d, dx, dy)
                ));
            }
        }

        /// <summary>The finished outline, or null when the glyph has no contours.</summary>
        public GlyphOutline ToOutline()
        {
            Close();
            if (_contours.Count == 0)
                return null;

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var contour in _contours)
            {
                foreach (var point in contour)
                {
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            return new GlyphOutline(_contours.ToArray(), min, max, _commands.ToArray());
        }

        // Smallest n with errorFactor / n^2 <= flatness.
        private int Subdivisions(float errorFactor)
            => Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(errorFactor / _flatness)), MinSubdivisions, MaxSubdivisions);

        private int LastMoveIndex()
        {
            for (int index = _commands.Count - 1; index >= 0; index--)
            {
                if (_commands[index].Type == PathCommandType.MoveTo)
                    return index;
            }
            return 0;
        }

        private static Vector2 Transform(Vector2 point, float a, float b, float c, float d, float dx, float dy)
            => new(a * point.x + c * point.y + dx, b * point.x + d * point.y + dy);
    }
}
