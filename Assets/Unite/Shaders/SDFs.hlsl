// UV-based signed distance functions.

#ifndef UNITE_SDFS_INCLUDED
#define UNITE_SDFS_INCLUDED

// Signed distance to a circle centered at 0.5 with radius 0.5.
float Circle(float2 uv)
{
    return length(uv - 0.5) - 0.5;
}

// Signed distance to a square centered at 0.5 with half-extent 0.5.
float Square(float2 uv)
{
    float2 d = abs(uv - 0.5) - 0.5;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// Smooth fill alpha from SDF distance (1 inside, 0 outside).
float SDF_FillAlpha(float d)
{
    float aa = fwidth(d);
    return smoothstep(aa, -aa, d);
}

// Composites fill and stroke colors using SDF distance.
//
// strokeWeight: per-axis stroke thickness in UV space (0 = no stroke).
//               The mesh has been expanded by this amount on each side.
// size:         expanded mesh size (ShapeData.Size), used for aspect correction.
half4 ComputeShapeColor(float2 uv, int shapeType, float2 strokeWeight,
                         float2 size, half4 fillColor, half4 strokeColor, half4 baseColor)
{
    // Remap UV so the original (pre-expansion) shape boundary maps to [0,1].
    float2 expansion = max(1.0 - 2.0 * strokeWeight, 0.001);
    float2 scaledUV = (uv - 0.5) / expansion + 0.5;

    // Correct for aspect ratio so SDF distances are uniform in physical space.
    float2 aspect = size / max(max(size.x, size.y), 0.001);
    float2 correctedUV = (scaledUV - 0.5) * aspect + 0.5;

    // SDF in aspect-corrected space.
    float d = (shapeType == 0) ? Circle(correctedUV) : Square(correctedUV);

    // Stroke band width in corrected space.
    // Both axes produce the same value (uniform mesh expansion); min() is a safety guard.
    float strokeBand = min(strokeWeight.x * aspect.x, strokeWeight.y * aspect.y);

    // Fill / stroke alpha
    float fillAlpha = step(d, 0.0);
    float strokeAlpha = 0.0;
    if (strokeBand > 0.0)
    {
        strokeAlpha = step(0.0, d) * step(d, strokeBand);
    }

    // Composite: fill over stroke
    half4 fill = fillColor * baseColor;
    fill.a *= fillAlpha;

    half4 stroke = strokeColor * baseColor;
    stroke.a *= strokeAlpha;

    half4 result;
    result.rgb = lerp(stroke.rgb, fill.rgb, fill.a);
    result.a = saturate(fill.a + stroke.a * (1.0 - fill.a));

    return result;
}

#endif
