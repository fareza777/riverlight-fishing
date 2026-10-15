// Instanced flower head: an upward-facing quad cut into a five-petal blossom with a coloured centre.
Shader "CozyFishing/StylizedFlower"
{
    Properties
    {
        _Petal ("Petal", Color) = (0.98, 0.96, 0.92, 1)
        _PetalEdge ("Petal Edge", Color) = (0.92, 0.86, 0.95, 1)
        _Centre ("Centre", Color) = (0.98, 0.78, 0.25, 1)
        _Petals ("Petal Count", Float) = 5
        _WindStrength ("Wind Strength", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Petal, _PetalEdge, _Centre;
                float _Petals, _WindStrength;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; float fog : TEXCOORD2; float seed : TEXCOORD3; };

            V Vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float t = _Time.y * 1.3;
                ws.xz += float2(sin(t + root.x * 0.7), cos(t * 0.8 + root.z * 0.5)) * _WindStrength;
                o.positionWS = ws;
                o.uv = i.uv;
                o.seed = frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453);
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float r = length(d) * 2;
                float a = atan2(d.y, d.x) + i.seed * 6.2831;
                float petal = 0.62 + 0.38 * abs(cos(a * _Petals * 0.5));
                clip(petal - r);
                half3 col = lerp(_Petal.rgb, _PetalEdge.rgb, smoothstep(0.3, 1.0, r / petal));
                col = lerp(_Centre.rgb, col, smoothstep(0.2, 0.3, r));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lit = col * (sun.color * lerp(0.5, 1.0, sun.shadowAttenuation) * 0.85 + SampleSH(float3(0, 1, 0)) * 0.6);
                return half4(MixFog(lit, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
