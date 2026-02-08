Shader "Hidden/Unite/WorldSpaceDrawing"
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
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD1;
                nointerpolation int shapeType : TEXCOORD3;
                nointerpolation float4 fillColor : TEXCOORD4;
                nointerpolation float4 strokeColor : TEXCOORD5;
                nointerpolation float strokeWeight : TEXCOORD6;
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
                
                float3 pos = data.Position;
                float2 size = data.Size;
                float4 rot = data.Rotation;

                float4x4 m = BuildTRSMatrix(pos, rot, size);

                float3 positionWS = mul(m, i.positionOS).xyz;

                o.positionOS = i.positionOS.xyz;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                
                o.shapeType = data.ShapeType;
                o.fillColor = data.FillColor;
                o.strokeColor = data.StrokeColor;
                o.strokeWeight = data.StrokeWeight;
                
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return ComputeShapeColor(i.uv, i.shapeType, i.strokeWeight,
                                          i.fillColor, i.strokeColor, baseColor);
            }

            ENDHLSL
        }
    }
}
