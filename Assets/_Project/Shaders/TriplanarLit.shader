// 트라이플래너(Triplanar) URP Lit 셰이더
//
// 왜 만들었나:
//   Low Poly AR Weapon Pack 3의 부착물들은 UV가 사실상 없다(정점 500개에 UV 좌표가 1~2개뿐).
//   색 팔레트 텍스처 한 장에서 픽셀 하나만 가리키도록 만들어져 있어서, 일반적인 방법으로
//   노멀맵/러프니스맵을 붙이면 전부 같은 픽셀만 읽어 아무 효과가 없다.
//   이 셰이더는 UV를 아예 무시하고 XYZ 세 방향에서 텍스처를 투영해서 입힌다.
//
// 오브젝트 공간 투영이 기본값인 이유:
//   부착물은 총에 붙어서 같이 움직인다. 월드 공간으로 투영하면 총을 움직일 때마다
//   텍스처가 표면 위를 미끄러져 흘러 보인다(texture swimming). 오브젝트 공간이면
//   텍스처가 모델에 고정된다.
//
// 제약: 라이트맵을 지원하지 않는다(SampleSH로 라이트 프로브만 받는다).
//       부착물처럼 움직이는 오브젝트 전용이다. 정적인 배경 물체에는 쓰지 말 것.
Shader "KOD/Triplanar Lit"
{
    Properties
    {
        [MainColor] _TintColor("Tint Color", Color) = (1,1,1,1)

        [NoScaleOffset] _BaseMap("Base Color", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _MetallicMap("Metallic Map", 2D) = "white" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "white" {}
        [NoScaleOffset] _AOMap("Occlusion Map", 2D) = "white" {}

        // 투영 설정
        _TriplanarScale("Tiles Per Meter", Float) = 8
        _BlendSharpness("Blend Sharpness", Range(1, 32)) = 8
        [Toggle(_TRIPLANAR_WORLD)] _TriplanarWorld("World Space Mapping", Float) = 0

        // 표면 값 보정 (텍스처가 없으면 입력이 1이므로 remap의 y값이 그대로 결과가 된다)
        _NormalStrength("Normal Strength", Range(0, 4)) = 1
        [Toggle] _NormalFlipY("Flip Normal Green Channel", Float) = 0
        _MetallicRemap("Metallic Remap (min, max)", Vector) = (0, 1, 0, 0)
        _RoughnessRemap("Roughness Remap (min, max)", Vector) = (0, 1, 0, 0)
        _AOStrength("Occlusion Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _TintColor;
            float _TriplanarScale;
            float _BlendSharpness;
            float _NormalStrength;
            float _NormalFlipY;
            float4 _MetallicRemap;
            float4 _RoughnessRemap;
            float _AOStrength;
        CBUFFER_END

        // 세 방향 투영의 가중치. 법선이 향한 축의 투영이 강해진다.
        float3 TriplanarBlend(float3 n, float sharpness)
        {
            float3 w = pow(abs(n), sharpness);
            return w / max(w.x + w.y + w.z, 1e-5);
        }

        // 축 부호. 뒷면에서 텍스처가 좌우 반전돼 보이는 것을 막는다.
        float3 TriplanarAxisSign(float3 n)
        {
            return n < 0 ? -1.0 : 1.0;
        }

        void TriplanarUVs(float3 p, float3 axisSign, float scale,
                          out float2 uvX, out float2 uvY, out float2 uvZ)
        {
            uvX = float2(p.z * axisSign.x, p.y) * scale;
            uvY = float2(p.x * axisSign.y, p.z) * scale;
            uvZ = float2(p.x * -axisSign.z, p.y) * scale;
        }
        ENDHLSL

        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TriplanarForwardVertex
            #pragma fragment TriplanarForwardFragment

            #pragma shader_feature_local _TRIPLANAR_WORLD

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetallicMap);    SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_RoughnessMap);   SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_AOMap);          SAMPLER(sampler_AOMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 mapPos     : TEXCOORD2;   // 투영에 쓸 위치 (오브젝트 또는 월드 공간)
                float3 mapNormal  : TEXCOORD3;   // 같은 공간의 법선
                float4 fogAndVertexLight : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings TriplanarForwardVertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;

            #if defined(_TRIPLANAR_WORLD)
                OUT.mapPos = pos.positionWS;
                OUT.mapNormal = nrm.normalWS;
            #else
                OUT.mapPos = IN.positionOS.xyz;
                OUT.mapNormal = IN.normalOS;
            #endif

                OUT.fogAndVertexLight.x = ComputeFogFactor(pos.positionCS.z);
                OUT.fogAndVertexLight.yzw = VertexLighting(pos.positionWS, nrm.normalWS);
                return OUT;
            }

            half4 TriplanarForwardFragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 p = IN.mapPos;
                float3 n = normalize(IN.mapNormal);
                float3 blend = TriplanarBlend(n, _BlendSharpness);
                float3 axisSign = TriplanarAxisSign(n);

                float2 uvX, uvY, uvZ;
                TriplanarUVs(p, axisSign, _TriplanarScale, uvX, uvY, uvZ);

                // 베이스 컬러
                half4 baseColor =
                      SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX) * blend.x
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY) * blend.y
                    + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ) * blend.z;
                baseColor *= _TintColor;

                // 노멀 — 화이트아웃(whiteout) 블렌드
                half3 tnX = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvX), _NormalStrength);
                half3 tnY = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvY), _NormalStrength);
                half3 tnZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvZ), _NormalStrength);

                half flipY = lerp(1.0, -1.0, _NormalFlipY);
                tnX.y *= flipY; tnY.y *= flipY; tnZ.y *= flipY;

                tnX.z *= axisSign.x;
                tnY.z *= axisSign.y;
                tnZ.z *= axisSign.z;

                tnX = half3(tnX.xy + n.zy, abs(tnX.z) * n.x);
                tnY = half3(tnY.xy + n.xz, abs(tnY.z) * n.y);
                tnZ = half3(tnZ.xy + n.xy, abs(tnZ.z) * n.z);

                float3 mappedNormal = normalize(tnX.zyx * blend.x + tnY.xzy * blend.y + tnZ.xyz * blend.z);

            #if defined(_TRIPLANAR_WORLD)
                float3 normalWS = mappedNormal;
            #else
                float3 normalWS = normalize(TransformObjectToWorldNormal(mappedNormal));
            #endif

                // 메탈릭 / 러프니스 / AO
                half metallicRaw =
                      SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uvX).r * blend.x
                    + SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uvY).r * blend.y
                    + SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uvZ).r * blend.z;

                half roughnessRaw =
                      SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uvX).r * blend.x
                    + SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uvY).r * blend.y
                    + SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uvZ).r * blend.z;

                half aoRaw =
                      SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uvX).r * blend.x
                    + SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uvY).r * blend.y
                    + SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uvZ).r * blend.z;

                half metallic = lerp(_MetallicRemap.x, _MetallicRemap.y, metallicRaw);
                half roughness = lerp(_RoughnessRemap.x, _RoughnessRemap.y, roughnessRaw);
                half occlusion = lerp(1.0h, aoRaw, _AOStrength);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = baseColor.rgb;
                surface.metallic = saturate(metallic);
                surface.smoothness = saturate(1.0h - roughness);
                surface.occlusion = occlusion;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = 0;
                surface.specular = 0;
                surface.alpha = 1;
                surface.clearCoatMask = 0;
                surface.clearCoatSmoothness = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.positionCS = IN.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = IN.fogAndVertexLight.x;
                inputData.vertexLighting = IN.fogAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DepthNormalsFragment(Varyings IN) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
