// Dusty glass of the wrecked Golf: a dark, see-through pane whose dust layer (thicker on faces looking up) turns it milky,
// plus an optional crack mask projected on a plane in object space (spider web round an impact, crushed milky centre).
Shader "HideAndSeek/WreckGlass"
{
    Properties
    {
        _Color ("Glass tint (alpha = opacity)", Color) = (0.08, 0.1, 0.11, 0.5)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _DustTex ("Dust (R grain, G blotches, B streaks, A specks)", 2D) = "gray" {}
        _DustColor ("Dust colour", Color) = (0.5, 0.48, 0.44, 1)
        _DustTop ("Dust on faces looking up", Range(0, 1.5)) = 0.8
        _DustSide ("Dust on vertical faces", Range(0, 1)) = 0.3
        _DustScale ("Dust tiling (per metre)", Float) = 0.9
        [NoScaleOffset] _CrackTex ("Cracks (R lines, G crushed, B haze)", 2D) = "black" {}
        _CrackO ("Crack plane origin", Vector) = (0, 0, 0, 0)
        _CrackU ("Crack plane U (1/size)", Vector) = (0, 0, 0, 0)
        _CrackV ("Crack plane V (1/size)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:premul vertex:vert
        #pragma target 3.0

        sampler2D _DustTex, _CrackTex;
        fixed4 _Color, _DustColor;
        half _Glossiness, _DustTop, _DustSide, _DustScale;
        float4 _CrackO, _CrackU, _CrackV;

        struct Input
        {
            float3 op;
            float3 on;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.op = v.vertex.xyz;
            o.on = v.normal;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.on);
            float3 p = IN.op;
            float3 w = pow(abs(n), 4);
            w /= dot(w, 1);
            float3 q = p * _DustScale;
            float4 d = tex2D(_DustTex, q.zy) * w.x + tex2D(_DustTex, q.xz) * w.y + tex2D(_DustTex, q.xy) * w.z;
            float up = saturate((abs(n.y) - 0.1) / 0.75) * step(0, n.y);
            float amt = lerp(_DustSide, _DustTop, up);
            float dust = saturate(amt * (0.35 + 0.9 * d.g) * (0.8 + 0.4 * d.r) + d.a * 0.2 * amt);

            float4 c = 0;
            if (dot(_CrackU.xyz, _CrackU.xyz) > 1e-6)
            {
                float2 uv = float2(dot(p - _CrackO.xyz, _CrackU.xyz), dot(p - _CrackO.xyz, _CrackV.xyz));
                float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                c = tex2D(_CrackTex, uv) * inside;
            }
            float crack = saturate(c.r + c.g * 0.8);
            float3 col = lerp(_Color.rgb, _DustColor.rgb * (0.85 + 0.3 * d.r), dust);
            col = lerp(col, float3(0.78, 0.82, 0.82), crack);
            float a = saturate(_Color.a + dust * 0.55 + crack * 0.7 + c.b * 0.15);
            o.Albedo = col * a;   // premultiplied
            o.Alpha = a;
            o.Metallic = 0;
            o.Smoothness = lerp(_Glossiness, 0.25, saturate(dust * 1.3 + c.g * 0.6));
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
