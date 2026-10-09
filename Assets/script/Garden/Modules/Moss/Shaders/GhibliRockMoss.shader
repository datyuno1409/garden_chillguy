// Đá phủ rêu: hai texture (đá, rêu) và một mặt nạ rêu, độ phủ rêu chỉnh được từ 0 đến 1 (rêu mọc dần theo ngày).
// Rêu bám trước ở mặt hướng lên và ở chỗ mặt nạ cao; độ phủ tăng thì loang dần ra khắp đá.
// Rêu dày lên khỏi mặt đá, quanh mép rêu đá sẫm màu như bị ẩm, mép rêu hơi tối để có khối.
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
        _MossColor ("Moss Tint (phần thấp)", Color) = (0.34, 0.58, 0.2, 1)
        _MossLightColor ("Moss Tint (phần cao, sáng hơn)", Color) = (0.5, 0.74, 0.27, 1)
        _MossTiling ("Moss Tiling (lặp mỗi mét)", Float) = 1.2
        _MossMask ("Moss Mask (R = mảng rêu, G = lông mịn ở mép)", 2D) = "white" {}
        _MaskTiling ("Mask Tiling (lặp mỗi mét)", Float) = 0.3
        _MossCoverage ("Moss Coverage (độ phủ rêu)", Range(0, 1)) = 0.6
        _MossSeed ("Moss Seed (mỗi vườn một kiểu loang)", Float) = 0
        _MossUpBias ("Up Bias (rêu thích mặt hướng lên)", Range(0, 1)) = 0.55
        _MossEdgeSoftness ("Moss Edge Softness", Range(0.01, 0.3)) = 0.08
        _MossFuzz ("Moss Fuzzy Edge", Range(0, 0.6)) = 0.25
        _MossThickness ("Moss Thickness (mét, rêu dày lên khỏi mặt đá)", Range(0, 0.15)) = 0.04
        _WetHalo ("Wet Halo (đá sẫm quanh mép rêu)", Range(0, 1)) = 0.35
        _MossDamageable ("Damageable (1 = click bóc được rêu, 0 = không)", Range(0, 1)) = 1

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
                float _MossSeed;
                half _MossCoverage;
                half _MossUpBias;
                half _MossEdgeSoftness;
                half _MossFuzz;
                half _MossThickness;
                half _WetHalo;
                half _MossDamageable;
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

            // Danh sách vết rêu bị bóc, dùng chung cho mọi vật liệu rêu (đặt từ MossGrowth.SetHits).
            // xyz = vị trí thế giới, w = bán kính. Kích thước mảng phải khớp MossDamage.Capacity.
            #define MOSS_MAX_HITS 16
            float4 _MossHits[MOSS_MAX_HITS];
            float _MossHitStrengths[MOSS_MAX_HITS];
            float _MossHitCount;

            // Dịch vị trí lấy mẫu mặt nạ theo hạt giống để mỗi vườn có các mảng rêu khác nhau
            float3 SeedShift() { return float3(_MossSeed * 3.7, _MossSeed * 1.3, _MossSeed * 2.9); }

            // Mức rêu bị bóc tại một điểm (0..1): mạnh nhất ở giữa vết, giảm mềm dần ra mép
            half MossDamageAt(float3 positionWS)
            {
                half damage = 0.0h;
                int count = min((int)_MossHitCount, MOSS_MAX_HITS);
                [loop] for (int i = 0; i < count; i++)
                {
                    float radius = max(_MossHits[i].w, 1e-4);
                    float dist = distance(positionWS, _MossHits[i].xyz);
                    half falloff = 1.0h - smoothstep(radius * 0.4, radius, dist);
                    damage = max(damage, falloff * (half)_MossHitStrengths[i]);
                }
                return damage * _MossDamageable;
            }

            // Điểm "dễ có rêu" (mặt hướng lên, mảng rêu trong mặt nạ, lông mịn ở mép) so với ngưỡng theo độ phủ.
            // Trả về lượng rêu 0..1 và, qua score/threshold, khoảng cách tới mép rêu.
            // Chỗ bị bóc thì điểm bị trừ đi nhiều, nên rêu lùi ra và mép vẫn xù tự nhiên.
            half MossAmount(half4 mask, half3 normalWS, float3 positionWS, out half score, out half threshold)
            {
                half upFacing = saturate(normalWS.y);
                score = lerp(mask.r, upFacing, _MossUpBias);
                score += (mask.g - 0.5h) * _MossFuzz;
                score -= MossDamageAt(positionWS) * 1.6h;

                // Độ phủ càng cao thì ngưỡng càng thấp, rêu loang ra: 0 = không có rêu, 1 = phủ kín.
                // Lấy luỹ thừa 0.6 để độ phủ nhỏ đã thấy vài đốm rêu (ngày đầu), còn độ phủ cao thì loang chậm lại.
                half shaped = pow(saturate(_MossCoverage), 0.6h);
                threshold = lerp(1.05h, -0.15h, shaped);
                return smoothstep(threshold - _MossEdgeSoftness, threshold + _MossEdgeSoftness, score);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Rêu dày lên khỏi mặt đá: đẩy đỉnh ra theo pháp tuyến ở chỗ có rêu
                half4 mask = _UseUV > 0.5h
                    ? SAMPLE_TEXTURE2D_LOD(_MossMask, sampler_MossMask, (input.uv + _MossSeed * 0.137) * _MaskTiling, 0)
                    : SampleTriplanarLod(TEXTURE2D_ARGS(_MossMask, sampler_MossMask), positionWS + SeedShift(), normalWS, _MaskTiling);
                half score, threshold;
                half moss = MossAmount(mask, normalWS, positionWS, score, threshold);
                float3 displaced = positionWS + normalWS * (moss * _MossThickness);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(displaced);
                output.normalWS = normalWS;
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
                    mask = SAMPLE_TEXTURE2D(_MossMask, sampler_MossMask, (input.uv + _MossSeed * 0.137) * _MaskTiling);
                }
                else
                {
                    rock = SampleTriplanar(TEXTURE2D_ARGS(_RockTex, sampler_RockTex), input.positionWS, normalWS, _RockTiling);
                    moss = SampleTriplanar(TEXTURE2D_ARGS(_MossTex, sampler_MossTex), input.positionWS, normalWS, _MossTiling);
                    mask = SampleTriplanar(TEXTURE2D_ARGS(_MossMask, sampler_MossMask), input.positionWS + SeedShift(), normalWS, _MaskTiling);
                }

                half score, threshold;
                half mossAmount = MossAmount(mask, normalWS, input.positionWS, score, threshold);
                half upFacing = saturate(normalWS.y);

                // Đá sẫm lại ở vùng sát mép rêu, như chỗ đá bị ẩm do rêu giữ nước
                half3 rockColor = rock.rgb * _RockColor.rgb;
                half nearMoss = smoothstep(threshold - 0.3h, threshold, score) * (1.0h - mossAmount);
                rockColor *= 1.0h - nearMoss * _WetHalo * 0.5h;

                // Rêu sáng ở phần cao, mép rêu tối nhẹ để trông có khối
                half3 mossColor = moss.rgb * lerp(_MossColor.rgb, _MossLightColor.rgb, upFacing);
                mossColor *= lerp(0.8h, 1.0h, smoothstep(0.0h, 0.6h, mossAmount));

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
