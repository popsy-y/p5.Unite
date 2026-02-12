Shader "Hidden/Unite/ClipSpaceDrawing"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        LOD 100

        Pass
        {
            Name "Draw"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Unite/Shaders/SDFs.hlsl"
            #include "Assets/Unite/Shaders/UniteCommon.hlsl"

            StructuredBuffer<ShapeData> _ShapeData;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float4 positionCSRaw : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float3 positionOS    : TEXCOORD2;
                nointerpolation int    shapeType    : TEXCOORD3;
                nointerpolation float4 fillColor    : TEXCOORD4;
                nointerpolation float4 strokeColor  : TEXCOORD5;
                nointerpolation float2 strokeWeight : TEXCOORD6;
                nointerpolation float2 shapeSize    : TEXCOORD7;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;

                ShapeData data = _ShapeData[i.instanceID];
                float4x4 m = BuildTRSMatrix(data.Position, data.Rotation, data.Size);

                o.positionCS = mul(m, i.positionOS);
                o.positionCSRaw = o.positionCS;

                // Convert linear depth [0,1] to non-linear NDC depth for perspective cameras
                if (unity_OrthoParams.w < .5)
                {
                    float nearOverFar = _ProjectionParams.y / _ProjectionParams.z;
                    float z01 = max(o.positionCS.z, nearOverFar + 1e-5);
                    float zNdc = (1.0 / z01 - _ZBufferParams.y) / _ZBufferParams.x;
                    o.positionCS.z = zNdc;

                    // Discard vertices behind the camera
                    if (o.positionCSRaw.z < 0.0)
                        o.positionCS = float4(0, 0, 2, 1);
                }
                else
                {
                    o.positionCS.z = UNITY_REVERSED_Z ? 1. - o.positionCS.z : o.positionCS.z;
                }

                o.positionCS.w = 1.;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.positionOS = i.positionOS.xyz;

                o.shapeType    = data.ShapeType;
                o.fillColor    = data.FillColor;
                o.strokeColor  = data.StrokeColor;
                o.strokeWeight = data.StrokeWeight;
                o.shapeSize    = data.Size;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return ComputeShapeColor(i.uv, i.shapeType, i.strokeWeight,
                                          i.shapeSize, i.fillColor, i.strokeColor, baseColor);
            }

            ENDHLSL
        }
    }
}
