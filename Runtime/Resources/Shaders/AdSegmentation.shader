Shader "EasterAd/AdSegmentation"
{
    Properties
    {
        _adSegmentationId ("Ad Segmentation Id", Int) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "AdSegmentationPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
            uint _adSegmentationId;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // Phase 4 Compute Shader: ID를 0~1 범위로 변환하여 RenderTexture에 저장
                // Compute Shader가 이 텍스처를 읽어서 픽셀 카운팅 수행
                float val = _adSegmentationId / 255.0;
                return float4(val, 0, 0, 1.0);  // R 채널에만 ID 저장
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
