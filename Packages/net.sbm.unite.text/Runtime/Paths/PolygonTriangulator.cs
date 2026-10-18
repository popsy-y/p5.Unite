using System.Collections.Generic;
using UnityEngine;

namespace Unite.Text
{
    /// <summary>
    /// Triangulates glyph contours (closed polygons, any winding): contours nested an
    /// odd number of times become holes of their enclosing outer contour, holes are
    /// bridged into the outer polygon and the result is ear-clipped. Output triangles
    /// are counter-clockwise in the xy plane.
    /// </summary>
    internal static class PolygonTriangulator
    {
        private const float Epsilon = 1e-7f;

        public static void Triangulate(IReadOnlyList<Vector2[]> contours, List<Vector2> vertices, List<int> triangles)
        {
            var polygons = new List<List<Vector2>>();
            foreach (var contour in contours)
            {
                var cleaned = Clean(contour);
                if (cleaned.Count >= 3 && Mathf.Abs(SignedArea(cleaned)) > Epsilon)
                    polygons.Add(cleaned);
            }

            // Nesting depth: even = outer contour, odd = hole.
            var depth = new int[polygons.Count];
            for (int index = 0; index < polygons.Count; index++)
            {
                for (int other = 0; other < polygons.Count; other++)
                {
                    if (other != index && Contains(polygons[other], polygons[index][0]))
                        depth[index]++;
                }
            }

            for (int outer = 0; outer < polygons.Count; outer++)
            {
                if (depth[outer] % 2 != 0)
                    continue;

                var merged = Oriented(polygons[outer], counterClockwise: true);
                var holes = new List<List<Vector2>>();
                for (int hole = 0; hole < polygons.Count; hole++)
                {
                    if (depth[hole] == depth[outer] + 1 && Contains(polygons[outer], polygons[hole][0]))
                        holes.Add(Oriented(polygons[hole], counterClockwise: false));
                }

                // Rightmost holes first, so each bridge sees the holes still to merge.
                holes.Sort((a, b) => MaxX(b).CompareTo(MaxX(a)));
                for (int index = 0; index < holes.Count; index++)
                    merged = Bridge(merged, holes[index], holes, index + 1);

                EarClip(merged, vertices, triangles);
            }
        }

        private static List<Vector2> Clean(Vector2[] contour)
        {
            var cleaned = new List<Vector2>(contour.Length);
            foreach (var point in contour)
            {
                if (cleaned.Count == 0 || (cleaned[cleaned.Count - 1] - point).sqrMagnitude > Epsilon)
                    cleaned.Add(point);
            }

            while (cleaned.Count > 1 && (cleaned[0] - cleaned[cleaned.Count - 1]).sqrMagnitude <= Epsilon)
                cleaned.RemoveAt(cleaned.Count - 1);
            return cleaned;
        }

        private static List<Vector2> Oriented(List<Vector2> polygon, bool counterClockwise)
        {
            var copy = new List<Vector2>(polygon);
            if (SignedArea(copy) > 0f != counterClockwise)
                copy.Reverse();
            return copy;
        }

        // Connects the hole to the closest outer vertex whose bridge crosses no edge,
        // turning outer + hole into one polygon with a zero-width slit.
        private static List<Vector2> Bridge(List<Vector2> outer, List<Vector2> hole, List<List<Vector2>> pendingHoles, int firstPending)
        {
            int start = 0;
            for (int index = 1; index < hole.Count; index++)
            {
                if (hole[index].x > hole[start].x)
                    start = index;
            }
            var anchor = hole[start];

            var candidates = new List<int>(outer.Count);
            for (int index = 0; index < outer.Count; index++)
                candidates.Add(index);
            candidates.Sort((a, b) => (outer[a] - anchor).sqrMagnitude.CompareTo((outer[b] - anchor).sqrMagnitude));

            int target = candidates[0];
            foreach (int candidate in candidates)
            {
                var point = outer[candidate];
                if (!Crosses(anchor, point, outer) &&
                    !Crosses(anchor, point, hole) &&
                    !CrossesAny(anchor, point, pendingHoles, firstPending))
                {
                    target = candidate;
                    break;
                }
            }

            var merged = new List<Vector2>(outer.Count + hole.Count + 2);
            for (int index = 0; index <= target; index++)
                merged.Add(outer[index]);
            for (int step = 0; step <= hole.Count; step++)
                merged.Add(hole[(start + step) % hole.Count]);
            merged.Add(outer[target]);
            for (int index = target + 1; index < outer.Count; index++)
                merged.Add(outer[index]);
            return merged;
        }

        private static bool CrossesAny(Vector2 from, Vector2 to, List<List<Vector2>> polygons, int first)
        {
            for (int index = first; index < polygons.Count; index++)
            {
                if (Crosses(from, to, polygons[index]))
                    return true;
            }
            return false;
        }

        // Proper crossing of segment from-to with any polygon edge (touching ends excluded).
        private static bool Crosses(Vector2 from, Vector2 to, List<Vector2> polygon)
        {
            for (int index = 0; index < polygon.Count; index++)
            {
                var a = polygon[index];
                var b = polygon[(index + 1) % polygon.Count];
                float d1 = Cross(from, to, a);
                float d2 = Cross(from, to, b);
                float d3 = Cross(a, b, from);
                float d4 = Cross(a, b, to);
                if (d1 * d2 < -Epsilon && d3 * d4 < -Epsilon)
                    return true;
            }
            return false;
        }

        private static void EarClip(List<Vector2> polygon, List<Vector2> vertices, List<int> triangles)
        {
            int baseIndex = vertices.Count;
            vertices.AddRange(polygon);

            var remaining = new List<int>(polygon.Count);
            for (int index = 0; index < polygon.Count; index++)
                remaining.Add(index);

            int guard = 0;
            int cursor = 0;
            while (remaining.Count > 3 && guard < remaining.Count)
            {
                int count = remaining.Count;
                int previous = remaining[(cursor + count - 1) % count];
                int current = remaining[cursor % count];
                int next = remaining[(cursor + 1) % count];

                if (IsEar(polygon, remaining, previous, current, next))
                {
                    triangles.Add(baseIndex + previous);
                    triangles.Add(baseIndex + current);
                    triangles.Add(baseIndex + next);
                    remaining.RemoveAt(cursor % count);
                    guard = 0;
                    cursor = Mathf.Max(0, cursor - 1);
                    continue;
                }

                cursor = (cursor + 1) % count;
                guard++;
            }

            // Degenerate leftovers (no clean ear) are fanned so the loop always ends.
            for (int index = 1; index + 1 < remaining.Count; index++)
            {
                triangles.Add(baseIndex + remaining[0]);
                triangles.Add(baseIndex + remaining[index]);
                triangles.Add(baseIndex + remaining[index + 1]);
            }
        }

        private static bool IsEar(List<Vector2> polygon, List<int> remaining, int previous, int current, int next)
        {
            var a = polygon[previous];
            var b = polygon[current];
            var c = polygon[next];
            if (Cross(a, b, c) <= Epsilon)
                return false; // reflex or degenerate

            foreach (int index in remaining)
            {
                var point = polygon[index];
                if (index == previous || index == current || index == next ||
                    point == a || point == b || point == c)
                    continue;

                if (Cross(a, b, point) >= 0f && Cross(b, c, point) >= 0f && Cross(c, a, point) >= 0f)
                    return false;
            }
            return true;
        }

        private static bool Contains(List<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
            {
                var a = polygon[index];
                var b = polygon[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                var a = polygon[index];
                var b = polygon[(index + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static float MaxX(List<Vector2> polygon)
        {
            float max = float.MinValue;
            foreach (var point in polygon)
                max = Mathf.Max(max, point.x);
            return max;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
            => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }
}
