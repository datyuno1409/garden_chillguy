// Vệt mưa: nét mảnh mờ hai đầu, trong suốt, không đổ bóng. Dùng cho hạt mưa kéo dài theo hướng rơi (Stretched Billboard).
Shader "Garden/RainStreak"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.92, 1, 0.55)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half across = 1.0h - abs(input.uv.x * 2.0h - 1.0h);   // mờ dần ra hai bên mép nét
                half along = sin(saturate(input.uv.y) * 3.14159h);    // mờ dần ở hai đầu nét
                half4 tint = _Color * input.color;
                return half4(tint.rgb, tint.a * across * along);
            }
            ENDHLSL
        }
    }
}
