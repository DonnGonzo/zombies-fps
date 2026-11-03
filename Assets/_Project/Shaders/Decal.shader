// Blood splats, scorch marks and bullet holes: a lit, alpha-blended quad laid on a surface.
Shader "ZombiesFPS/Decal"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Decal (RGBA)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+20" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        Offset -1, -1
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard decal:blend
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color * IN.color;
            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness * c.a;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack Off
}
