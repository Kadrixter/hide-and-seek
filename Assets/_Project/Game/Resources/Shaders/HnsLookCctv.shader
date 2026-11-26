// "Monitoring" look (GfxPost, Styl obrazu 2): security-camera feed. Slight barrel distortion (zoomed so no edge shows),
// a little chroma bleeding to the right, desaturated green-grey grade with auto-gain mids and crushed blacks, very faint
// scanlines, monochrome sensor noise and a barely visible flicker. Kept subtle so the game stays readable.
Shader "Hidden/HnsLookCctv"
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
                // barrel distortion, scaled so the corners land exactly on the frame corners
                const float k = 0.05;
                float2 d = i.uv - 0.5;
                float2 uv = 0.5 + d * (1 + k * dot(d, d) * 4) / (1 + k * 2);

                float4 mid = tex2D(_MainTex, uv);
                float3 g0 = LookToGamma(saturate(HnsSafe3(mid.rgb)));
                // chroma bleed: colour smeared from the left, brightness kept sharp
                float tx = _MainTex_TexelSize.x;
                float3 g1 = LookToGamma(saturate(LookTap(uv - float2(tx * 1.5, 0))));
                float3 g2 = LookToGamma(saturate(LookTap(uv - float2(tx * 3.5, 0))));
                float3 g3 = LookToGamma(saturate(LookTap(uv - float2(tx * 5.5, 0))));
                float l = LookLuma(g0);
                float3 chroma = (g0 - l) * 0.4 + (g1 - LookLuma(g1)) * 0.3 + (g2 - LookLuma(g2)) * 0.2 + (g3 - LookLuma(g3)) * 0.1;
                float3 g = l + chroma;

                // green-grey sensor grade: mostly grey, auto-gain lifts the mids, blacks crushed
                g = LookSaturation(g, 0.3);
                g *= float3(0.88, 1.04, 0.9);
                g = pow(saturate(g * 1.12), 0.85);
                g = saturate((g - 0.035) / (1 - 0.035));

                // faint scanlines (3 px period), flicker, monochrome noise, lens fall-off
                float y = i.uv.y * _MainTex_TexelSize.w;
                g *= 1 - 0.045 * (0.5 + 0.5 * sin(y * 2.0944));
                g *= 1 + (LookHash(float2(floor(_Time.y * 15.0), 7.0)) - 0.5) * 0.025;
                g += LookGrain(i.uv) * 0.06;
                g *= LookVignette(i.uv, 0.35, 0.5);
                return float4(LookToLinear(g), mid.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
