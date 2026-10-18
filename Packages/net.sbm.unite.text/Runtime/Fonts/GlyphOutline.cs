using System;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>Kind of one outline path command (SVG-style M / L / Q / C / Z).</summary>
    public enum PathCommandType
    {
        MoveTo,
        LineTo,
        QuadraticTo,
        CubicTo,
        Close
    }

    /// <summary>
    /// One outline path command in glyph units. <see cref="Control"/> is the quadratic
    /// control point or the first cubic control point, <see cref="Control2"/> the second
    /// cubic control point; <see cref="Point"/> is the end point (unused for
    /// <see cref="PathCommandType.Close"/>).
    /// </summary>
    public readonly struct PathCommand
    {
        public PathCommand(PathCommandType type, Vector2 control, Vector2 point)
            : this(type, control, default, point)
        {
        }

        public PathCommand(PathCommandType type, Vector2 control, Vector2 control2, Vector2 point)
        {
            Type = type;
            Control = control;
            Control2 = control2;
            Point = point;
        }

        public PathCommandType Type { get; }
        public Vector2 Control { get; }
        public Vector2 Control2 { get; }
        public Vector2 Point { get; }
    }

    /// <summary>
    /// A glyph outline in glyph units (y up): flattened closed polygon contours, the
    /// union bounding box of every contour point and the unflattened path commands
    /// (curves kept). Composites are resolved into plain contours so downstream code
    /// never sees component relationships.
    /// </summary>
    public sealed class GlyphOutline
    {
        public GlyphOutline(
            Vector2[][] contours,
            Vector2 boundsMin,
            Vector2 boundsMax,
            PathCommand[] commands = null
        )
        {
            Contours = contours;
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            Commands = commands ?? Array.Empty<PathCommand>();
        }

        public Vector2[][] Contours { get; }
        public Vector2 BoundsMin { get; }
        public Vector2 BoundsMax { get; }
        public PathCommand[] Commands { get; }
    }
}
