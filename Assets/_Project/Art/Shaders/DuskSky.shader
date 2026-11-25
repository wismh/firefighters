// Cartoon dusk: indigo overhead through violet to an orange band at the horizon, warmest around the sun that has
// just touched it, with the first stars out on the dark side.
Shader "Project/Dusk Sky"
{
    Properties
    {
        _ZenithColor("Zenith", Color) = (0.1, 0.12, 0.33, 1)
        _MiddleColor("Middle", Color) = (0.36, 0.24, 0.5, 1)
        _HorizonColor("Horizon", Color) = (0.78, 0.4, 0.42, 1)
        _GroundColor("Below the horizon", Color) = (0.3, 0.2, 0.3, 1)
        [HDR] _GlowColor("Glow around the sun", Color) = (1.25, 0.55, 0.2, 1)
        [HDR] _SunColor("Sun disk", Color) = (2.4, 1.3, 0.6, 1)
        _SunSize("Sun size, degrees", Float) = 2.2
        _Stars("Star density", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _MiddleColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _GlowColor;
                half4 _SunColor;
                float _SunSize;
                float _Stars;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.direction = positionOS.xyz;
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float3 sun = normalize(_MainLightPosition.xyz);
                float height = direction.y;

                half3 color = lerp(_HorizonColor.rgb, _MiddleColor.rgb, smoothstep(0.0, 0.22, height));
                color = lerp(color, _ZenithColor.rgb, smoothstep(0.15, 0.75, height));

                // The glow hugs the horizon on the sun's side of the sky.
                float towardSun = dot(normalize(direction.xz + 1e-4), normalize(sun.xz)) * 0.5 + 0.5;
                float low = exp(-abs(height) * 7.0);
                color += _GlowColor.rgb * (pow(towardSun, 4.0) * low * 0.75);
                float angle = degrees(acos(clamp(dot(direction, sun), -1.0, 1.0)));
                color += _GlowColor.rgb * (exp(-angle * 0.12) * 0.5);
                color = lerp(color, _SunColor.rgb, 1.0 - smoothstep(_SunSize * 0.9, _SunSize, angle));

                float3 cell = floor(direction * 90.0);
                float3 offset = frac(direction * 90.0) - 0.5;
                float present = step(1.0 - _Stars * 0.06, Hash(cell));
                float spark = 1.0 - smoothstep(0.08, 0.2, length(offset));
                float dark = smoothstep(0.25, 0.65, height) * (1.0 - towardSun * 0.6);
                color += present * spark * dark * lerp(0.3, 1.0, Hash(cell + 7.0));

                color = lerp(_GroundColor.rgb, color, smoothstep(-0.06, 0.0, height));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
