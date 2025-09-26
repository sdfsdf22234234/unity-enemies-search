Shader "Custom/HpBarShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("HP Color", Color) = (0.886,0.753,0.702,1)         
        _UserdataVec4 ("User Data", Vector) = (0.5,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        LOD 100
        
        Pass
        {
            Name "HpBarPass"
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ DOTS_INSTANCING_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 userData : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float hpRatio : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // 定义固定值
           // static const float4 _BgColor = float4(0.404, 0.678, 0.337, 1);
            static const float4 _BgColor = float4(0.0, 1.0, 0.0, 1);
            static const float4 _BorderColor = float4(0, 0, 0, 1);
            static const float _GlowIntensity = 1;
            static const float _GlowWidth = 0;
            
            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _Color)
                    UNITY_DOTS_INSTANCED_PROP(float4, _UserdataVec4)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _Color UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _Color)
                #define _UserdataVec4 UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _UserdataVec4)
            #else
                CBUFFER_START(UnityPerMaterial)
                    float4 _MainTex_ST;
                    float4 _Color;
                    float4 _UserdataVec4;
                CBUFFER_END
            #endif

            void setup() {}
            
                v2f vert(appdata v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_TRANSFER_INSTANCE_ID(v, o);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                    // 1. 将世界坐标转换到视图空间
                    float4 viewPos = mul(UNITY_MATRIX_MV, float4(0, 0, 0, 1));

                    // 2. 在视图空间添加顶点偏移
                    viewPos += float4(v.vertex.x, v.vertex.y, 0, 0);

                    // 3. 投影到屏幕空间
                    o.vertex = mul(UNITY_MATRIX_P, viewPos);

                    o.uv = float2(1 - v.uv.x, v.uv.y);
                    o.color = v.color;
                    // 直接使用血量值，不进行反转
                    o.hpRatio = 1-_UserdataVec4.x;

                    return o;
                }

            float4 AddGlow(float4 color, float distance, float intensity)
            {
                return color + color * smoothstep(1.0, 0.0, distance) * intensity;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 确保血量比例在0-1范围内
                float hpRatio = saturate(i.hpRatio);

                // 血条颜色（使用垂直渐变）
                float healthGradient = 1.0 - abs(i.uv.y - 0.5) * 2.0;
                float4 hpColor = lerp(_Color * 0.7, _Color, healthGradient);
                
                // 背景颜色（添加垂直渐变）
                float4 bgColor = lerp(_BgColor * 0.7, _BgColor, healthGradient);
                
                // 添加发光效果
                float glowDistance = abs(i.uv.x - hpRatio);
                hpColor = AddGlow(hpColor, glowDistance / _GlowWidth, _GlowIntensity);
                
                // 从左到右的平滑过渡
                float smoothTransition = smoothstep(hpRatio - 0.02, hpRatio + 0.02,   i.uv.x);
                float4 finalColor = lerp(hpColor, bgColor, smoothTransition);
                
                // 添加边框
                float borderSize = 0.05;
                float2 border = abs(i.uv - 0.5) * 2;
                float borderMask = max(border.x, border.y);
                
                // 平滑边框过渡
                float smoothBorder = smoothstep(0.9, 1.0, borderMask);
                finalColor = lerp(finalColor, _BorderColor, smoothBorder);
                
                // 内部发光
              //  float innerGlow = 1.0 - saturate(abs(i.uv.x - hpRatio) / _GlowWidth);
               // finalColor += hpColor * innerGlow * _GlowIntensity * smoothstep(hpRatio + 0.02, hpRatio - 0.02, i.uv.x);
                
                // 顶部高光
               // float highlight = smoothstep(0.48, 0.52, i.uv.y);
               // finalColor += float4(1,1,1,0) * highlight * 0.1 * smoothstep(hpRatio + 0.02, hpRatio - 0.02, i.uv.x);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}