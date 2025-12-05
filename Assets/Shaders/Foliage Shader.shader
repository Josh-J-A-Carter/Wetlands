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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Tint;

            float3 _TreeCentreWS;

            v2f vert (appdata v) {
                v2f o;
                
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                VertexPositionInputs positions = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionCS = positions.positionCS;
                // Foliage normals are calculated from the centre of the tree
                o.normalWS = positions.positionWS - _TreeCentreWS;

                
                // o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // Get the VertexNormalInputs of the vertex, which contains the normal in world space
                // VertexNormalInputs positions = GetVertexNormalInputs(v.positionOS);
                // o.normalWS = positions.normalWS.xyz;
                // Get the properties of the main light
                // Light light = GetMainLight();
                // o.lightAmount = LightingLambert(light.color, light.direction, o.normalWS.xyz);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);

                if (col.a == 0) discard;

                // return float4(normalize(i.normalWS), 1);

                Light light = GetMainLight();
                float3 lightAmount = LightingLambert(light.color, light.direction, normalize(i.normalWS));

                return col * _Tint * float4(lightAmount, 1);
            }

            ENDHLSL
        }
    }
}
