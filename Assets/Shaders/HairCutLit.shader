Shader "HairSalonSim/HairCutLit"
{
    Properties
    {
        _BaseColor ("Hair Color", Color) = (0.15, 0.1, 0.07, 1)
        _SpecularColor ("Specular Color", Color) = (0.3, 0.25, 0.2, 1)
        _SpecularShift ("Specular Shift", Range(-1, 1)) = 0.1
        _SpecularWidth ("Specular Width", Range(0, 1)) = 0.5
        _HairWidth ("Hair Width", Range(0.0001, 0.005)) = 0.0005
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HairCutLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecularColor;
                float _SpecularShift;
                float _SpecularWidth;
                float _HairWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 tangentWS : TEXCOORD2;
                float alpha : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 prevPosWS = posWS; // Ideally from previous particle; fallback to same

                float3 cutPos;
                float cutAlpha;
                ApplyHairCut_float(
                    (float)input.vertexID,
                    posWS,
                    prevPosWS,
                    cutPos,
                    cutAlpha);

                output.positionWS = cutPos;
                output.positionCS = TransformWorldToHClip(cutPos);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = float3(0, 1, 0); // Hair tangent along strand
                output.alpha = cutAlpha;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Discard cut particles
                clip(input.alpha - 0.01);

                // Simple hair shading (Kajiya-Kay model)
                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 T = normalize(input.tangentWS);

                // Diffuse
                float TdotL = dot(T, L);
                float sinTL = sqrt(1.0 - TdotL * TdotL);
                float3 diffuse = _BaseColor.rgb * sinTL;

                // Specular (shifted tangent)
                float3 Ts = normalize(T + input.normalWS * _SpecularShift);
                float TdotH = dot(Ts, normalize(L + V));
                float sinTH = sqrt(1.0 - TdotH * TdotH);
                float spec = pow(saturate(sinTH), 1.0 / max(_SpecularWidth, 0.001));
                float3 specular = _SpecularColor.rgb * spec;

                float3 ambient = _BaseColor.rgb * 0.15;
                float3 finalColor = ambient + diffuse * mainLight.color + specular * mainLight.color;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HairCutLit.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint vertexID : SV_VertexID;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float alpha : TEXCOORD0;
            };

            float3 _LightDirection;

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 cutPos;
                float cutAlpha;
                ApplyHairCut_float((float)input.vertexID, posWS, posWS, cutPos, cutAlpha);

                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(cutPos, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = posCS;
                output.alpha = cutAlpha;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                clip(input.alpha - 0.01);
                return 0;
            }
            ENDHLSL
        }
    }
}
