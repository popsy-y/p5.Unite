// UniteCommon.hlsl - Shared shader utilities for p5.Unite

#ifndef UNITE_COMMON_HLSL
#define UNITE_COMMON_HLSL

struct ShapeData
{
    float4 Transform0;
    float4 Transform1;
    float4 Transform2;
    float4 Transform3;
    float4 Bounds;
    float4 ShapeParameters;

    float4 Point0;
    float4 Point1;
    float4 Point2;
    float4 Point3;

    float4 Parameters0;
    float4 Parameters1;
    float4 CornerRadiiX;
    float4 CornerRadiiY;

    float4 Style;

    float4 FillColor;
    float4 StrokeColor;
};

float2 ShapePosition(float2 uv, ShapeData data)
{
    return lerp(data.Bounds.xy, data.Bounds.zw, uv);
}

float4 DrawingPosition(float2 shapePosition, ShapeData data)
{
    return data.Transform0 * shapePosition.x
         + data.Transform1 * shapePosition.y
         + data.Transform3;
}

#endif // UNITE_COMMON_HLSL
