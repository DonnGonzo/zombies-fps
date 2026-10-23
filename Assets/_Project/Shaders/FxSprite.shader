// Camera-facing effect sprites from Ivan's Brackeys VFX bundle (Resources/Fx): explosions, smoke and muzzle flashes.
// _Grid/_Frame pick one cell of a flipbook sheet (cells read left to right, top to bottom). _Additive 1 adds light
// (fire, flashes; fades to nothing in fog), 0 blends over (smoke; fades into the fog colour). _Intensity above 1
// pushes fire past white for the bloom.
Shader "ZombiesFPS/FxSprite"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Grid ("Columns, rows", Vector) = (1,1,0,0)
        _Frame ("Frame", Float) = 0
        _Additive ("Additive", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            half _Intensity, _Frame, _Additive;
            float4 _Grid;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 grid = max(_Grid.xy, 1);
                float frame = floor(_Frame);
                float col = fmod(frame, grid.x);
                float row = grid.y - 1 - floor(frame / grid.x);
                o.uv = (v.texcoord.xy + float2(col, row)) / grid;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * _Color;
                c.rgb *= _Intensity;
                if (_Additive > 0.5)
                {
                    c.rgb *= c.a;
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0, 0, 0, 0));
                }
                else
                {
                    UNITY_APPLY_FOG(i.fogCoord, c);
                }
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
