// Full-screen UI overlay for a player standing in a fire: cartoon flames licking in from the edges, an orange
// glare, and soot that stays in the corners for a while after.
Shader "Project/Screen Fire"
{
    Properties
    {
        [PerRendererData] _MainTex("Unused", 2D) = "white" {}
        _Heat("Heat", Range(0, 1)) = 0
        _Soot("Soot", Range(0, 1)) = 0
        _RimColor("Flame rim", Color) = (0.85, 0.12, 0.02, 1)
        _BodyColor("Flame body", Color) = (1, 0.45, 0.05, 1)
        _CoreColor("Flame core", Color) = (1, 0.86, 0.3, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            float _Heat;
            float _Soot;
            fixed4 _RimColor;
            fixed4 _BodyColor;
            fixed4 _CoreColor;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                return Noise(p) * 0.55 + Noise(p * 2.1 + 7.0) * 0.3 + Noise(p * 4.3 + 19.0) * 0.15;
            }

            fixed4 Over(fixed4 below, fixed3 color, float alpha)
            {
                float a = alpha + below.a * (1.0 - alpha);
                fixed3 rgb = (color * alpha + below.rgb * below.a * (1.0 - alpha)) / max(a, 0.0001);
                return fixed4(rgb, a);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2(uv.x * aspect, uv.y);
                float t = _Time.y;

                // How deep in from the edge: the bottom burns tallest, the top only singes.
                float side = min(uv.x, 1.0 - uv.x) * aspect * 1.9;
                float depth = min(min(uv.y, side), (1.0 - uv.y) * 2.6);

                float waver = Fbm(float2(p.x * 4.0, p.y * 2.2 - t * 0.9)) - 0.5;
                float tongues = Fbm(float2(p.x * 9.0 + waver * 1.6, p.y * 2.6 - t * 2.2));
                float reach = _Heat * (0.44 * smoothstep(0.2, 0.9, tongues) + 0.03);
                float inside = reach - depth;

                fixed4 color = fixed4(1.0, 0.4, 0.05, _Heat * 0.1);
                float corner = smoothstep(0.35, 1.0, length((uv - 0.5) * float2(1.5, 1.5)) + (Fbm(p * 3.0) - 0.5) * 0.6);
                color = Over(color, fixed3(0.03, 0.025, 0.02), corner * _Soot * 0.95);

                // Hard-edged bands, like the pack's flames.
                float soft = 0.012;
                color = Over(color, _RimColor.rgb, smoothstep(0.0, soft, inside) * 0.7);
                color = Over(color, _BodyColor.rgb, smoothstep(0.05, 0.05 + soft, inside) * 0.75);
                color = Over(color, _CoreColor.rgb, smoothstep(0.14, 0.14 + soft, inside) * 0.8);
                return color;
            }
            ENDCG
        }
    }
}
