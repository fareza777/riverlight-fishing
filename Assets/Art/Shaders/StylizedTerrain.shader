// Painterly terrain for Riverlight. Surface type is decided per pixel (not per triangle) from height, slope and
// layered noise, so grass, dirt, wet sand, cliff rock and the riverbed blend organically.
// Vertex colour R = hand-painted path mask (from Blender).
Shader "CozyFishing/StylizedTerrain"
{
    Properties
    {
        [Header(Grass)]
        _GrassA ("Grass Base", Color) = (0.33, 0.48, 0.22, 1)
        _GrassB ("Grass Light", Color) = (0.47, 0.58, 0.27, 1)
        _GrassC ("Grass Dark", Color) = (0.22, 0.35, 0.18, 1)
        _GrassDry ("Grass Dry", Color) = (0.60, 0.57, 0.33, 1)
        _PatchScale ("Patch Scale", Float) = 0.07
        _DetailScale ("Detail Scale", Float) = 1.6

        [Header(Ground)]
        _Dirt ("Dirt", Color) = (0.55, 0.41, 0.27, 1)
        _Path ("Path", Color) = (0.66, 0.52, 0.35, 1)
        _Sand ("Sand", Color) = (0.86, 0.77, 0.57, 1)
        _WetSand ("Wet Sand", Color) = (0.62, 0.55, 0.42, 1)
        _Riverbed ("Riverbed", Color) = (0.45, 0.43, 0.34, 1)
        _Rock ("Rock", Color) = (0.56, 0.55, 0.50, 1)
        _RockDark ("Rock Dark", Color) = (0.42, 0.41, 0.38, 1)

        [Header(Bands)]
        _WaterLevel ("Water Level", Float) = 0
        _ShoreHeight ("Shore Height", Float) = 0.42
        _SlopeDirt ("Slope Dirt (1-n.y)", Range(0, 1)) = 0.2
        _SlopeRock ("Slope Rock (1-n.y)", Range(0, 1)) = 0.34
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _GrassA, _GrassB, _GrassC, _GrassDry, _Dirt, _Path, _Sand, _WetSand, _Riverbed, _Rock, _RockDark;
            float _PatchScale, _DetailScale, _WaterLevel, _ShoreHeight, _SlopeDirt, _SlopeRock;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile _ CF_GFX_LOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            // noise.x = colour patches, noise.y = dry spots, noise.z = riverbed (all large-scale, so per vertex is enough)
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; half4 color : TEXCOORD2; float fog : TEXCOORD3; float3 noise : TEXCOORD4; half3 sh : TEXCOORD5; };

            // Sin-free hash (cheap on mobile GPUs).
            float Hash(float2 p) { float3 p3 = frac(p.xyx * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * VNoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            V Vert(A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                float2 xz = o.positionWS.xz;
                o.noise = float3(Fbm(xz * _PatchScale), Fbm(xz * 0.11 + 31.0), Fbm(xz * 0.9));
                o.sh = SampleSH(o.normalWS);
                return o;
            }

            half3 Albedo(float3 p, float3 n, half pathMask, float3 vn)
            {
                float2 xz = p.xz;
                float patch = vn.x;
                #if defined(CF_GFX_LOW)
                // Low: one noise per pixel (same value range as the 4-octave version).
                float detail = 0.47 + (VNoise(xz * _DetailScale) - 0.5) * 0.6;
                float fine = 0.5;
                float bedNoise = vn.z;
                #else
                float detail = Fbm(xz * _DetailScale);
                float fine = VNoise(xz * 9.0);
                float bedNoise = Fbm(xz * 0.9);
                #endif

                // Grass: big soft colour patches + small clumps + a few dry spots.
                half3 grass = lerp(_GrassC.rgb, _GrassA.rgb, smoothstep(0.25, 0.55, patch));
                grass = lerp(grass, _GrassB.rgb, smoothstep(0.55, 0.8, patch));
                grass = lerp(grass, _GrassDry.rgb, smoothstep(0.68, 0.82, vn.y) * 0.7);
                grass *= lerp(0.88, 1.1, detail) * lerp(0.95, 1.04, fine);

                float slope = 1 - saturate(n.y);
                float edgeNoise = (detail - 0.5) * 0.08;
                half3 c = grass;

                // Dirt on slopes, then rock on steep and high faces (with horizontal strata).
                c = lerp(c, _Dirt.rgb * lerp(0.85, 1.1, detail), smoothstep(_SlopeDirt - 0.04, _SlopeDirt + 0.04, slope + edgeNoise));
                float strata = smoothstep(0.35, 0.65, frac(p.y * 1.4 + detail * 0.6));
                half3 rock = lerp(_RockDark.rgb, _Rock.rgb, strata * 0.35 + detail * 0.65) * lerp(0.92, 1.06, fine);
                c = lerp(c, rock, smoothstep(_SlopeRock - 0.05, _SlopeRock + 0.05, slope + edgeNoise) * smoothstep(1.4, 2.2, p.y));

                // Hand-painted path.
                c = lerp(c, _Path.rgb * lerp(0.85, 1.08, detail), smoothstep(0.25, 0.7, pathMask + (detail - 0.5) * 0.3));

                // Shoreline: dry sand -> wet sand -> riverbed.
                float h = p.y - _WaterLevel + edgeNoise * 2.0;
                half3 sand = lerp(_WetSand.rgb, _Sand.rgb, smoothstep(0.02, 0.22, h)) * lerp(0.92, 1.05, fine);
                c = lerp(sand, c, smoothstep(_ShoreHeight - 0.08, _ShoreHeight + 0.08, h));
                half3 bed = _Riverbed.rgb * lerp(0.75, 1.1, bedNoise) * lerp(0.9, 1.05, fine);
                c = lerp(bed, c, smoothstep(-0.35, -0.05, h));
                return c;
            }

            half4 Frag(V i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 albedo = Albedo(i.positionWS, n, i.color.r, i.noise);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Soft wrap lighting keeps the painterly look; shadows stay readable.
                float ndl = saturate(dot(n, sun.direction) * 0.7 + 0.3);
                half3 lit = albedo * (sun.color * ndl * lerp(0.35, 1.0, sun.shadowAttenuation) + i.sh * 0.75);

                #if defined(_ADDITIONAL_LIGHTS) && !defined(CF_GFX_LOW)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    lit += albedo * l.color * l.distanceAttenuation * saturate(dot(n, l.direction));
                }
                #endif

                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 Vert(A i) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, nws, _LightDirection));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5);
                return half4(PackFloat2To888(oct), 0);
                #else
                return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
