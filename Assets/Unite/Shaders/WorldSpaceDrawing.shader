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

            #pragma enable_d3d11_debug_symbols

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Unite/Shaders/SDFs.hlsl"

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

                o.positionOS = i.positionOS;
                o.positionCS = TransformObjectToHClip(i.positionOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = Square(i.uv);
                float alpha = SDF_FillAlpha(d);
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                return half4(baseColor.rgb, baseColor.a * alpha);
            }

            ENDHLSL
        }
    }
}