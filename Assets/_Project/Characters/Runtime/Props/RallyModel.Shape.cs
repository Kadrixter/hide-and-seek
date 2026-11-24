using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Body shape of the toy rally car (a compact 3-door WRC hatchback), car space: origin on the ground between the
    /// axles, +Z forward, metres - the RC car's footprint (wheelbase 0.27 m, track 0.21 m). Wide box flares over the
    /// wheels, short sloping hood, steep windscreen, flat roof, raked hatch. Built like <see cref="YarisModel"/>: key
    /// curves along z, every cross-section from them with fixed ring indices.
    /// </summary>
    public static partial class RallyModel
    {
        // ---- key stations (z)
        internal const float ZF = 0.247f, ZR = -0.232f;
        internal const float ZCowlF = 0.084f, ZCowl = 0.076f, ZRoofF = -0.004f, ZRoofR = -0.116f, ZHatchB = -0.198f;
        internal const float ZFrontGlassF = 0.064f, ZBpF = -0.05f, ZBpR = -0.062f, ZRearGlassR = -0.122f;

        // ---- wheels / arches (wheel centres = BlockyProps.RcWheelPos)
        internal const float WheelR = 0.045f, WheelW = 0.034f, TrackX = 0.105f;
        internal const float AxleF = 0.14f, AxleR = -0.13f, ArchR = 0.054f, Flare = 0.016f;

        // ---- ring indices of a half section (as the Yaris)
        internal const int RBC = 0, RWI = 1, RWT = 2, RLip = 3, RRk = 4, RLow = 5, RMax = 6, RSh = 7, RGB = 8, RGM = 9, RGT = 10, RRE = 11, RRM = 12, RC = 13;

        static readonly Vector2[] KW =
        {
            new(ZR, 0.088f), new(-0.228f, 0.097f), new(-0.22f, 0.103f), new(-0.2f, 0.106f), new(-0.1f, 0.108f),
            new(0.05f, 0.108f), new(0.17f, 0.106f), new(0.215f, 0.101f), new(0.232f, 0.092f), new(0.242f, 0.08f), new(ZF, 0.068f),
        };
        static readonly Vector2[] KBot =
        {
            new(ZR, 0.05f), new(-0.225f, 0.04f), new(-0.212f, 0.034f), new(0.215f, 0.034f), new(0.235f, 0.038f), new(ZF, 0.046f),
        };
        static readonly Vector2[] KBelt =
        {
            new(ZR, 0.104f), new(-0.226f, 0.11f), new(-0.21f, 0.116f), new(-0.17f, 0.119f), new(-0.1f, 0.118f), new(0f, 0.115f),
            new(ZCowl, 0.112f), new(0.13f, 0.108f), new(0.2f, 0.103f), new(0.232f, 0.097f), new(0.243f, 0.09f), new(ZF, 0.085f),
        };
        static readonly Vector2[] KRoof =
        {
            new(ZR, 0.106f), new(-0.228f, 0.113f), new(-0.215f, 0.12f), new(ZHatchB, 0.128f), new(-0.175f, 0.143f),
            new(-0.145f, 0.16f), new(ZRoofR, 0.17f), new(-0.08f, 0.1745f), new(-0.04f, 0.1752f), new(ZRoofF, 0.1728f),
            new(0.03f, 0.152f), new(0.055f, 0.1335f), new(ZCowl, 0.1195f), new(ZCowlF, 0.1172f), new(0.12f, 0.1125f),
            new(0.17f, 0.1085f), new(0.205f, 0.105f), new(0.228f, 0.1005f), new(0.24f, 0.095f), new(ZF, 0.0895f),
        };
        static readonly Vector2[] KRoofW =
        {
            new(ZR, 0.06f), new(ZHatchB, 0.068f), new(ZRoofR, 0.07f), new(-0.06f, 0.0725f), new(ZRoofF, 0.071f),
            new(0.04f, 0.078f), new(ZCowl, 0.088f), new(0.12f, 0.088f), new(0.2f, 0.08f), new(ZF, 0.058f),
        };
        static readonly float[] Arches = { AxleF, AxleR };

        /// <summary>Plan half width of the lower body with the wheel flares.</summary>
        internal static float W(float z) => Smooth(KW, z) + FlareAt(z);
        /// <summary>Half width without flares (shoulder / belt line).</summary>
        internal static float WBase(float z) => Smooth(KW, z);
        internal static float YBot(float z) => Smooth(KBot, z);
        internal static float YBelt(float z) => Smooth(KBelt, z);
        internal static float YRoof(float z) => Mathf.Max(Smooth(KRoof, z), YBelt(z) + 0.0015f);
        internal static float RoofW(float z) => Smooth(KRoofW, z);

        /// <summary>Box flare over each wheel: full width over the arch, fading out ahead / behind it.</summary>
        internal static float FlareAt(float z)
        {
            float best = 0f;
            foreach (float a in Arches)
            {
                float d = Mathf.Abs(z - a);
                float t = Mathf.InverseLerp(ArchR + 0.034f, ArchR + 0.004f, d);
                best = Mathf.Max(best, Flare * Mathf.SmoothStep(0f, 1f, t));
            }
            return best;
        }

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
            const float step = 0.0045f;
            int n = Mathf.CeilToInt((ZF - ZR) / step);
            for (int i = 0; i <= n; i++) zs.Add(Mathf.Lerp(ZR, ZF, i / (float)n));
            var breaks = new List<float>
            {
                ZCowlF, ZCowl, ZRoofF, ZRoofR, ZHatchB, ZFrontGlassF, ZBpF, ZBpR, ZRearGlassR,
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
            foreach (int r in new[] { RWI, RWT, RLip, RBC + 1, RSh }) loft.SharpRing.Add(r);
            return loft;
        }

        /// <summary>Half cross-section at z, bottom centre -> top centre (fixed ring indices).</summary>
        static Vector2[] Section(float z)
        {
            float w = W(z), w0 = WBase(z), yb = YBot(z), yBelt = YBelt(z), yRoof = YRoof(z), wr = Mathf.Min(RoofW(z), w0 - 0.012f);
            float aY = ArchTop(z);
            bool arch = aY > yb + 0.001f;
            var p = new Vector2[14];
            float wellX = w - 0.036f;
            p[RBC] = new Vector2(0f, yb);
            p[RWI] = new Vector2(wellX, yb);
            if (arch)
            {
                p[RWT] = new Vector2(wellX, aY);
                p[RLip] = new Vector2(w - 0.001f, aY);
                p[RRk] = new Vector2(w, aY + 0.003f);
            }
            else
            {
                p[RWT] = new Vector2(wellX, yb);
                p[RLip] = new Vector2(w - 0.006f, yb + 0.0004f);
                p[RRk] = new Vector2(w - 0.0012f, yb + 0.004f);
                aY = yb;
            }
            // The flare is a box: straight wall up to its top edge, then a step in to the narrower shoulder.
            float yFlareTop = Mathf.Max(aY + 0.009f, Mathf.Lerp(yb, yBelt, 0.62f));
            float ySh = Mathf.Max(yBelt - 0.003f, yFlareTop + 0.004f);
            float yLow = Mathf.Max(Mathf.Lerp(yb, yFlareTop, 0.45f), p[RRk].y + 0.001f);
            yFlareTop = Mathf.Max(yFlareTop, yLow + 0.001f);
            p[RLow] = new Vector2(w, yLow);
            p[RMax] = new Vector2(w - 0.0008f, yFlareTop);
            p[RSh] = new Vector2(Mathf.Min(w0 - 0.002f, w - 0.0015f), ySh);

            float h = Mathf.Max(yRoof - yBelt, 0.0015f);
            float f = Mathf.Clamp01(h / 0.045f);
            float wb = p[RSh].x - 0.0055f - 0.002f * (1f - f);
            p[RGB] = new Vector2(wb, yBelt + 0.0015f * f + 0.0004f);
            var gt = new Vector2(wr + 0.003f * f, yRoof - h * 0.16f);
            p[RGM] = new Vector2(Mathf.Lerp(p[RGB].x, gt.x, 0.5f) + 0.0018f * f, Mathf.Lerp(p[RGB].y, gt.y, 0.5f));
            p[RGT] = gt;
            p[RRE] = new Vector2(wr - 0.004f * f, yRoof - h * 0.05f);
            p[RRM] = new Vector2(wr * 0.5f, yRoof - h * 0.008f);
            p[RC] = new Vector2(0f, yRoof);
            for (int i = RLow; i <= RC; i++) p[i].y = Mathf.Max(p[i].y, p[i - 1].y + 0.0002f * (i <= RGT ? 1f : 0f));
            // Hood: one smooth crown from the fender line to the centre.
            if (z > ZCowl - 0.006f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ZCowl - 0.006f, ZCowlF + 0.008f, z));
                float y0 = p[RGB].y, rise = Mathf.Max(0f, yRoof - y0);
                for (int i = RGM; i <= RC; i++)
                {
                    float kx = (RC - i) / (float)(RC - RGB);
                    var hood = new Vector2(p[RGB].x * kx, y0 + rise * (1f - Mathf.Pow(kx, 2.2f)));
                    p[i] = Vector2.Lerp(p[i], hood, t);
                }
            }
            return p;
        }

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
