// UniteCommon.hlsl - Shared shader utilities for p5.Unite

#ifndef UNITE_COMMON_HLSL
#define UNITE_COMMON_HLSL

struct ShapeData
{
    float3 Position;
    float4 Rotation; // Quaternion
    float2 Size;
    int ShapeType;
    float4 FillColor;
    float4 StrokeColor;
    float StrokeWeight;
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

// Build transform matrix from position, rotation, and size
float4x4 BuildTRSMatrix(float3 pos, float4 rot, float2 size)
{
    float3 s = float3(size.x, size.y, 1.0);
    
    float4x4 rMat = QuaternionToMatrix(rot);
    
    float4x4 tMat = float4x4(
        1, 0, 0, pos.x,
        0, 1, 0, pos.y,
        0, 0, 1, pos.z,
        0, 0, 0, 1
    );
    
    float4x4 sMat = float4x4(
        s.x, 0, 0, 0,
        0, s.y, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    );

    return mul(tMat, mul(rMat, sMat));
}

#endif // UNITE_COMMON_HLSL
