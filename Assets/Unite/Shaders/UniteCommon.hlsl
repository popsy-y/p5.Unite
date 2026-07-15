// UniteCommon.hlsl - Shared shader utilities for p5.Unite

#ifndef UNITE_COMMON_HLSL
#define UNITE_COMMON_HLSL

struct ShapeData
{
    float4 Position;
    float4 Rotation;
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

float4x4 QuaternionToMatrix(float4 q)
{
    float x = q.x, y = q.y, z = q.z, w = q.w;
    return float4x4(
        1 - 2*y*y - 2*z*z, 2*x*y - 2*z*w,     2*x*z + 2*y*w,     0,
        2*x*y + 2*z*w,     1 - 2*x*x - 2*z*z, 2*y*z - 2*x*w,     0,
        2*x*z - 2*y*w,     2*y*z + 2*x*w,     1 - 2*x*x - 2*y*y, 0,
        0,                 0,                 0,                 1
    );
}

float3 RotateShapePosition(float2 position, float4 rotation)
{
    return mul(QuaternionToMatrix(rotation), float4(position, 0.0, 1.0)).xyz;
}

float2 ShapePosition(float2 uv, ShapeData data)
{
    return lerp(data.Bounds.xy, data.Bounds.zw, uv);
}

float3 DrawingPosition(float2 shapePosition, ShapeData data)
{
    return data.Position.xyz + RotateShapePosition(shapePosition, data.Rotation);
}

#endif // UNITE_COMMON_HLSL
