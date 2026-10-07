// Đá phủ rêu: hai texture (đá, rêu) và một mặt nạ rêu, độ phủ rêu chỉnh được từ 0 đến 1 (rêu mọc dần theo ngày).
// Rêu bám trước ở mặt hướng lên và ở chỗ mặt nạ cao; độ phủ tăng thì loang dần ra khắp đá.
// Mặc định lấy mẫu texture theo 3 mặt phẳng nên mesh chưa có UV vẫn dùng được; mesh đã có UV thì bật Use UV.
Shader "Garden/RockMoss"
{
    Properties
    {
        [Header(Rock)]
        _RockTex ("Rock Albedo", 2D) = "white" {}
        _RockColor ("Rock Tint", Color) = (1, 1, 1, 1)
        _RockTiling ("Rock Tiling (lặp mỗi mét)", Float) = 0.35

        [Header(Moss)]
        _MossTex ("Moss Albedo", 2D) = "white" {}
        _MossColor ("Moss Tint (phần thấp)", Color) = (0.42, 0.7, 0.24, 1)
        _MossLightColor ("Moss Tint (phần cao, sáng hơn)", Color) = (0.62, 0.86, 0.32, 1)
        _MossTiling ("Moss Tiling (lặp mỗi mét)", Float) = 1.2
        _MossMask ("Moss Mask (R = mảng rêu, G = lông mịn ở mép)", 2D) = "white" {}
        _MaskTiling ("Mask Tiling (lặp mỗi mét)", Float) = 0.3
        _MossCoverage ("Moss Coverage (độ phủ rêu)", Range(0, 1)) = 0.6
        _MossUpBias ("Up Bias (rêu thích mặt hướng lên)", Range(0, 1)) = 0.55
        _MossEdgeSoftness ("Moss Edge Softness", Range(0.01, 0.3)) = 0.08
        _MossFuzz ("Moss Fuzzy Edge", Range(0, 0.6)) = 0.25

        [Header(Lighting)]
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.66, 0.82, 1)
        _VertexColorStrength ("Vertex Color Strength", Range(0, 1)) = 0.6
        _Bands ("Light Bands", Range(2, 6)) = 3
        _Softness ("Band Softness", Range(0.01, 0.5)) = 0.18
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.3
        _RimColor ("Rim Color", Color) = (1, 0.98, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.25

        [Header(Mapping)]
        [Toggle] _UseUV ("Use mesh UV (thay vì 3 mặt phẳng)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Assets/Shaders/GhibliToonCommon.hlsl"

            TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);
            TEXTURE2D(_MossTex); SAMPLER(sampler_MossTex);
            TEXTURE2D(_MossMask); SAMPLER(sampler_MossMask);

            CBUFFER_START(UnityPerMaterial)
                half4 _RockColor;
                half4 _MossColor;
                half4 _MossLightColor;
                half4 _ShadeColor;
                half4 _RimColor;
                float _RockTiling;
                float _MossTiling;
                float _MaskTiling;
                half _MossCoverage;
                half _MossUpBias;
                half _MossEdgeSoftness;
                half _MossFuzz;
                half _VertexColorStrength;
                half _Bands;
                half _Softness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _UseUV;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);

                // UV của mesh (nếu bật) hoặc 3 mặt phẳng theo toạ độ thế giới
                half4 rock, moss, mask;
                if (_UseUV > 0.5h)
                {
                    rock = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, input.uv * _RockTiling);
                    moss = SAMPLE_TEXTURE2D(_MossTex, sampler_MossTex, input.uv * _MossTiling);
                    mask = SAMPLE_TEXTURE2D(_MossMask, sampler_MossMask, input.uv * _MaskTiling);
                }
                else
                {
                    rock = SampleTriplanar(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), input.positionWS, normalWS, _RockTiling);
                    moss = SampleTriplanar(TEXTURE2D_ARGS(_MossTex, sampler_MossTex), input.positionWS, normalWS, _MossTiling);
                    mask = SampleTriplanar(TEXTURE2D_ARGS(_MossMask, sampler_MossMask), input.positionWS, normalWS, _MaskTiling);
                }

                // Điểm "dễ có rêu": mặt hướng lên và mảng rêu trong mặt nạ; lông mịn ở mép từ kênh G
                half upFacing = saturate(normalWS.y);
                half score = lerp(mask.r, upFacing, _MossUpBias);
                score += (mask.g - 0.5h) * _MossFuzz;

                // Độ phủ càng cao thì ngưỡng càng thấp, rêu loang ra: 0 = không có rêu, 1 = phủ kín
                half threshold = lerp(1.15h, -0.15h, _MossCoverage);
                half mossAmount = smoothstep(threshold - _MossEdgeSoftness, threshold + _MossEdgeSoftness, score);

                half3 rockColor = rock.rgb * _RockColor.rgb;
                half3 mossColor = moss.rgb * lerp(_MossColor.rgb, _MossLightColor.rgb, upFacing);
                half3 albedo = lerp(rockColor, mossColor, mossAmount);

                // Màu đỉnh (lưu ở nửa giá trị) để chỉnh sáng tối từng chỗ
                half3 vertexTint = lerp(half3(1, 1, 1), input.color.rgb * 2.0h, _VertexColorStrength);
                albedo *= vertexTint;

                // Rêu có viền sáng rõ hơn để trông xốp
                half rim = _RimStrength * (1.0h + mossAmount * 1.5h);
                half3 color = ToonLighting(albedo, _ShadeColor.rgb, normalWS, input.positionWS,
                    _Bands, _Softness, _AmbientStrength, _RimColor.rgb, _RimPower, rim);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
