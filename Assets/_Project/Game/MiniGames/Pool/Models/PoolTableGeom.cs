using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Plan (x, z) geometry of the pool table (2026-11-25), all in table space (see <see cref="PoolDims"/>).
    /// Cushions are 5 cm deep (nose to rail), the wooden rails 11 cm. Pocket mouths are measured between the cushion
    /// noses; the facings are cut at 142 deg (corner) / 104 deg (side) to the nose line, so the jaws narrow to a throat
    /// at the back of the cushion. Each pocket hole is a circle through its two throat points: its short arc (table
    /// side) cuts the bed, its long arc (rail side) cuts the rails. Pockets: 0 (-X,-Z), 1 (0,-Z), 2 (+X,-Z),
    /// 3 (-X,+Z), 4 (0,+Z), 5 (+X,+Z).
    /// </summary>
    internal static class PoolTableGeom
    {
        public const float HalfL = PoolDims.PlayL * 0.5f, HalfW = PoolDims.PlayW * 0.5f;
        public const float CushionW = 0.05f, RailW = 0.11f;
        /// <summary>Back of the cushions = inner edge of the rails = edge of the cloth-covered bed.</summary>
        public const float BedX = HalfL + CushionW, BedZ = HalfW + CushionW;
        public const float OutX = BedX + RailW, OutZ = BedZ + RailW;
        /// <summary>Plan radius of the rails' outer corners, width of the rails' outer edge moulding.</summary>
        public const float OuterR = 0.045f, MouldW = 0.012f;
        public const float CornerHoleR = 0.064f, SideHoleR = 0.058f;
        public const float SlateT = 0.03f;
        /// <summary>The apron (frame) runs this far inside the rails' outer edge and this deep under the cloth.</summary>
        public const float FrameInset = 0.035f, FrameHX = OutX - FrameInset, FrameHZ = OutZ - FrameInset, ApronH = 0.18f;
        /// <summary>Pocket baskets: rim below the cloth, depth of the net.</summary>
        public const float BagRimY = -0.052f, CornerBagDepth = 0.15f, SideBagDepth = 0.13f;
        /// <summary>Sights: distance from the cushion nose to the middle of the inlay.</summary>
        public const float SightD = 0.1f;
        /// <summary>How far the castings reach along the rails past the throats.</summary>
        public const float CornerReach = 0.045f, SideReach = 0.035f;

        /// <summary>Facing slope: how far the cushion end moves towards its pocket per metre outwards from the nose.</summary>
        public static readonly float CornerK = 1f / Mathf.Tan((180f - 142f) * Mathf.Deg2Rad);
        public static readonly float SideK = 1f / Mathf.Tan((180f - 104f) * Mathf.Deg2Rad);
        /// <summary>Nose ends: distance from the corner of the nose lines (corner) / from the middle of the table (side).</summary>
        public static readonly float CornerJaw = PoolDims.CornerMouth / Mathf.Sqrt(2f);
        public static readonly float SideJaw = PoolDims.SideMouth * 0.5f;
        public static readonly float CornerThroat = CornerJaw - CornerK * CushionW;
        public static readonly float SideThroat = SideJaw - SideK * CushionW;
        /// <summary>Hole centres outside the nose lines (corner: along both axes).</summary>
        public static readonly float CornerOff = SolveCornerOff();
        public static readonly float SideOff = CushionW + Mathf.Sqrt(SideHoleR * SideHoleR - SideThroat * SideThroat);
        /// <summary>Frame corners are cut at |x| + |z| = ChamferK (clear of the corner baskets); notches round the side baskets.</summary>
        public static readonly float ChamferK = (HalfL + CornerOff) + (HalfW + CornerOff) - (CornerHoleR + 0.034f) * Mathf.Sqrt(2f);
        public static readonly float NotchR = SideHoleR + 0.034f;

        static float SolveCornerOff()
        {
            // Centre (e, e) outside the nose corner, circle through (-throat, cushion) and (cushion, -throat).
            float b = CornerThroat - CushionW, k = CornerThroat * CornerThroat + CushionW * CushionW - CornerHoleR * CornerHoleR;
            return (-b + Mathf.Sqrt(b * b - 2f * k)) * 0.5f;
        }

        public static int Sx(int i) => i % 3 - 1;
        public static int Sz(int i) => i < 3 ? -1 : 1;
        public static bool IsSide(int i) => i % 3 == 1;
        public static float HoleR(int i) => IsSide(i) ? SideHoleR : CornerHoleR;

        public static Vector2 Centre(int i) => IsSide(i)
            ? new Vector2(0f, Sz(i) * (HalfW + SideOff))
            : new Vector2(Sx(i) * (HalfL + CornerOff), Sz(i) * (HalfW + CornerOff));

        /// <summary>Throat of a corner pocket on its long rail / on its short rail; of a side pocket towards -X (s = -1) or +X.</summary>
        public static Vector2 TLong(int i) => new Vector2(Sx(i) * (HalfL - CornerThroat), Sz(i) * BedZ);
        public static Vector2 TShort(int i) => new Vector2(Sx(i) * BedX, Sz(i) * (HalfW - CornerThroat));
        public static Vector2 TSide(int i, int s) => new Vector2(s * SideThroat, Sz(i) * BedZ);

        /// <summary>Throats in the counter-clockwise walk round the bed (entry, exit).</summary>
        public static (Vector2 entry, Vector2 exit) BedThroats(int i) => i switch
        {
            0 => (TShort(0), TLong(0)),
            1 => (TSide(1, -1), TSide(1, 1)),
            2 => (TLong(2), TShort(2)),
            5 => (TShort(5), TLong(5)),
            4 => (TSide(4, 1), TSide(4, -1)),
            _ => (TLong(3), TShort(3)),
        };

        /// <summary>Outermost point of the hole, where the rails are split.</summary>
        public static Vector2 Split(int i) => Centre(i) + (IsSide(i) ? new Vector2(0f, Sz(i)) : new Vector2(Sx(i), Sz(i)).normalized) * HoleR(i);

        /// <summary>Arc of circle (c, r) from a to b (both on it), the short or the long way; a and b included.</summary>
        public static List<Vector2> ArcPts(Vector2 c, float r, Vector2 a, Vector2 b, bool longWay, float stepDeg = 6f)
        {
            float a0 = Mathf.Atan2(a.y - c.y, a.x - c.x) * Mathf.Rad2Deg, a1 = Mathf.Atan2(b.y - c.y, b.x - c.x) * Mathf.Rad2Deg;
            float d = Mathf.DeltaAngle(a0, a1);
            if (longWay) d = d > 0f ? d - 360f : d + 360f;
            int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(d) / stepDeg));
            var p = new List<Vector2>(n + 1) { a };
            for (int k = 1; k < n; k++)
            {
                float t = (a0 + d * k / n) * Mathf.Deg2Rad;
                p.Add(c + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * r);
            }
            p.Add(b);
            return p;
        }

        // ================================================================ outlines

        /// <summary>Cloth-covered bed: the rectangle behind the cushions with the short (table-side) arcs of the holes.</summary>
        public static List<Vector2> BedOutline()
        {
            var p = new List<Vector2>();
            foreach (int i in new[] { 0, 1, 2, 5, 4, 3 })
            {
                var (entry, exit) = BedThroats(i);
                p.AddRange(ArcPts(Centre(i), HoleR(i), entry, exit, false));
            }
            return p;
        }

        /// <summary>Rounded outer rectangle of the rails, inset by <paramref name="inset"/>, counter-clockwise; mark = index of each pocket's split.</summary>
        public static List<Vector2> OuterPath(float inset, out int[] mark, int segs = 8)
        {
            float hx = OutX - inset, hz = OutZ - inset, r = Mathf.Max(0.004f, OuterR - inset);
            var p = new List<Vector2>();
            var mk = new int[6];
            void Corner(int sx, int sz, float a0, int pocket)
            {
                for (int i = 0; i <= segs; i++)
                {
                    if (i == segs / 2) mk[pocket] = p.Count;
                    float a = (a0 + 90f * i / segs) * Mathf.Deg2Rad;
                    p.Add(new Vector2(sx * (hx - r) + r * Mathf.Cos(a), sz * (hz - r) + r * Mathf.Sin(a)));
                }
            }
            Corner(1, -1, 270f, 2);
            Corner(1, 1, 0f, 5);
            mk[4] = p.Count; p.Add(new Vector2(0f, hz));
            Corner(-1, 1, 90f, 3);
            Corner(-1, -1, 180f, 0);
            mk[1] = p.Count; p.Add(new Vector2(0f, -hz));
            mark = mk;
            return p;
        }

        /// <summary>Rail pieces between pockets (a, b), b following a counter-clockwise. Pieces 2 and 5 are the short rails.</summary>
        public static readonly (int a, int b)[] RailPieces = { (0, 1), (1, 2), (2, 5), (5, 4), (4, 3), (3, 0) };

        /// <summary>Plan of rail piece k (inside the edge moulding): inner edge, half of each hole, split lines, outer edge.</summary>
        public static List<Vector2> RailPiece(int k)
        {
            var (a, b) = RailPieces[k];
            Vector2 tA = BedThroats(a).exit, tB = BedThroats(b).entry;
            var outer = OuterPath(MouldW, out var mark);
            var poly = ArcPts(Centre(b), HoleR(b), tB, Split(b), false);
            for (int j = mark[b]; ; j = (j - 1 + outer.Count) % outer.Count)
            {
                poly.Add(outer[j]);
                if (j == mark[a]) break;
            }
            var arc = ArcPts(Centre(a), HoleR(a), Split(a), tA, false);
            poly.AddRange(arc);
            return poly;
        }

        /// <summary>Plan of the apron: inset rectangle, corners chamfered clear of the corner baskets, notches round the side ones.</summary>
        public static List<Vector2> FramePath()
        {
            float hx = FrameHX, hz = FrameHZ, k = ChamferK;
            var p = new List<Vector2>();
            Notch(p, -1);
            p.Add(new Vector2(k - hz, -hz)); p.Add(new Vector2(hx, -(k - hx)));
            p.Add(new Vector2(hx, k - hx)); p.Add(new Vector2(k - hz, hz));
            Notch(p, 1);
            p.Add(new Vector2(-(k - hz), hz)); p.Add(new Vector2(-hx, k - hx));
            p.Add(new Vector2(-hx, -(k - hx))); p.Add(new Vector2(-(k - hz), -hz));
            return p;
        }

        static void Notch(List<Vector2> p, int sz)
        {
            float cz = HalfW + SideOff, dz = FrameHZ - cz;
            float nx = Mathf.Sqrt(NotchR * NotchR - dz * dz);
            Vector2 c = new Vector2(0f, sz * cz);
            p.AddRange(ArcPts(c, NotchR, new Vector2(sz * nx, sz * FrameHZ), new Vector2(-sz * nx, sz * FrameHZ), true));
        }

        /// <summary>
        /// Plan of the casting over pocket i (rail top round the hole, out to the outer edge), the path of its outer
        /// skirt (outward on the right) and the rail-side arc of the hole (hole centre on the right).
        /// </summary>
        public static List<Vector2> CastingOutline(int i, out List<Vector2> skirt, out List<Vector2> holeArc)
        {
            var poly = new List<Vector2>();
            Vector2 c = Centre(i);
            float r = HoleR(i);
            if (IsSide(i))
            {
                int sz = Sz(i);
                float xe = SideThroat + SideReach;
                Vector2 tp = TSide(i, 1), tm = TSide(i, -1);
                poly.Add(tp); poly.Add(new Vector2(xe, sz * BedZ)); poly.Add(new Vector2(xe, sz * OutZ));
                poly.Add(new Vector2(-xe, sz * OutZ)); poly.Add(new Vector2(-xe, sz * BedZ)); poly.Add(tm);
                holeArc = ArcPts(c, r, tm, tp, true);
                skirt = new List<Vector2> { new Vector2(xe, sz * OutZ), new Vector2(0f, sz * OutZ), new Vector2(-xe, sz * OutZ) };
            }
            else
            {
                int sx = Sx(i), sz = Sz(i);
                Vector2 tl = TLong(i), ts = TShort(i);
                float xl = tl.x - sx * CornerReach, zs = ts.y - sz * CornerReach;
                poly.Add(tl); poly.Add(new Vector2(xl, sz * BedZ));
                skirt = new List<Vector2> { new Vector2(xl, sz * OutZ) };
                Vector2 oc = new Vector2(sx * (OutX - OuterR), sz * (OutZ - OuterR));
                for (int j = 0; j <= 8; j++)
                {
                    float a = j / 8f * Mathf.PI * 0.5f;
                    skirt.Add(oc + OuterR * new Vector2(sx * Mathf.Sin(a), sz * Mathf.Cos(a)));
                }
                skirt.Add(new Vector2(sx * OutX, zs));
                poly.AddRange(skirt);
                poly.Add(new Vector2(sx * BedX, zs)); poly.Add(ts);
                holeArc = ArcPts(c, r, ts, tl, true);
            }
            for (int j = 1; j < holeArc.Count - 1; j++) poly.Add(holeArc[j]);
            PoolTableMesh.OrientRight(skirt, m => m);
            PoolTableMesh.OrientRight(holeArc, m => c - m);
            return poly;
        }

        // ================================================================ cushions, sights, legs

        /// <summary>Cushion k: nose line from a to b, outward direction, facing slopes at a and at b.</summary>
        public static (Vector2 a, Vector2 b, Vector2 o, float ka, float kb)[] Cushions()
        {
            float cj = HalfL - CornerJaw, sj = SideJaw, cz = HalfW - CornerJaw;
            Vector2 dn = new Vector2(0f, -1f), up = new Vector2(0f, 1f);
            return new[]
            {
                (new Vector2(-cj, -HalfW), new Vector2(-sj, -HalfW), dn, CornerK, SideK),
                (new Vector2(sj, -HalfW), new Vector2(cj, -HalfW), dn, SideK, CornerK),
                (new Vector2(-cj, HalfW), new Vector2(-sj, HalfW), up, CornerK, SideK),
                (new Vector2(sj, HalfW), new Vector2(cj, HalfW), up, SideK, CornerK),
                (new Vector2(-HalfL, -cz), new Vector2(-HalfL, cz), new Vector2(-1f, 0f), CornerK, CornerK),
                (new Vector2(HalfL, -cz), new Vector2(HalfL, cz), new Vector2(1f, 0f), CornerK, CornerK),
            };
        }

        /// <summary>The 18 sights: 3 between each pair of pockets (eighths of the length, quarters of the width); across = towards the rail's outside.</summary>
        public static List<(Vector2 pos, Vector2 across)> Sights()
        {
            var s = new List<(Vector2, Vector2)>();
            foreach (int sz in new[] { -1, 1 })
                for (int k = -3; k <= 3; k++)
                    if (k != 0) s.Add((new Vector2(k * PoolDims.PlayL / 8f, sz * (HalfW + SightD)), new Vector2(0f, sz)));
            foreach (int sx in new[] { -1, 1 })
                for (int k = -1; k <= 1; k++)
                    s.Add((new Vector2(sx * (HalfL + SightD), k * PoolDims.PlayW / 4f), new Vector2(sx, 0f)));
            return s;
        }

        /// <summary>Leg centre (square leg of side <paramref name="side"/>) tucked inside the chamfered frame corner.</summary>
        public static Vector2 LegPos(int sx, int sz, float side)
        {
            float hx = FrameHX, hz = FrameHZ, k = ChamferK;
            Vector2 mid = new Vector2((hx + (k - hz)) * 0.5f, ((k - hx) + hz) * 0.5f);
            Vector2 p = mid - new Vector2(1f, 1f) * (side * 0.5f + 0.004f);
            return new Vector2(sx * p.x, sz * p.y);
        }
    }
}
