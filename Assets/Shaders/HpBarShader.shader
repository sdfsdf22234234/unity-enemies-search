Shader "Custom/HpBarShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("HP Color", Color) = (1,0,0,1)        // 血条颜色（默认红色）
        _BgColor ("Background Color", Color) = (0.3,0.3,0.3,0.8)  // 背景颜色
        _ClipId ("Clip ID", Vector) = (0,0,0,0)
        _UserdataVec4 ("User Data", Vector) = (0,0,0,0)
        _BorderColor ("Border Color", Color) = (0.1,0.1,0.1,1)    // 边框颜色
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.5     // 发光强度
        _GlowWidth ("Glow Width", Range(0, 0.1)) = 0.02         // 发光宽度
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
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _BgColor;
                float4 _BorderColor;
                float _GlowIntensity;
                float _GlowWidth;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(float4, _ClipId)
                    UNITY_DOTS_INSTANCED_PROP(float4, _UserdataVec4)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

                #define _ClipId UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _ClipId)
                #define _UserdataVec4 UNITY_ACCESS_DOTS_INSTANCED_PROP(float4, _UserdataVec4)
            #endif

            void setup() {}
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;  // 使用顶点色和血条颜色
                o.hpRatio = v.userData.x;
                return o;
            }

            float4 AddGlow(float4 color, float distance, float intensity)
            {
                return color + color * smoothstep(1.0, 0.0, distance) * intensity;
            }
            
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // 血条颜色（添加垂直渐变）
                float healthGradient = 1.0 - abs(i.uv.y - 0.5) * 2.0;
                float4 hpColor = lerp(i.color * 0.7, i.color, healthGradient);
                
                // 背景颜色（添加垂直渐变）
                float4 bgColor = lerp(_BgColor * 0.7, _BgColor, healthGradient);
                
                // 添加发光效果
                float glowDistance = abs(i.uv.x - i.hpRatio);
                hpColor = AddGlow(hpColor, glowDistance / _GlowWidth, _GlowIntensity);
                
                // 在血量比例范围内显示血条颜色，否则显示背景色
                // 添加平滑过渡
                float transition = smoothstep(i.hpRatio - 0.01, i.hpRatio + 0.01, i.uv.x);
                float4 finalColor = lerp(hpColor, bgColor, transition);
                
                // 添加边框 - 使用更平滑的过渡
                float borderSize = 0.05;
                float2 border = abs(i.uv - 0.5) * 2;
                float borderMask = max(border.x, border.y);
                
                // 增加边框过渡的平滑度
                float smoothBorder = smoothstep(0.85, 0.95, borderMask);
                finalColor = lerp(finalColor, _BorderColor, smoothBorder);
                
                // 添加内发光
                float innerGlow = 1.0 - saturate(abs(i.uv.x - i.hpRatio) / _GlowWidth);
                finalColor += hpColor * innerGlow * _GlowIntensity * step(i.uv.x, i.hpRatio);
                
                // 添加顶部高光
                float highlight = smoothstep(0.48, 0.52, i.uv.y);
                finalColor += float4(1,1,1,0) * highlight * 0.1 * step(i.uv.x, i.hpRatio);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
} 