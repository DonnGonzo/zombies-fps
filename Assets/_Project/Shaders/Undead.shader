// The zombie model's skin. Takes the character's own textures and turns them into rotting flesh: the colour is
// replaced by a dead grey-green (keeping all the texture's detail as light and dark), deep red wounds stay bloody,
// and damp patches catch the light. _Color tints it per zombie type.
Shader "ZombiesFPS/Undead"
{
    Properties
    {
        _Color ("Type tint", Color) = (1,1,1,1)
        _MainTex ("Diffuse", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _GlossMap ("Glossiness", 2D) = "grey" {}
        _Rot ("Rot", Range(0,1)) = 1
        _Dark ("Rot dark", Color) = (0.05,0.06,0.05,1)
        _Pale ("Rot pale", Color) = (0.5,0.55,0.44,1)
        _Gore ("Gore", Range(0,1)) = 0.8
        _Wet ("Wetness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap, _GlossMap;
        fixed4 _Color, _Dark, _Pale;
        half _Rot, _Gore, _Wet;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half3 c = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half lum = dot(c, half3(0.3, 0.59, 0.11));
            half3 rot = lerp(_Dark.rgb, _Pale.rgb, saturate((lum - 0.15) * 1.7));
            // Only the deep reds (wounds, gums, torn muscle) stay blood-coloured.
            half red = saturate((c.r - max(c.g, c.b) - 0.15) * 4) * _Gore;
            half3 blood = c * half3(0.55, 0.16, 0.12);
            half3 dead = lerp(rot, blood, red * 0.85);
            o.Albedo = lerp(c, dead, _Rot) * _Color.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Smoothness = saturate(tex2D(_GlossMap, IN.uv_MainTex).r * _Wet + red * 0.25);
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
