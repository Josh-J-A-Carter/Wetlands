Shader "Custom/Foliage Shader" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _TreeCentreWS ("Tree Centre (World Space)", Vector) = (0, 0, 0, 0)
    }

    SubShader {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Cull Off

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct attributes {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct interpolators {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            interpolators vert(attributes input) {
                interpolators output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS);
                output.positionCS = positions.positionCS;

                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            float4 frag(interpolators input) : SV_Target {
                float4 col = tex2D(_MainTex, input.uv);

                if (col.a == 0) discard;

                return 0;
            }

            ENDHLSL
        }

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct interpolators {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoords : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Tint;

            float3 _TreeCentreWS;

            interpolators vert (attributes input) {
                interpolators output;
                
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positions.positionCS;
                // Foliage normals are calculated from the centre of the tree
                output.normalWS = positions.positionWS - _TreeCentreWS;
                
                // Shadows
                output.shadowCoords = GetShadowCoord(positions);
                
                // output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // Get the VertexNormalInputs of the vertex, which contains the normal in world space
                // VertexNormalInputs positions = GetVertexNormalInputs(input.positionOS);
                // output.normalWS = positions.normalWS.xyz;
                // Get the properties of the main light
                // Light light = GetMainLight();
                // output.lightAmount = LightingLambert(light.color, light.direction, o.normalWS.xyz);
                
                return output;
            }

            float4 frag (interpolators input) : SV_Target {
                float4 col = tex2D(_MainTex, input.uv);

                if (col.a == 0) discard;

                float shadowAmount = MainLightRealtimeShadow(input.shadowCoords);

                Light light = GetMainLight();
                float3 lightAmount = LightingLambert(light.color, light.direction, normalize(input.normalWS));
                lightAmount *= shadowAmount;
                
                float3 lightAmbient = float3(0.2, 0.2, 0.2);
                lightAmount += lightAmbient;
                lightAmount = clamp(lightAmount, float3(0, 0, 0), float3(1, 1, 1));

                return col * _Tint * float4(lightAmount, 1.0);
            }

            ENDHLSL
        }
    }
}
