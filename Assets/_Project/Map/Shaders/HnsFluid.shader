// Fluid leaked from under a car (engine oil, coolant, brake fluid): a flat decal over the floor.
// _MainTex: R = film thickness (0 = dry floor), G = noise. Thin spots show a rainbow film in the reflections
// (oil), the edge dries to a darker ring, the wet middle is glossy. Premultiplied, so the highlights stay
// strong where the film is almost clear.
Shader "Hns/Fluid"
{
    Properties
    {
        _MainTex ("Thickness (R) noise (G)", 2D) = "black" {}
        _Color ("Fluid colour (a = opacity of a thick film)", Color) = (0.03, 0.025, 0.02, 0.9)
        _EdgeColor ("Dried edge", Color) = (0.05, 0.045, 0.04, 1)
        _Sheen ("Rainbow film", Range(0, 1)) = 0.8
        _Spec ("Specular level", Range(0, 1)) = 0.06
        _Smooth ("Wet smoothness", Range(0, 1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }
        LOD 200
        Offset -1, -1
        CGPROGRAM
        #pragma surface surf StandardSpecular alpha:premul nofog
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _EdgeColor;
        half _Sheen, _Spec, _Smooth;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            half4 t = tex2D(_MainTex, IN.uv_MainTex);
            half th = t.r;
            half cover = smoothstep(0.015, 0.09, th);
            half rim = cover * (1.0 - smoothstep(0.07, 0.24, th));
            float3 v = normalize(_WorldSpaceCameraPos - IN.worldPos);
            half graze = 1.0 - saturate(v.y);
            half phase = th * 2.6 + t.g * 1.7 + graze * 0.9;
            half3 rainbow = saturate(abs(frac(phase + half3(0.0, 0.333, 0.667)) * 6.0 - 3.0) - 1.0);
            half film = _Sheen * smoothstep(0.04, 0.25, th) * (1.0 - smoothstep(0.55, 0.95, th) * 0.6);
            o.Albedo = lerp(_Color.rgb, _EdgeColor.rgb, rim * 0.8);
            o.Specular = lerp(_Spec.xxx, rainbow * 0.32 + 0.03, film) * cover;
            o.Smoothness = lerp(0.55, _Smooth, smoothstep(0.06, 0.22, th)) * cover;
            o.Alpha = cover * saturate(_Color.a * (0.45 + 0.75 * th) + rim * 0.35);
        }
        ENDCG
    }
    FallBack Off
}
