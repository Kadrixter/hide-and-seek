// Test tool (NanScan, -testnan / -nanscan): finds pixels of the HDR frame that are not normal colours.
// Pass 0: flags per pixel, R = NaN, G = +Inf, B = -Inf, A = negative. Pass 1: the frame for a PNG with the bad
// pixels painted (NaN magenta, +Inf green, -Inf cyan, negative yellow).
Shader "Hidden/HnsNanScan"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    Texture2D _MainTex;
    SamplerState sampler_point_clamp;

    // Bit tests: the compiler may drop x != x.
    float IsNan(float x) { uint u = asuint(x) & 0x7fffffffu; return u > 0x7f800000u ? 1.0 : 0.0; }
    float IsPosInf(float x) { return asuint(x) == 0x7f800000u ? 1.0 : 0.0; }
    float IsNegInf(float x) { return asuint(x) == 0xff800000u ? 1.0 : 0.0; }
    float IsNeg(float x) { return (asuint(x) & 0x80000000u) != 0u && (asuint(x) & 0x7fffffffu) > 0x38d1b717u ? 1.0 : 0.0; } // < -1e-4

    float4 Flags(float3 c)
    {
        float n = max(IsNan(c.r), max(IsNan(c.g), IsNan(c.b)));
        float p = max(IsPosInf(c.r), max(IsPosInf(c.g), IsPosInf(c.b)));
        float m = max(IsNegInf(c.r), max(IsNegInf(c.g), IsNegInf(c.b)));
        float g = max(IsNeg(c.r), max(IsNeg(c.g), IsNeg(c.b)));
        return float4(n, p, m, g);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.5
            float4 frag(v2f_img i) : SV_Target { return Flags(_MainTex.Sample(sampler_point_clamp, i.uv).rgb); }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 4.5
            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = _MainTex.Sample(sampler_point_clamp, i.uv).rgb;
                float4 f = Flags(c);
                if (f.x > 0.5) return float4(1, 0, 1, 1);
                if (f.y > 0.5) return float4(0, 1, 0, 1);
                if (f.z > 0.5) return float4(0, 1, 1, 1);
                if (f.w > 0.5) return float4(1, 1, 0, 1);
                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
