Shader "EasterAd/UnifiedShader"
{
    Properties
    {
        _MainTex ("BackgroundTexture", 2D) = "white" {}
        _LogoTex ("LogoTexture", 2D) = "white" {}
    }

    // HDRP SubShader
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition": "10.0"
        }
        Tags{ "RenderPipeline" = "HDRenderPipeline" "RenderType" = "HDUnlitShader" }

        Pass
        {
            Name "DepthForwardOnly"
            Tags{ "LightMode" = "DepthForwardOnly" }

            HLSLPROGRAM

            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma multi_compile_fragment _ WRITE_MSAA_DEPTH

            #define SHADERPASS SHADERPASS_DEPTH_ONLY

            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/Unlit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/ShaderPass/UnlitDepthPass.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Unlit/UnlitData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            sampler2D _MainTex;
            sampler2D _LogoTex;
            float3 _ConstantData = float3(1.0, 1.0, 1.0); // x: planeScale.x, y: planeScale.y, z: drawLogo
            float4 _MainTex_TexelSize;
            float4 _LogoTex_TexelSize;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            #pragma vertex Vert
            #pragma fragment Frag

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                float planeRatio = _ConstantData.x / _ConstantData.y;
                float logoWidth = saturate(1.0 / planeRatio) * 0.9;

                float2 logoCenter = float2(0.5, 0.5);
                float logoRatio = _LogoTex_TexelSize.z / _LogoTex_TexelSize.w;
                float2 logoSizeOnPlane = float2(logoWidth, (logoWidth / logoRatio) * planeRatio);
                float2 logoLeftBottom = logoCenter - logoSizeOnPlane * 0.5;
                float2 logoRightTop = logoCenter + logoSizeOnPlane * 0.5;

                half4 tex = tex2D(_MainTex, i.uv);
                if(_ConstantData.z > 0.0 &&
                    i.uv.x > logoLeftBottom.x && i.uv.x < logoRightTop.x &&
                    i.uv.y > logoLeftBottom.y && i.uv.y < logoRightTop.y
                )
                {
                    float2 transformedUv = (i.uv - logoLeftBottom) * (float2(1.0 , 1.0) / logoSizeOnPlane);
                    half4 logoTex = tex2D(_LogoTex, transformedUv);
                    tex = lerp(tex, logoTex, logoTex.a);
                }

                return tex;
            }
            ENDHLSL
        }
    }

    // URP SubShader
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "10.0"
        }
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _LogoTex;
            float3 _ConstantData = float3(1.0, 1.0, 1.0); // x: planeScale.x, y: planeScale.y, z: drawLogo
            float4 _MainTex_TexelSize;
            float4 _LogoTex_TexelSize;

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float planeRatio = _ConstantData.x / _ConstantData.y;
                float logoWidth = saturate(1.0 / planeRatio) * 0.9;

                float2 logoCenter = float2(0.5, 0.5);
                float logoRatio = _LogoTex_TexelSize.z / _LogoTex_TexelSize.w;
                float2 logoSizeOnPlane = float2(logoWidth, (logoWidth / logoRatio) * planeRatio);
                float2 logoLeftBottom = logoCenter - logoSizeOnPlane * 0.5;
                float2 logoRightTop = logoCenter + logoSizeOnPlane * 0.5;

                half4 tex = tex2D(_MainTex, i.uv);
                if(_ConstantData.z > 0.0 &&
                    i.uv.x > logoLeftBottom.x && i.uv.x < logoRightTop.x &&
                    i.uv.y > logoLeftBottom.y && i.uv.y < logoRightTop.y
                )
                {
                    float2 transformedUv = (i.uv - logoLeftBottom) * (float2(1.0 , 1.0) / logoSizeOnPlane);
                    half4 logoTex = tex2D(_LogoTex, transformedUv);
                    tex = lerp(tex, logoTex, logoTex.a);
                }

                return tex;
            }
            ENDCG
        }
    }

    // Built-in RP SubShader (Fallback)
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _LogoTex;
            float3 _ConstantData = float3(1.0, 1.0, 1.0); // x: planeScale.x, y: planeScale.y, z: drawLogo
            float4 _MainTex_TexelSize;
            float4 _LogoTex_TexelSize;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float planeRatio = _ConstantData.x / _ConstantData.y;
                float logoWidth = saturate(1.0 / planeRatio) * 0.9;

                float2 logoCenter = float2(0.5, 0.5);
                float logoRatio = _LogoTex_TexelSize.z / _LogoTex_TexelSize.w;
                float2 logoSizeOnPlane = float2(logoWidth, (logoWidth / logoRatio) * planeRatio);
                float2 logoLeftBottom = logoCenter - logoSizeOnPlane * 0.5;
                float2 logoRightTop = logoCenter + logoSizeOnPlane * 0.5;

                float4 tex = tex2D(_MainTex, i.uv);
                if(_ConstantData.z > 0.0 &&
                    i.uv.x > logoLeftBottom.x && i.uv.x < logoRightTop.x &&
                    i.uv.y > logoLeftBottom.y && i.uv.y < logoRightTop.y
                )
                {
                    float2 transformedUv = (i.uv - logoLeftBottom) * (float2(1.0 , 1.0) / logoSizeOnPlane);
                    fixed4 logoTex = tex2D(_LogoTex, transformedUv);
                    tex = lerp(tex, logoTex, logoTex.a);
                }

                return tex;
            }
            ENDCG
        }
    }
}