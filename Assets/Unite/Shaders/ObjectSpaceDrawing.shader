Shader "Hidden/Unite/ObjectSpaceDrawing"
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
                float4x4 _ParentTransform;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;

                ShapeData data = _ShapeData[i.instanceID];
                
                float3 pos = data.Position;
                float2 size = data.Size;
                float4 rot = data.Rotation;

                // Build local transform matrix
                float4x4 m = BuildTRSMatrix(pos, rot, size);

                // Get position in local space
                float3 positionLocal = mul(m, i.positionOS).xyz;
                
                // Transform to world space using parent transform
                float3 positionWS = mul(_ParentTransform, float4(positionLocal, 1.0)).xyz;

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
                float d = 0;
                
                if (i.shapeType == 0) // Circle
                {
                    d = Circle(i.uv);
                }
                else // Rectangle or Square
                {
                    d = Square(i.uv);
                }

                float alpha = SDF_FillAlpha(d);
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                
                // Fill color with alpha
                half4 fillColor = i.fillColor * baseColor;
                fillColor.a *= alpha;
                
                // Stroke: d is in [0, 0.5] range in UV space
                // StrokeWeight is already converted to appropriate coordinate space
                float strokeThreshold = i.strokeWeight;
                float strokeAlpha = 0.0;
                if (d > 0.0 && d < strokeThreshold)
                {
                    strokeAlpha = 1.0;
                }
                
                half4 strokeColor = i.strokeColor * baseColor;
                strokeColor.a *= strokeAlpha;
                
                // Composite: stroke behind fill
                half4 result;
                result.rgb = lerp(strokeColor.rgb, fillColor.rgb, fillColor.a);
                result.a = saturate(fillColor.a + strokeColor.a * (1.0 - fillColor.a));
                
                return result;
            }

            ENDHLSL
        }
    }
}
