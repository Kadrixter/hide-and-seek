// Shared dust / grime layer of the parked cars (Hns/CarDust and Hns/CarDustGlass).
// Everything is in world space (the hero cars are statically batched, so object space is not reliable):
//  - dust settles on faces looking up (bonnet, roof, boot lid, window sills), a thinner film on the sides,
//    broken up by three noise channels of _DustTex (R fine grain, G blotches, B vertical streaks), triplanar;
//  - grime (road dirt thrown up) on the lower body, measured from the floor of the car's level (_FloorY).
#ifndef HNS_DUST_INCLUDED
#define HNS_DUST_INCLUDED

sampler2D _MainTex;
sampler2D _DustTex;
fixed4 _Color;
half _Metallic, _Glossiness;
fixed4 _DustColor, _GrimeColor;
half _DustTop, _DustSide, _Grime, _GrimeHeight, _Haze;
float _FloorY, _DustScale;
float4 _Seed;

struct Input
{
    float2 uv_MainTex;
    float3 worldPos;
    float3 wN;
};

void vert(inout appdata_full v, out Input o)
{
    UNITY_INITIALIZE_OUTPUT(Input, o);
    o.wN = UnityObjectToWorldNormal(v.normal);
}

// x = dust cover 0..1, y = grime 0..1, z = fine grain (for tone variation)
half3 DustMask(Input IN)
{
    float3 n = normalize(IN.wN);
    float3 wp = IN.worldPos + _Seed.xyz;
    float3 bw = pow(abs(n), 4.0);
    bw /= (bw.x + bw.y + bw.z + 1e-4);
    half4 tx = tex2D(_DustTex, wp.zy * _DustScale);
    half4 ty = tex2D(_DustTex, wp.xz * _DustScale);
    half4 tz = tex2D(_DustTex, wp.xy * _DustScale);
    half4 t = tx * bw.x + ty * bw.y + tz * bw.z;
    // big, slow blotches across the whole car (one side dustier than the other)
    half big = tex2D(_DustTex, wp.xz * _DustScale * 0.11 + _Seed.zw).g;
    half up = saturate(n.y);
    half top = smoothstep(0.25, 0.92, up);
    half dust = _DustTop * top * (0.45 + 0.85 * t.g) * (0.7 + 0.6 * big)
              + _DustSide * (1.0 - top) * (0.5 + 0.6 * t.g) * (0.8 + 0.35 * t.b) * (0.75 + 0.5 * big);
    dust = saturate(dust * (0.8 + 0.4 * t.r) + (_DustTop > 0.9 ? (t.r - 0.5) * 0.15 : 0));
    float h = IN.worldPos.y - _FloorY;
    half low = saturate(1.0 - h / max(0.05, _GrimeHeight));
    half down = saturate(-n.y);
    half grime = saturate(_Grime * (low * low * (0.6 + 0.5 * t.b) + down * 0.6) * (1.0 - top * 0.7));
    return half3(dust, grime, t.r);
}

#endif
