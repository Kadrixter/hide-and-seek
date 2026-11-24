// Soft bloom for bright lamps / emissive parts (GfxPost): threshold on HDR colour, 2x2 box downsample chain,
// tent upsample added back level by level, composite on top of the frame.
Shader "Hidden/HnsBloom"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "HnsSafe.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _BloomTex_TexelSize;
    float4 _Threshold;   // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    float _Intensity;

    half3 Box4(sampler2D t, float2 uv, float2 texel)
    {
        float4 d = texel.xyxy * float4(-1, -1, 1, 1);
        // NaN / Inf taps dropped here, not only by the min() of Prefilter (GPUs differ in min(NaN, x))
        half3 s = HnsSafe3(tex2D(t, uv + d.xy).rgb) + HnsSafe3(tex2D(t, uv + d.zy).rgb) + HnsSafe3(tex2D(t, uv + d.xw).rgb) + HnsSafe3(tex2D(t, uv + d.zw).rgb);
        return s * 0.25;
    }

    half3 Tent(sampler2D t, float2 uv, float2 texel)
    {
        float4 d = texel.xyxy * float4(1, 1, -1, 0);
        half3 s = tex2D(t, uv - d.xy).rgb;
        s += tex2D(t, uv - d.wy).rgb * 2;
        s += tex2D(t, uv - d.zy).rgb;
        s += tex2D(t, uv + d.zw).rgb * 2;
        s += tex2D(t, uv).rgb * 4;
        s += tex2D(t, uv + d.xw).rgb * 2;
        s += tex2D(t, uv + d.zy).rgb;
        s += tex2D(t, uv + d.wy).rgb * 2;
        s += tex2D(t, uv + d.xy).rgb;
        return s * (1.0 / 16);
    }

    half3 Prefilter(half3 c)
    {
        c = min(c, 30.0);   // single fireflies (sun-bright LEDs) must not explode
        half br = max(c.r, max(c.g, c.b));
        half rq = clamp(br - _Threshold.y, 0, _Threshold.z);
        rq = _Threshold.w * rq * rq;
        c *= max(rq, br - _Threshold.x) / max(br, 1e-4);
        return c;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0: prefilter + first downsample
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(Prefilter(Box4(_MainTex, i.uv, _MainTex_TexelSize.xy)), 1); }
            ENDCG
        }

        Pass // 1: downsample
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(Box4(_MainTex, i.uv, _MainTex_TexelSize.xy), 1); }
            ENDCG
        }

        Pass // 2: upsample, added onto the next bigger level
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(Tent(_MainTex, i.uv, _MainTex_TexelSize.xy), 1); }
            ENDCG
        }

        Pass // 3: composite onto the frame
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                if (HnsBad3(c.rgb))
                {
                    // a NaN / Inf pixel (broken normal somewhere): filled from its neighbours instead of a black dot
                    float2 t = _MainTex_TexelSize.xy;
                    c.rgb = (HnsSafe3(tex2D(_MainTex, i.uv + float2(t.x, 0)).rgb) + HnsSafe3(tex2D(_MainTex, i.uv - float2(t.x, 0)).rgb)
                           + HnsSafe3(tex2D(_MainTex, i.uv + float2(0, t.y)).rgb) + HnsSafe3(tex2D(_MainTex, i.uv - float2(0, t.y)).rgb)) * 0.25;
                    c.a = 1;
                }
                c.rgb += Tent(_BloomTex, i.uv, _BloomTex_TexelSize.xy) * _Intensity;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
