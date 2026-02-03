// UV-based signed distance functions.

#ifndef UNITE_SDFS_INCLUDED
#define UNITE_SDFS_INCLUDED

// Both Circles and Ellipses are drawn as Circles;
// Non-uniform sizing of Ellipses is achieved by resizing the mesh.
float Circle(float2 uv)
{
    return length(uv - .5) > .5;
}

// Returns signed distance to an axis-aligned square in UV space (negative inside).
float Square(float2 uv)
{
    return 0.;
}

// Converts signed distance to a smooth alpha (1 inside, 0 outside).
float SDF_FillAlpha(float d)
{
    float aa = max(fwidth(d), 1e-5);
    return smoothstep(aa, -aa, d);
}

#endif
