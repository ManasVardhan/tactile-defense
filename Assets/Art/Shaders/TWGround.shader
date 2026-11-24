// Open battlefield ground: gentle large-scale color variation, soft tonal patches (optionally cracked), fine
// speckles, a worn path from the map mask, trampled areas and darker edges. No tiling pattern and no grid: the
// deployment grid is invisible. Each biome (BiomeStyle) sets the colors and amounts.
Shader "TW/Ground"
{
    Properties
    {
        _BaseColor1 ("Base", Color) = (0.55, 0.68, 0.36, 1)
        _BaseColor2 ("Base Variation", Color) = (0.5, 0.63, 0.33, 1)
        _NoiseScale ("Variation Scale", Float) = 0.06
        _PatchColor ("Patch", Color) = (0.62, 0.72, 0.4, 1)
        _PatchScale ("Patch Scale", Float) = 0.16
        _PatchCoverage ("Patch Coverage", Range(0, 1)) = 0.35
        _PatchSoftness ("Patch Edge Softness", Range(0.01, 0.5)) = 0.12
        _CrackColor ("Crack", Color) = (0.4, 0.25, 0.18, 1)
        _CrackStrength ("Crack Strength (inside patches)", Range(0, 1)) = 0
        _CrackScale ("Crack Scale", Float) = 0.9
        _SpeckleColor ("Speckle", Color) = (0.95, 0.92, 0.7, 1)
        _SpeckleAmount ("Speckle Amount", Range(0, 0.3)) = 0.04
        _SpeckleScale ("Speckle Scale", Float) = 2.2
        _PathColor ("Path", Color) = (0.66, 0.56, 0.4, 1)
        _PathStrength ("Path Strength", Range(0, 1)) = 0.9
        _WornColor ("Trampled (mask G)", Color) = (0.6, 0.6, 0.42, 1)
        _WornStrength ("Trampled Strength", Range(0, 1)) = 0.6
        _DarkColor ("Edge (mask B)", Color) = (0.38, 0.5, 0.3, 1)
        _WaterColor ("Water (mask A below 1)", Color) = (0.36, 0.62, 0.74, 1)
        _WaterDeep ("Water Middle", Color) = (0.27, 0.5, 0.64, 1)
        _BankColor ("Water Bank", Color) = (0.5, 0.55, 0.4, 1)
        _WaterStrength ("Water Strength", Range(0, 1)) = 0
        _MaskTex ("Mask (R path, G trampled, B edge, A 1 - water)", 2D) = "black" {}
        _MapRect ("Map Rect (x0, z0, width, depth)", Vector) = (-30, -20, 60, 150)
        _ShadowColor ("Shadow Tint", Color) = (0.62, 0.6, 0.66, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.75
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor1, _BaseColor2, _PatchColor, _CrackColor, _SpeckleColor, _PathColor, _WornColor, _DarkColor, _ShadowColor;
            half4 _WaterColor, _WaterDeep, _BankColor;
            float _WaterStrength;
            float _NoiseScale, _PatchScale, _PatchCoverage, _PatchSoftness, _CrackStrength, _CrackScale;
            float _SpeckleAmount, _SpeckleScale, _PathStrength, _WornStrength;
            float4 _MaskTex_ST;
            float4 _MapRect;
            float _ShadowStrength, _Brightness;
        CBUFFER_END
        TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float2 Hash22(float2 p)
        {
            float n = Hash21(p);
            return float2(n, Hash21(p + n + 17.3));
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
            float2 u = f * f * (3 - 2 * f);
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float Fbm(float2 p)
        {
            return ValueNoise(p) * 0.6 + ValueNoise(p * 2.13 + 7.1) * 0.3 + ValueNoise(p * 4.7 + 3.3) * 0.1;
        }

        // Distance to the nearest cell border of a jittered cellular pattern (thin irregular crack lines).
        float CrackDist(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float d1 = 8, d2 = 8;
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y);
                    float2 r = o + Hash22(i + o) * 0.85 + 0.075 - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
            return sqrt(d2) - sqrt(d1);
        }

        Varyings GroundVert(Attributes i)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.fog = ComputeFogFactor(o.positionCS.z);
            return o;
        }

        half4 GroundFrag(Varyings i) : SV_Target
        {
            float2 w = i.positionWS.xz;
            float2 muv = (w - _MapRect.xy) / _MapRect.zw;
            half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, muv);

            // Large soft variation.
            float n = Fbm(w * _NoiseScale);
            half3 col = lerp(_BaseColor1.rgb, _BaseColor2.rgb, saturate(n * 1.4 - 0.2));

            // Soft tonal patches with a ragged edge.
            float pn = Fbm(w * _PatchScale + 11.7) + (ValueNoise(w * 1.1) - 0.5) * 0.08;
            float patch = smoothstep(1.0 - _PatchCoverage - _PatchSoftness, 1.0 - _PatchCoverage + _PatchSoftness, pn);
            col = lerp(col, _PatchColor.rgb * (0.96 + 0.08 * ValueNoise(w * 0.9)), patch);

            // Cracks inside the patches (dry clay); off where _CrackStrength is 0.
            float cd = CrackDist(w * _CrackScale);
            float aa = fwidth(cd) * 1.5 + 1e-4;
            // Broken up by a slow noise so the lines read as scattered cracks, never as a tiling of cells.
            float broken = smoothstep(0.42, 0.62, ValueNoise(w * 0.45 + 5.3));
            float crack = (1.0 - smoothstep(0.028 - aa, 0.028 + aa, cd)) * patch * broken * _CrackStrength;
            col = lerp(col, _CrackColor.rgb, crack);

            // Fine speckles (flowers, pebbles, glints), sparse and scattered.
            float2 sc = w * _SpeckleScale;
            float2 sid = floor(sc);
            float2 sp = frac(sc) - (Hash22(sid) * 0.6 + 0.2);
            float sOn = step(1.0 - _SpeckleAmount, Hash21(sid + 3.7));
            float dot1 = 1.0 - smoothstep(0.07, 0.12, length(sp));
            col = lerp(col, _SpeckleColor.rgb, dot1 * sOn * 0.85);

            // Darker or lighter edges (mask B) and trampled ground (mask G: under pads, the keep and camps).
            col = lerp(col, _DarkColor.rgb, saturate(mask.b) * 0.75);
            float worn = smoothstep(0.2, 0.8, mask.g + (ValueNoise(w * 0.8) - 0.5) * 0.4) * _WornStrength;
            col = lerp(col, _WornColor.rgb * (0.95 + 0.1 * ValueNoise(w * 2.3)), worn);

            // The path, with a ragged noisy edge.
            float pathN = mask.r + (ValueNoise(w * 1.3) - 0.5) * 0.35;
            float path = smoothstep(0.3, 0.7, pathN) * _PathStrength;
            half3 pathCol = _PathColor.rgb * (0.93 + 0.14 * ValueNoise(w * 2.7));
            col = lerp(col, pathCol, path);

            // Water, a dry riverbed or ice (mask A below 1, per biome): a soft bank, then the surface with gentle
            // streaks along the flow and a deeper middle.
            float wet = 1.0 - mask.a;
            float wn = wet + (ValueNoise(w * 0.9 + 2.1) - 0.5) * 0.22;
            float bank = smoothstep(0.04, 0.3, wn) * _WaterStrength;
            float surface = smoothstep(0.38, 0.58, wn) * _WaterStrength;
            col = lerp(col, _BankColor.rgb * (0.95 + 0.1 * ValueNoise(w * 2.1)), bank * (1.0 - surface));
            half3 wc = lerp(_WaterColor.rgb, _WaterDeep.rgb, smoothstep(0.62, 0.95, wet));
            wc *= 0.94 + 0.12 * ValueNoise(w * float2(0.35, 1.7) + 9.0);
            col = lerp(col, wc, surface);

            // Shadows from props, tinted rather than black.
            float4 shc = TransformWorldToShadowCoord(i.positionWS);
            Light light = GetMainLight(shc);
            half sh = lerp(1.0h, light.shadowAttenuation, _ShadowStrength);
            col *= lerp(_ShadowColor.rgb, half3(1, 1, 1), sh);
            col *= _Brightness;
            col = MixFog(col, i.fog);
            return half4(col, 1);
        }

        float3 _LightDirection;
        float4 ShadowVert(Attributes i) : SV_POSITION
        {
            float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
            float3 nWS = TransformObjectToWorldNormal(i.normalOS);
            float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
        #if UNITY_REVERSED_Z
            cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
        #else
            cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
        #endif
            return cs;
        }
        float4 DepthVert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
        half4 NullFrag() : SV_Target { return 0; }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex GroundVert
            #pragma fragment GroundFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment NullFrag
            ENDHLSL
        }
    }
    FallBack Off
}
