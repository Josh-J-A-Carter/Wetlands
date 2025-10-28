Shader "Unlit/Tree"
{
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f {
                float2 uv2 : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv2 = TRANSFORM_TEX(v.uv2, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float u = i.uv2.x;
                fixed4 col = fixed4(u, u, u, 1.0);
                return col;
            }
            ENDCG
        }
    }
}
