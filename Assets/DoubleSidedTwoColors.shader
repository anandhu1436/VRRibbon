Shader "Custom/DoubleSidedTwoColors"
{
    Properties
    {
        _FrontColor("Front Face Color", Color) = (1,1,1,1)
        _BackColor("Back Face Color", Color) = (1,0,0,1)
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off  // Renders both sides

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            fixed4 _FrontColor;
            fixed4 _BackColor;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Determine if it's the front or back face
                bool isFrontFace = dot(i.worldNormal, i.viewDir) > 0;
                return isFrontFace ? _FrontColor : _BackColor;
            }
            ENDCG
        }
    }
}
