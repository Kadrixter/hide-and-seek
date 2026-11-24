// Guard for the full-screen passes (bloom, FXAA / SMAA, Esc menu blur, drunk look, night vision).
// One NaN / Inf pixel of the frame (e.g. a zero normal in the Standard shader) must not leak into a blur: every tap
// copies it and NaN shows as black - squares behind the Esc menu, clusters of dots in the drunk look, a white glow
// through the bloom. Bit test of the exponent (the compiler may optimise x != x away); a bad colour becomes 0.
#ifndef HNS_SAFE_INCLUDED
#define HNS_SAFE_INCLUDED

inline bool HnsBad(float x) { return (asuint(x) & 0x7f800000u) == 0x7f800000u; }
inline bool HnsBad3(float3 c) { return HnsBad(c.r) || HnsBad(c.g) || HnsBad(c.b); }
inline float3 HnsSafe3(float3 c) { return HnsBad3(c) ? float3(0, 0, 0) : max(c, 0); }
inline float4 HnsSafe4(float4 c) { return float4(HnsSafe3(c.rgb), HnsBad(c.a) ? 1 : c.a); }

#endif
