// UV-based signed distance functions.

#ifndef UNITE_SDFS_INCLUDED
#define UNITE_SDFS_INCLUDED

// Returns signed distance to a circle (negative inside).
// Center at 0.5, radius 0.5
float Circle(float2 uv)
{
    return length(uv - 0.5) - 0.5;
}

// Returns signed distance to an axis-aligned square in UV space (negative inside).
// Center at 0.5, extents 0.5
float Square(float2 uv)
{
    float2 d = abs(uv - 0.5) - 0.5;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// Converts signed distance to a smooth alpha (1 inside, 0 outside).
float SDF_FillAlpha(float d)
{
    float aa = fwidth(d);
    return smoothstep(aa, -aa, d);
}

// Compute shape SDF with stroke support.
// strokeWeight is in UV space (0 = no stroke).
// The mesh has been expanded by strokeWeight on each side.
// Original shape edge is at 0.5 - strokeWeight from center.
// Returns: fillAlpha, strokeAlpha, and composited color.
half4 ComputeShapeColor(float2 uv, int shapeType, float strokeWeight,
                         half4 fillColor, half4 strokeColor, half4 baseColor)
{
    // Remap UV so that the original shape boundary maps to the standard SDF edge.
    // Expanded mesh UV: center = 0.5, edge = 0.5
    // Original shape edge in expanded UV = 0.5 - strokeWeight from center
    // We need to scale UV so that the original shape fills [0,1] for SDF purposes.
    // scaledUV = (uv - 0.5) / (1.0 - 2.0 * strokeWeight) + 0.5
    float expansion = 1.0 - 2.0 * strokeWeight;
    // Guard against zero division when stroke is extremely large
    expansion = max(expansion, 0.001);
    float2 scaledUV = (uv - 0.5) / expansion + 0.5;

    float d = 0;
    if (shapeType == 0) // Circle
    {
        d = Circle(scaledUV);
    }
    else // Rectangle or Square
    {
        d = Square(scaledUV);
    }

    // d is in scaled UV space. Convert back to expanded UV space for consistent thresholds.
    d *= expansion;

    // Fill: inside the original shape boundary (d < 0)
    float fillAlpha = step(d, 0.0);

    // Stroke: outside shape but within stroke band
    // In expanded UV space, stroke band = [0, strokeWeight] from original edge
    float strokeAlpha = 0.0;
    if (strokeWeight > 0.0)
    {
        // d > 0 means outside original shape, d < strokeWeight means within stroke band
        strokeAlpha = step(0.0, d) * step(d, strokeWeight);
    }

    // Outer edge: beyond stroke, discard (alpha = 0)
    // This handles the expanded mesh area beyond the stroke

    half4 fill = fillColor * baseColor;
    fill.a *= fillAlpha;

    half4 stroke = strokeColor * baseColor;
    stroke.a *= strokeAlpha;

    // Composite: fill on top of stroke
    half4 result;
    result.rgb = lerp(stroke.rgb, fill.rgb, fill.a);
    result.a = saturate(fill.a + stroke.a * (1.0 - fill.a));

    return result;
}

#endif

