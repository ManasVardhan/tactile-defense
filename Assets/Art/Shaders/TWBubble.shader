// Glass bubble: transparent center, bright fresnel rim and a soft highlight.
Shader "TW/Bubble"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0.12
        _RimAlpha ("Rim Alpha", Range(0, 1)) = 0.9
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _Highlight ("Highlight", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _CenterAlpha, _RimAlpha, _RimPower, _Highlight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float f = pow(saturate(1.0 - dot(N, V)), _RimPower);
                float3 H = normalize(V + normalize(float3(-0.4, 0.8, -0.3)));
                float spec = pow(saturate(dot(N, H)), 60.0) * _Highlight;
                half a = saturate(lerp(_CenterAlpha, _RimAlpha, f) + spec);
                return half4(_BaseColor.rgb, a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
