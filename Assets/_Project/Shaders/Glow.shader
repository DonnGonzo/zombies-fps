// Unlit, self-lit colour for lamps, eyes, signs and glowing parts. _Intensity above 1 pushes the colour past
// white so the bloom post effect picks it up. Fades into the fog like everything else.
Shader "ZombiesFPS/Glow"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Intensity;

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = half4(_Color.rgb * _Intensity, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
