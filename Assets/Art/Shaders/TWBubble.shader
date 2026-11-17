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
        _BackColor ("Back Face Fill", Color) = (0.93, 0.93, 0.9, 0.6)
        _BackEdgeColor ("Back Face Edge", Color) = (0.66, 0.66, 0.64, 0.85)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _CenterAlpha, _RimAlpha, _RimPower, _Highlight;
            half4 _BackColor, _BackEdgeColor;
        CBUFFER_END
        ENDHLSL

        // Back faces first: a milky disc BEHIND the bearer (the soldier occludes it through the depth test), like the
        // reference bubbles where the bearer reads clearly in front of a pale glass interior (ps_12, f_0022).
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            HLSLPROGRAM
            #pragma vertex vertB
            #pragma fragment fragB
            #pragma multi_compile_instancing
            struct AttributesB { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VaryingsB { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            VaryingsB vertB(AttributesB i)
            {
                VaryingsB o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            half4 fragB(VaryingsB i) : SV_Target
            {
                float3 N = -normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float f = pow(saturate(1.0 - abs(dot(N, V))), 1.6);
                half4 c = lerp(_BackColor, _BackEdgeColor, f);
                return half4(c.rgb, c.a * _BaseColor.a);
            }
            ENDHLSL
        }

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
