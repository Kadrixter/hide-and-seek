// Lit (PBR) shader of the trash bin, its liner bag and the crumpled papers (TrashBinProp). Like Standard, but:
// vertex colour = baked occlusion (rgb darkens the albedo in folds / dents / inside the bin, a = ambient occlusion),
// texture alpha = smoothness mask (perforation holes, scratches, grime), normal map always on (no keyword variants
// that could be stripped from the build).
Shader "HideAndSeek/TrashProp"
{
    Properties
    {
        _MainTex ("Albedo (RGB), smoothness mask (A)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float4 vc;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.vc = v.color;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 t = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = t.rgb * _Color.rgb * IN.vc.rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Smoothness = _Glossiness * t.a;
            o.Metallic = _Metallic;
            o.Occlusion = IN.vc.a;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
