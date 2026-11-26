// Broken glass of the abandoned parked cars: the dusty car glass (Hns/CarDustGlass) plus damage modelled on how real car
// glass breaks. Every broken car has its own material (ParkingGarageBuilder.BreakGlass) that says per pane what happened;
// the shader finds the pane from the object-space position / normal (the parked cars are not statically batched):
//  - windscreen (laminated: the plastic interlayer holds it, it never falls out): 1 = a small star crack, 2 = a spider
//    web: a crushed bull's-eye, thin radial cracks of different lengths that wander a little, forks further out, and
//    straight chord cracks between neighbouring radials (the web rings);
//  - side / rear windows (tempered: they break all at once into small cubes): 3 = crazed all over but still in the
//    frame (fine cells, smaller round the impact, sometimes a few cells knocked out there), 4 = gone, only a jagged
//    fringe of crazed cells along the bottom and the pillars stays.
// Cracks are drawn with pixel-size anti-aliasing: far away they fade to a milky frosting instead of white noise.
Shader "Hns/CarBrokenGlass"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.6)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.95
        _DustTex ("Dust noise (R grain, G blotches, B streaks)", 2D) = "gray" {}
        _DustColor ("Dust colour", Color) = (0.44, 0.42, 0.38, 1)
        _DustTop ("Dust on upward faces", Range(0, 1.5)) = 0.5
        _DustSide ("Dust on the sides", Range(0, 1)) = 0.1
        _GrimeColor ("Grime colour", Color) = (0.13, 0.11, 0.09, 1)
        _Grime ("Grime on the lower body", Range(0, 1)) = 0
        _GrimeHeight ("Grime height above the floor (m)", Float) = 0.45
        _FloorY ("Floor height of the level", Float) = 0
        _DustScale ("Noise tiling per metre", Float) = 0.8
        _Seed ("Noise offset", Vector) = (0, 0, 0, 0)
        _Haze ("Milky haze", Range(0, 1)) = 0.2
        _CrackColor ("Crack colour", Color) = (0.86, 0.9, 0.9, 1)
        _PanesA ("Pane states: windscreen, rear, left 0, left 1", Vector) = (0, 0, 0, 0)
        _PanesB ("Pane states: left 2, right 0, right 1, right 2", Vector) = (0, 0, 0, 0)
        _Seg0 ("Side window 0 (z0, z1)", Vector) = (-99, -99, 0, 0)
        _Seg1 ("Side window 1 (z0, z1)", Vector) = (-99, -99, 0, 0)
        _Seg2 ("Side window 2 (z0, z1)", Vector) = (-99, -99, 0, 0)
        _SideY ("Side glass y0, y1, mid z, seed", Vector) = (0, 1, 0, 0)
        _FrontUp ("Windscreen up (0, y, z)", Vector) = (0, 1, 0, 0)
        _RearUp ("Rear window up (0, y, z)", Vector) = (0, 1, 0, 0)
        _RearBox ("Rear window x0, x1, v0, v1", Vector) = (-1, 1, 0, 1)
        _Hit ("Windscreen impact (x, up, reach, seed)", Vector) = (0, 0, 0, 0)
        _Hit2 ("Second windscreen impact", Vector) = (0, 0, 0, 0)
        _NoGoneSide ("Pillar strips: never fall out", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard alpha:premul vertex:vert
        #pragma target 3.5
        #include "HnsDust.cginc"

        fixed4 _CrackColor;
        float4 _PanesA, _PanesB, _Seg0, _Seg1, _Seg2, _SideY, _FrontUp, _RearUp, _RearBox, _Hit, _Hit2;
        float _NoGoneSide;

        float H1(float n) { return frac(sin(n * 12.9898 + 4.1414) * 43758.5453); }
        float2 H2(float2 p)
        {
            p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
            return frac(sin(p) * 43758.5453);
        }
        float Noise1(float x, float y) { return tex2Dlod(_DustTex, float4(x, y, 0, 0)).r; }

        // anti-aliased line of half width w (m) at distance d (m), px = metres per pixel
        float Line(float d, float w, float px) { return saturate(2.0 * w / px) * (1.0 - smoothstep(w, w + px, d)); }

        float SegDist(float2 p, float2 a, float2 b)
        {
            float2 pa = p - a, ba = b - a;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-8));
            return length(pa - ba * h);
        }

        // ---------------------------------------------------------------- laminated windscreen: star / spider web
        float SpokeAngle(float i, float N, float seed) { float im = i - N * floor(i / N); return (i + 0.15 + 0.7 * H1(im + seed)) * 6.2831853 / N; }
        float SpokeLen(float i, float N, float seed, float reach) { float im = i - N * floor(i / N); return reach * (0.4 + 0.6 * H1(im * 3.7 + seed + 11.0)); }

        float Web(float2 p, float4 hit, float px, float web)
        {
            float reach = hit.z;
            float2 q = p - hit.xy;
            float r = length(q);
            if (reach <= 0.0 || r > reach * 1.05) return 0.0;
            float seed = hit.w;
            float ang = atan2(q.y, q.x);
            float N = web > 0.5 ? 10.0 + floor(H1(seed) * 5.0) : 6.0 + floor(H1(seed) * 3.0);
            float sector = 6.2831853 / N;
            float i0 = floor(ang / sector);
            float c = 0.0;
            // radial cracks, wandering a little, thinner towards the tip
            [unroll] for (int k = -1; k <= 1; k++)
            {
                float i = i0 + k;
                float th = SpokeAngle(i, N, seed);
                float len = SpokeLen(i, N, seed, reach);
                float lat = (Noise1(r * 1.9 + H1(i + seed) * 7.0, 0.31 * k + seed * 0.01) - 0.5) * 0.035 * saturate(r / 0.05);
                float dth = ang - th;
                float d = abs(r * sin(dth) - lat) + (cos(dth) < 0.0 ? 1.0 : 0.0);
                float w = max(0.00028, 0.0008 * (1.0 - 0.7 * r / len));
                c = max(c, Line(d, w, px) * (1.0 - smoothstep(len * 0.85, len, r)));
            }
            // forks: shorter cracks starting part way out
            float N2 = N * 2.0;
            float sector2 = 6.2831853 / N2;
            float j0 = floor(ang / sector2);
            [unroll] for (int k2 = 0; k2 <= 1; k2++)
            {
                float i = j0 + k2;
                float im = i - N2 * floor(i / N2);
                float th = SpokeAngle(i, N2, seed + 37.0);
                float r0 = reach * (0.18 + 0.4 * H1(im + seed + 5.0));
                float len = r0 + reach * (0.15 + 0.45 * H1(im + seed + 9.0));
                float lat = (Noise1(r * 2.3 + H1(im + seed + 2.0) * 5.0, 0.7) - 0.5) * 0.03;
                float dth = ang - th;
                float d = abs(r * sin(dth) - lat) + (cos(dth) < 0.0 ? 1.0 : 0.0);
                float on = smoothstep(r0, r0 + 0.01, r) * (1.0 - smoothstep(len * 0.85, len, r)) * step(H1(im + seed + 21.0), web > 0.5 ? 0.75 : 0.3);
                c = max(c, Line(d, 0.00028, px) * on);
            }
            // rings: straight chords between neighbouring radials
            int rings = web > 0.5 ? 4 : 2;
            [unroll] for (int s = -1; s <= 0; s++)
            {
                float i = i0 + s;
                float thA = SpokeAngle(i, N, seed), thB = SpokeAngle(i + 1.0, N, seed);
                float lA = SpokeLen(i, N, seed, reach), lB = SpokeLen(i + 1.0, N, seed, reach);
                float im = i - N * floor(i / N);
                [unroll] for (int j = 0; j < 4; j++)
                {
                    if (j >= rings) break;
                    float hj = H1(im * 5.3 + j * 17.0 + seed);
                    float rj = reach * (web > 0.5 ? 0.09 + 0.19 * j : 0.16 + 0.22 * j) * (0.85 + 0.3 * hj);
                    if (rj > min(lA, lB) * 0.92 || H1(im * 2.1 + j * 7.0 + seed + 3.0) > (web > 0.5 ? 0.8 - 0.12 * j : 0.55)) continue;
                    float2 A = float2(cos(thA), sin(thA)) * rj * (0.9 + 0.2 * H1(hj + 1.0));
                    float2 B = float2(cos(thB), sin(thB)) * rj * (0.9 + 0.2 * H1(hj + 2.0));
                    c = max(c, Line(SegDist(q, A, B), 0.00026, px) * 0.85);
                }
            }
            // crushed bull's-eye and the cone ring round it
            float cr = clamp(reach * 0.06, 0.01, 0.03);
            float n = Noise1(ang * 0.4 + seed, r * 3.0);
            c = max(c, (1.0 - smoothstep(cr * 0.55, cr, r + (n - 0.5) * 0.006)) * 0.6);
            c = max(c, Line(abs(r - cr * 1.6), 0.0005, px) * 0.6);
            return c;
        }

        // ---------------------------------------------------------------- tempered: crazing (Voronoi cells)
        // returns the crack line value; cc = centre of the cell (pane coordinates), cid = its random id
        float Craze(float2 p, float2 hitP, float px, float seed, out float2 cc, out float cid)
        {
            float r = length(p - hitP);
            float sc = lerp(120.0, 55.0, saturate(r / 0.4));   // cells per metre: ~0.8 cm at the impact, ~1.8 cm away
            float2 x = (p - hitP) * sc + seed;
            float2 n = floor(x), f = frac(x);
            float2 mg = 0, mr = 0;
            float md = 8.0;
            [unroll] for (int j = -1; j <= 1; j++)
            [unroll] for (int i = -1; i <= 1; i++)
            {
                float2 g = float2(i, j);
                float2 rr = g + H2(n + g) - f;
                float d = dot(rr, rr);
                if (d < md) { md = d; mr = rr; mg = g; }
            }
            md = 8.0;
            [unroll] for (int j2 = -2; j2 <= 2; j2++)
            [unroll] for (int i2 = -2; i2 <= 2; i2++)
            {
                float2 g = mg + float2(i2, j2);
                float2 rr = g + H2(n + g) - f;
                float2 dd = rr - mr;
                if (dot(dd, dd) > 1e-5) md = min(md, dot(0.5 * (mr + rr), normalize(dd)));
            }
            float2 cell = n + mg;
            cc = (cell + H2(cell) - seed) / sc + hitP;
            cid = H1(dot(cell, float2(1.0, 57.0)));
            float edge = md / sc;                                   // metres to the cell border
            float lines = Line(edge, 0.00022, px);
            float frost = saturate((px * sc - 0.12) / 0.3);         // cells smaller than a few pixels: milky
            return lerp(lines, 0.2, frost);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 m = DustMask(IN);
            float3 op = mul(unity_WorldToObject, float4(IN.worldPos, 1.0)).xyz;
            float3 on = normalize(mul(IN.wN, (float3x3)unity_ObjectToWorld));
            float seed = _SideY.w;

            // ---- which pane, its state and its 2D coordinates
            float state, paneId;
            float2 p2, hitP;
            float3 box; // x0, x1, bottom in pane coordinates (for the fringe of a pane that fell out)
            bool side = abs(on.x) > 0.55;
            float s01 = _Seg1.y > -98.0 ? (_Seg0.x + _Seg1.y) * 0.5 : -99.0;
            float s12 = _Seg2.y > -98.0 ? (_Seg1.x + _Seg2.y) * 0.5 : -99.0;
            float seg = op.z > s01 ? 0.0 : (op.z > s12 ? 1.0 : 2.0);
            float2 sz = seg < 0.5 ? _Seg0.xy : (seg < 1.5 ? _Seg1.xy : _Seg2.xy);
            bool right = op.x > 0.0;
            float sideState = right ? (seg < 0.5 ? _PanesB.y : (seg < 1.5 ? _PanesB.z : _PanesB.w))
                                    : (seg < 0.5 ? _PanesA.z : (seg < 1.5 ? _PanesA.w : _PanesB.x));
            bool front = op.z > _SideY.z;
            if (side)
            {
                state = (_NoGoneSide > 0.5 && sideState > 3.5) ? 3.0 : sideState;
                paneId = 2.0 + seg + (right ? 3.0 : 0.0);
                p2 = op.zy;
                hitP = float2(lerp(sz.x, sz.y, 0.3 + 0.4 * H1(seed + paneId)), lerp(_SideY.x, _SideY.y, 0.3 + 0.35 * H1(seed + paneId * 2.3)));
                box = float3(sz.x, sz.y, _SideY.x);
            }
            else if (front)
            {
                state = _PanesA.x;
                paneId = 0.0;
                p2 = float2(op.x, dot(op.yz, _FrontUp.yz));
                hitP = _Hit.xy;
                box = float3(-99.0, 99.0, -99.0);
            }
            else
            {
                state = _PanesA.y;
                paneId = 1.0;
                p2 = float2(op.x, dot(op.yz, _RearUp.yz));
                hitP = float2(lerp(_RearBox.x, _RearBox.y, 0.3 + 0.4 * H1(seed + 1.0)), lerp(_RearBox.z, _RearBox.w, 0.35 + 0.3 * H1(seed + 4.1)));
                box = float3(_RearBox.x, _RearBox.y, _RearBox.z);
            }
            float px = max(max(length(ddx(p2)), length(ddy(p2))), 1e-5);

            // ---- damage
            float crack = 0.0, tint = 0.0, tone = 1.0;
            if (state > 0.5 && state < 2.5)
            {
                crack = Web(p2, _Hit, px, state > 1.5 ? 1.0 : 0.0);
                if (_Hit2.z > 0.0) crack = max(crack, Web(p2, _Hit2, px, 0.0));
            }
            else if (state > 2.5)
            {
                float2 cc;
                float cid;
                crack = Craze(p2, hitP, px, seed * 7.0 + paneId * 13.0, cc, cid);
                tint = cid;
                tone = 0.72;
                if (state > 3.5)
                {
                    // fell out: a jagged fringe of cells along the bottom and the pillars stays in the frame
                    float edge = min(cc.y - box.z, min(cc.x - box.x, box.y - cc.x));
                    float fringe = 0.008 + 0.05 * Noise1(cc.x * 1.7 + seed, cc.y * 1.7 + paneId) * Noise1(cc.y * 0.9 + paneId, cc.x * 0.6);
                    clip(fringe - edge - (cid > 0.85 ? 1.0 : 0.0));
                }
                else
                {
                    // crazed in place, sometimes a few cells knocked out at the impact
                    float holeR = H1(seed + paneId * 5.0) < 0.3 ? 0.012 + 0.03 * H1(seed + paneId * 9.0) : 0.0;
                    clip(length(cc - hitP) - holeR * (0.6 + 0.8 * cid));
                }
            }

            half dust = saturate(m.x * 1.1);
            half haze = _Haze * (0.75 + 0.5 * m.z);
            fixed3 alb = lerp(c.rgb, _DustColor.rgb * 0.75, saturate(haze));
            alb = lerp(alb, _DustColor.rgb * (0.82 + 0.36 * m.z), dust);
            alb = lerp(alb, _CrackColor.rgb * tone * (0.85 + 0.25 * tint), crack);
            half a = saturate(c.a + (1.0 - c.a) * saturate(haze * 0.85 + dust * 0.95));
            a = saturate(a + crack * (state > 2.5 ? 0.45 : 0.6) + (state > 2.5 ? 0.06 : 0.0));
            o.Albedo = alb;
            o.Metallic = _Metallic * (1.0 - saturate(dust + haze + crack));
            o.Smoothness = lerp(lerp(_Glossiness, 0.45, saturate(haze)), 0.25, saturate(dust)) * (1.0 - crack * 0.25);
            o.Alpha = a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
