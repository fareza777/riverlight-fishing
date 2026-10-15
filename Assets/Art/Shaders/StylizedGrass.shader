// Instanced grass blades (Graphics.RenderMeshInstanced). Wind sway grows toward the tip (uv.y),
// colour runs from a shaded root to a sunlit tip, and each clump picks up a hue shift from world-space noise.
Shader "CozyFishing/StylizedGrass"
{
    Properties
    {
        _Root ("Root Colour", Color) = (0.17, 0.28, 0.13, 1)
        _Tip ("Tip Colour", Color) = (0.50, 0.63, 0.29, 1)
        _TipAlt ("Tip Alternate", Color) = (0.66, 0.64, 0.34, 1)
        _WindStrength ("Wind Strength", Float) = 0.12
        _WindSpeed ("Wind Speed", Float) = 1.3
        _WindScale ("Wind Gust Scale", Float) = 0.12
        _PushRadius ("Player Push Radius", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+5" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Root, _Tip, _TipAlt;
            float _WindStrength, _WindSpeed, _WindScale, _PushRadius;
        CBUFFER_END
        float4 _GrassPushers[4]; // xyz = position, w = active

        float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float VNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
        }

        float3 Bend(float3 ws, float3 rootWS, float k)
        {
            float t = _Time.y * _WindSpeed;
            float gust = VNoise(rootWS.xz * _WindScale + float2(t * 0.6, t * 0.35));
            float2 dir = normalize(float2(1, 0.45));
            float sway = (sin(t * 2.1 + rootWS.x * 0.7 + rootWS.z * 0.4) * 0.35 + gust) * _WindStrength;
            float k2 = k * k;
            ws.xz += dir * sway * k2;
            // Grass parts around the player / boat.
            for (int p = 0; p < 4; p++)
            {
                if (_GrassPushers[p].w <= 0) continue;
                float2 d = rootWS.xz - _GrassPushers[p].xz;
                float dist = length(d);
                float push = saturate(1 - dist / _PushRadius);
                ws.xz += (d / max(dist, 1e-3)) * push * 0.35 * k2;
                ws.y -= push * 0.15 * k2;
            }
            ws.y -= length(ws.xz - rootWS.xz) * 0.25 * k;
            return ws;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float3 rootWS : TEXCOORD2; float fog : TEXCOORD3; };

            V Vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 ws = Bend(TransformObjectToWorld(i.positionOS.xyz), rootWS, i.uv.y);
                o.positionWS = ws;
                o.rootWS = rootWS;
                o.uv = i.uv;
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float hue = VNoise(i.rootWS.xz * 0.35);
                half3 tip = lerp(_Tip.rgb, _TipAlt.rgb, smoothstep(0.55, 0.85, hue));
                half3 albedo = lerp(_Root.rgb, tip, smoothstep(0.0, 1.0, i.uv.y));
                albedo *= lerp(0.9, 1.08, VNoise(i.rootWS.xz * 3.1));

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float shade = lerp(0.45, 1.0, sun.shadowAttenuation);
                half3 lit = albedo * (sun.color * (0.55 + 0.45 * i.uv.y) * shade + SampleSH(float3(0, 1, 0)) * 0.7);
                // Subtle translucency glow on the tips when the sun is low.
                lit += _Tip.rgb * sun.color * 0.15 * i.uv.y * saturate(1 - sun.direction.y) * shade;
                return half4(MixFog(lit, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
