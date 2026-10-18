// Dev only: shows a single-channel glyph atlas page as gray (the R8 texture would
// otherwise render as red), dimmed by _Brightness for spare pages. Used by
// CanvasTester's atlas viewer.
Shader "Hidden/Dev/AtlasPreview"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 1)) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed _Brightness;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                fixed value = tex2D(_MainTex, input.uv).r * _Brightness;
                return fixed4(value, value, value, 1);
            }
            ENDCG
        }
    }
}
