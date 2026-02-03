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
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD1;
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
                
                float3 pos = data.Position;
                float2 size = data.Size;
                float4 rot = data.Rotation;

                // Scale
                // Mesh is -1 to 1 (width 2).
                // If Size represents full width, we need to scale by size/2.
                // Or if we pre-calculated the correct scale in C#, just use it.
                // In C# LayoutWorldSpaceShape: Vector2.one * radius * s;
                // If we pass 'size' directly, and we want 'size' to be the width.
                // Mesh width is 2. Scale = size / 2.
                // However, C# previously did: new Vector3(cmd.Size.x / 2, cmd.Size.y / 2, 1f)
                // So I will assume the data.Size passed here is the "Scale Factor" already prepared by C#?
                // No, better to pass raw Size and let Shader handle, or consistant.
                // I will update C# to pass the "Scale Vector" that should be applied to the Unit Quad.
                // Unit Quad is size 2.
                // So if I want size S, Scale = S/2.
                // But let's assume C# passes the *Model Scale*.
                
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

                float3 positionWS = mul(m, i.positionOS).xyz;

                o.positionOS = i.positionOS.xyz;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                
                o.shapeType = data.ShapeType;
                o.fillColor = data.FillColor;
                o.strokeColor = data.StrokeColor;
                
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
                
                half4 color = i.fillColor * baseColor;
                
                return half4(color.rgb, color.a * alpha);
            }

            ENDHLSL
        }
    }
}
