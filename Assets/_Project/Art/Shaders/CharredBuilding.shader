// A building that is burning or has burnt: URP Lit with the fire's damage painted in world space around every
// flame. Outwards from a flame: charcoal split by glowing cracks, a brown scorched rim, soot. Soot is drawn out
// upwards, the way smoke licks a wall. The glow follows the flame's strength; the damage itself stays.
Shader "Project/Charred Building"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.1

        [Header(Damage)]
        _CharRadius("Charcoal radius, m", Float) = 2.1
        _SootRadius("Soot radius, m", Float) = 4.6
        _Rise("Upward stretch", Range(1, 4)) = 2.2
        _EdgeNoise("Ragged edge, m", Float) = 1.3
        _Haze("Soot all over the building", Range(0, 1)) = 0.3
        _CharColor("Charcoal", Color) = (0.035, 0.032, 0.03, 1)
        _CharEdgeColor("Charcoal highlight", Color) = (0.13, 0.12, 0.115, 1)
        _ScorchColor("Scorched rim", Color) = (0.24, 0.12, 0.05, 1)
        _SootColor("Soot", Color) = (0.09, 0.08, 0.075, 1)
        _AshColor("Ash", Color) = (0.42, 0.41, 0.4, 1)
        _CrackScale("Cracks per metre", Float) = 3.2
        [HDR] _EmberColor("Ember glow", Color) = (2.2, 0.5, 0.07, 1)

        [HideInInspector] _Cutoff("Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_FLAMES 32

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _CharColor;
                half4 _CharEdgeColor;
                half4 _ScorchColor;
                half4 _SootColor;
                half4 _AshColor;
                half4 _EmberColor;
                float _Smoothness;
                float _CharRadius;
                float _SootRadius;
                float _Rise;
                float _EdgeNoise;
                float _Haze;
                float _CrackScale;
            CBUFFER_END

            // Set by BuildingFire: world position, and in w how hot the flame still is, 0..1.
            float4 _Flames[MAX_FLAMES];
            float _FlameCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float3 Hash3(float3 p)
            {
                return float3(Hash(p), Hash(p + 31.7), Hash(p + 73.3));
            }

            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                         lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                         lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.3 + 11.0) * 0.3 + Noise(p * 5.1 + 29.0) * 0.15;
            }

            // Cracked-wood cells: x is the distance to the nearest crack, y a random value per cell.
            float2 Cracks(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                float nearest = 8.0;
                float second = 8.0;
                float id = 0.0;
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int z = -1; z <= 1; z++)
                        {
                            float3 o = float3(x, y, z);
                            float3 d = o + Hash3(cell + o) - f;
                            float distance = dot(d, d);
                            if (distance < nearest)
                            {
                                second = nearest;
                                nearest = distance;
                                id = Hash(cell + o + 5.0);
                            }
                            else
                            {
                                second = min(second, distance);
                            }
                        }
                    }
                }

                return float2(sqrt(second) - sqrt(nearest), id);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float3 normal = normalize(input.normalWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                // Distance to the closest flame, shorter above a flame than beside it, and the heat around here.
                float closest = 1000.0;
                float heat = 0.0;
                int count = (int)_FlameCount;
                for (int i = 0; i < count; i++)
                {
                    float3 offset = p - _Flames[i].xyz;
                    float3 stretched = offset;
                    stretched.y = offset.y > 0.0 ? offset.y / _Rise : offset.y * 1.4;
                    closest = min(closest, length(stretched));
                    float reach = saturate(1.0 - length(stretched) / (_CharRadius * 1.3));
                    heat = max(heat, reach * _Flames[i].w);
                }

                float ragged = (Fbm(p * 0.9) - 0.5) * 2.0 * _EdgeNoise + (Noise(p * 6.0) - 0.5) * 0.25;
                // Soot runs in vertical streaks.
                float streak = Noise(float3(p.x * 2.6, p.y * 0.25, p.z * 2.6));
                float burnt = closest + ragged;
                float sooty = closest + ragged * 0.8 + (streak - 0.5) * 2.2;

                float charred = 1.0 - smoothstep(_CharRadius - 0.12, _CharRadius + 0.12, burnt);
                float scorched = 1.0 - smoothstep(_CharRadius + 0.1, _CharRadius + 0.75, burnt);
                float soot = 1.0 - smoothstep(_CharRadius * 0.8, _SootRadius, sooty);
                float haze = _Haze * (0.55 + 0.9 * (Fbm(p * 0.45 + 40.0) - 0.2));

                float2 crack = Cracks(p * _CrackScale * float3(1.0, 0.75, 1.0) + Noise(p * 2.0) * 0.35);
                float split = 1.0 - smoothstep(0.03, 0.1, crack.x);

                half3 coal = lerp(_CharColor.rgb, _CharEdgeColor.rgb, crack.y * crack.y);
                coal *= lerp(0.8, 1.25, Noise(p * 9.0));
                // Ash settles on ledges and roofs and in flecks over the coal.
                float flecks = smoothstep(0.66, 0.74, Noise(p * 7.0 + 3.0)) * 0.35;
                float ash = saturate(smoothstep(0.35, 0.85, normal.y) * smoothstep(0.42, 0.62, Fbm(p * 1.6 + 7.0)) +
                                     flecks);
                coal = lerp(coal, _AshColor.rgb, ash * 0.75);
                coal *= 1.0 - split * 0.85;

                albedo = lerp(albedo, _SootColor.rgb, saturate(haze));
                albedo = lerp(albedo, _SootColor.rgb, soot * 0.88);
                albedo = lerp(albedo, _ScorchColor.rgb * lerp(0.7, 1.2, streak), scorched * 0.8);
                albedo = lerp(albedo, coal, charred);

                float breathing = 0.65 + 0.35 * sin(_Time.y * 2.3 + crack.y * 25.0);
                float hotCell = smoothstep(0.47, 0.68, Fbm(p * 1.1 + _Time.y * 0.06));
                float glowing = 1.0 - smoothstep(0.015, 0.07, crack.x);
                half3 emission = _EmberColor.rgb * (glowing * charred * heat * hotCell * breathing);

                InputData inputData = (InputData)0;
                inputData.positionWS = p;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(p);
                inputData.shadowCoord = TransformWorldToShadowCoord(p);
                inputData.fogCoord = InitializeInputDataFog(float4(p, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1.0;
                surface.smoothness = lerp(_Smoothness, 0.04, saturate(charred + soot));
                surface.occlusion = 1.0;
                surface.emission = emission;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    Fallback "Universal Render Pipeline/Lit"
}
