using System;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    internal enum RimStyle { FiveSpoke, CrossSpoke, Telephone, TenSpoke, FourHole, Turbine }

    /// <summary>Turned parts (tyres, rims, discs) and wheel arches for the detailed cars.</summary>
    internal static class CarWheels
    {
        /// <summary>
        /// Revolves a profile (x = radius, y = axial offset) around <paramref name="axis"/>. The profile runs from the
        /// -axis side over the outer radius to the +axis side, so normals point away from the solid.
        /// </summary>
        public static void Lathe(MeshBuilder mb, Vector3 c, Vector3 axis, Vector2[] prof, int sides, Material m,
            float smoothDeg = 50f)
        {
            axis.Normalize();
            Vector3 e1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(axis, e1);
            int n = prof.Length;
            var segN = new Vector2[n - 1];
            for (int k = 0; k < n - 1; k++)
            {
                Vector2 t = prof[k + 1] - prof[k];
                segN[k] = new Vector2(t.y, -t.x).normalized; // (radial, axial)
            }
            Vector2 VN(int k, int seg)
            {
                Vector2 own = segN[seg];
                int other = k == seg ? seg - 1 : seg + 1;
                if (other < 0 || other >= n - 1) return own;
                return Vector2.Angle(own, segN[other]) < smoothDeg ? (own + segN[other]).normalized : own;
            }
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = Mathf.Cos(a0) * e1 + Mathf.Sin(a0) * e2, d1 = Mathf.Cos(a1) * e1 + Mathf.Sin(a1) * e2;
                for (int k = 0; k < n - 1; k++)
                {
                    Vector2 p = prof[k], q = prof[k + 1];
                    Vector2 np = VN(k, k), nq = VN(k + 1, k);
                    mb.Quad(c + d0 * p.x + axis * p.y, c + d1 * p.x + axis * p.y, c + d1 * q.x + axis * q.y, c + d0 * q.x + axis * q.y,
                        d0 * np.x + axis * np.y, d1 * np.x + axis * np.y, d1 * nq.x + axis * nq.y, d0 * nq.x + axis * nq.y, m);
                }
            }
        }

        /// <summary>
        /// Full wheel at <paramref name="c"/>; <paramref name="side"/> = +1 for the right (outer face +x), -1 left.
        /// </summary>
        public static void Wheel(MeshBuilder mb, Vector3 c, float side, float r, float width, float rimR, RimStyle style,
            Material rimMat, Material accent = null)
        {
            var lib = mb.Lib;
            var rubber = lib["Rubber"];
            var dark = lib["Car_Well"];
            var disc = lib["Metal_Dark"];
            Vector3 ax = Vector3.right * side;
            float hw = width * 0.5f;
            // tyre with rounded shoulders and a little sidewall bulge
            Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR - 0.004f, -hw + 0.015f), new Vector2(rimR + 0.03f, -hw - 0.004f), new Vector2(r - 0.055f, -hw - 0.008f),
                new Vector2(r - 0.016f, -hw + 0.008f), new Vector2(r, -hw + 0.035f), new Vector2(r, hw - 0.035f),
                new Vector2(r - 0.016f, hw - 0.008f), new Vector2(r - 0.055f, hw + 0.008f), new Vector2(rimR + 0.03f, hw + 0.004f),
                new Vector2(rimR - 0.004f, hw - 0.015f),
            }, 32, rubber, 35f);
            // tread grooves
            foreach (float a in new[] { -0.32f, 0f, 0.32f })
                Lathe(mb, c + ax * (a * hw), ax, new[] { new Vector2(r + 0.0015f, -0.006f), new Vector2(r + 0.0015f, 0.006f) }, 32, dark);
            // rim barrel + outer lip
            float face = hw - 0.045f;
            Lathe(mb, c, ax, new[]
            {
                new Vector2(rimR - 0.02f, -hw + 0.03f), new Vector2(rimR, -hw + 0.035f), new Vector2(rimR, hw - 0.03f),
                new Vector2(rimR + 0.008f, hw - 0.018f), new Vector2(rimR - 0.006f, hw - 0.01f), new Vector2(rimR - 0.02f, hw - 0.022f),
                new Vector2(rimR - 0.03f, face),
            }, 32, rimMat, 40f);
            // dark inside of the barrel, brake disc and calliper behind the spokes
            Lathe(mb, c, ax, new[] { new Vector2(rimR - 0.02f, face - 0.12f), new Vector2(0f, face - 0.12f) }, 20, dark);
            Lathe(mb, c, ax, new[] { new Vector2(rimR * 0.2f, face - 0.065f), new Vector2(rimR * 0.78f, face - 0.065f),
                new Vector2(rimR * 0.78f, face - 0.045f), new Vector2(rimR * 0.2f, face - 0.045f) }, 24, disc);
            mb.Box(c + ax * (face - 0.04f) + new Vector3(0, rimR * 0.45f, -rimR * 0.5f), new Vector3(0.05f, rimR * 0.5f, rimR * 0.28f),
                accent ?? lib["Car_Trim"], Quaternion.Euler(-40f, 0, 0));
            Hub(mb, c, ax, face, rimR, style, rimMat, accent);
        }

        private static void Hub(MeshBuilder mb, Vector3 c, Vector3 ax, float face, float rimR, RimStyle style, Material rim,
            Material accent)
        {
            var lib = mb.Lib;
            var dark = lib["Car_Well"];
            var chrome = lib["Car_ChromeBright"];
            Vector3 e1 = Vector3.up, e2 = Vector3.Cross(ax, e1);
            float hubR = style == RimStyle.CrossSpoke ? 0.075f : 0.06f;
            // centre cap + nut ring
            Lathe(mb, c, ax, new[] { new Vector2(hubR, face - 0.02f), new Vector2(hubR, face + 0.004f), new Vector2(hubR * 0.6f, face + 0.012f), new Vector2(0f, face + 0.014f) }, 20, rim);
            Lathe(mb, c + ax * (face + 0.0145f), ax, new[] { new Vector2(hubR * 0.45f, 0f), new Vector2(0f, 0f) }, 16, accent ?? chrome);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                mb.Cylinder(c + ax * (face + 0.002f) + d * (hubR * 0.72f), c + ax * (face + 0.012f) + d * (hubR * 0.72f), 0.009f, 6, chrome);
            }
            float rOut = rimR - 0.022f;
            switch (style)
            {
                case RimStyle.FiveSpoke: Spokes(mb, c, ax, e1, e2, face, hubR, rOut, 5, 0.075f, 0.03f, rim, 0f); break;
                case RimStyle.TenSpoke:
                    Spokes(mb, c, ax, e1, e2, face, hubR, rOut, 5, 0.03f, 0.025f, rim, 0.13f);
                    Spokes(mb, c, ax, e1, e2, face, hubR, rOut, 5, 0.03f, 0.025f, rim, -0.13f);
                    break;
                case RimStyle.CrossSpoke:
                    Spokes(mb, c, ax, e1, e2, face, hubR, rOut, 10, 0.016f, 0.02f, rim, 0.33f);
                    Spokes(mb, c, ax, e1, e2, face - 0.006f, hubR, rOut, 10, 0.016f, 0.02f, rim, -0.33f);
                    Lathe(mb, c, ax, new[] { new Vector2(rOut - 0.004f, face - 0.006f), new Vector2(rOut - 0.004f, face + 0.008f), new Vector2(rOut - 0.024f, face + 0.008f) }, 32, chrome);
                    break;
                case RimStyle.Turbine: Spokes(mb, c, ax, e1, e2, face, hubR, rOut, 12, 0.03f, 0.018f, rim, 0.45f); break;
                case RimStyle.Telephone:
                case RimStyle.FourHole:
                {
                    // solid dish with round holes: dark discs set into a flat face
                    Lathe(mb, c, ax, new[] { new Vector2(rOut, face - 0.004f), new Vector2(hubR, face + 0.006f) }, 32, rim);
                    int holes = style == RimStyle.Telephone ? 5 : 4;
                    float hr = style == RimStyle.Telephone ? rOut * 0.2f : rOut * 0.24f;
                    for (int i = 0; i < holes; i++)
                    {
                        float a = i * Mathf.PI * 2f / holes;
                        Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                        Vector3 hc = c + d * ((rOut + hubR) * 0.52f) + ax * (face + 0.0025f);
                        Lathe(mb, hc, ax, new[] { new Vector2(hr + 0.008f, 0.0005f), new Vector2(hr, 0.0015f), new Vector2(0f, 0.0015f) }, 16, dark);
                    }
                    break;
                }
            }
        }

        /// <summary>Radial spokes; <paramref name="twist"/> leans the outer end (radians) for crossed / turbine looks.</summary>
        private static void Spokes(MeshBuilder mb, Vector3 c, Vector3 ax, Vector3 e1, Vector3 e2, float face, float r0, float r1,
            int count, float width, float thick, Material m, float twist)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Vector3 d0 = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                Vector3 d1 = Mathf.Cos(a + twist) * e1 + Mathf.Sin(a + twist) * e2;
                Vector3 p0 = c + d0 * (r0 * 0.9f) + ax * (face + 0.004f);
                Vector3 p1 = c + d1 * r1 + ax * (face - 0.012f);
                CarBlob.Bar(mb, p0, p1, ax, width * 1.2f, thick, m, 0.45f, 0.62f);
            }
        }

        /// <summary>
        /// Rolled arch lip: a bead of radius <paramref name="bead"/> following the arch edge at |x| = xLip, so the
        /// flare has its own rounded edge and throws a shadow under it.
        /// </summary>
        public static void ArchLip(MeshBuilder mb, Arch a, float yBot, float xLip, float side, Material m, float bead = 0.022f)
        {
            float r = a.R + bead * 0.4f;
            float s0 = Mathf.Asin(Mathf.Clamp((yBot + bead - a.Y) / r, -1f, 1f));
            const int n = 28, k = 10;
            Vector3 X = Vector3.right * side;
            var P = new Vector3[n + 1, k + 1];
            var N = new Vector3[n + 1, k + 1];
            for (int i = 0; i <= n; i++)
            {
                float th = Mathf.Lerp(s0, Mathf.PI - s0, i / (float)n);
                Vector3 rad = new Vector3(0f, Mathf.Sin(th), Mathf.Cos(th));
                Vector3 c = new Vector3(xLip * side, a.Y, a.Z) + rad * r;
                for (int j = 0; j <= k; j++)
                {
                    float ph = j * Mathf.PI * 2f / k;
                    Vector3 d = rad * Mathf.Cos(ph) + X * Mathf.Sin(ph);
                    P[i, j] = c + d * bead;
                    N[i, j] = d;
                }
            }
            for (int i = 0; i < n; i++)
            for (int j = 0; j < k; j++)
                mb.Quad(P[i, j], P[i + 1, j], P[i + 1, j + 1], P[i, j + 1], N[i, j], N[i + 1, j], N[i + 1, j + 1], N[i, j + 1], m);
        }

        /// <summary>Dark wheel-well liner: an arch strip between x0 and x1 (|x|) plus the inner wall, for one side.</summary>
        public static void Well(MeshBuilder mb, Arch a, float yBot, float xIn, float xOut, float side)
        {
            var m = mb.Lib["Car_Well"];
            float r = a.R - 0.004f;
            float s0 = Mathf.Asin(Mathf.Clamp((yBot - a.Y) / r, -1f, 1f));
            int n = 18;
            Vector3 Pt(float th, float x) => new Vector3(x * side, a.Y + Mathf.Sin(th) * r, a.Z + Mathf.Cos(th) * r);
            Vector3 inward = Vector3.right * side;
            for (int i = 0; i < n; i++)
            {
                float t0 = Mathf.Lerp(s0, Mathf.PI - s0, i / (float)n), t1 = Mathf.Lerp(s0, Mathf.PI - s0, (i + 1) / (float)n);
                Vector3 n0 = -new Vector3(0, Mathf.Sin(t0), Mathf.Cos(t0)), n1 = -new Vector3(0, Mathf.Sin(t1), Mathf.Cos(t1));
                mb.Quad(Pt(t0, xIn), Pt(t0, xOut), Pt(t1, xOut), Pt(t1, xIn), n0, n0, n1, n1, m);
                mb.Tri(new Vector3(xIn * side, yBot, a.Z), Pt(t0, xIn + 0.001f), Pt(t1, xIn + 0.001f), inward, inward, inward, m);
            }
        }
    }
}
