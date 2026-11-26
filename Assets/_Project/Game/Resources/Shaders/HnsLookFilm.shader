// "Kino" look (GfxPost, Styl obrazu 1): filmic ACES tonemap of the HDR frame, teal shadows / orange highlights split
// toning, a gentle S-curve, soft vignette, fine animated grain and a slight chromatic aberration towards the edges.
Shader "Hidden/HnsLookFilm"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "HnsLook.cginc"

            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 d = uv - 0.5;
                // lateral chromatic aberration: red pulled in, blue pushed out, zero in the middle
                float2 ca = d * dot(d, d) * 0.006;
                float4 mid = tex2D(_MainTex, uv);
                float3 c = float3(LookTap(uv - ca).r, HnsSafe3(mid.rgb).g, LookTap(uv + ca).b);

                c = LookAces(c * 1.9);
                float3 g = LookToGamma(c);
                float l = LookLuma(g);

                // split toning: teal in the shadows (not on pure black), warm orange in the highlights
                float sh = saturate(l * 8) * (1 - smoothstep(0.15, 0.55, l));
                float hi = smoothstep(0.35, 0.95, l);
                g += float3(-0.025, 0.012, 0.04) * sh + float3(0.05, 0.015, -0.045) * hi;
                g += float3(0.004, 0.010, 0.014); // a hint of lifted, cool blacks (print stock)

                g = LookSaturation(saturate(g), 1.1);
                g = lerp(g, g * g * (3 - 2 * g), 0.18);
                g *= LookVignette(uv, 0.3, 0.45);
                g += LookGrain(uv) * 0.04 * (0.35 + 0.65 * saturate(l * 3));
                return float4(LookToLinear(g), mid.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
