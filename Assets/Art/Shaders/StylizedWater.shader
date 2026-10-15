// Stylized-realistic river water for URP.
// Depth-based absorption, screen-space refraction, procedural ripples,
// shoreline foam, sky reflection with fresnel, sun glints and gentle vertex waves.
// Requires Depth Texture + Opaque Texture enabled on the URP asset.
Shader "CozyFishing/StylizedWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.36, 0.78, 0.74, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.27, 0.36, 1)
        _AbsorptionDepth ("Absorption Depth (m)", Range(0.1, 10)) = 2.2
        _Clarity ("Clarity", Range(0, 1)) = 0.75

        [Header(Surface)]
        _RippleScale ("Ripple Scale", Float) = 0.9
        _RippleStrength ("Ripple Normal Strength", Range(0, 2)) = 0.55
        _FlowDirection ("Flow Direction (xz)", Vector) = (0, -1, 0, 0)
        _FlowSpeed ("Flow Speed", Float) = 0.35
        _RefractionStrength ("Refraction Strength", Range(0, 0.15)) = 0.04

        [Header(Waves)]
        _WaveAmplitude ("Wave Amplitude", Float) = 0.035
        _WaveLength ("Wave Length", Float) = 4.0
        _WaveSpeed ("Wave Speed", Float) = 0.8

        [Header(Lighting)]
        _SkyColor ("Sky Reflection Tint", Color) = (0.78, 0.9, 0.98, 1)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 4.5
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.65
        _SpecularPower ("Sun Specular Power", Range(16, 2048)) = 600
        _SpecularIntensity ("Sun Specular Intensity", Range(0, 10)) = 3

        [Header(Foam)]
        _FoamColor ("Foam Color", Color) = (0.96, 0.98, 1, 1)
        _FoamDistance ("Shore Foam Distance", Range(0, 2)) = 0.22
        _FoamNoiseScale ("Foam Noise Scale", Float) = 3.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            // No depth write: ripples, bubbles and the waterfall must never be hidden by wave crests.
            ZWrite Off
            Cull Back
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ CF_GFX_LOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _FlowDirection;
                float _AbsorptionDepth, _Clarity, _RippleScale, _RippleStrength, _FlowSpeed, _RefractionStrength;
                float _WaveAmplitude, _WaveLength, _WaveSpeed;
                float _FresnelPower, _ReflectionStrength, _SpecularPower, _SpecularIntensity;
                float _FoamDistance, _FoamNoiseScale;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            // --- noise -------------------------------------------------------
            float2 Hash22(float2 p)
            {
                // Sin-free hash (cheap on mobile GPUs).
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return -1.0 + 2.0 * frac((p3.xx + p3.yz) * p3.zy);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(dot(Hash22(i + float2(0, 0)), f - float2(0, 0)),
                                 dot(Hash22(i + float2(1, 0)), f - float2(1, 0)), u.x),
                            lerp(dot(Hash22(i + float2(0, 1)), f - float2(0, 1)),
                                 dot(Hash22(i + float2(1, 1)), f - float2(1, 1)), u.x), u.y);
            }

            float RippleHeight(float2 p, float t)
            {
                float2 flow = normalize(_FlowDirection.xy + 1e-5) * _FlowSpeed * t;
                float h = GradientNoise(p * _RippleScale + flow) * 0.6;
                #if !defined(CF_GFX_LOW)
                h += GradientNoise(p * _RippleScale * 2.3 - flow * 1.3 + 17.0) * 0.3;
                h += GradientNoise(p * _RippleScale * 5.1 + flow * 0.7 + 41.0) * 0.12;
                #endif
                return h;
            }

            // --- vertex waves --------------------------------------------------
            float3 Waves(float3 p, out float3 normal)
            {
                float t = _Time.y * _WaveSpeed;
                float k = 6.2831853 / max(_WaveLength, 0.01);
                float2 d1 = normalize(float2(0.3, 1.0));
                float2 d2 = normalize(float2(-0.8, 0.45));
                float2 d3 = normalize(float2(0.9, -0.2));
                float f1 = k * dot(d1, p.xz) - t;
                float f2 = k * 1.7 * dot(d2, p.xz) - t * 1.3;
                float f3 = k * 0.6 * dot(d3, p.xz) - t * 0.7;
                float a = _WaveAmplitude;
                p.y += a * sin(f1) + a * 0.5 * sin(f2) + a * 0.8 * sin(f3);
                float dx = a * k * (d1.x * cos(f1) + 0.85 * d2.x * cos(f2) + 0.48 * d3.x * cos(f3));
                float dz = a * k * (d1.y * cos(f1) + 0.85 * d2.y * cos(f2) + 0.48 * d3.y * cos(f3));
                normal = normalize(float3(-dx, 1, -dz));
                return p;
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 n;
                posWS = Waves(posWS, n);
                o.positionWS = posWS;
                o.normalWS = n;
                o.positionCS = TransformWorldToHClip(posWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.positionWS.xz;

                // Ripple normal from finite differences of the procedural height field.
                const float e = 0.05;
                float h0 = RippleHeight(p, t);
                float hx = RippleHeight(p + float2(e, 0), t);
                float hz = RippleHeight(p + float2(0, e), t);
                float3 rippleN = normalize(float3(-(hx - h0) / e * _RippleStrength * 0.25, 1, -(hz - h0) / e * _RippleStrength * 0.25));
                float3 N = normalize(float3(i.normalWS.xz + rippleN.xz, 1).xzy);

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float surfaceEye = i.screenPos.w;

                // Depth below the surface (unrefracted) for foam/absorption.
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depth = max(0, sceneEye - surfaceEye);

                #if defined(CF_GFX_LOW)
                // Low: no refraction and no scene-colour copy (the opaque texture is off); the shallows take the water tint.
                float refrDepth = depth;
                half3 sceneCol = _ShallowColor.rgb * 0.8;
                #else
                // Refraction: offset uv, fall back if the sample lands in front of the water.
                float2 offset = N.xz * _RefractionStrength * saturate(depth * 0.8);
                float2 refrUV = screenUV + offset;
                float refrEye = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEye < surfaceEye) { refrUV = screenUV; refrEye = sceneEye; }
                float refrDepth = max(0, refrEye - surfaceEye);
                half3 sceneCol = SampleSceneColor(refrUV);
                #endif

                // Lighting: the water's own colour responds to sun + ambient so dusk and night darken it.
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord);
                float shadow = lerp(0.55, 1.0, sun.shadowAttenuation);
                half3 ambient = SampleSH(N);
                half3 lightAmount = sun.color * saturate(dot(N, sun.direction)) * sun.shadowAttenuation * 0.45 + ambient * 0.55;

                // Beer-Lambert style absorption: clear in the shallows, deep teal-blue further out.
                float absorb = 1 - exp(-refrDepth / _AbsorptionDepth);
                half3 shallowLit = _ShallowColor.rgb * lightAmount;
                half3 deepLit = _DeepColor.rgb * lightAmount;
                half3 tinted = sceneCol * lerp(half3(1, 1, 1), _ShallowColor.rgb, 0.55) * _Clarity;
                half3 water = lerp(lerp(shallowLit, tinted, _Clarity * (1 - absorb)), deepLit, absorb);
                water *= lerp(0.85, 1.0, sun.shadowAttenuation);

                float fresnel = pow(1 - saturate(dot(N, V)), _FresnelPower);
                float3 R = reflect(-V, N);
                half3 sky = _SkyColor.rgb * saturate(ambient * 1.2 + sun.color * 0.15);
                #if !defined(CF_GFX_LOW)
                sky = lerp(sky, GlossyEnvironmentReflection(R, 0.05, 1), 0.35);
                #endif
                water = lerp(water, sky, saturate(fresnel * _ReflectionStrength + 0.04));

                float3 H = normalize(sun.direction + V);
                float spec = pow(saturate(dot(N, H)), _SpecularPower) * _SpecularIntensity * sun.shadowAttenuation;
                water += sun.color * spec;

                // Shoreline / intersection foam with broken, animated edge.
                // Solid foam hugs the contact edge; a thinner animated line pulses a little further out.
                float foamNoise = GradientNoise(p * _FoamNoiseScale + t * 0.3) * 0.5 + 0.5;
                float foamBand = saturate(1 - depth / max(_FoamDistance, 1e-3));
                float foam = smoothstep(0.5, 0.56, foamBand * foamBand - foamNoise * 0.35 + 0.2);
                float lineBand = saturate(1 - depth / max(_FoamDistance * 2.2, 1e-3));
                float foamLine = smoothstep(0.9, 0.95, frac(lineBand * 2.0 - t * 0.25)) * lineBand * step(0.45, foamNoise) * (1 - foam);
                water = lerp(water, _FoamColor.rgb * (0.8 + 0.2 * shadow), saturate(foam + foamLine * 0.55) * _FoamColor.a);

                water = MixFog(water, i.fogFactor);
                return half4(water, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
