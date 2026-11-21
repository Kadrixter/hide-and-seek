using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Lower body profile at one z: widths and heights of the cross-section (half widths, metres).</summary>
    internal struct BodyKey
    {
        public float Z, YBot, WBot, WMax, YMax, WSh, YSh, YTop, Round, Tub;
        public BodyKey(float z, float yBot, float wBot, float wMax, float yMax, float wSh, float ySh, float yTop,
            float round = 1f, float tub = 0f)
        {
            Z = z; YBot = yBot; WBot = wBot; WMax = wMax; YMax = yMax; WSh = wSh; YSh = ySh; YTop = yTop; Round = round; Tub = tub;
        }
    }

    /// <summary>Greenhouse profile at one z: belt line (glass base) and roof line (half widths).</summary>
    internal struct CabinKey
    {
        public float Z, YBelt, WBelt, YRoof, WRoof;
        public CabinKey(float z, float yBelt, float wBelt, float yRoof, float wRoof)
        {
            Z = z; YBelt = yBelt; WBelt = wBelt; YRoof = yRoof; WRoof = wRoof;
        }
    }

    /// <summary>A wheel opening: centre z, axle height and arch radius.</summary>
    internal struct Arch
    {
        public float Z, Y, R;
        public Arch(float z, float y, float r) { Z = z; Y = y; R = r; }
        public float Top(float z)
        {
            float d = z - Z;
            return Mathf.Abs(d) >= R ? float.NegativeInfinity : Y + Mathf.Sqrt(R * R - d * d);
        }
    }

    /// <summary>Turns body / cabin keys into lofts. Ring indices are fixed so models can address panels.</summary>
    internal static class CarShapes
    {
        // body ring indices
        public const int BUnder = 0, BWellIn = 1, BWellTop = 2, BLip = 3, BRocker = 4, BLower = 5, BMax = 6, BUpper = 7,
            BShoulder = 8, BRound = 9, BDeck1 = 10, BDeck2 = 11, BCentre = 12;
        // cabin ring indices
        public const int CBelt = 0, CSeal = 1, CGlassMid = 2, CGlassTop = 3, CFrame = 4, CRoofEdge = 5, CRoof = 6, CCentre = 7;

        public static CarLoft Body(IList<BodyKey> keys, IList<Arch> arches, float wellInset, float tubWall, float tubFloor,
            IEnumerable<float> breaks, float step = 0.035f)
        {
            var k = keys.OrderBy(q => q.Z).ToArray();
            float[] xs = k.Select(q => q.Z).ToArray();
            float F(System.Func<BodyKey, float> f, float z) => SmoothCurve.Eval(xs, k.Select(f).ToArray(), z);

            var zs = Stations(xs[0], xs[xs.Length - 1], step, breaks.Concat(arches.SelectMany(a => new[]
                { a.Z - a.R - 0.001f, a.Z - a.R + 0.001f, a.Z + a.R - 0.001f, a.Z + a.R + 0.001f })).Concat(xs));
            var secs = new List<Vector2[]>();
            foreach (float z in zs)
            {
                float yBot = F(q => q.YBot, z), wBot = F(q => q.WBot, z), wMax = F(q => q.WMax, z), yMax = F(q => q.YMax, z);
                float wSh = F(q => q.WSh, z), ySh = F(q => q.YSh, z), yTop = F(q => q.YTop, z), round = F(q => q.Round, z);
                float tub = Mathf.Clamp01(F(q => q.Tub, z));
                float aY = yBot;
                foreach (var a in arches) aY = Mathf.Max(aY, a.Top(z));
                float inX = wBot - wellInset;
                float crown = yTop - ySh;
                var p = new Vector2[13];
                p[BUnder] = new Vector2(0f, yBot);
                p[BWellIn] = new Vector2(inX, yBot);
                p[BWellTop] = new Vector2(inX, aY);
                p[BLip] = new Vector2(wBot - 0.022f, aY);
                p[BRocker] = new Vector2(wBot, aY + 0.012f);
                p[BLower] = new Vector2(Mathf.Lerp(wBot, wMax, 0.72f), Mathf.Max(Mathf.Lerp(yBot, yMax, 0.45f), aY + 0.03f));
                p[BMax] = new Vector2(wMax, Mathf.Max(yMax, aY + 0.05f));
                p[BUpper] = new Vector2(Mathf.Lerp(wMax, wSh, 0.55f), Mathf.Max(Mathf.Lerp(yMax, ySh, 0.6f), aY + 0.065f));
                p[BShoulder] = new Vector2(wSh, Mathf.Max(ySh, aY + 0.08f));
                float yS = p[BShoulder].y;
                var r9 = new Vector2(wSh - 0.012f - 0.05f * round, yS + Mathf.Max(0.004f, crown * 0.22f + 0.022f * round));
                var d1 = new Vector2(wSh * 0.7f, yS + crown * 0.78f + 0.012f * round);
                var d2 = new Vector2(wSh * 0.36f, yS + crown * 0.97f + 0.004f * round);
                var c = new Vector2(0f, yTop + 0.004f * round);
                // cabin tub: door top, inner door panel, floor
                var t9 = new Vector2(wSh - 0.015f, yS + 0.012f);
                var t10 = new Vector2(tubWall, yS + 0.01f);
                var t11 = new Vector2(tubWall, tubFloor);
                var t12 = new Vector2(0f, tubFloor);
                p[BRound] = Vector2.Lerp(r9, t9, tub);
                p[BDeck1] = Vector2.Lerp(d1, t10, tub);
                p[BDeck2] = Vector2.Lerp(d2, t11, tub);
                p[BCentre] = Vector2.Lerp(c, t12, tub);
                secs.Add(p);
            }
            var loft = new CarLoft(zs, secs);
            loft.SharpRing.Add(BWellIn);
            loft.SharpRing.Add(BWellTop);
            loft.SharpRing.Add(BLip);
            loft.SharpRing.Add(BUnder + 1);
            return loft;
        }

        public static CarLoft Cabin(IList<CabinKey> keys, IEnumerable<float> breaks, float roofRound = 1f, float step = 0.03f,
            float roofDip = 0f)
        {
            var k = keys.OrderBy(q => q.Z).ToArray();
            float[] xs = k.Select(q => q.Z).ToArray();
            float F(System.Func<CabinKey, float> f, float z) => SmoothCurve.Eval(xs, k.Select(f).ToArray(), z);
            var zs = Stations(xs[0], xs[xs.Length - 1], step, breaks.Concat(xs));
            var secs = new List<Vector2[]>();
            foreach (float z in zs)
            {
                float yB = F(q => q.YBelt, z), wB = F(q => q.WBelt, z), yR = Mathf.Max(F(q => q.YRoof, z), yB), wR = F(q => q.WRoof, z);
                float h = yR - yB;
                float seal = Mathf.Min(0.022f, h * 0.15f), frame = Mathf.Min(0.03f, h * 0.15f), edge = Mathf.Min(0.03f * roofRound, h * 0.12f);
                float wTop = wR + 0.012f * roofRound;
                var p = new Vector2[8];
                p[CBelt] = new Vector2(wB, yB);
                p[CSeal] = new Vector2(Mathf.Lerp(wB, wTop, seal / Mathf.Max(h, 1e-4f)), yB + seal);
                float yTopGlass = yR - edge - frame;
                p[CGlassMid] = new Vector2(Mathf.Lerp(wB, wTop, 0.55f) + 0.012f * Mathf.Clamp01(h / 0.3f), Mathf.Lerp(yB + seal, yTopGlass, 0.55f));
                p[CGlassTop] = new Vector2(wTop, yTopGlass);
                p[CFrame] = new Vector2(wTop - 0.004f, yR - edge);
                p[CRoofEdge] = new Vector2(wR - 0.03f * roofRound, yR - 0.002f);
                p[CRoof] = new Vector2(wR * 0.5f, yR + 0.012f * roofRound * Mathf.Clamp01(h / 0.2f));
                // roofDip > 0: a shallow channel down the middle (double-bubble roof)
                p[CCentre] = new Vector2(0f, yR + (0.015f * roofRound - roofDip) * Mathf.Clamp01(h / 0.2f));
                if (p[CRoofEdge].x < p[CRoof].x) p[CRoofEdge].x = p[CRoof].x + 0.005f;
                secs.Add(p);
            }
            var loft = new CarLoft(zs, secs);
            loft.SharpRing.Add(CSeal);
            loft.SharpRing.Add(CGlassTop);
            return loft;
        }

        /// <summary>Uniform stations plus every requested break (deduplicated, ascending).</summary>
        private static List<float> Stations(float z0, float z1, float step, IEnumerable<float> breaks)
        {
            var list = new List<float>();
            int n = Mathf.Max(2, Mathf.CeilToInt((z1 - z0) / step));
            for (int i = 0; i <= n; i++) list.Add(Mathf.Lerp(z0, z1, i / (float)n));
            foreach (float b in breaks)
                if (b > z0 && b < z1) list.Add(b);
            list.Sort();
            var outList = new List<float>();
            foreach (float z in list)
                if (outList.Count == 0 || z - outList[outList.Count - 1] > 0.0015f) outList.Add(z);
            return outList;
        }
    }
}
