// Phần tính ánh sáng toon dùng chung cho các shader Garden/*: ánh sáng chia dải mềm, bóng ngả màu lạnh, viền sáng nhẹ.
#ifndef GHIBLI_TOON_COMMON_INCLUDED
#define GHIBLI_TOON_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Biến độ sáng liên tục thành các dải, mép mỗi dải được làm mềm
half Banded(half x, half bands, half softness)
{
    half scaled = x * bands;
    half band = floor(scaled);
    half edge = smoothstep(0.5h - softness, 0.5h + softness, frac(scaled));
    return saturate((band + edge) / bands);
}

half3 ToonLighting(half3 albedo, half3 shadeTint, half3 normalWS, float3 positionWS,
    half bands, half softness, half ambientStrength, half3 rimColor, half rimPower, half rimStrength)
{
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half halfLambert = dot(normalWS, mainLight.direction) * 0.5h + 0.5h;
    half lit = Banded(halfLambert * mainLight.shadowAttenuation, bands, softness);

    half3 shade = albedo * shadeTint;
    half3 lightTint = lerp(half3(1, 1, 1), mainLight.color, 0.6h);

    half3 color = lerp(shade, albedo * lightTint, lit);
    color += albedo * SampleSH(normalWS) * ambientStrength;

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), rimPower) * rimStrength;
    color += rimColor * rim * saturate(halfLambert);
    return color;
}

// Lấy mẫu texture theo 3 mặt phẳng (XY, XZ, ZY) theo toạ độ thế giới: dùng được cho mesh chưa có UV.
half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS, float tiling)
{
    half3 weights = pow(abs(normalWS), 4.0h);
    weights /= (weights.x + weights.y + weights.z + 1e-4h);

    half4 fromX = SAMPLE_TEXTURE2D(tex, samp, positionWS.zy * tiling);
    half4 fromY = SAMPLE_TEXTURE2D(tex, samp, positionWS.xz * tiling);
    half4 fromZ = SAMPLE_TEXTURE2D(tex, samp, positionWS.xy * tiling);
    return fromX * weights.x + fromY * weights.y + fromZ * weights.z;
}

// Bản dùng được trong vertex shader (không có đạo hàm nên chọn mức mip 0)
half4 SampleTriplanarLod(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS, float tiling)
{
    half3 weights = pow(abs(normalWS), 4.0h);
    weights /= (weights.x + weights.y + weights.z + 1e-4h);

    half4 fromX = SAMPLE_TEXTURE2D_LOD(tex, samp, positionWS.zy * tiling, 0);
    half4 fromY = SAMPLE_TEXTURE2D_LOD(tex, samp, positionWS.xz * tiling, 0);
    half4 fromZ = SAMPLE_TEXTURE2D_LOD(tex, samp, positionWS.xy * tiling, 0);
    return fromX * weights.x + fromY * weights.y + fromZ * weights.z;
}

#endif
