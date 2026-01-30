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
                float4 positionCS : SV_POSITION;
                float4 positionCSRaw : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;

                // ObjectToWorld contains Object to Clip(like) transformation matrix
                o.positionCS = mul(GetObjectToWorldMatrix(), i.positionOS);

                // set positionCSRaw before apply NDC adjustment
                // positionCSRaw much looks like view coordinate.
                // x and y is clip space coord, but z is normalized distance from camera, between nearClip and farClip.
                o.positionCS.z = saturate(o.positionCS.z);
                o.positionCSRaw = o.positionCS;

                // ~~absorb Reverse-Z. currentry, positionCS.z is 0 to 1.~~
                // edit: it doesn't seem to we have to fix reverse thing; everything looks fine now.
                // edit: actually _ZBufferParams absorbed Reverse-Z

                // currently, z01 is linear depth, 0-1, near-far.
                // convert it into non-linear NDC depth if camera is persp
                if (unity_OrthoParams.w < .5)
                {
                    float zNdc = (1.0 / max(1e-6, o.positionCS.z) - _ZBufferParams.y) / _ZBufferParams.x;
                    o.positionCS.z = zNdc;
                }else
                {
                    // TODO: I'm using this gpt-generated stuff with completely unaware of what's going on here
                    // edit: and it doesn't seem working properly...
                    float zNdc = o.positionCS.z * _ZBufferParams.x + _ZBufferParams.y;
                    o.positionCS.z = zNdc;
                }

                // because we calculated non-linear NDC depth manually
                o.positionCS.w = 1.;
                
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

                return half4(i.positionCSRaw.z, 0., 0., 1.);
            }
            
            ENDHLSL
        }
    }
}