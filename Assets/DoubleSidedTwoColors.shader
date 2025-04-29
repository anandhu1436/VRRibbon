Shader "Custom/DoubleSidedMetallic"
{
    Properties
    {
        _FrontColor("Front Albedo", Color) = (1,1,1,1)
        _BackColor("Back Albedo", Color) = (1,0,0,1)
        _Metallic("Metallic", Range(0,1)) = 0.5
        _Smoothness("Smoothness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off // render both sides

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _FrontColor;
        fixed4 _BackColor;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            float3 worldNormal;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float facing = dot(IN.worldNormal, IN.viewDir);

            fixed4 albedo = (facing >= 0) ? _FrontColor : _BackColor;

            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = albedo.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
