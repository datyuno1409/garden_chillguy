// Shader toon kiểu Ghibli cho URP: ánh sáng chia dải mềm, bóng ngả màu lạnh, viền sáng nhẹ, gió lay lá.
// Màu đỉnh (vertex color): rgb nhân vào màu gốc để tạo biến thiên (lưu ở nửa giá trị, 0.5 = giữ nguyên),
// alpha là mức lay động theo gió.
Shader "Garden/GhibliToon"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.5, 0.7, 0.3, 1)
        _ShadeColor ("Shade Tint (nhân vào vùng tối)", Color) = (0.6, 0.65, 0.8, 1)
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 1
        _Bands ("Light Bands", Range(2, 6)) = 3
        _Softness ("Band Softness", Range(0.01, 0.5)) = 0.15
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.25
        _RimColor ("Rim Color", Color) = (1, 0.98, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25
        _WindStrength ("Wind Strength", Float) = 0
        _WindSpeed ("Wind Speed", Float) = 1.3
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Assets/Shaders/GhibliToonCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _RimColor;
                half _VertexColorStrength;
                half _Bands;
                half _Softness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _WindStrength;
                half _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // Gió: lay theo sóng chạy ngang qua cảnh, mạnh dần về phía ngọn (alpha của vertex color)
                float sway = input.color.a * _WindStrength;
                float phase = _Time.y * _WindSpeed + positionWS.x * 0.6 + positionWS.z * 0.4;
                positionWS.x += sin(phase) * sway;
                positionWS.z += cos(phase * 0.8) * sway * 0.5;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 vertexTint = lerp(half3(1, 1, 1), input.color.rgb * 2.0h, _VertexColorStrength);
                half3 albedo = _BaseColor.rgb * vertexTint;

                half3 color = ToonLighting(albedo, _ShadeColor.rgb, normalize(input.normalWS), input.positionWS,
                    _Bands, _Softness, _AmbientStrength, _RimColor.rgb, _RimPower, _RimStrength);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Đổ bóng và độ sâu dùng lại của shader Lit chuẩn
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
