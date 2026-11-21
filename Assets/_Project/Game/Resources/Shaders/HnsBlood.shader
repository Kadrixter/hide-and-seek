// Wet blood decal: lit (PBR), glossy, with a normal map from the splat's thickness, alpha-faded edges.
// Used by BloodFx for pools, splats on walls, runs, drops and bloody footprints, and (alpha 1) for stumps.
Shader "HideAndSeek/Blood"
{
    Properties
    {
        _MainTex ("Splat (RGB shade, A coverage)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Color ("Color", Color) = (0.4, 0.02, 0.02, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.66
        _Wet ("Wet (1 fresh .. 0 dried)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+60" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 200
        ZWrite Off
        Offset -2, -2

        CGPROGRAM
        #pragma surface surf Standard alpha:fade nolightmap nodynlightmap nodirlightmap nometa
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _Glossiness;
        half _Wet;

        struct Input { float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 t = tex2D(_MainTex, IN.uv_MainTex);
            // Drying blood darkens to brown and loses its shine.
            fixed3 dried = _Color.rgb * fixed3(0.55, 0.42, 0.38);
            o.Albedo = lerp(dried, _Color.rgb, _Wet) * t.rgb;
            o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            o.Smoothness = _Glossiness * lerp(0.35, 1.0, _Wet);
            o.Metallic = 0;
            // Indoors: no sky reflection (it turned pools pale blue at grazing angles); lamps still give a wet shine.
            o.Occlusion = 0.12;
            o.Alpha = t.a * _Color.a;
        }
        ENDCG
    }
    Fallback Off
}
