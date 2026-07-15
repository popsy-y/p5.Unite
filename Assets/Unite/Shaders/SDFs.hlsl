// Signed distance functions evaluated in drawing-space units.

#ifndef UNITE_SDFS_INCLUDED
#define UNITE_SDFS_INCLUDED

float SdEllipse(float2 position, float2 radius)
{
    radius = max(abs(radius), 1e-6);
    float k0 = length(position / radius);
    float k1 = length(position / (radius * radius));
    return k0 * (k0 - 1.0) / max(k1, 1e-6);
}

float SdBox(float2 position, float2 halfSize)
{
    float2 delta = abs(position) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0);
}

float2 CornerRadius(float2 position, ShapeData data, float2 halfSize)
{
    int cornerIndex;
    if (position.y >= 0.0)
        cornerIndex = position.x < 0.0 ? 0 : 1;
    else
        cornerIndex = position.x >= 0.0 ? 2 : 3;

    float2 radius = float2(data.CornerRadiiX[cornerIndex], data.CornerRadiiY[cornerIndex]);
    return min(abs(radius), halfSize);
}

float SdRoundedBox(float2 position, float2 halfSize, ShapeData data)
{
    float2 radius = CornerRadius(position, data, halfSize);
    float2 absolutePosition = abs(position);
    float2 cornerCenter = halfSize - radius;

    if (radius.x > 1e-6 && radius.y > 1e-6 &&
        absolutePosition.x > cornerCenter.x && absolutePosition.y > cornerCenter.y)
    {
        return SdEllipse(absolutePosition - cornerCenter, radius);
    }

    return SdBox(position, halfSize);
}

float SdfAlpha(float distance)
{
    float antialiasing = max(fwidth(distance), 1e-6);
    return smoothstep(antialiasing, -antialiasing, distance);
}

half4 CompositeShape(float distance, ShapeData data)
{
    int styleFlags = (int)data.Style.w;
    float fillAlpha = (styleFlags & 1) != 0 ? SdfAlpha(distance) : 0.0;
    float strokeDistance = abs(distance) - data.Style.z * 0.5;
    float strokeAlpha = (styleFlags & 2) != 0 ? SdfAlpha(strokeDistance) : 0.0;

    half4 fill = data.FillColor;
    fill.a *= fillAlpha;

    half4 stroke = data.StrokeColor;
    stroke.a *= strokeAlpha;

    half4 result;
    result.rgb = lerp(fill.rgb, stroke.rgb, stroke.a);
    result.a = stroke.a + fill.a * (1.0 - stroke.a);
    return result;
}

half4 ComputeShapeColor(float2 shapePosition, ShapeData data)
{
    float2 halfSize = data.ShapeParameters.xy * 0.5;
    float distance = (int)data.Style.x == 0
        ? SdEllipse(shapePosition, halfSize)
        : SdRoundedBox(shapePosition, halfSize, data);

    return CompositeShape(distance, data);
}

#endif
