Shader "Kartikaris/URP/WarFX Universal"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
        _ScrollSpeed ("Scroll Speed", Float) = 0.0
        _Shininess ("Shininess", Range(0.01,1.0)) = 0.078125
        _SpecColor ("Specular Color", Color) = (0.5,0.5,0.5,0)
        [HideInInspector] _EffectMode ("Effect Mode", Float) = 0
        [HideInInspector] _UseSoftParticles ("Use Soft Particles", Float) = 0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "UniversalMaterialType"="Unlit"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ColorMask RGB
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half4 _SpecColor;
                float _InvFade;
                float _ScrollSpeed;
                float _Shininess;
                float _EffectMode;
                float _UseSoftParticles;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                float eyeDepth : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.eyeDepth = -TransformWorldToView(positionInputs.positionWS).z;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half SoftParticleFade(Varyings input)
            {
                if (_UseSoftParticles < 0.5)
                    return 1.0h;

                float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUv);
                float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                return saturate(_InvFade * (sceneEyeDepth - input.eyeDepth));
            }

            half3 ApplyEffectFog(half3 color, half fogFactor, float effectMode)
            {
                if ((effectMode > 0.5 && effectMode < 5.5 && effectMode != 3.0) ||
                    effectMode > 10.5)
                    return MixFogColor(color, half3(0, 0, 0), fogFactor);
                if ((effectMode > 2.5 && effectMode < 4.5) ||
                    (effectMode > 5.5 && effectMode < 9.5))
                {
                    return lerp(color, half3(0.5, 0.5, 0.5), fogFactor);
                }
                return MixFog(color, fogFactor);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fade = SoftParticleFade(input);
                half4 baseSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 scrolledUv = input.uv;
                scrolledUv.y -= frac(_Time.y * _ScrollSpeed);
                half4 scrolledSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrolledUv);
                half4 outputColor;

                if (_EffectMode < 0.5)
                {
                    outputColor = 2.0h * input.color * _TintColor * baseSample;
                    outputColor.a *= fade;
                }
                else if (_EffectMode < 1.5)
                {
                    half intensity = baseSample.a * input.color.a * fade;
                    outputColor = 4.0h * input.color * _TintColor * intensity;
                }
                else if (_EffectMode < 2.5)
                {
                    outputColor = 2.0h * input.color * _TintColor * baseSample.a * fade;
                }
                else if (_EffectMode < 3.5)
                {
                    outputColor = input.color * baseSample.a;
                    outputColor.rgb = lerp(half3(1, 1, 1), outputColor.rgb, fade);
                }
                else if (_EffectMode < 4.5)
                {
                    half mask = baseSample.a * input.color.a * fade;
                    outputColor = half4(scrolledSample.rgb * input.color.rgb * mask, mask);
                }
                else if (_EffectMode < 5.5)
                {
                    outputColor = half4(
                        scrolledSample.rgb * input.color.rgb,
                        baseSample.a * input.color.a * fade);
                }
                else if (_EffectMode < 6.5)
                {
                    half mask = baseSample.a * input.color.a;
                    half3 multiplied = baseSample.rgb * input.color.rgb * _TintColor.rgb;
                    outputColor = half4(lerp(half3(0.5, 0.5, 0.5), multiplied, mask), mask);
                }
                else if (_EffectMode < 7.5)
                {
                    half mask = baseSample.a * input.color.a;
                    half3 multiplied = scrolledSample.rgb * input.color.rgb * _TintColor.rgb;
                    outputColor = half4(lerp(half3(0.5, 0.5, 0.5), multiplied, mask), mask);
                }
                else if (_EffectMode < 8.5)
                {
                    half mask = baseSample.a * input.color.a;
                    half3 smoke = scrolledSample.rgb * input.color.rgb * _TintColor.rgb;
                    outputColor = half4(lerp(half3(0.5, 0.5, 0.5), smoke, mask), mask);
                }
                else if (_EffectMode < 10.5)
                {
                    half3 normalWS = SafeNormalize(input.normalWS);
                    Light mainLight = GetMainLight();
                    half nDotL = saturate(dot(normalWS, mainLight.direction));
                    half3 lighting = SampleSH(normalWS) +
                        mainLight.color * nDotL * mainLight.distanceAttenuation;
                    half3 surface = baseSample.rgb * _TintColor.rgb * input.color.rgb;
                    outputColor = half4(surface * lighting, baseSample.a * _TintColor.a * input.color.a);

                    if (_EffectMode > 9.5)
                    {
                        half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                        half3 halfDirection = SafeNormalize(mainLight.direction + viewDirection);
                        half exponent = lerp(8.0h, 128.0h, saturate(_Shininess));
                        half specular = pow(saturate(dot(normalWS, halfDirection)), exponent);
                        outputColor.rgb += specular * _SpecColor.rgb * mainLight.color;
                    }
                }
                else
                {
                    outputColor = 2.0h * input.color * _TintColor * baseSample * fade;
                }

                outputColor.rgb = ApplyEffectFog(outputColor.rgb, input.fogFactor, _EffectMode);
                return outputColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
