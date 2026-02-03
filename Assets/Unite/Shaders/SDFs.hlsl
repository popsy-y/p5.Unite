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

#endif
