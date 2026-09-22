Shader "Hidden/Unite/CanvasLocalDrawing"
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
            Blend One OneMinusSrcAlpha
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 shapePosition : TEXCOORD0;
                nointerpolation uint instanceID : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                uint dataIndex = _UseShapeIndex != 0
                    ? (uint)_ShapeIndex
                    : input.instanceID;
                ShapeData data = _ShapeData[dataIndex];
                float2 shapePosition = ShapePosition(input.uv, data);
                float4 positionLocal = DrawingPosition(shapePosition, data);
                float4 positionWS = mul(_ParentTransform, positionLocal);

                output.positionCS = mul(UNITY_MATRIX_VP, positionWS);
                output.shapePosition = shapePosition;
                output.instanceID = dataIndex;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                ShapeData data = _ShapeData[input.instanceID];
                if (_DebugShapeBounds != 0)
                {
                    half debugAlpha = max(data.FillColor.a, 0.35);
                    return half4(data.FillColor.rgb * debugAlpha, debugAlpha);
                }
                return ComputeShapeColor(input.shapePosition, data);
            }

            ENDHLSL
        }
    }
}
