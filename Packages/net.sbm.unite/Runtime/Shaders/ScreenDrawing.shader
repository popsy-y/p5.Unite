Shader "Hidden/Unite/ScreenDrawing"
{
    Properties
    {
        _DrawingMode ("Drawing Mode", Int) = 0
        _SrcBlend ("Src Blend", Float) = 1
        _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("ZWrite", Float) = 0
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "Draw"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_local _ _UNITE_DEPTH_PREPASS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/net.sbm.unite/Runtime/Shaders/UniteCommon.hlsl"
            #include "Packages/net.sbm.unite/Runtime/Shaders/SDFs.hlsl"

            StructuredBuffer<ShapeData> _ShapeData;
            int _DrawingMode;
            int _ShapeIndex;
            int _UseShapeIndex;
            int _DebugShapeBounds;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            typedef UniteVaryings Varyings;

            Varyings vert(Attributes input)
            {
                uint dataIndex = _UseShapeIndex != 0
                    ? (uint)_ShapeIndex
                    : input.instanceID;
                ShapeData data = _ShapeData[dataIndex];
                bool isPixelScreen = _DrawingMode == 0 || _DrawingMode == 1;
                bool isOverlay = _DrawingMode == 0 || _DrawingMode == 2;

                if ((int)data.Style.x == 7)
                {
                    return UniteBuildPlanar3DLine(
                        input.uv,
                        data,
                        dataIndex,
                        isPixelScreen,
                        isOverlay
                    );
                }

                Varyings output;
                float2 shapePosition = ShapePosition(input.uv, data);
                float4 drawingPositionH = DrawingPosition(shapePosition, data);
                float safeW = abs(drawingPositionH.w) > 1e-6
                    ? drawingPositionH.w
                    : (drawingPositionH.w < 0.0 ? -1e-6 : 1e-6);
                float3 drawingPosition = drawingPositionH.xyz / safeW;

                float2 screenPosition = isPixelScreen
                    ? drawingPosition.xy / _ScreenParams.xy
                    : drawingPosition.xy;

                float anchorSafeW = abs(data.Transform3.w) > 1e-6
                    ? data.Transform3.w
                    : (data.Transform3.w < 0.0 ? -1e-6 : 1e-6);
                float anchorDepth = data.Transform3.z / anchorSafeW;
                float depthBias = DepthBiasFactor(dataIndex);

                output.positionCS = float4(screenPosition * 2.0 - 1.0,
                                           isOverlay ? 0.0 : anchorDepth, 1.0);

                if (!isOverlay)
                {
                    if (anchorDepth < 0.0)
                    {
                        output.positionCS = UniteCulledPosition();
                    }
                    else
                    {
                        output.positionCS.z =
                            UniteLinearDepthToDeviceDepth(saturate(anchorDepth)) * depthBias;
                    }
                }

                output.shapePosition = shapePosition;
                output.instanceID = dataIndex;
                output.lineCoords = 0.0;
                output.halfWidth = 0.0;
                output.lineLength = 0.0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ShapeData data = _ShapeData[input.instanceID];
                half4 color;
                if (_DebugShapeBounds != 0)
                {
                    half debugAlpha = max(data.FillColor.a, 0.35);
                    color = half4(data.FillColor.rgb * debugAlpha, debugAlpha);
                }
                else if ((int)data.Style.x == 7)
                {
                    color = ComputeLineColor(
                        input.lineCoords,
                        input.halfWidth,
                        input.lineLength,
                        data
                    );
                }
                else
                {
                    color = ComputeShapeColor(input.shapePosition, data);
                }

                #if defined(_UNITE_DEPTH_PREPASS)
                // Depth prepass: write depth only where the shape is visible.
                // Line strokes overlap at every joint, so they never write depth.
                // They are still depth-tested against shapes and scene geometry,
                // but overlapping segments blend together as one stroke.
                if ((int)data.Style.x == 7)
                    clip(-1);
                clip(color.a - 0.004);
                #endif

                return color;
            }

            ENDHLSL
        }
    }
}
