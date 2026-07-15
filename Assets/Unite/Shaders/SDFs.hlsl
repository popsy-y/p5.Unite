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

float SdSegment(float2 position, float2 start, float2 end)
{
    float2 segment = end - start;
    float2 relativePosition = position - start;
    float projection = saturate(dot(relativePosition, segment) / max(dot(segment, segment), 1e-12));
    return length(relativePosition - segment * projection);
}

float SdTriangle(float2 position, ShapeData data)
{
    float2 points[3] = { data.Point0.xy, data.Point1.xy, data.Point2.xy };
    float minimumDistanceSquared = dot(position - points[0], position - points[0]);
    float signValue = 1.0;

    [unroll]
    for (int index = 0, previous = 2; index < 3; previous = index, index++)
    {
        float2 edge = points[previous] - points[index];
        float2 relativePosition = position - points[index];
        float2 nearest = relativePosition - edge * saturate(
            dot(relativePosition, edge) / max(dot(edge, edge), 1e-12)
        );
        minimumDistanceSquared = min(minimumDistanceSquared, dot(nearest, nearest));

        bool3 crossings = bool3(
            position.y >= points[index].y,
            position.y < points[previous].y,
            edge.x * relativePosition.y > edge.y * relativePosition.x
        );
        if (all(crossings) || all(!crossings))
            signValue = -signValue;
    }

    return signValue * sqrt(max(minimumDistanceSquared, 0.0));
}

float SdQuad(float2 position, ShapeData data)
{
    float2 points[4] = { data.Point0.xy, data.Point1.xy, data.Point2.xy, data.Point3.xy };
    float minimumDistanceSquared = dot(position - points[0], position - points[0]);
    float signValue = 1.0;

    [unroll]
    for (int index = 0, previous = 3; index < 4; previous = index, index++)
    {
        float2 edge = points[previous] - points[index];
        float2 relativePosition = position - points[index];
        float2 nearest = relativePosition - edge * saturate(
            dot(relativePosition, edge) / max(dot(edge, edge), 1e-12)
        );
        minimumDistanceSquared = min(minimumDistanceSquared, dot(nearest, nearest));

        bool3 crossings = bool3(
            position.y >= points[index].y,
            position.y < points[previous].y,
            edge.x * relativePosition.y > edge.y * relativePosition.x
        );
        if (all(crossings) || all(!crossings))
            signValue = -signValue;
    }

    return signValue * sqrt(max(minimumDistanceSquared, 0.0));
}

float PositiveModulo(float value, float divisor)
{
    return value - divisor * floor(value / divisor);
}

float EllipseParameterAngle(float2 position, float2 radius)
{
    radius = max(abs(radius), 1e-6);
    return PositiveModulo(atan2(-position.y / radius.y, position.x / radius.x), TWO_PI);
}

bool AngleIsInSweep(float angle, float start, float sweep)
{
    return PositiveModulo(angle - start, TWO_PI) <= sweep + 1e-5;
}

float2 EllipsePoint(float2 radius, float angle)
{
    return float2(radius.x * cos(angle), -radius.y * sin(angle));
}

float SdfAlpha(float distance)
{
    float antialiasing = max(fwidth(distance), 1e-6);
    return smoothstep(antialiasing, -antialiasing, distance);
}

void ComputeArcAlphas(float2 position, ShapeData data, out float fillAlpha, out float strokeAlpha)
{
    int styleFlags = (int)data.Style.w;
    int arcMode = (int)data.Style.y;
    float2 radius = max(abs(data.ShapeParameters.xy * 0.5), 1e-6);
    float start = data.Parameters0.x;
    float sweep = data.Parameters0.y;
    float ellipseDistance = SdEllipse(position, radius);

    if (sweep >= TWO_PI - 1e-5)
    {
        fillAlpha = (styleFlags & 1) != 0 ? SdfAlpha(ellipseDistance) : 0.0;
        float strokeDistance = abs(ellipseDistance) - data.Style.z * 0.5;
        strokeAlpha = (styleFlags & 2) != 0 ? SdfAlpha(strokeDistance) : 0.0;
        return;
    }

    float end = start + sweep;
    float2 startPoint = EllipsePoint(radius, start);
    float2 endPoint = EllipsePoint(radius, end);
    float parameterAngle = EllipseParameterAngle(position, radius);
    bool onArcSide = AngleIsInSweep(parameterAngle, start, sweep);
    float curveDistance = onArcSide
        ? abs(ellipseDistance)
        : min(length(position - startPoint), length(position - endPoint));

    float fillDistance;
    if (arcMode == 2)
    {
        float radialDistance = min(
            SdSegment(position, float2(0.0, 0.0), startPoint),
            SdSegment(position, float2(0.0, 0.0), endPoint)
        );
        float wedgeDistance = onArcSide ? -radialDistance : radialDistance;
        fillDistance = max(ellipseDistance, wedgeDistance);
    }
    else
    {
        float2 chord = endPoint - startPoint;
        float2 midpoint = EllipsePoint(radius, start + sweep * 0.5);
        float midpointSide = sign(chord.x * (midpoint.y - startPoint.y) -
                                  chord.y * (midpoint.x - startPoint.x));
        midpointSide = midpointSide == 0.0 ? 1.0 : midpointSide;
        float chordDistance = -midpointSide *
            (chord.x * (position.y - startPoint.y) -
             chord.y * (position.x - startPoint.x)) / max(length(chord), 1e-6);
        fillDistance = max(ellipseDistance, chordDistance);
    }

    float pathDistance = curveDistance;
    if (arcMode == 1)
    {
        pathDistance = min(pathDistance, SdSegment(position, startPoint, endPoint));
    }
    else if (arcMode == 2)
    {
        pathDistance = min(
            pathDistance,
            min(
                SdSegment(position, float2(0.0, 0.0), startPoint),
                SdSegment(position, float2(0.0, 0.0), endPoint)
            )
        );
    }

    fillAlpha = (styleFlags & 1) != 0 ? SdfAlpha(fillDistance) : 0.0;
    float strokeDistance = pathDistance - data.Style.z * 0.5;
    strokeAlpha = (styleFlags & 2) != 0 ? SdfAlpha(strokeDistance) : 0.0;
}

half4 CompositeShapeAlphas(float fillAlpha, float strokeAlpha, ShapeData data)
{
    half4 fill = data.FillColor;
    fill.a *= fillAlpha;

    half4 stroke = data.StrokeColor;
    stroke.a *= strokeAlpha;

    half4 result;
    result.rgb = lerp(fill.rgb, stroke.rgb, stroke.a);
    result.a = stroke.a + fill.a * (1.0 - stroke.a);
    return result;
}

half4 CompositeShape(float distance, ShapeData data)
{
    int styleFlags = (int)data.Style.w;
    float fillAlpha = (styleFlags & 1) != 0 ? SdfAlpha(distance) : 0.0;
    float strokeDistance = abs(distance) - data.Style.z * 0.5;
    float strokeAlpha = (styleFlags & 2) != 0 ? SdfAlpha(strokeDistance) : 0.0;
    return CompositeShapeAlphas(fillAlpha, strokeAlpha, data);
}

half4 ComputeShapeColor(float2 shapePosition, ShapeData data)
{
    float2 halfSize = data.ShapeParameters.xy * 0.5;
    int shapeType = (int)data.Style.x;

    if (shapeType == 2 || shapeType == 3)
    {
        float distance = shapeType == 2
            ? SdSegment(shapePosition, data.Point0.xy, data.Point1.xy)
            : length(shapePosition - data.Point0.xy);
        int styleFlags = (int)data.Style.w;
        float strokeDistance = distance - data.Style.z * 0.5;
        float strokeAlpha = (styleFlags & 2) != 0 ? SdfAlpha(strokeDistance) : 0.0;
        return CompositeShapeAlphas(0.0, strokeAlpha, data);
    }

    if (shapeType == 6)
    {
        float fillAlpha;
        float strokeAlpha;
        ComputeArcAlphas(shapePosition, data, fillAlpha, strokeAlpha);
        return CompositeShapeAlphas(fillAlpha, strokeAlpha, data);
    }

    float distance;
    if (shapeType == 0)
        distance = SdEllipse(shapePosition, halfSize);
    else if (shapeType == 1)
        distance = SdRoundedBox(shapePosition, halfSize, data);
    else if (shapeType == 4)
        distance = SdTriangle(shapePosition, data);
    else
        distance = SdQuad(shapePosition, data);

    return CompositeShape(distance, data);
}

#endif
