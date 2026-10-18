// The game's main lit material. Detail and normal textures are projected from three sides (triplanar), so
// stretched greybox cubes get evenly sized bricks, planks and cracks without any UVs. World-space projection
// for the map; object-space (_ObjectSpace = 1) for things that move, so their texture doesn't swim.
Shader "ZombiesFPS/Surface"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Detail (RGB) Height (A)", 2D) = "grey" {}
        _BumpMap ("Normal (RG)", 2D) = "grey" {}
        _Scale ("Tiles per metre", Float) = 0.5
        _Detail ("Detail strength", Range(0,1)) = 1
        _BumpStrength ("Normal strength", Range(0,2)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _Grime ("Grime", Range(0,1)) = 0.35
        _ObjectSpace ("Object space", Float) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half4 _EmissionColor;
        float _Scale;
        half _Detail, _BumpStrength, _Glossiness, _Metallic, _Grime, _ObjectSpace;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        // World-space direction to this pixel's tangent space (what o.Normal expects).
        float3 WorldToTangent(Input IN, float3 n)
        {
            float3 t2w0 = WorldNormalVector(IN, float3(1, 0, 0));
            float3 t2w1 = WorldNormalVector(IN, float3(0, 1, 0));
            float3 t2w2 = WorldNormalVector(IN, float3(0, 0, 1));
            float3x3 t2w = float3x3(t2w0, t2w1, t2w2);
            return normalize(mul(t2w, n));
        }

        half3 UnpackRG(half4 c, half strength)
        {
            half3 n;
            n.xy = (c.rg * 2 - 1) * strength;
            n.z = sqrt(saturate(1 - dot(n.xy, n.xy)));
            return n;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 worldN = normalize(WorldNormalVector(IN, float3(0, 0, 1)));
            float3 p = IN.worldPos;
            float3 n = worldN;
            float3 sc = float3(1, 1, 1);
            if (_ObjectSpace > 0.5)
            {
                // Object space in metres: the object's own axes, but its scale kept, so tiles stay a fixed size.
                sc = float3(length(unity_ObjectToWorld._m00_m10_m20), length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                p = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz * sc;
                n = normalize(mul(worldN, (float3x3)unity_ObjectToWorld) / sc);
            }

            float3 blend = pow(abs(n), 4);
            blend /= max(dot(blend, float3(1, 1, 1)), 1e-4);

            float2 uvX = p.zy * _Scale;
            float2 uvY = p.xz * _Scale;
            float2 uvZ = p.xy * _Scale;

            half4 cX = tex2D(_MainTex, uvX);
            half4 cY = tex2D(_MainTex, uvY);
            half4 cZ = tex2D(_MainTex, uvZ);
            half4 detail = cX * blend.x + cY * blend.y + cZ * blend.z;

            // Big, slow blotches of dirt and damp from the same texture at a much larger size.
            float g = _Scale * 0.11;
            half blotch = tex2D(_MainTex, p.zy * g + 0.37).a * blend.x + tex2D(_MainTex, p.xz * g + 0.71).a * blend.y + tex2D(_MainTex, p.xy * g + 0.13).a * blend.z;
            half grime = saturate((0.55 - blotch) * 2.2) * _Grime;
            // Dirt collects at the foot of walls.
            if (_ObjectSpace < 0.5) grime += saturate(1 - IN.worldPos.y / 1.4) * (1 - blend.y) * _Grime * 0.45;
            grime = saturate(grime);

            half3 albedo = _Color.rgb * lerp(half3(1, 1, 1), detail.rgb * 2, _Detail);
            albedo *= lerp(1, 0.55, grime);
            o.Albedo = saturate(albedo);
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness * lerp(0.6, 1.25, detail.a) * (1 - grime * 0.6));
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;

            // Whiteout-blended triplanar normals.
            half3 tX = UnpackRG(tex2D(_BumpMap, uvX), _BumpStrength);
            half3 tY = UnpackRG(tex2D(_BumpMap, uvY), _BumpStrength);
            half3 tZ = UnpackRG(tex2D(_BumpMap, uvZ), _BumpStrength);
            tX = half3(tX.xy + n.zy, abs(tX.z) * n.x);
            tY = half3(tY.xy + n.xz, abs(tY.z) * n.y);
            tZ = half3(tZ.xy + n.xy, abs(tZ.z) * n.z);
            float3 bent = normalize(tX.zyx * blend.x + tY.xzy * blend.y + tZ.xyz * blend.z);
            if (_ObjectSpace > 0.5) bent = normalize(mul((float3x3)unity_ObjectToWorld, bent / sc));
            o.Normal = WorldToTangent(IN, bent);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
