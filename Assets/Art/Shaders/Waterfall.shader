// Stylized waterfall: streaks scrolling down the UV v-axis, frothy lip and base, soft side edges.
Shader "CozyFishing/Waterfall"
{
    Properties
    {
        _WaterColor ("Water Color", Color) = (0.42, 0.78, 0.82, 1)
        _StreakColor ("Streak Color", Color) = (0.86, 0.96, 1, 1)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _Speed ("Fall Speed", Float) = 1.6
        _StreakDensity ("Streak Density", Float) = 9
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.12
        _Opacity ("Opacity", Range(0, 1)) = 0.92
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _StreakColor, _FoamColor;
                float _Speed, _StreakDensity, _EdgeSoftness, _Opacity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float fog : TEXCOORD2; };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                // subtle wobble so the sheet never looks rigid
                ws.x += sin(_Time.y * 3 + v.uv.y * 12) * 0.03 * (1 - v.uv.y);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv;
                float fall = 1 - uv.y;                       // 0 at lip, 1 at base
                float streaks = Noise(float2(uv.x * _StreakDensity, uv.y * 2.5 + t));
                streaks = smoothstep(0.55, 0.85, streaks + Noise(float2(uv.x * _StreakDensity * 2.7, uv.y * 4 + t * 1.4)) * 0.35);

                half3 col = lerp(_WaterColor.rgb, _StreakColor.rgb, streaks);
                float lipFoam = smoothstep(0.12, 0.0, fall + (Noise(float2(uv.x * 20, t)) - 0.5) * 0.06);
                float baseFoam = smoothstep(0.78, 1.0, fall + (Noise(float2(uv.x * 14, uv.y * 10 + t * 2)) - 0.5) * 0.25);
                col = lerp(col, _FoamColor.rgb, saturate(lipFoam + baseFoam));

                Light sun = GetMainLight();
                col *= lerp(0.8, 1.1, saturate(sun.direction.y)) * sun.color;
                col += SampleSH(float3(0, 1, 0)) * 0.15;

                float edge = smoothstep(0, _EdgeSoftness, uv.x) * smoothstep(0, _EdgeSoftness, 1 - uv.x);
                float alpha = _Opacity * edge * lerp(0.8, 1, streaks);
                col = MixFog(col, i.fog);
                return half4(col, saturate(alpha + baseFoam * 0.3));
            }
            ENDHLSL
        }
    }
}
