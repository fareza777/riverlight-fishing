// Expanding ring ripple for fishing spots (draw on a quad, per-instance _Phase offsets the cycle).
Shader "CozyFishing/Ripple"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.75)
        _Speed ("Cycle Speed", Float) = 0.45
        _Width ("Ring Width", Range(0.01, 0.2)) = 0.06
        _Phase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; float _Speed; float _Width; float _Phase;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 Frag(V i) : SV_Target
            {
                // Rings grow to 80% of the quad and fade out before reaching its edge, so they are never clipped.
                float r = length(i.uv - 0.5) * 2;
                float cycle = frac(_Time.y * _Speed + _Phase);
                float radius = cycle * 0.8;
                float ring = 1 - smoothstep(0, _Width, abs(r - radius));
                float inner = 1 - smoothstep(0, _Width * 0.6, abs(r - radius * 0.6));
                float fade = (1 - cycle) * smoothstep(0, 0.1, cycle);
                float edge = 1 - smoothstep(0.82, 0.98, r);
                float a = saturate(ring + inner * 0.5) * fade * edge * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
