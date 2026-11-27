// URP Lit for SimpleTown buildings in the evening. Window panes are found by the one colour the pack paints
// them, on walls only, and some of the rooms behind them have the light on: each room is a cell of the wall.
Shader "Project/Night Building"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.1

        [Header(Windows)]
        _PaneGrey("Pane grey in the texture, 0..1 sRGB", Float) = 0.2
        _PaneTolerance("Tolerance", Float) = 0.014
        _LitShare("Share of rooms with the light on", Range(0, 1)) = 0.55
        _RoomWidth("Room width, m", Float) = 3.4
        _RoomHeight("Room height, m", Float) = 3.6
        [HDR] _WarmColor("Lamp light", Color) = (1.15, 0.56, 0.17, 1)
        [HDR] _CoolColor("TV light", Color) = (0.22, 0.5, 0.95, 1)
        _CoolShare("Share of lit rooms with a TV", Range(0, 1)) = 0.14

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _WarmColor;
                half4 _CoolColor;
                float _Smoothness;
                float _PaneGrey;
                float _PaneTolerance;
                float _LitShare;
                float _RoomWidth;
                float _RoomHeight;
                float _CoolShare;
            CBUFFER_END

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
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half3 WindowLight(float3 p, float3 normal, half3 texel)
            {
                half3 srgb = LinearToSRGB(texel);
                half3 off = abs(srgb - _PaneGrey);
                float pane = 1.0 - smoothstep(_PaneTolerance, _PaneTolerance * 2.0, max(off.r, max(off.g, off.b)));
                float wall = 1.0 - smoothstep(0.25, 0.35, abs(normal.y));
                float mask = pane * wall;

                // Along the wall, and which of its sides it is.
                bool facesX = abs(normal.x) > abs(normal.z);
                float along = facesX ? p.z : p.x;
                float depth = facesX ? p.x : p.z;
                float side = floor(dot(normal, float3(2.0, 0.0, 1.0)) + 0.5);
                float3 room = float3(floor(along / _RoomWidth), floor(p.y / _RoomHeight),
                                     side * 7.0 + floor(depth / 12.0));
                float on = step(Hash(room + 17.0), _LitShare);
                float isTv = step(Hash(room + 91.0), _CoolShare);
                float brightness = lerp(0.45, 1.0, Hash(room + 53.0));
                float flicker = 0.75 + 0.25 * sin(_Time.y * 7.0 + Hash(room) * 40.0) * sin(_Time.y * 2.7);
                // Light falls off towards the ceiling, so a pane is not a flat card.
                float fall = lerp(1.1, 0.75, frac(p.y / _RoomHeight));
                half3 light = lerp(_WarmColor.rgb, _CoolColor.rgb * flicker, isTv);
                return light * (mask * on * brightness * fall);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                float3 normal = normalize(input.normalWS);
                half3 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

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
                surface.albedo = texel * _BaseColor.rgb;
                surface.alpha = 1.0;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1.0;
                surface.emission = WindowLight(p, normal, texel);

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
