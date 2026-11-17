// Procedural flat ring with a fill arc (capture progress, structure health, flag bearer health).
Shader "TW/RingArc"
{
    Properties
    {
        _BaseColor ("Fill Color", Color) = (1, 0.3, 0.2, 1)
        _BackColor ("Background Color", Color) = (1, 1, 1, 0.35)
        _Inner ("Inner Radius", Range(0, 1)) = 0.82
        _Outer ("Outer Radius", Range(0, 1)) = 0.97
        _Fill ("Fill", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Pulse ("Pulse (contested)", Range(0, 1)) = 0
        _Dashes ("Background Dashes", Float) = 28
        _Emission ("Emission", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -2, -2
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _BackColor;
                half _Inner, _Outer, _Fill, _Alpha, _Pulse, _Dashes, _Emission;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv * 2.0 - 1.0;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float aa = fwidth(r) * 1.5;
                float ring = smoothstep(_Inner - aa, _Inner + aa, r) * (1.0 - smoothstep(_Outer - aa, _Outer + aa, r));
                // Angle: 0 at the top (+V), increasing clockwise as seen from above.
                float ang = atan2(i.uv.x, i.uv.y) / 6.2831853 + 0.5;
                ang = frac(ang + 0.5);
                float filled = step(ang, _Fill) * step(0.0001, _Fill);
                float dash = step(0.45, frac(ang * _Dashes));
                half3 c = lerp(_BackColor.rgb, _BaseColor.rgb * (1.0h + _Emission), filled);
                half a = lerp(_BackColor.a * dash, _BaseColor.a, filled);
                float pulse = _Pulse * (0.5 + 0.5 * sin(_Time.y * 12.0));
                c = lerp(c, half3(1, 1, 1), pulse * 0.6);
                return half4(c, ring * a * _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
