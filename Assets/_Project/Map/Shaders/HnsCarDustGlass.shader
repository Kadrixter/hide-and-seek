// Car glass / lamp lenses under dust: the transparent glass of the cars (premultiplied like the Standard
// "Transparent" mode) turns milky (_Haze) and gets an opaque dust film on upward faces (windscreen, rear window).
Shader "Hns/CarDustGlass"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.6)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _DustTex ("Dust noise (R grain, G blotches, B streaks)", 2D) = "gray" {}
        _DustColor ("Dust colour", Color) = (0.44, 0.42, 0.38, 1)
        _DustTop ("Dust on upward faces", Range(0, 1.5)) = 0.5
        _DustSide ("Dust on the sides", Range(0, 1)) = 0.1
        _GrimeColor ("Grime colour", Color) = (0.13, 0.11, 0.09, 1)
        _Grime ("Grime on the lower body", Range(0, 1)) = 0
        _GrimeHeight ("Grime height above the floor (m)", Float) = 0.45
        _FloorY ("Floor height of the level", Float) = 0
        _DustScale ("Noise tiling per metre", Float) = 0.8
        _Seed ("Noise offset", Vector) = (0, 0, 0, 0)
        _Haze ("Milky haze", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard alpha:premul vertex:vert
        #pragma target 3.0
        #include "HnsDust.cginc"

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 m = DustMask(IN);
            half dust = saturate(m.x * 1.1);
            half haze = _Haze * (0.75 + 0.5 * m.z);
            fixed3 hazeCol = _DustColor.rgb * 0.75;
            fixed3 alb = lerp(c.rgb, hazeCol, saturate(haze));
            alb = lerp(alb, _DustColor.rgb * (0.82 + 0.36 * m.z), dust);
            o.Albedo = alb;
            o.Metallic = _Metallic * (1.0 - saturate(dust + haze));
            o.Smoothness = lerp(lerp(_Glossiness, 0.45, saturate(haze)), 0.12, dust);
            o.Alpha = saturate(c.a + (1.0 - c.a) * saturate(haze * 0.85 + dust * 0.95));
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
