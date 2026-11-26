// "Zimny horror" look (GfxPost, Styl obrazu 3): cold desaturated blue-cyan grade, strong local contrast (unsharp mask
// with a ~5 px radius, "clarity"), darker mids, a cold haze lifting the darkest parts, deep vignette, light grain.
Shader "Hidden/HnsLookCold"
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
                float2 t = _MainTex_TexelSize.xy;
                float4 mid = tex2D(_MainTex, uv);
                float3 c = HnsSafe3(mid.rgb) * 1.3;

                // wide blur for the clarity: 8 bilinear taps on two rings
                float3 b = LookTap(uv + t * float2(2.5, 2.5)) + LookTap(uv + t * float2(-2.5, 2.5))
                         + LookTap(uv + t * float2(2.5, -2.5)) + LookTap(uv + t * float2(-2.5, -2.5))
                         + LookTap(uv + t * float2(5, 0)) + LookTap(uv + t * float2(-5, 0))
                         + LookTap(uv + t * float2(0, 5)) + LookTap(uv + t * float2(0, -5));
                float3 gb = LookToGamma(saturate(b * (1.3 / 8)));
                float3 g = LookToGamma(saturate(c));
                g = saturate(g + (g - gb) * 0.75);

                // cold grade: desaturated, pushed to blue-cyan (shadows more than highlights), heavier mids
                float l = LookLuma(g);
                g = LookSaturation(g, 0.45);
                g *= lerp(float3(0.8, 0.95, 1.15), float3(0.92, 1.0, 1.06), smoothstep(0.2, 0.8, l));
                g = pow(saturate(g), 1.0);

                // haze: the blackest parts become a dim cold fog instead of pure black
                float3 haze = float3(0.018, 0.026, 0.036);
                g = haze + g * (1 - haze);

                g *= LookVignette(uv, 0.6, 0.3);
                g += LookGrain(uv) * 0.025;
                return float4(LookToLinear(g), mid.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
