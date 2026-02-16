Shader "Hidden/Unite/ClipSpaceDrawing"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _DrawingMode ("Drawing Mode", Int) = 0 // 0: Screen, 1: Clip
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

            int _DrawingMode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            // struct Varyings
            // {
            //     float4 positionCS : SV_POSITION;
            //     float4 positionCSRaw : TEXCOORD0;
            //     float2 uv : TEXCOORD1;
            //     float3 positionOS : TEXCOORD2;
            //     nointerpolation int shapeType : TEXCOORD3;
            //     nointerpolation float4 fillColor : TEXCOORD4;
            //     nointerpolation float4 strokeColor : TEXCOORD5;
            //     nointerpolation float strokeWeight : TEXCOORD6;
            // };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 shapePositionDS : TEXCOORD0;// Position in %DrawingMode% space
                float2 uv : TEXCOORD1;
                nointerpolation int shapeType : TEXCOORD2;
                nointerpolation float4 fillColor : TEXCOORD3;
                nointerpolation float4 strokeColor : TEXCOORD4;
                nointerpolation float strokeWeight : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
            CBUFFER_END

            // TODO: シェーダー側で常にUniformな座標でSDFの計算を行うなら、Sizeの入力が正規化されたものではいけなくて(別に戻せるっちゃ戻せるが)
            // そうなるとシェーダー側でで正規化処理も行うから、現在の描画モードも渡す必要があり、さらにそうなるとストロークもUniformな座標でシェーダー側で計算した方がいいよね
            // そっちの方が並列化効くしな　よし、改修するぞー

            Varyings vert(Attributes i)
            {
                Varyings o;

                ShapeData data = _ShapeData[i.instanceID];
                
                // Build TRS matrix using shared function
                float3 pos;
                float2 size;
                if (_DrawingMode == 0 || _DrawingMode == 1)
                {
                    pos.xy = data.Position.xy / _ScreenParams.xy;
                    pos.z = data.Position.z;
                    size = data.Size / _ScreenParams.xy;
                }
                else
                {
                    pos = data.Position;
                    size = data.Size;
                }
                float4x4 m = BuildTRSMatrix(pos, data.Rotation, size);
                // float4x4 m = BuildTRSMatrix(data.Position, data.Rotation, data.Size);

                // Apply transformation
                o.positionCS = mul(m, i.positionOS);

                o.shapePositionDS = data.Position;
                
                // ∘⋆․⊹․∘⟡˖*⊹∘⋆․⊹․∘⟡˖*⊹
                //   DEPTH ADJUSTMENT
                // ∘⋆․⊹․∘⟡˖*⊹∘⋆․⊹․∘⟡˖*⊹
                
                // set positionCSRaw before apply NDC adjustment
                float objZ = o.positionCS;

                // currently, z01 is linear depth, 0-1, near-far.
                // convert it into non-linear NDC depth if camera is persp
                if (unity_OrthoParams.w < .5)
                {
                    // clamp to avoid precision issues around near plane
                    float nearOverFar = _ProjectionParams.y / _ProjectionParams.z;
                    float z01 = max(o.positionCS.z, nearOverFar + 1e-5);
                    float zNdc = (1.0 / z01 - _ZBufferParams.y) / _ZBufferParams.x;
                    o.positionCS.z = zNdc;

                    // discard vertices with negative depth (z < 0 should not be rendered)
                    if (objZ < 0.0)
                        o.positionCS = float4(0, 0, 2, 1);  // behind far plane, invisible
                }
                else
                {
                    o.positionCS.z = UNITY_REVERSED_Z ? 1. - o.positionCS.z : o.positionCS.z;
                }

                o.positionCS.w = 1.;

                

                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                
                o.shapeType = data.ShapeType;
                o.fillColor = data.FillColor;
                o.strokeColor = data.StrokeColor;
                o.strokeWeight = data.StrokeWidth;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // NOTE: SDFはCircle(pixPosDS, shapePosDS, sizeParams)で呼ぼう！

                float2 fragCoordDS;
                if (_DrawingMode == 0 || _DrawingMode == 1)
                {
                    fragCoordDS = floor(i.positionCS.xy * _ScreenParams.xy);
                }
                else if (_DrawingMode == 2 || _DrawingMode == 3)
                {
                    fragCoordDS = i.positionCS.xy;
                }
                else
                {
                    // unsupported DrawingMode
                    fragCoordDS = float2(0., 0.);
                }
                
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

                d = length(fragCoordDS.xy - i.shapePositionDS.xy);

                float alpha = SDF_FillAlpha(d);
                // half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                
                // Use Fill Color
                // TODO: Stroke implementation (not requested in detail but good to have)
                // Using FillColor for now.
                half4 color = i.fillColor;

                color = d > .5 ? half4(1., 1., 1., 1.) : half4(0., 0., 0., 1.);
                
                // return half4(color.rgb, color.a * alpha);
                // シャ いみわかんね
                return half4(color.rgb, color.a);
            }
            
            ENDHLSL
        }
    }
}
