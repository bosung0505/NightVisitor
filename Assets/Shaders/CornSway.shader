// URP (Universal Render Pipeline) 전용 옥수수 흔들림 셰이더
// Alpha Clipping 모드: URP/Lit Opaque + Alpha Clipping과 동일한 렌더링 방식
Shader "Custom/CornSway_URP"
{
    Properties
    {
        _MainTex ("Corn Texture (PNG)", 2D) = "white" {}

        [HDR]
        _Color ("Color Tint", Color) = (1,1,1,1)

        [Header(AlphaClipping)]
        _AlphaCutoff ("Alpha Clip Threshold", Range(0.0, 1.0)) = 0.5

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
        // ★ AlphaTest Queue: Opaque(2000)와 Transparent(3000) 사이
        // URP/Lit의 Opaque + Alpha Clipping과 동일한 렌더 순서
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
        }

        // ★ ZWrite On: 깊이 버퍼에 기록 → 다른 오브젝트와 올바르게 depth 정렬
        ZWrite On
        // Blend 없음 → 임계값 이상 픽셀은 완전 불투명 (URP/Lit Opaque와 동일)
        Cull Off   // 양면 렌더 (뒤에서도 보이게)

        Pass
        {
            Name "AlphaClipForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ─── CBUFFER (SRP Batcher 호환) ────────────────────────────
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);

                // 뮤턴트 근접 거리 계산
                float2 diff      = float2(worldPos.x - _MutantWorldPos.x,
                                          worldPos.z - _MutantWorldPos.z);
                float  dist      = length(diff);
                float  proximity = (_MutantWorldPos.y < -100.0)
                                    ? 0.0
                                    : saturate(1.0 - dist / max(_ProximityRadius, 0.001));

                float rootFactor = IN.uv.y;

                float baseSway = sin(_Time.y * _SwaySpeed) * _SwayAmount * rootFactor;
                float proxSway = sin(_Time.y * _ProximitySpeed + worldPos.x * 3.0)
                                 * _ProximityAmount * rootFactor * proximity;

                IN.positionOS.x += baseSway + proxSway;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Color Tint 적용
                col *= _Color;

                // ★ Alpha Clipping: 임계값 미만 픽셀 완전 제거
                // URP/Lit Opaque+AlphaClip과 동일 방식
                clip(col.a - _AlphaCutoff);

                // 임계값 이상 픽셀은 완전 불투명으로 반환 (alpha 1로 고정)
                return half4(col.rgb, 1.0);
            }
            ENDHLSL
        }

        // ─── 그림자 패스 (그림자도 AlphaClip 적용) ────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vertShadow(Attributes IN)
            {
                Varyings OUT;
                // 흔들림 동일하게 적용 (그림자도 흔들리게)
                float3 worldPos  = TransformObjectToWorld(IN.positionOS.xyz);
                float2 diff      = float2(worldPos.x - _MutantWorldPos.x, worldPos.z - _MutantWorldPos.z);
                float  proximity = (_MutantWorldPos.y < -100.0)
                                    ? 0.0
                                    : saturate(1.0 - length(diff) / max(_ProximityRadius, 0.001));
                float rootFactor = IN.uv.y;
                IN.positionOS.x += sin(_Time.y * _SwaySpeed) * _SwayAmount * rootFactor
                                 + sin(_Time.y * _ProximitySpeed + worldPos.x * 3.0) * _ProximityAmount * rootFactor * proximity;

                float3 normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS  = TransformWorldToHClip(ApplyShadowBias(
                                       TransformObjectToWorld(IN.positionOS.xyz), normalWS, _LightDirection));
                OUT.uv           = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 fragShadow(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                clip(col.a - _AlphaCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
