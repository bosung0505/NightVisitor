// URP (Universal Render Pipeline) - Lit + Sway Shader
Shader "Custom/CornSway_URP_Lit"
{
    Properties
    {
        _MainTex ("Corn Texture (PNG)", 2D) = "white" {}
        [HDR] _Color ("Color Tint", Color) = (1,1,1,1)
        _AlphaCutoff ("Alpha Clip Threshold", Range(0.0, 1.0)) = 0.5
        
        [Header(PBR Properties)]
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2

        [Header(AmbientSway)]
        _SwaySpeed  ("Sway Speed", Float) = 1.5
        _SwayAmount ("Sway Amount", Float) = 0.02

        [Header(MutantProximitySway)]
        _MutantWorldPos  ("Mutant World Pos (Auto)", Vector) = (0, -9999, 0, 0)
        _ProximityRadius ("Proximity Radius m", Float) = 8.0
        _ProximityAmount ("Proximity Sway Amount", Float) = 0.12
        _ProximitySpeed  ("Proximity Sway Speed", Float) = 5.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        ZWrite On
        Cull Off // ★ 양면 렌더링 사용 (뒷면 라이팅 계산 시 법선을 뒤집습니다)

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            
            // URP Lighting & Fog keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _AlphaCutoff;
                half   _Metallic;
                half   _Smoothness;
                
                float  _SwaySpeed;
                float  _SwayAmount;
                float4 _MutantWorldPos;
                float  _ProximityRadius;
                float  _ProximityAmount;
                float  _ProximitySpeed;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);

                float2 diff = float2(worldPos.x - _MutantWorldPos.x, worldPos.z - _MutantWorldPos.z);
                float dist = length(diff);
                float proximity = (_MutantWorldPos.y < -100.0) ? 0.0 : saturate(1.0 - dist / max(_ProximityRadius, 0.001));

                float rootFactor = IN.uv.y;
                float baseSway = sin(_Time.y * _SwaySpeed) * _SwayAmount * rootFactor;
                float proxSway = sin(_Time.y * _ProximitySpeed + worldPos.x * 3.0) * _ProximityAmount * rootFactor * proximity;

                IN.positionOS.x += baseSway + proxSway;

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                
                float3 safeNormalOS = length(IN.normalOS) > 0.01 ? IN.normalOS : float3(0, 1, 0);
                OUT.normalWS = TransformObjectToWorldNormal(safeNormalOS);
                
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                
                #if defined(REQUIRES_VERTEX_SHADOWCOORD_INTERPOLATOR)
                OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                #else
                OUT.shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                return OUT;
            }

            // FRONT_FACE_SEMANTIC 으로 앞면/뒷면 판별하여 완전 검은색 현상 해결
            half4 frag(Varyings IN, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                clip(albedoAlpha.a - _AlphaCutoff);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedoAlpha.rgb;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1.0;
                surfaceData.normalTS = float3(0,0,1);
                surfaceData.emission = float3(0,0,0);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                
                // 뒷면은 빛을 받지 못해 검게 나오는 현상 해결 -> 법선 반전 적용
                float3 normalWS = normalize(IN.normalWS);
                inputData.normalWS = IS_FRONT_VFACE(isFrontFace, normalWS, -normalWS);
                
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                
                #if defined(REQUIRES_VERTEX_SHADOWCOORD_INTERPOLATOR)
                inputData.shadowCoord = IN.shadowCoord;
                #else
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #endif

                // 간접광(GI / 하늘 빛) 반사 추가
                inputData.bakedGI = SampleSH(inputData.normalWS);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // --- Shadow Caster Pass ---
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0 // 색상 계산 불필요 (성능 향상)
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            // 에러를 유발하는 Shadows.hlsl 종속성을 깔끔히 제거하고, Core 연산만으로 그림자 투영
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _AlphaCutoff;
                
                float  _SwaySpeed;
                float  _SwayAmount;
                float4 _MutantWorldPos;
                float  _ProximityRadius;
                float  _ProximityAmount;
                float  _ProximitySpeed;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vertShadow(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float2 diff = float2(worldPos.x - _MutantWorldPos.x, worldPos.z - _MutantWorldPos.z);
                float proximity = (_MutantWorldPos.y < -100.0) ? 0.0 : saturate(1.0 - length(diff) / max(_ProximityRadius, 0.001));

                float rootFactor = IN.uv.y;
                IN.positionOS.x += sin(_Time.y * _SwaySpeed) * _SwayAmount * rootFactor
                                 + sin(_Time.y * _ProximitySpeed + worldPos.x * 3.0) * _ProximityAmount * rootFactor * proximity;

                // Shadows.hlsl 의 복잡한 함수 대신, HClip 변환 수학식 직접 사용 (완벽하게 안전함)
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(col.a - _AlphaCutoff);
                return 0; // 그림자 패스에서는 버릴 수 없는 픽셀만 통과시켜 깊이(Depth)를 기록합니다.
            }
            ENDHLSL
        }
        
        // --- Depth Only Pass ---
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _AlphaCutoff;
                
                float  _SwaySpeed;
                float  _SwayAmount;
                float4 _MutantWorldPos;
                float  _ProximityRadius;
                float  _ProximityAmount;
                float  _ProximitySpeed;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vertDepth(Attributes IN)
            {
                Varyings OUT;
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float2 diff = float2(worldPos.x - _MutantWorldPos.x, worldPos.z - _MutantWorldPos.z);
                float proximity = (_MutantWorldPos.y < -100.0) ? 0.0 : saturate(1.0 - length(diff) / max(_ProximityRadius, 0.001));

                float rootFactor = IN.uv.y;
                IN.positionOS.x += sin(_Time.y * _SwaySpeed) * _SwayAmount * rootFactor
                                 + sin(_Time.y * _ProximitySpeed + worldPos.x * 3.0) * _ProximityAmount * rootFactor * proximity;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 fragDepth(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(col.a - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
