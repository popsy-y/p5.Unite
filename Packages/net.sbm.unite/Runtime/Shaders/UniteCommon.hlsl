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

// PolygonFill only: shape-local outline points, referenced by Parameters0.xy.
StructuredBuffer<float4> _PolygonPoints;

float4 DrawingPosition(float2 shapePosition, ShapeData data)
{
    return data.Transform0 * shapePosition.x
         + data.Transform1 * shapePosition.y
         + data.Transform3;
}

float4 TransformPoint3D(float3 position, ShapeData data)
{
    return data.Transform0 * position.x
         + data.Transform1 * position.y
         + data.Transform2 * position.z
         + data.Transform3;
}

float SafeDivide(float value, float divisor)
{
    float safeDivisor = abs(divisor) > 1e-6
        ? divisor
        : (divisor < 0.0 ? -1e-6 : 1e-6);
    return value / safeDivisor;
}

// Shared varyings for SDF shapes and 3D line segments.
struct UniteVaryings
{
    float4 positionCS : SV_POSITION;
    float2 shapePosition : TEXCOORD0;
    nointerpolation uint instanceID : TEXCOORD1;
    noperspective float2 lineCoords : TEXCOORD2;
    noperspective float halfWidth : TEXCOORD3;
    noperspective float lineLength : TEXCOORD4;
};

struct UniteLineResult
{
    float4 positionCS;
    float2 lineCoords;
    float halfWidth;
    float lineLength;
};

#define UNITE_LINE_CAP_ROUND 0
#define UNITE_LINE_CAP_SQUARE 1
#define UNITE_LINE_CAP_PROJECT 2

// Signed distance to a stroked line segment. The position, halfWidth and length
// are expressed in the same coordinate space, and the caps are packed with the
// start cap in bits 0..1 and the end cap in bits 2..3.
float UniteLineDistance(float2 position, float halfWidth, float segmentLength, int caps)
{
    int capStart = caps & 3;
    int capEnd = (caps >> 2) & 3;
    float startExtension = capStart == UNITE_LINE_CAP_PROJECT ? halfWidth : 0.0;
    float endExtension = capEnd == UNITE_LINE_CAP_PROJECT ? halfWidth : 0.0;
    float2 center = float2((segmentLength + endExtension - startExtension) * 0.5, 0.0);
    float2 halfExtent = float2(
        (segmentLength + startExtension + endExtension) * 0.5,
        halfWidth
    );
    float2 delta = abs(position - center) - halfExtent;
    float distance = length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0);

    if (capStart == UNITE_LINE_CAP_ROUND)
        distance = min(distance, length(position) - halfWidth);
    if (capEnd == UNITE_LINE_CAP_ROUND)
        distance = min(distance, length(position - float2(segmentLength, 0.0)) - halfWidth);

    return distance;
}

// Builds the camera facing quad that covers a stroked segment. The quad corners
// are one unit quad's uv, and the returned coordinates are expressed in the
// same space as startPosition and endPosition. Pixel-space positions are
// converted to clip space before they are returned.
UniteLineResult UniteBuildLine(
    float2 startPosition,
    float2 endPosition,
    float startDepth,
    float endDepth,
    float2 startEndHalfWidth,
    float2 uv,
    float padding,
    int caps,
    bool positionInPixels)
{
    UniteLineResult result;
    float2 delta = endPosition - startPosition;
    float lineLength = length(delta);
    float2 direction = lineLength > 1e-6 ? delta / lineLength : float2(1.0, 0.0);
    float2 normal = float2(-direction.y, direction.x);

    int capStart = caps & 3;
    int capEnd = (caps >> 2) & 3;
    float startExtension = capStart == UNITE_LINE_CAP_PROJECT ? startEndHalfWidth.x : 0.0;
    float endExtension = capEnd == UNITE_LINE_CAP_PROJECT ? startEndHalfWidth.y : 0.0;
    float cover = max(startEndHalfWidth.x, startEndHalfWidth.y) + padding;

    float t = uv.x;
    float side = uv.y * 2.0 - 1.0;
    float halfWidth = lerp(startEndHalfWidth.x, startEndHalfWidth.y, t);
    float along = lerp(-startExtension - cover, lineLength + endExtension + cover, t);
    float cross = side * (halfWidth + padding);
    float2 position = startPosition + direction * along + normal * cross;
    float2 clipPosition = positionInPixels
        ? position / _ScreenParams.xy * 2.0 - 1.0
        : position * 2.0 - 1.0;

    result.positionCS = float4(clipPosition, lerp(startDepth, endDepth, t), 1.0);
    result.lineCoords = float2(along, cross);
    result.halfWidth = halfWidth;
    result.lineLength = lineLength;
    return result;
}

// Clips a view-space segment against the camera's near plane. Returns false
// when both endpoints are behind the camera.
bool UniteClipLineToNear(inout float3 startView, inout float3 endView, float nearPlane)
{
    float startDistance = -startView.z;
    float endDistance = -endView.z;

    if (startDistance < nearPlane && endDistance < nearPlane)
        return false;

    if (startDistance < nearPlane)
    {
        float t = (nearPlane - startDistance) / (endDistance - startDistance);
        startView = startView + (endView - startView) * t;
    }
    else if (endDistance < nearPlane)
    {
        float t = (nearPlane - endDistance) / (startDistance - endDistance);
        endView = endView + (startView - endView) * t;
    }

    return true;
}

// Converts a view-space width into screen pixels. Perspective cameras scale the
// width with distance, while orthographic cameras keep it constant.
float UnitePixelScalePerViewUnit(float viewZ)
{
    float screenScale = abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
    return unity_OrthoParams.w > 0.5
        ? screenScale
        : screenScale / max(-viewZ, 1e-4);
}

// Converts a 0..1 linear depth to the platform's device depth.
float UniteLinearDepthToDeviceDepth(float linearDepth)
{
    float nearPlane = _ProjectionParams.y;
    float farPlane = _ProjectionParams.z;

    if (unity_OrthoParams.w < 0.5)
    {
        float eyeDepth = lerp(nearPlane, farPlane, linearDepth);
        return UNITY_REVERSED_Z
            ? (1.0 / eyeDepth - 1.0 / farPlane) / (1.0 / nearPlane - 1.0 / farPlane)
            : (1.0 / eyeDepth - 1.0 / nearPlane) / (1.0 / farPlane - 1.0 / nearPlane);
    }

    return UNITY_REVERSED_Z ? 1.0 - linearDepth : linearDepth;
}

float4 UniteCulledPosition()
{
    return float4(0.0, 0.0, 2.0, 1.0);
}

UniteVaryings UniteCulledVaryings(uint dataIndex)
{
    UniteVaryings output;
    output.positionCS = UniteCulledPosition();
    output.shapePosition = 0.0;
    output.instanceID = dataIndex;
    output.lineCoords = 0.0;
    output.halfWidth = 0.0;
    output.lineLength = 0.0;
    return output;
}

// Later instances are nudged toward the camera so that coplanar shapes keep
// the painter's (later wins) order when depth testing is enabled.
#define UNITE_DEPTH_BIAS 1e-6

float DepthBiasFactor(uint instanceIndex)
{
    float direction = UNITY_REVERSED_Z ? 1.0 : -1.0;
    return 1.0 + direction * instanceIndex * UNITE_DEPTH_BIAS;
}

// Builds a 3D line from two endpoints that are already in world space.
UniteVaryings UniteBuildWorldLine(
    float2 uv,
    ShapeData data,
    uint dataIndex,
    float3 startWorld,
    float3 endWorld)
{
    float3 startView = mul(UNITY_MATRIX_V, float4(startWorld, 1.0)).xyz;
    float3 endView = mul(UNITY_MATRIX_V, float4(endWorld, 1.0)).xyz;

    if (!UniteClipLineToNear(startView, endView, _ProjectionParams.y))
        return UniteCulledVaryings(dataIndex);

    float4 startClip = mul(UNITY_MATRIX_P, float4(startView, 1.0));
    float4 endClip = mul(UNITY_MATRIX_P, float4(endView, 1.0));
    // UniteBuildLine takes pixels from the bottom-left corner (0.._ScreenParams.xy).
    float2 startPixel = ((startClip.xy / startClip.w) * 0.5 + 0.5) * _ScreenParams.xy;
    float2 endPixel = ((endClip.xy / endClip.w) * 0.5 + 0.5) * _ScreenParams.xy;

    float bias = DepthBiasFactor(dataIndex);
    float2 depths = float2(startClip.z / startClip.w, endClip.z / endClip.w) * bias;
    float halfWidthWorld = max(data.Style.z, 0.0) * 0.5;
    float2 halfWidths = data.Parameters0.y != 0.0
        ? float2(
            halfWidthWorld * UnitePixelScalePerViewUnit(startView.z),
            halfWidthWorld * UnitePixelScalePerViewUnit(endView.z))
        : float2(halfWidthWorld, halfWidthWorld);

    UniteLineResult lineResult = UniteBuildLine(
        startPixel,
        endPixel,
        depths.x,
        depths.y,
        halfWidths,
        uv,
        1.5,
        (int)data.Parameters0.x,
        true
    );

    UniteVaryings output;
    output.positionCS = lineResult.positionCS;
    output.shapePosition = 0.0;
    output.instanceID = dataIndex;
    output.lineCoords = lineResult.lineCoords;
    output.halfWidth = lineResult.halfWidth;
    output.lineLength = lineResult.lineLength;
    return output;
}

// Builds a 3D line from the shape-local endpoints stored in the shape data.
UniteVaryings UniteBuildLocal3DLine(float2 uv, ShapeData data, uint dataIndex)
{
    float4 startH = TransformPoint3D(data.Point0.xyz, data);
    float4 endH = TransformPoint3D(data.Point1.xyz, data);
    float3 startWorld = float3(
        SafeDivide(startH.x, startH.w),
        SafeDivide(startH.y, startH.w),
        SafeDivide(startH.z, startH.w)
    );
    float3 endWorld = float3(
        SafeDivide(endH.x, endH.w),
        SafeDivide(endH.y, endH.w),
        SafeDivide(endH.z, endH.w)
    );

    return UniteBuildWorldLine(uv, data, dataIndex, startWorld, endWorld);
}

// Builds a 3D line in screen drawing space. The x/y coordinates are used as
// screen units and z is a 0..1 linear depth.
UniteVaryings UniteBuildPlanar3DLine(
    float2 uv,
    ShapeData data,
    uint dataIndex,
    bool pixelScreen,
    bool overlay)
{
    float4 startH = TransformPoint3D(data.Point0.xyz, data);
    float4 endH = TransformPoint3D(data.Point1.xyz, data);
    float3 start = float3(
        SafeDivide(startH.x, startH.w),
        SafeDivide(startH.y, startH.w),
        SafeDivide(startH.z, startH.w)
    );
    float3 end = float3(
        SafeDivide(endH.x, endH.w),
        SafeDivide(endH.y, endH.w),
        SafeDivide(endH.z, endH.w)
    );

    float2 depths = float2(0.0, 0.0);
    float bias = DepthBiasFactor(dataIndex);

    if (!overlay)
    {
        if (start.z < 0.0 && end.z < 0.0)
            return UniteCulledVaryings(dataIndex);

        depths = float2(
            UniteLinearDepthToDeviceDepth(saturate(start.z)) * bias,
            UniteLinearDepthToDeviceDepth(saturate(end.z)) * bias
        );
    }

    float halfWidth = max(data.Style.z, 0.0) * 0.5;
    float padding = pixelScreen
        ? 1.5
        : 1.5 / max(1.0, min(_ScreenParams.x, _ScreenParams.y));
    UniteLineResult lineResult = UniteBuildLine(
        start.xy,
        end.xy,
        depths.x,
        depths.y,
        float2(halfWidth, halfWidth),
        uv,
        padding,
        (int)data.Parameters0.x,
        pixelScreen
    );

    UniteVaryings output;
    output.positionCS = lineResult.positionCS;
    output.shapePosition = 0.0;
    output.instanceID = dataIndex;
    output.lineCoords = lineResult.lineCoords;
    output.halfWidth = lineResult.halfWidth;
    output.lineLength = lineResult.lineLength;
    return output;
}

#endif // UNITE_COMMON_HLSL
