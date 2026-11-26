using System;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Rim designs of the ordinary parked cars: five-spoke and ten-spoke alloys, wheel cover, pressed steel
    /// (black or silver by the rim material), cross-spoke mesh, swirled turbine.</summary>
    internal enum ParkedRim { Alloy5, AlloyMulti, Hubcap, Steel, Mesh, Turbine }

    /// <summary>
    /// Wheel of the parked cars, all real geometry: a tyre with a block tread (shoulder blocks and a chevron centre
    /// row cut by circumferential grooves), a bulging sidewall with a rim protector ridge; a rim with a deep outer lip,
    /// a dished face whose openings are cut through (spokes are what is left of the face, so you see the brake disc and
    /// the dark barrel through them), lug nuts and a centre cap. Built in local space like <see cref="CarWheels.Wheel"/>:
    /// c = hub centre, side = +1 right (outer face +x). A spoke (or a solid part of the cover) always points straight
    /// down, so the builder's side probe at 0.6 r above the floor lands on the rim face.
    /// </summary>
    internal static class ParkedWheel
    {
        /// <summary>Tread pitches round the tyre; the turned parts use the same angular grid (no cracks).</summary>
        private const int Pitches = 20;
        private const int Sides = Pitches;
        /// <summary>Depth of the tread grooves.</summary>
        private const float Groove = 0.009f;

        /// <summary>One face of openings between hub radius rh and outer radius ro (see <see cref="PolarFace"/>).</summary>
        private struct Openings
        {
            public int N, K;
            /// <summary>Radial centre and radial half size of an opening, superellipse exponent.</summary>
            public float Rc, Ar, E;
            /// <summary>Spoke half widths at the hub and at the rim (spoked faces).</summary>
            public float WsHub, WsRim;
            /// <summary>Tangential half size of an opening (&gt; 0: round holes / slots instead of spokes).</summary>
            public float Gap;
            /// <summary>Swirl of the spokes from hub to rim (radians).</summary>
            public float Twist;
            /// <summary>Height of the spoke ridges above the face.</summary>
            public float Ridge;
        }

        public static void Build(MeshBuilder mb, Vector3 c, float side, float r, float width, float rimR, ParkedRim style, Material rim)
        {
            var lib = mb.Lib;
            Vector3 ax = Vector3.right * side;
            // the same basis as CarWheels.Lathe, so the tread grid and the turned parts share their vertices
            Vector3 e1 = Vector3.Cross(ax, Vector3.up).normalized, e2 = Vector3.Cross(ax, e1);
            float hw = width * 0.5f;
            Tyre(mb, c, ax, e1, e2, r, hw, rimR);
            if (style == ParkedRim.Hubcap) { Hubcap(mb, c, ax, e1, e2, hw, rimR); return; }

            float face = hw - 0.04f, rh = style == ParkedRim.Steel ? 0.072f : 0.062f, ro = rimR - 0.02f;
            float dish = style == ParkedRim.Steel ? 0.006f : style == ParkedRim.Mesh ? 0.004f : 0.012f;
            Func<float, float> axial = q => face + dish * (1f - Mathf.Clamp01((q - rh) / (ro - rh)));

            // barrel, then the outer lip: flange against the tyre bead, rounded edge, inner wall down to the face
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR - 0.01f, -hw + 0.03f), new Vector2(rimR, -hw + 0.035f), new Vector2(rimR, hw - 0.012f),
                new Vector2(rimR + 0.004f, hw - 0.004f), new Vector2(rimR + 0.002f, hw + 0.002f), new Vector2(rimR - 0.008f, hw + 0.001f),
                new Vector2(ro - 0.004f, face - 0.006f),
            }, Sides, rim, 40f);
            // behind the face: dark well, brake disc, calliper
            var dark = lib["Car_Well"];
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rimR - 0.002f, face - 0.085f), new Vector2(0f, face - 0.085f) }, Sides, dark);
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rimR * 0.8f, face - 0.064f), new Vector2(rimR * 0.8f, face - 0.05f),
                new Vector2(rimR * 0.28f, face - 0.05f) }, Sides, lib["Metal_Dark"], 30f);
            mb.Box(c + ax * (face - 0.042f) + new Vector3(0, rimR * 0.5f, -rimR * 0.45f), new Vector3(0.045f, rimR * 0.42f, rimR * 0.26f),
                lib["Car_Trim"], Quaternion.Euler(-42f, 0, 0));

            float band = ro - rh;
            switch (style)
            {
                case ParkedRim.Alloy5:
                    PolarFace(mb, c, ax, e1, e2, rh, ro, rh, ro, axial, new Openings
                    {
                        N = 5, K = 12, Rc = (rh + ro) * 0.5f + 0.004f, Ar = band * 0.5f - 0.012f, E = 0.4f,
                        WsHub = 0.021f, WsRim = 0.014f, Ridge = 0.007f,
                    }, rim);
                    break;
                case ParkedRim.AlloyMulti:
                    PolarFace(mb, c, ax, e1, e2, rh, ro, rh, ro, axial, new Openings
                    {
                        N = 10, K = 8, Rc = (rh + ro) * 0.5f + 0.006f, Ar = band * 0.5f - 0.011f, E = 0.5f,
                        WsHub = 0.008f, WsRim = 0.0065f, Ridge = 0.005f,
                    }, rim);
                    break;
                case ParkedRim.Turbine:
                    PolarFace(mb, c, ax, e1, e2, rh, ro, rh, ro, axial, new Openings
                    {
                        N = 8, K = 10, Rc = (rh + ro) * 0.5f + 0.004f, Ar = band * 0.5f - 0.01f, E = 0.5f,
                        WsHub = 0.01f, WsRim = 0.007f, Twist = 0.5f, Ridge = 0.006f,
                    }, rim);
                    break;
                case ParkedRim.Mesh:
                {
                    // cross-spoke mesh: a ring of eight openings round the hub, sixteen smaller ones outside it
                    float rm = rh + band * 0.42f;
                    PolarFace(mb, c, ax, e1, e2, rh, rm, rh, ro, axial, new Openings
                    {
                        N = 8, K = 8, Rc = (rh + rm) * 0.5f, Ar = (rm - rh) * 0.5f - 0.006f, E = 0.6f,
                        WsHub = 0.006f, WsRim = 0.006f, Ridge = 0.004f,
                    }, rim);
                    PolarFace(mb, c, ax, e1, e2, rm, ro, rh, ro, axial, new Openings
                    {
                        N = 16, K = 6, Rc = (rm + ro) * 0.5f, Ar = (ro - rm) * 0.5f - 0.006f, E = 0.6f,
                        WsHub = 0.005f, WsRim = 0.005f, Ridge = 0.004f,
                    }, rim);
                    // polished ring where the two meshes meet
                    CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rm + 0.004f, axial(rm) + 0.003f), new Vector2(rm + 0.002f, axial(rm) + 0.008f),
                        new Vector2(rm - 0.002f, axial(rm) + 0.008f), new Vector2(rm - 0.004f, axial(rm) + 0.003f) }, Sides, lib["Car_ChromeBright"]);
                    break;
                }
                case ParkedRim.Steel:
                    // pressed steel: a flat dish with six round holes, a raised ring outside them
                    PolarFace(mb, c, ax, e1, e2, rh, ro, rh, ro, axial, new Openings
                    {
                        N = 6, K = 10, Rc = (rh + ro) * 0.5f, Ar = 0.017f, E = 1f, Gap = 0.017f, Ridge = 0.002f,
                    }, rim);
                    CarWheels.Lathe(mb, c, ax, new[] { new Vector2(ro - 0.002f, axial(ro) + 0.001f), new Vector2(ro - 0.008f, axial(ro) + 0.007f),
                        new Vector2(ro - 0.016f, axial(ro) + 0.001f) }, Sides, rim, 60f);
                    break;
            }
            Hub(mb, c, ax, e1, e2, rh, axial(rh) + (style == ParkedRim.Steel ? 0.002f : 0.006f), rim,
                style == ParkedRim.Steel ? rim : lib["Car_ChromeBright"], style != ParkedRim.Steel);
        }

        // ------------------------------------------------------------------ tyre

        /// <summary>
        /// Tyre: outer sidewall (shoulder, bulge, rim protector ridge, bead) and the inner one as turned profiles; the
        /// running surface as a grid of 4 samples per pitch: a shoulder block row and a centre block row with slanted
        /// lateral grooves (opposite slants: a chevron), two circumferential grooves, a plain inner rib.
        /// </summary>
        private static void Tyre(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float r, float hw, float rimR)
        {
            var rubber = mb.Lib["Rubber"];
            float g1 = hw * 0.3f, g2 = -hw * 0.2f, g3 = -hw * 0.58f;
            // outer sidewall, from the tread shoulder down to the bead under the rim flange
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(r - 0.012f, hw - 0.008f), new Vector2(r - 0.032f, hw + 0.002f), new Vector2(r - 0.055f, hw + 0.008f),
                new Vector2(rimR + 0.045f, hw + 0.008f), new Vector2(rimR + 0.032f, hw + 0.005f), new Vector2(rimR + 0.024f, hw + 0.011f),
                new Vector2(rimR + 0.013f, hw + 0.003f), new Vector2(rimR + 0.008f, hw - 0.006f),
            }, Sides, rubber, 30f);
            // inner bead, sidewall, inner shoulder, plain rib with one groove, up to the second circumferential groove
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR + 0.008f, -hw + 0.006f), new Vector2(rimR + 0.03f, -hw - 0.006f), new Vector2(r - 0.04f, -hw - 0.004f),
                new Vector2(r - 0.012f, -hw + 0.008f), new Vector2(r, -hw + 0.028f), new Vector2(r, g3 - 0.006f), new Vector2(r - Groove, g3),
                new Vector2(r, g3 + 0.006f), new Vector2(r, g2 - 0.006f), new Vector2(r - Groove, g2),
            }, Sides, rubber, 30f);

            // block rows: columns across the tread (outer shoulder .. groove 2), 4 samples per pitch round it
            float[] colAx = { hw - 0.008f, hw - 0.028f, g1 + 0.006f, g1, g1 - 0.006f, g2 + 0.006f, g2 };
            float[] fixedR = { r - 0.012f, -1f, -1f, r - Groove, -1f, -1f, r - Groove };
            int[] low = { -1, 0, 1, -1, 3, 2, -1 }; // sample (of 4) where a block row's lateral groove bottoms out
            int m4 = Pitches * 4, cols = colAx.Length;
            var P = new Vector3[m4, cols];
            Vector3 Dir(float a) => Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
            for (int m = 0; m < m4; m++)
            {
                float a = m * Mathf.PI * 2f / m4;
                int pitch = m / 4, t = m % 4;
                for (int k = 0; k < cols; k++)
                {
                    Vector3 p;
                    if (fixedR[k] > 0f)
                    {
                        // fixed rings follow the chords of the turned parts they join
                        float a0 = pitch * Mathf.PI * 2f / Pitches, a1 = (pitch + 1) * Mathf.PI * 2f / Pitches;
                        p = Vector3.Lerp(Dir(a0), Dir(a1), t / 4f) * fixedR[k];
                    }
                    else p = Dir(a) * (t == low[k] ? r - Groove : r);
                    P[m, k] = c + p + ax * colAx[k];
                }
            }
            for (int m = 0; m < m4; m++)
            {
                int m1 = (m + 1) % m4;
                Vector3 radial = Dir((m + 0.5f) * Mathf.PI * 2f / m4);
                for (int k = 0; k < cols - 1; k++)
                {
                    Vector3 a = P[m, k], b = P[m1, k], cc = P[m1, k + 1], d = P[m, k + 1];
                    Vector3 n = Vector3.Cross(cc - a, d - b);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    n.Normalize();
                    mb.Quad(a, b, cc, d, n, n, n, n, rubber);
                }
            }
        }

        // ------------------------------------------------------------------ rim faces

        /// <summary>
        /// Rim face between radii r0 and r1 with N openings cut through it. Each sector is one tile: a ring of K points
        /// round its opening, the same directions mapped onto the sector's edges (spoke centre lines, hub and rim
        /// rings), a row in between; the edges stand <see cref="Openings.Ridge"/> proud (peaked spokes) and the opening
        /// has a wall going back behind the face. Neighbouring tiles share their edge points exactly.
        /// <paramref name="dishR0"/>..<paramref name="dishR1"/> is the range of the whole face (twist reference).
        /// </summary>
        private static void PolarFace(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float r0, float r1,
            float dishR0, float dishR1, Func<float, float> axial, Openings f, Material m)
        {
            float half = Mathf.PI / f.N;
            const float down = Mathf.PI * 0.5f;  // e2 = straight down for both sides: a spoke centre line sits there
            float Sp(float v, float e) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), e);
            Vector3 At(float rr, float th, float ay)
            {
                th += f.Twist * ((rr - dishR0) / (dishR1 - dishR0) - 0.5f);
                return c + (Mathf.Cos(th) * e1 + Mathf.Sin(th) * e2) * rr + ax * ay;
            }
            int K = f.K;
            var P = new Vector3[4, K];
            var N = new Vector3[4, K];
            for (int s = 0; s < f.N; s++)
            {
                float thc = down + (s + 0.5f) * 2f * half;
                Vector3 hole = At(f.Rc, thc, axial(f.Rc));
                for (int i = 0; i < K; i++)
                {
                    float a = i * Mathf.PI * 2f / K, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float rr = f.Rc + f.Ar * Sp(ca, f.E);
                    float w = Mathf.Lerp(f.WsHub, f.WsRim, Mathf.Clamp01((rr - r0) / (r1 - r0)));
                    float gap = f.Gap > 0f ? f.Gap : Mathf.Max(0.003f, rr * half - w);
                    float th = thc + gap * Sp(sa, f.E) / rr;
                    P[0, i] = At(rr, th, axial(rr) - 0.024f);
                    P[1, i] = At(rr, th, axial(rr) - 0.002f);
                    float mm = Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa));
                    float rb = Mathf.Lerp(r0, r1, 0.5f + 0.5f * ca / mm), tb = thc + half * sa / mm;
                    float rm = Mathf.Lerp(rr, rb, 0.45f);
                    P[2, i] = At(rm, Mathf.Lerp(th, tb, 0.45f), axial(rm) + f.Ridge * 0.55f);
                    P[3, i] = At(rb, tb, axial(rb) + f.Ridge);
                }
                for (int i = 0; i < K; i++)
                {
                    int ip = (i + 1) % K, im = (i + K - 1) % K;
                    Vector3 wall = hole - P[1, i];
                    wall -= ax * Vector3.Dot(wall, ax);
                    N[0, i] = wall.normalized;
                    for (int row = 1; row <= 3; row++)
                    {
                        Vector3 along = P[row, ip] - P[row, im];
                        Vector3 across = P[Mathf.Min(3, row + 1), i] - P[Mathf.Max(1, row - 1), i];
                        Vector3 n = Vector3.Cross(along, across);
                        if (Vector3.Dot(n, ax) < 0f) n = -n;
                        N[row, i] = n.normalized;
                    }
                }
                // the hole edge row shades like the face, tipped towards the opening (a rounded edge)
                var edge = new Vector3[K];
                for (int i = 0; i < K; i++) edge[i] = (N[1, i] + N[0, i] * 0.6f).normalized;
                for (int i = 0; i < K; i++)
                {
                    int ip = (i + 1) % K;
                    mb.Quad(P[0, i], P[0, ip], P[1, ip], P[1, i], N[0, i], N[0, ip], N[0, ip], N[0, i], m);
                    mb.Quad(P[1, i], P[1, ip], P[2, ip], P[2, i], edge[i], edge[ip], N[2, ip], N[2, i], m);
                    mb.Quad(P[2, i], P[2, ip], P[3, ip], P[3, i], N[2, i], N[2, ip], N[3, ip], N[3, i], m);
                }
            }
        }

        /// <summary>Hub: raised plate, five lug nuts, centre cap with a badge disc.</summary>
        private static void Hub(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float rh, float a0, Material rim,
            Material nuts, bool cap)
        {
            var lib = mb.Lib;
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rh + 0.006f, a0 - 0.006f), new Vector2(rh + 0.001f, a0 + 0.004f), new Vector2(rh * 0.8f, a0 + 0.006f),
                new Vector2(cap ? 0.03f : 0.024f, a0 + 0.006f), new Vector2(cap ? 0.03f : 0.024f, a0 + (cap ? 0.014f : 0.002f)),
                new Vector2(cap ? 0.022f : 0f, a0 + (cap ? 0.019f : 0.002f)), new Vector2(0f, a0 + (cap ? 0.02f : 0.002f)),
            }, 12, rim, 35f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                Vector3 p = c + d * (rh * 0.66f);
                mb.Cylinder(p + ax * (a0 + 0.004f), p + ax * (a0 + 0.016f), 0.0085f, 6, nuts);
            }
            if (cap)
                CarWheels.Lathe(mb, c + ax * (a0 + 0.0202f), ax, new[] { new Vector2(0.014f, 0f), new Vector2(0f, 0.0006f) }, 12, lib["Car_Trim"]);
        }

        /// <summary>
        /// Plastic wheel cover over a dark steel wheel: a rolled outer edge tucked into the tyre bead, a ring of slots
        /// cut through it (dark wheel behind), a domed centre with a badge.
        /// </summary>
        private static void Hubcap(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float hw, float rimR)
        {
            var lib = mb.Lib;
            var cover = lib["Car_RimSilver"];
            float rh = rimR * 0.48f, ro = rimR - 0.014f, face = hw + 0.003f;
            Func<float, float> axial = q => face + 0.005f * (1f - Mathf.Clamp01((q - rh) / (ro - rh)));
            CarWheels.Lathe(mb, c, ax, new[] { new Vector2(rimR - 0.002f, face - 0.035f), new Vector2(0f, face - 0.035f) }, Sides, lib["Car_Well"]);
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR + 0.004f, hw - 0.01f), new Vector2(rimR + 0.003f, hw + 0.004f), new Vector2(rimR - 0.005f, hw + 0.009f),
                new Vector2(ro, axial(ro) + 0.002f),
            }, Sides, cover, 50f);
            PolarFace(mb, c, ax, e1, e2, rh, ro, rh, ro, axial, new Openings
            {
                N = 10, K = 8, Rc = (rh + ro) * 0.5f + 0.004f, Ar = (ro - rh) * 0.3f, E = 0.7f, Gap = 0.008f, Ridge = 0.002f,
            }, cover);
            float a0 = axial(rh) + 0.002f;
            CarWheels.Lathe(mb, c, ax, new[]
            {
                new Vector2(rh + 0.002f, a0 - 0.003f), new Vector2(rh - 0.004f, a0 + 0.004f), new Vector2(rh * 0.55f, a0 + 0.01f),
                new Vector2(0f, a0 + 0.013f),
            }, Sides, cover, 40f);
            CarWheels.Lathe(mb, c + ax * (a0 + 0.0135f), ax, new[] { new Vector2(0.025f, 0f), new Vector2(0f, 0.0008f) }, 12, lib["Car_Trim"]);
        }
    }
}
