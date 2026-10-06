Shader "Hidden/Unite/CanvasLocalDrawing"
{
    Properties
    {
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
            #include "Assets/Unite/Shaders/UniteCommon.hlsl"
            #include "Assets/Unite/Shaders/SDFs.hlsl"

            StructuredBuffer<ShapeData> _ShapeData;
            int _ShapeIndex;
            int _UseShapeIndex;
            int _DebugShapeBounds;

            CBUFFER_START(UnityPerMaterial)
                float4x4 _ParentTransform;
            CBUFFER_END

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

                if ((int)data.Style.x == 7)
                {
                    float4 startH = TransformPoint3D(data.Point0.xyz, data);
                    float4 endH = TransformPoint3D(data.Point1.xyz, data);
                    float3 startLocal = float3(
                        SafeDivide(startH.x, startH.w),
                        SafeDivide(startH.y, startH.w),
                        SafeDivide(startH.z, startH.w)
                    );
                    float3 endLocal = float3(
                        SafeDivide(endH.x, endH.w),
                        SafeDivide(endH.y, endH.w),
                        SafeDivide(endH.z, endH.w)
                    );
                    float3 startWorld = mul(_ParentTransform, float4(startLocal, 1.0)).xyz;
                    float3 endWorld = mul(_ParentTransform, float4(endLocal, 1.0)).xyz;
                    return UniteBuildWorldLine(
                        input.uv,
                        data,
                        dataIndex,
                        startWorld,
                        endWorld
                    );
                }

                Varyings output;
                float2 shapePosition = ShapePosition(input.uv, data);
                float4 positionLocal = DrawingPosition(shapePosition, data);
                float4 positionWS = mul(_ParentTransform, positionLocal);

                output.positionCS = mul(UNITY_MATRIX_VP, positionWS);
                output.positionCS.z *= DepthBiasFactor(dataIndex);
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
