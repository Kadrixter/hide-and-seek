// Shared helpers of the "Styl obrazu" looks (HnsLookFilm / Cctv / Cold / Sodium, run by GfxPost after bloom, before
// FXAA / SMAA). The frame comes in as linear HDR (linear colour space, HDR camera) or linear LDR; the grade itself works
// on perceptual (sRGB-like) values and the result goes back to linear, clamped to 0..1 (the AA passes and the screen
// expect display values).
#ifndef HNS_LOOK_INCLUDED
#define HNS_LOOK_INCLUDED

#include "UnityCG.cginc"
#include "HnsSafe.cginc"

sampler2D _MainTex;
float4 _MainTex_TexelSize;

// Exact sRGB curves: the fast UnityCG approximations do not round-trip in the darks and would crush the dark garage
// even with a neutral grade.
inline float3 LookToGamma(float3 c)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return max(c, 0);
#else
    c = max(c, 0);
    float3 lo = c * 12.92;
    float3 hi = 1.055 * pow(max(c, 1e-6), 1.0 / 2.4) - 0.055;
    return lerp(hi, lo, step(c, 0.0031308));
#endif
}

inline float3 LookToLinear(float3 c)
{
#ifdef UNITY_COLORSPACE_GAMMA
    return saturate(c);
#else
    c = saturate(c);
    float3 lo = c / 12.92;
    float3 hi = pow((c + 0.055) / 1.055, 2.4);
    return lerp(hi, lo, step(c, 0.04045));
#endif
}

inline float LookLuma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

inline float3 LookSaturation(float3 c, float s) { float l = LookLuma(c); return l + (c - l) * s; }

// Frame colour at uv with NaN / Inf guarded (bloom off means nothing upstream cleaned the frame).
inline float3 LookTap(float2 uv) { return HnsSafe3(tex2D(_MainTex, uv).rgb); }

// Hash without sine (Dave Hoskins), 0..1.
inline float LookHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Per-pixel noise -0.5..0.5, a new pattern 24 times a second.
inline float LookGrain(float2 uv)
{
    float2 p = floor(uv * _MainTex_TexelSize.zw);
    float f = floor(_Time.y * 24.0);
    p += float2(frac(f * 0.6180339) * 997.0, frac(f * 0.4142135) * 613.0);
    return LookHash(p) - 0.5;
}

// 1 in the middle, 1 - strength in the corners; inner = radius (0 centre .. 1 corner) where the darkening starts.
inline float LookVignette(float2 uv, float strength, float inner)
{
    float aspect = _MainTex_TexelSize.z * _MainTex_TexelSize.y;
    float2 d = (uv - 0.5) * float2(aspect, 1);
    float r = length(d) / (0.5 * sqrt(aspect * aspect + 1));
    return 1 - strength * smoothstep(inner, 1.0, r);
}

// Filmic curve (ACES fit by K. Narkowicz): linear HDR in, display linear out.
inline float3 LookAces(float3 x)
{
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
}

#endif
