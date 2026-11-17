Shader "TW/Ground"
{
    Properties
    {
        _SandColor ("Sand", Color) = (0.76, 0.71, 0.56, 1)
        _SandColor2 ("Sand Variation", Color) = (0.72, 0.68, 0.53, 1)
        _HexLineColor ("Hex Line", Color) = (0.62, 0.57, 0.44, 1)
        _HexSize ("Hex Size", Float) = 5.2
        _HexLineWidth ("Hex Line Width", Range(0.001, 0.2)) = 0.035
        _HexLineStrength ("Hex Line Strength", Range(0, 1)) = 0.45
        _HexTint ("Per Hex Variation", Range(0, 0.2)) = 0.035
        _PathColor ("Dirt Path", Color) = (0.62, 0.55, 0.42, 1)
        _PathStrength ("Dirt Path Strength", Range(0, 1)) = 1
        _TileColor ("Paved Tile", Color) = (0.84, 0.77, 0.62, 1)
        _DarkColor ("Dark Patch", Color) = (0.6, 0.6, 0.47, 1)
        _MaskTex ("Mask (R path, G tiles, B dark)", 2D) = "black" {}
        _MapRect ("Map Rect (x0, z0, width, depth)", Vector) = (-30, -20, 60, 150)
        _NoiseScale ("Noise Scale", Float) = 0.09
        _NoiseStrength ("Noise Strength", Range(0, 0.3)) = 0.06
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
            half4 _SandColor, _SandColor2, _HexLineColor, _PathColor, _TileColor, _DarkColor, _ShadowColor;
            float _HexSize, _HexLineWidth, _HexLineStrength, _HexTint;
            float4 _MaskTex_ST;
            float4 _MapRect;
            float _NoiseScale, _NoiseStrength, _ShadowStrength, _Brightness, _PathStrength;
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

        // Hex tiling: returns local coords inside the cell and the cell id.
        float2 Hex(float2 p, out float2 id)
        {
            const float2 s = float2(1, 1.7320508);
            float4 hc = floor(float4(p, p - float2(0.5, 1)) / s.xyxy) + 0.5;
            float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
            float4 r = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? float4(h.xy, hc.xy) : float4(h.zw, hc.zw + 0.5);
            id = r.zw;
            return r.xy;
        }

        float HexEdge(float2 local)
        {
            local = abs(local);
            return 0.5 - max(dot(local, normalize(float2(1, 1.7320508))), local.x);
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
            half3 col = lerp(_SandColor.rgb, _SandColor2.rgb, saturate(n * 1.3 - 0.15));

            // Big hex cells with a subtle per cell tint and thin worn seams.
            float2 id;
            float2 local = Hex(w / _HexSize, id);
            float edge = HexEdge(local);
            float cellTint = (Hash21(id) - 0.5) * 2.0 * _HexTint;
            col *= 1.0 + cellTint;
            float aa = fwidth(edge) * 1.2;
            float seam = 1.0 - smoothstep(_HexLineWidth - aa, _HexLineWidth + aa, edge);
            float wear = smoothstep(0.25, 0.75, ValueNoise(w * 0.7 + id * 3.1));
            col = lerp(col, _HexLineColor.rgb, seam * _HexLineStrength * (0.55 + 0.45 * wear));

            // Dark olive patches.
            col = lerp(col, _DarkColor.rgb, saturate(mask.b) * 0.7);

            // Dirt paths with a ragged noisy edge.
            float pathN = mask.r + (ValueNoise(w * 1.3) - 0.5) * 0.35;
            float path = smoothstep(0.3, 0.7, pathN) * _PathStrength;
            half3 pathCol = _PathColor.rgb * (0.94 + 0.12 * ValueNoise(w * 2.7));
            col = lerp(col, pathCol, path);

            // Paved hex tile accents (small hexes, lighter, with dark grout).
            float2 tid;
            float2 tl = Hex(w / 1.55, tid);
            float te = HexEdge(tl);
            float tileOn = step(0.5, mask.g + (Hash21(tid) - 0.5) * 0.5);
            float grout = 1.0 - smoothstep(0.04, 0.07, te);
            half3 tileCol = _TileColor.rgb * (0.95 + 0.1 * Hash21(tid + 3.0));
            tileCol = lerp(tileCol, _HexLineColor.rgb * 0.95, grout * 0.85);
            col = lerp(col, tileCol, tileOn * saturate(mask.g * 4.0));

            // Shadows from props, tinted rather than black.
            float4 sc = TransformWorldToShadowCoord(i.positionWS);
            Light light = GetMainLight(sc);
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
