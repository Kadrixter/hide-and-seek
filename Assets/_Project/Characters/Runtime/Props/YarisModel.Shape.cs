using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Body shape of the toy Yaris (XP90 facelift, 5-door), car space: origin on the ground between the axles,
    /// +Z forward, metres (about 1:9 of the real car, wheelbase 0.27 m like the RC car). The silhouette is given by
    /// smooth key curves along z (plan width, sill, belt line, centre line of hood / windscreen / roof / hatch, roof
    /// width); every cross-section is built from them with fixed ring indices so panels can be addressed.
    /// </summary>
    public static partial class YarisModel
    {
        // ---- key stations (z)
        internal const float ZF = 0.2235f, ZR = -0.1935f;
        internal const float ZCowlF = 0.112f, ZCowl = 0.104f, ZRoofF = 0.012f, ZRoofR = -0.150f, ZHatchB = -0.180f;
        internal const float ZQuarterF = 0.092f, ZQuarterR = 0.072f;           // small fixed glass at the A-pillar base
        internal const float ZFrontGlassF = 0.068f, ZBpF = -0.004f, ZBpR = -0.016f, ZRearGlassR = -0.098f, ZCGlassF = -0.106f, ZCGlassR = -0.128f;

        // ---- wheels / arches
        internal const float WheelR = 0.036f, WheelW = 0.026f, TrackX = 0.0785f;
        internal const float AxleF = 0.14f, AxleR = -0.13f, ArchR = 0.042f;

        // ---- ring indices of a half section
        internal const int RBC = 0, RWI = 1, RWT = 2, RLip = 3, RRk = 4, RLow = 5, RMax = 6, RSh = 7, RGB = 8, RGM = 9, RGT = 10, RRE = 11, RRM = 12, RC = 13;

        static readonly Vector2[] KW =
        {
            new(ZR, 0.074f), new(-0.191f, 0.0835f), new(-0.186f, 0.0885f), new(-0.175f, 0.0918f), new(-0.12f, 0.0932f),
            new(0.0f, 0.094f), new(0.1f, 0.0935f), new(0.16f, 0.0915f), new(0.19f, 0.0878f), new(0.2f, 0.0842f),
            new(0.21f, 0.0775f), new(0.217f, 0.0688f), new(ZF, 0.055f),
        };
        static readonly Vector2[] KBot =
        {
            new(ZR, 0.029f), new(-0.188f, 0.0245f), new(-0.17f, 0.0215f), new(0.17f, 0.021f), new(0.212f, 0.0235f), new(ZF, 0.0275f),
        };
        static readonly Vector2[] KBelt =
        {
            new(ZR, 0.103f), new(-0.186f, 0.1105f), new(-0.14f, 0.1115f), new(-0.06f, 0.1095f), new(0.03f, 0.1065f),
            new(ZCowl, 0.1025f), new(0.15f, 0.0945f), new(0.19f, 0.0855f), new(0.205f, 0.0785f), new(0.215f, 0.0715f), new(ZF, 0.063f),
        };
        static readonly Vector2[] KRoof =
        {
            new(ZR, 0.1075f), new(-0.191f, 0.1115f), new(ZHatchB, 0.1185f), new(-0.168f, 0.1385f), new(-0.158f, 0.1525f),
            new(ZRoofR, 0.1595f), new(-0.13f, 0.1655f), new(-0.07f, 0.1685f), new(-0.02f, 0.1675f), new(ZRoofF, 0.1635f),
            new(0.04f, 0.151f), new(0.075f, 0.1325f), new(ZCowl, 0.1155f), new(ZCowlF, 0.1105f), new(0.14f, 0.1035f),
            new(0.18f, 0.0965f), new(0.2f, 0.0905f), new(0.212f, 0.0815f), new(ZF, 0.0665f),
        };
        static readonly Vector2[] KRoofW =
        {
            new(ZR, 0.052f), new(ZHatchB, 0.0705f), new(ZRoofR, 0.0645f), new(-0.07f, 0.0655f), new(ZRoofF, 0.0665f),
            new(0.06f, 0.0745f), new(ZCowl, 0.0815f), new(0.125f, 0.0655f), new(0.16f, 0.0565f), new(0.2f, 0.046f), new(ZF, 0.032f),
        };
        static readonly float[] Arches = { AxleF, AxleR };

        internal static float W(float z) => Smooth(KW, z);
        internal static float YBot(float z) => Smooth(KBot, z);
        internal static float YBelt(float z) => Smooth(KBelt, z);
        internal static float YRoof(float z) => Mathf.Max(Smooth(KRoof, z), YBelt(z) + 0.0015f);
        internal static float RoofW(float z) => Smooth(KRoofW, z);

        /// <summary>Top of the wheel opening at z (-inf outside the arches).</summary>
        internal static float ArchTop(float z)
        {
            float best = float.NegativeInfinity;
            foreach (float a in Arches)
            {
                float d = z - a;
                if (Mathf.Abs(d) < ArchR) best = Mathf.Max(best, WheelR + Mathf.Sqrt(ArchR * ArchR - d * d));
            }
            return best;
        }

        internal static YarisLoft BuildBodyLoft()
        {
            var zs = new List<float>();
            const float step = 0.0042f;
            int n = Mathf.CeilToInt((ZF - ZR) / step);
            for (int i = 0; i <= n; i++) zs.Add(Mathf.Lerp(ZR, ZF, i / (float)n));
            var breaks = new List<float>
            {
                ZCowlF, ZCowl, ZRoofF, ZRoofR, ZHatchB, ZQuarterF, ZQuarterR, ZFrontGlassF, ZBpF, ZBpR, ZRearGlassR, ZCGlassF, ZCGlassR,
                ZR + 0.0012f, ZF - 0.0012f, ZR + 0.003f, ZF - 0.003f,
            };
            foreach (float a in Arches)
            {
                breaks.Add(a - ArchR - 0.0004f); breaks.Add(a - ArchR + 0.0004f);
                breaks.Add(a + ArchR - 0.0004f); breaks.Add(a + ArchR + 0.0004f);
                for (int k = -6; k <= 6; k++) breaks.Add(a + ArchR * k / 7f);
            }
            foreach (float b in breaks) if (b > ZR && b < ZF) zs.Add(b);
            zs.Sort();
            var clean = new List<float>();
            foreach (float z in zs) if (clean.Count == 0 || z - clean[clean.Count - 1] > 0.0006f) clean.Add(z);

            var secs = new List<Vector2[]>();
            foreach (float z in clean) secs.Add(Section(z));
            var loft = new YarisLoft(clean, secs);
            foreach (int r in new[] { RWI, RWT, RLip, RBC + 1 }) loft.SharpRing.Add(r);
            return loft;
        }

        /// <summary>Half cross-section at z, bottom centre -> top centre (fixed ring indices).</summary>
        static Vector2[] Section(float z)
        {
            float w = W(z), yb = YBot(z), yBelt = YBelt(z), yRoof = YRoof(z), wr = Mathf.Min(RoofW(z), w - 0.012f);
            float aY = ArchTop(z);
            bool arch = aY > yb + 0.001f;
            var p = new Vector2[14];
            float wellX = w - 0.034f;
            p[RBC] = new Vector2(0f, yb);
            p[RWI] = new Vector2(wellX, yb);
            if (arch)
            {
                p[RWT] = new Vector2(wellX, aY);
                p[RLip] = new Vector2(w - 0.0016f, aY);
                p[RRk] = new Vector2(w - 0.0003f, aY + 0.0035f);
            }
            else
            {
                p[RWT] = new Vector2(wellX, yb);
                p[RLip] = new Vector2(w - 0.0075f, yb + 0.0004f);
                p[RRk] = new Vector2(w - 0.0018f, yb + 0.0042f);
                aY = yb;
            }
            float ySh = yBelt - 0.0028f;
            float yLow = Mathf.Min(Mathf.Max(Mathf.Lerp(yb, yBelt, 0.3f), aY + 0.0075f), ySh - 0.008f);
            float yMax = Mathf.Min(Mathf.Max(Mathf.Lerp(yb, yBelt, 0.64f), aY + 0.0125f), ySh - 0.0035f);
            yLow = Mathf.Max(yLow, p[RRk].y + 0.001f);
            yMax = Mathf.Max(yMax, yLow + 0.001f);
            ySh = Mathf.Max(ySh, yMax + 0.001f);
            // The flank tucks in a little towards the sill (the lower body is not a straight wall).
            p[RLow] = new Vector2(w - 0.0006f, yLow);
            p[RMax] = new Vector2(w, yMax);
            p[RSh] = new Vector2(w - 0.0024f, ySh);

            float h = Mathf.Max(yRoof - yBelt, 0.0015f);
            float f = Mathf.Clamp01(h / 0.045f);
            float wb = w - 0.0072f - 0.0025f * (1f - f);
            p[RGB] = new Vector2(wb, yBelt + 0.0016f * f + 0.0004f);
            var gt = new Vector2(wr + 0.0034f * f, yRoof - h * 0.27f);
            p[RGM] = new Vector2(Mathf.Lerp(p[RGB].x, gt.x, 0.5f) + 0.0028f * f, Mathf.Lerp(p[RGB].y, gt.y, 0.5f));
            p[RGT] = gt;
            p[RRE] = new Vector2(wr - 0.0045f * f, yRoof - h * 0.085f);
            p[RRM] = new Vector2(wr * 0.5f, yRoof - h * 0.012f);
            p[RC] = new Vector2(0f, yRoof);
            for (int i = RGB; i <= RC; i++) p[i].y = Mathf.Max(p[i].y, p[i - 1].y + 0.0002f * (i <= RGT ? 1f : 0f));
            // Hood (and the cowl in front of the windscreen): one smooth crown from the fender line to the centre.
            if (z > ZCowl - 0.006f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZCowl - 0.006f, ZCowlF + 0.009f, z));
                float y0 = p[RGB].y, rise = Mathf.Max(0f, yRoof - y0);
                for (int i = RGM; i <= RC; i++)
                {
                    float kx = (RC - i) / (float)(RC - RGB);
                    var hood = new Vector2(p[RGB].x * kx, y0 + rise * (1f - Mathf.Pow(kx, 2.4f)));
                    p[i] = Vector2.Lerp(p[i], hood, t);
                }
            }
            return p;
        }

        // ================================================================ curves

        /// <summary>Monotone cubic (Fritsch-Carlson) through the keys - smooth without overshoot.</summary>
        static float Smooth(Vector2[] k, float x)
        {
            int n = k.Length;
            if (x <= k[0].x) return k[0].y;
            if (x >= k[n - 1].x) return k[n - 1].y;
            int i = 0;
            while (i < n - 2 && x > k[i + 1].x) i++;
            float Slope(int a) => (k[a + 1].y - k[a].y) / (k[a + 1].x - k[a].x);
            float Tan(int a)
            {
                if (a == 0) return Slope(0);
                if (a == n - 1) return Slope(n - 2);
                float s0 = Slope(a - 1), s1 = Slope(a);
                if (s0 * s1 <= 0f) return 0f;
                return 2f / (1f / s0 + 1f / s1);
            }
            float hx = k[i + 1].x - k[i].x, t = (x - k[i].x) / hx;
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * k[i].y + (t3 - 2f * t2 + t) * hx * Tan(i) +
                   (-2f * t3 + 3f * t2) * k[i + 1].y + (t3 - t2) * hx * Tan(i + 1);
        }
    }
}
