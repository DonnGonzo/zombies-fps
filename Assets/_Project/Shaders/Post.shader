// Post effects for the player's camera (see PostFx.cs): bloom, filmic tone mapping, a cold colour grade,
// vignette and film grain.
Shader "Hidden/ZombiesFPS/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Threshold;          // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    half _BloomIntensity;
    half _Exposure;
    half _Saturation;
    half _Contrast;
    half _Vignette;
    half _Grain;
    half4 _ShadowTint;
    half4 _HighlightTint;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 ToLinear(half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return GammaToLinearSpace(c);
    #else
        return c;
    #endif
    }

    half3 ToOutput(half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return LinearToGammaSpace(c);
    #else
        return c;
    #endif
    }

    half3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    half3 Prefilter(half3 c)
    {
        half brightness = max(c.r, max(c.g, c.b));
        half soft = brightness - _Threshold.y;
        soft = clamp(soft, 0, _Threshold.z);
        soft = soft * soft * _Threshold.w;
        half contribution = max(soft, brightness - _Threshold.x);
        contribution /= max(brightness, 0.00001);
        return c * contribution;
    }

    // Filmic curve (Narkowicz's fit of ACES).
    half3 Aces(half3 x)
    {
        return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
    }

    half4 FragPrefilter(v2f i) : SV_Target
    {
        half3 c = ToLinear(Box4(i.uv, 1));
        return half4(Prefilter(c), 1);
    }

    half4 FragDown(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, 1), 1);
    }

    half4 FragUp(v2f i) : SV_Target
    {
        return half4(Box4(i.uv, 0.5), 1);
    }

    half4 FragComposite(v2f i) : SV_Target
    {
        half3 c = ToLinear(tex2D(_MainTex, i.uv).rgb);
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure;
        c = Aces(c);

        // Grade: cold shadows, warm highlights, a little less colour and a little more contrast.
        half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c *= lerp(_ShadowTint.rgb, _HighlightTint.rgb, smoothstep(0.05, 0.6, luma));
        luma = dot(c, half3(0.2126, 0.7152, 0.0722));
        c = lerp(luma.xxx, c, _Saturation);
        c = saturate((c - 0.18) * _Contrast + 0.18);

        // Vignette.
        float2 d = (i.uv - 0.5) * float2(_MainTex_TexelSize.y * _MainTex_TexelSize.z, 1);
        c *= 1 - _Vignette * smoothstep(0.25, 0.85, dot(d, d) * 2.2);

        c = ToOutput(c);
        // Grain, added after the curve so it reads evenly.
        float n = frac(sin(dot(i.uv * _ScreenParams.xy + frac(_Time.y * 37.0) * 91.0, float2(12.9898, 78.233))) * 43758.5453);
        c += (n - 0.5) * _Grain;
        return half4(c, 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragPrefilter
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragDown
            ENDCG }

        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragUp
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert
            #pragma fragment FragComposite
            ENDCG }
    }
    FallBack Off
}
