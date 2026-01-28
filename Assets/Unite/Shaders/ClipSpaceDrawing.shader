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

            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;

                // Assume ObjectToWorld contains Object to Clip transformation matrix
                o.positionCS = mul(GetObjectToWorldMatrix(), i.positionOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.positionOS = i.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float within = length(i.positionOS.xy) < 1.;
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half4 col = half4(within, within, within, baseColor.a * within);

                float edge = (abs(i.positionOS.x) - .9 > 0. || abs(i.positionOS.y) - .9 > 0.) ? 1.0 : 0.0;
                
                // return half4(col.r + edge, col.g, col.b, col.a + edge);
                // return half4(IN.uv.xy, 0, 1);
                return half4(1., 1., 1., 1.);
            }
            
            ENDHLSL
        }
    }
}