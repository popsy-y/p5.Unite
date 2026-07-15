Shader "Hidden/Unite/WorldSpaceDrawing"
{
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
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Unite/Shaders/UniteCommon.hlsl"
            #include "Assets/Unite/Shaders/SDFs.hlsl"

            StructuredBuffer<ShapeData> _ShapeData;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 shapePosition : TEXCOORD0;
                nointerpolation uint instanceID : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                ShapeData data = _ShapeData[input.instanceID];
                float2 shapePosition = ShapePosition(input.uv, data);
                float3 positionWS = DrawingPosition(shapePosition, data);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.shapePosition = shapePosition;
                output.instanceID = input.instanceID;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ShapeData data = _ShapeData[input.instanceID];
                return ComputeShapeColor(input.shapePosition, data);
            }

            ENDHLSL
        }
    }
}
