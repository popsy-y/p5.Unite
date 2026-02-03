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

            struct ShapeData
            {
                float3 Position;
                float4 Rotation; // Quaternion
                float2 Size;
                int ShapeType;
                float4 FillColor;
                float4 StrokeColor;
            };

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
                float4 positionCSRaw : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
                nointerpolation int shapeType : TEXCOORD3;
                nointerpolation float4 fillColor : TEXCOORD4;
                nointerpolation float4 strokeColor : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            float4x4 QuaternionToMatrix(float4 q)
            {
                float x = q.x, y = q.y, z = q.z, w = q.w;
                return float4x4(
                    1 - 2*y*y - 2*z*z, 2*x*y - 2*z*w,     2*x*z + 2*y*w,     0,
                    2*x*y + 2*z*w,     1 - 2*x*x - 2*z*z, 2*y*z - 2*x*w,     0,
                    2*x*z - 2*y*w,     2*y*z + 2*x*w,     1 - 2*x*x - 2*y*y, 0,
                    0,                 0,                 0,                 1
                );
            }

            Varyings vert(Attributes i)
            {
                Varyings o;

                ShapeData data = _ShapeData[i.instanceID];
                
                // Construct Clip Space Matrix
                // Data comes in pre-transformed to Clip Space logic (mostly)
                // But rotation is quaternion.
                
                float3 pos = data.Position;
                float2 size = data.Size;
                float4 rot = data.Rotation;

                // Scale
                // Mesh is -1 to 1.
                float3 s = float3(size.x, size.y, 1.0);
                
                // Rotation
                float4x4 rMat = QuaternionToMatrix(rot);
                
                // Translation
                float4x4 tMat = float4x4(
                    1, 0, 0, pos.x,
                    0, 1, 0, pos.y,
                    0, 0, 1, pos.z,
                    0, 0, 0, 1
                );
                
                float4x4 sMat = float4x4(
                    s.x, 0, 0, 0,
                    0, s.y, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );

                float4x4 m = mul(tMat, mul(rMat, sMat));

                // Apply transformation
                o.positionCS = mul(m, i.positionOS);
                
                // --- Clip Space Logic from previous shader ---

                // set positionCSRaw before apply NDC adjustment
                o.positionCS.z = saturate(o.positionCS.z);
                o.positionCSRaw = o.positionCS;

                // currently, z01 is linear depth, 0-1, near-far.
                // convert it into non-linear NDC depth if camera is persp
                if (unity_OrthoParams.w < .5)
                {
                    float zNdc = (1.0 / max(1e-6, o.positionCS.z) - _ZBufferParams.y) / _ZBufferParams.x;
                    o.positionCS.z = zNdc;
                }
                else
                {
                    o.positionCS.z = UNITY_REVERSED_Z ? 1. - o.positionCS.z : o.positionCS.z;
                }

                o.positionCS.w = 1.;

                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.positionOS = i.positionOS.xyz;
                
                o.shapeType = data.ShapeType;
                o.fillColor = data.FillColor;
                o.strokeColor = data.StrokeColor;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = 0;
                // ShapeType: 0=Circle, 1=Rectangle, 2=Square
                // Note: Rectangle and Square use same SDF logic (box)
                // Circle = 1, Rectangle = 3, Square = 4 in Shape enum?
                // Let's check Shape enum.
                // Circle=1, Rectangle=3, Square=4.
                
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
                
                // Use Fill Color
                // TODO: Stroke implementation (not requested in detail but good to have)
                // Using FillColor for now.
                half4 color = i.fillColor * baseColor;
                
                return half4(color.rgb, color.a * alpha);
            }
            
            ENDHLSL
        }
    }
}
