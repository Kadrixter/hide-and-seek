// "Ciepły sodowy" look (GfxPost, Styl obrazu 4): sodium-lamp night. Soft shoulder for the highlights (lamps and bloom
// roll off instead of clipping), warm amber grade (brown shadows, orange-yellow highlights, muted blues), richer contrast,
// a touch of sharpening, light grain and a mild vignette.
Shader "Hidden/HnsLookSodium"
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

            float3 Shoulder(float3 c)
            {
                float3 o = max(c - 0.7, 0);
                return min(c, 0.7) + 0.3 * (1 - exp(-o / 0.3));
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 t = _MainTex_TexelSize.xy;
                float4 mid = tex2D(_MainTex, uv);
                float3 g = LookToGamma(Shoulder(HnsSafe3(mid.rgb)));
                // light sharpening against the 4 direct neighbours
                float3 n = LookToGamma(Shoulder(LookTap(uv + float2(t.x, 0)))) + LookToGamma(Shoulder(LookTap(uv - float2(t.x, 0))))
                         + LookToGamma(Shoulder(LookTap(uv + float2(0, t.y)))) + LookToGamma(Shoulder(LookTap(uv - float2(0, t.y))));
                g = saturate(g + (g - n * 0.25) * 0.35);

                // sodium grade: blues muted, brown shadows, amber highlights
                float l = LookLuma(g);
                g = LookSaturation(g, 0.85);
                g *= lerp(float3(1.03, 0.93, 0.82), float3(1.14, 0.97, 0.66), smoothstep(0.08, 0.75, l));
                g = saturate(g);
                g = lerp(g, g * g * (3 - 2 * g), 0.28);
                g = LookSaturation(g, 1.12);

                g *= LookVignette(uv, 0.25, 0.45);
                g += LookGrain(uv) * 0.022 * (0.4 + 0.6 * saturate(l * 3));
                return float4(LookToLinear(g), mid.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
