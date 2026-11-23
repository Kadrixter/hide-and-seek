// Car body with a dust layer (forgotten cars on the lower levels of the garage). Same inputs as the Standard
// shader (_Color, _MainTex, _Metallic, _Glossiness, optional _BumpMap) plus the dust / grime controls of
// HnsDust.cginc. Dust flattens the normal, kills the metallic flake and the gloss.
Shader "Hns/CarDust"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal scale", Float) = 1
        _DustTex ("Dust noise (R grain, G blotches, B streaks)", 2D) = "gray" {}
        _DustColor ("Dust colour", Color) = (0.44, 0.42, 0.38, 1)
        _DustTop ("Dust on upward faces", Range(0, 1.5)) = 0.5
        _DustSide ("Dust on the sides", Range(0, 1)) = 0.1
        _GrimeColor ("Grime colour", Color) = (0.13, 0.11, 0.09, 1)
        _Grime ("Grime on the lower body", Range(0, 1)) = 0.2
        _GrimeHeight ("Grime height above the floor (m)", Float) = 0.45
        _FloorY ("Floor height of the level", Float) = 0
        _DustScale ("Noise tiling per metre", Float) = 0.8
        _Seed ("Noise offset", Vector) = (0, 0, 0, 0)
        _Haze ("Haze (glass only)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP
        #include "HnsDust.cginc"

        sampler2D _BumpMap;
        half _BumpScale;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 m = DustMask(IN);
            half dust = m.x, grime = m.y;
            fixed3 alb = lerp(c.rgb, _GrimeColor.rgb * (0.8 + 0.4 * m.z), grime);
            alb = lerp(alb, _DustColor.rgb * (0.82 + 0.36 * m.z), dust);
            o.Albedo = alb;
        #ifdef _NORMALMAP
            half3 nm = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Normal = normalize(lerp(nm, half3(0, 0, 1), saturate(dust * 1.5)));
        #endif
            o.Metallic = _Metallic * (1.0 - saturate(dust * 1.6)) * (1.0 - grime * 0.6);
            half s = lerp(_Glossiness, 0.1, saturate(dust * 1.35));
            o.Smoothness = lerp(s, 0.18, grime);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
