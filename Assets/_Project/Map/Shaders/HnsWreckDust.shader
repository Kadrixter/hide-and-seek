// Dusty paint / plastic / metal of the wrecked Golf (map level -2). Everything is driven by the mesh-local position
// and normal (the wreck is not static-batched): dust settles thick on faces looking up and thin on the sides, road grime
// low on the body, streaks run down. Two projected masks on planes given in object space: scrapes (bare metal, scuffed
// lacquer, scratches) and a finger-written message wiped through the dust (wiped strokes, dust ridges pushed to the
// sides, faint finger lines). Plane = origin O, axes U and V scaled so that dot(p - O, U) runs 0..1 across the mask.
Shader "HideAndSeek/WreckDust"
{
    Properties
    {
        _Color ("Base colour", Color) = (0.03, 0.03, 0.035, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.82
        _Metallic ("Metallic", Range(0, 1)) = 0.35
        _DustTex ("Dust (R grain, G blotches, B streaks, A specks)", 2D) = "gray" {}
        _DustColor ("Dust colour", Color) = (0.46, 0.44, 0.4, 1)
        _DustTop ("Dust on faces looking up", Range(0, 1.5)) = 0.95
        _DustSide ("Dust on vertical faces", Range(0, 1)) = 0.4
        _DustLow ("Road grime low on the sides", Range(0, 1)) = 0.35
        _DustScale ("Dust tiling (per metre)", Float) = 0.9
        [NoScaleOffset] _ScrapeTex ("Scrapes (R bare metal, G scuffed lacquer, B scratches)", 2D) = "black" {}
        _ScrapeO ("Scrape plane origin", Vector) = (0, 0, 0, 0)
        _ScrapeU ("Scrape plane U (1/size)", Vector) = (0, 0, 0, 0)
        _ScrapeV ("Scrape plane V (1/size)", Vector) = (0, 0, 0, 0)
        [NoScaleOffset] _WriteTex ("Writing (R wiped, G ridge, B finger lines)", 2D) = "black" {}
        _WriteO ("Writing plane origin", Vector) = (0, 0, 0, 0)
        _WriteU ("Writing plane U (1/size)", Vector) = (0, 0, 0, 0)
        _WriteV ("Writing plane V (1/size)", Vector) = (0, 0, 0, 0)
        _MetalColor ("Bare metal", Color) = (0.58, 0.58, 0.57, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _DustTex, _ScrapeTex, _WriteTex;
        fixed4 _Color, _DustColor, _MetalColor;
        half _Glossiness, _Metallic, _DustTop, _DustSide, _DustLow, _DustScale;
        float4 _ScrapeO, _ScrapeU, _ScrapeV, _WriteO, _WriteU, _WriteV;

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

        // mask projected on a plane, only on faces turned towards it (front of the plane)
        float4 Plane(sampler2D t, float4 O, float4 U, float4 V, float3 p, float3 n)
        {
            float lu = dot(U.xyz, U.xyz);
            if (lu < 1e-6) return 0;
            float2 uv = float2(dot(p - O.xyz, U.xyz), dot(p - O.xyz, V.xyz));
            float3 face = normalize(cross(V.xyz, U.xyz));
            float inside = step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
            inside *= saturate((dot(n, face) - 0.2) * 3.0) * step(abs(dot(p - O.xyz, face)), 0.25);
            return tex2D(t, uv) * inside;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.on);
            float3 p = IN.op;
            float3 w = pow(abs(n), 4);
            w /= dot(w, 1);
            float3 q = p * _DustScale;
            float4 d = tex2D(_DustTex, q.zy) * w.x + tex2D(_DustTex, q.xz) * w.y + tex2D(_DustTex, q.xy) * w.z;
            // streaks run down the vertical faces
            float streak = tex2D(_DustTex, float2((q.z + q.x) * 0.45, p.y * 0.22)).b;

            float up = saturate((n.y - 0.1) / 0.75);
            float down = saturate(-n.y);
            float amt = lerp(_DustSide, _DustTop, up) * (1 - down * 0.7);
            amt += _DustLow * saturate((0.5 - p.y) / 0.35) * (1 - up);
            float dust = amt * (0.45 + 0.9 * d.g) * (0.8 + 0.4 * d.r);
            dust += (streak - 0.5) * 0.22 * (1 - up) * amt;
            dust += d.a * 0.3 * amt;
            dust = saturate(dust);

            float3 col = _Color.rgb;
            half met = _Metallic, sm = _Glossiness;
            float4 s = Plane(_ScrapeTex, _ScrapeO, _ScrapeU, _ScrapeV, p, n);
            col = lerp(col, float3(0.4, 0.4, 0.38) * (0.75 + 0.5 * d.r), s.g);      // scuffed, chalky lacquer and filler
            sm = lerp(sm, 0.3, s.g);
            float3 metal = _MetalColor.rgb * (0.8 + 0.35 * d.r);
            col = lerp(col, metal, s.r);
            met = lerp(met, 0.9, s.r);
            sm = lerp(sm, 0.48, s.r);
            col = lerp(col, metal * 1.1, s.b * 0.85);
            met = lerp(met, 0.75, s.b);
            dust *= 1 - s.r * 0.45;

            float4 wr = Plane(_WriteTex, _WriteO, _WriteU, _WriteV, p, n);
            // the finger wipes most of the dust off, leaving its skin lines; the dust piles up along both edges
            dust = dust * (1 - wr.r * (0.88 - 0.35 * wr.b)) + wr.g * 0.5 * (0.6 + 0.4 * d.r);
            dust = saturate(dust);

            float3 dc = _DustColor.rgb * (0.82 + 0.36 * d.r);
            o.Albedo = lerp(col, dc, dust);
            o.Metallic = met * (1 - saturate(dust * 1.6));
            o.Smoothness = lerp(sm, 0.08, saturate(dust * 1.5));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
