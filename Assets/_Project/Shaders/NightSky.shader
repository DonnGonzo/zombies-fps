// The night sky: a gradient that meets the fog at the horizon, stars, a hazy moon and slow-moving clouds.
// _ZFBlackout (a global set during blackout rounds) dims it all.
Shader "ZombiesFPS/NightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.02, 0.03, 0.07, 1)
        _MoonColor ("Moon", Color) = (0.85, 0.9, 1, 1)
        _MoonDir ("Moon direction", Vector) = (0.4, 0.55, -0.7, 0)
        _CloudColor ("Clouds", Color) = (0.1, 0.11, 0.15, 1)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _MoonColor, _CloudColor;
            float4 _MoonDir;
            float _ZFBlackout;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);
                f = f * f * (3 - 2 * f);
                float a = Hash(float3(i, 0));
                float b = Hash(float3(i + float2(1, 0), 0));
                float c = Hash(float3(i + float2(0, 1), 0));
                float d = Hash(float3(i + float2(1, 1), 0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 x)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * Noise(x);
                    x = x * 2.03 + 11.7;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                half3 horizon = unity_FogColor.rgb;
                half3 col = lerp(horizon, _Zenith.rgb, pow(up, 0.45));

                // Stars: one per cell at most, twinkling, hidden near the horizon haze.
                float3 cell = floor(d * 140);
                float h = Hash(cell);
                float3 centre = (cell + 0.5) / 140;
                float star = step(0.985, h) * saturate(1 - length(d - normalize(centre)) * 260);
                star *= 0.6 + 0.4 * sin(_Time.y * (1.5 + h * 4) + h * 40);
                col += star * smoothstep(0.08, 0.35, d.y) * 1.4;

                // Moon disc and halo.
                float3 m = normalize(_MoonDir.xyz);
                float md = dot(d, m);
                col += _MoonColor.rgb * smoothstep(0.9993, 0.9996, md) * 2.5;
                col += _MoonColor.rgb * pow(saturate(md), 90) * 0.35;
                col += _MoonColor.rgb * pow(saturate(md), 8) * 0.06;

                // Clouds on a flat layer overhead, drifting.
                float2 uv = d.xz / (d.y + 0.25) * 1.6 + _Time.y * float2(0.012, 0.004);
                float c = smoothstep(0.45, 0.8, Fbm(uv));
                half3 cloud = _CloudColor.rgb + _MoonColor.rgb * pow(saturate(md), 6) * 0.12;
                col = lerp(col, cloud, c * smoothstep(0.0, 0.25, d.y) * 0.85);

                col *= 1 - _ZFBlackout * 0.8;
                return half4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
