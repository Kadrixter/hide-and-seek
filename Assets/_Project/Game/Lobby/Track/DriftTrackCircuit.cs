using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The kart circuit on the drift pad (pure geometry, shared by the builders and the tests): a closed polygon of
    /// corner points, each rounded with its own radius, sampled every ~1 m. 2026-11-27 rebuild: ~2.3 km, 14 m wide,
    /// raced anticlockwise from the start / finish straight (x = -104, heading north) along the paddock (a gap in its
    /// east barrier lets the karts on and off): a banked left onto the north run, a tight left-right onto the long west
    /// run, the west edge down into a STACKED HELIX - two and a half laps of a 110 x 100 m rectangle climbing 7.2 m a
    /// lap, so the road runs over itself on three levels (0 / ~7 / ~14 m) - out of its top at 17 m heading north as a
    /// viaduct over the west run, a sweeping descent east and south (over the west run again) and back through a
    /// right-left to the south hairpin. Heights are eased between corner apexes (cosine, grades under ~7.5 %); any road
    /// above ~1.2 m is a viaduct (a slab on columns, open underneath - no earth banks), so the levels simply lie on top
    /// of each other. Banked corners roll the cross-section (the outside edge up) and lift a ground-level corner just
    /// enough to keep its low edge above the ground. North = +Z, right of the driving direction = <see cref="Right"/>.
    /// </summary>
    public static class DriftTrackCircuit
    {
        /// <summary>Half width to the white edge lines (14 m of racing asphalt; 12 m before 27.11).</summary>
        public const float HalfWidth = 7f;
        /// <summary>Half width of the asphalt deck (and its collider): it runs on under the barriers.</summary>
        public const float DeckHalf = 8.2f;
        /// <summary>Axis of the jersey barriers (base 0.62 m wide: the inner face stands 7.29 m off the centre line).</summary>
        public const float BarrierLat = 7.6f, BarrierHalfBase = 0.31f;
        /// <summary>Road higher than this is a viaduct (slab on columns); lower road stands on side walls down to the ground.</summary>
        public const float ViaductFrom = 1.2f;
        /// <summary>Height of the bridge deck over the diagonal; depth of the bridge slab under the road.</summary>
        public const float BridgeHeight = 4.6f, BridgeDepth = 0.7f;
        /// <summary>Tunnel: inner half width, clear height over the road.</summary>
        public const float TunnelHalf = 7.5f, TunnelHeight = 4.8f;
        /// <summary>Highest road surface (the top of the helix exit, plus the roll of a banked edge) - the karts are kept below this + a margin.</summary>
        public const float MaxHeight = 18f;
        /// <summary>Gap in the east barrier of the start straight (the paddock exit), z range.</summary>
        public const float PitZ0 = -6f, PitZ1 = 28f;
        /// <summary>Start / finish line across the start straight.</summary>
        public const float StartZ = 36f;
        /// <summary>Tunnel (none since 27.11: the roads under the viaducts are the covered bits now).</summary>
        public const float TunnelXEast = 0f, TunnelXWest = 0f;
        /// <summary>x of the start / finish straight.</summary>
        public const float StartX = -104f;

        private const float GroundY = 0f, Lead = 14f, Inside = 4f;

        /// <summary>Corner points: x, z, fillet radius, road height at the apex, bank (degrees, outside edge up). A point that
        /// does not turn (radius 0) only sets a height on a straight. Checked offline (no two levels closer than 5.3 m where
        /// the decks overlap, grades, the pad) - keep that in mind when moving points.</summary>
        private static readonly (float x, float z, float r, float h, float bank)[] Plan =
        {
            (-104f, 128f, 12f, 0f, 6f),    //  0 banked left onto the north run (west)
            (-150f, 128f, 10f, 0f, 6f),    //  1 tight left (south) ...
            (-150f, 70f, 10f, 0f, 0f),     //  2 ... right onto the long west run (under the descent and the exit viaduct)
            (-405f, 70f, 12f, 0f, 6f),     //  3 banked left, down the west edge (south)
            (-405f, -130f, 12f, 0f, 6f),   //  4 helix: SW, lap 1 (ground)
            (-295f, -130f, 12f, 1.8f, 6f), //  5        SE
            (-295f, -30f, 12f, 3.6f, 6f),  //  6        NE
            (-405f, -30f, 12f, 5.4f, 6f),  //  7        NW (over the way in)
            (-405f, -130f, 12f, 7.2f, 6f), //  8        SW, lap 2 (over lap 1)
            (-295f, -130f, 12f, 9.0f, 6f), //  9        SE
            (-295f, -30f, 12f, 10.8f, 6f), // 10        NE
            (-405f, -30f, 12f, 12.6f, 6f), // 11        NW
            (-405f, -130f, 12f, 14.4f, 6f),// 12        SW, lap 3 (over lap 2)
            (-295f, -130f, 12f, 16.2f, 6f),// 13        SE, out of the top heading north
            (-295f, -5f, 0f, 17f, 0f),     // 14 (straight) keeps the height past the lower NE corners
            (-295f, 105f, 14f, 12f, 0f),   // 15 right (east), the viaduct over the west run
            (-190f, 105f, 14f, 7.3f, 0f),  // 16 right (south), descending over the west run
            (-190f, -60f, 14f, 0f, 0f),    // 17 left (east)
            (-150f, -60f, 10f, 0f, 0f),    // 18 right (south) ...
            (-150f, -128f, 10f, 0f, 6f),   // 19 ... banked left (east) - the south hairpin
            (-104f, -128f, 12f, 0f, 6f),   // 20 ... banked left onto the start straight
        };

        /// <summary>One rounded corner of <see cref="Plan"/> along the sampled line.</summary>
        public struct Corner
        {
            public int Vertex;
            public float S0, S1, Apex, Radius, TurnDeg, Bank;
            /// <summary>+1 = left-hander (inside on the left), -1 = right-hander.</summary>
            public int Sign;
        }

        public static int Count => Build().Length;
        public static float Length { get { Build(); return _length; } }
        public static Vector3[] Center => Build();
        public static Vector3[] Fwd { get { Build(); return _fwd; } }
        public static Vector3[] Right { get { Build(); return _right; } }
        public static Vector3[] Normal { get { Build(); return _normal; } }
        public static float[] S { get { Build(); return _s; } }
        /// <summary>Signed roll, degrees (+ = right edge up).</summary>
        public static float[] Bank { get { Build(); return _bank; } }
        /// <summary>Signed curvature, 1/m (+ = turning left).</summary>
        public static float[] Curvature { get { Build(); return _curv; } }
        /// <summary>The row lies inside the tunnel.</summary>
        public static bool[] Tunnel { get { Build(); return _tunnel; } }
        /// <summary>The row is a viaduct (slab on columns, no side walls under it): any road above <see cref="ViaductFrom"/>.</summary>
        public static bool[] Span { get { Build(); return _span; } }
        /// <summary>No barrier on the left (0) / right (1) side of this row.</summary>
        public static bool[] OpenLeft { get { Build(); return _openL; } }
        public static bool[] OpenRight { get { Build(); return _openR; } }
        public static List<Corner> Corners { get { Build(); return _corners; } }
        /// <summary>Row of the start / finish line.</summary>
        public static int StartRow { get { Build(); return _startRow; } }
        /// <summary>Corners counted as a driver does (a hairpin made of two 90 degree points is one corner).</summary>
        public static int CornerCount { get { Build(); return _cornerCount; } }

        private static Vector3[] _c, _fwd, _right, _normal;
        private static float[] _s, _bank, _curv;
        private static bool[] _tunnel, _span, _openL, _openR;
        private static List<Corner> _corners;
        private static float _length;
        private static int _startRow, _cornerCount;

        /// <summary>Point on the (banked) road surface: row <paramref name="i"/>, <paramref name="lat"/> metres right of the centre.</summary>
        public static Vector3 Point(int i, float lat)
        {
            var c = Build();
            i = Wrap(i);
            return c[i] + _right[i] * lat;
        }

        public static int Wrap(int i)
        {
            int n = Build().Length;
            i %= n;
            return i < 0 ? i + n : i;
        }

        /// <summary>Signed distance along the loop from <paramref name="a"/> to <paramref name="b"/> (shortest way round).</summary>
        public static float DeltaS(float a, float b)
        {
            float d = Mathf.Repeat(b - a, Length);
            return d > _length * 0.5f ? d - _length : d;
        }

        /// <summary>Nearest row to <paramref name="p"/> (horizontal distance, roads at another level skipped when a hint is given: searched near it).</summary>
        public static int Nearest(Vector3 p, int hint = -1, int window = 60)
        {
            var c = Build();
            int best = 0;
            float bd = float.MaxValue;
            if (hint >= 0)
            {
                for (int k = -window; k <= window; k++)
                {
                    int i = Wrap(hint + k);
                    float d = Sq(c[i], p);
                    if (d < bd) { bd = d; best = i; }
                }
                return best;
            }
            for (int i = 0; i < c.Length; i++)
            {
                float d = Sq(c[i], p) + Mathf.Abs(c[i].y - p.y) * 40f; // prefer the road at the same level (bridge / diagonal)
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private static float Sq(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }

        /// <summary>
        /// Where a kart reset at <paramref name="p"/> (flipped, stuck, R) goes back onto the circuit: the centre line a few
        /// metres back, facing the racing direction. False when <paramref name="p"/> is not on the circuit (the paddock,
        /// the infield behind the barriers): then the kart goes to its parking slot as before.
        /// </summary>
        public static bool Respawn(Vector3 p, out Vector3 pos, out float yaw)
        {
            pos = default;
            yaw = 0f;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) return false;
            int i = Nearest(p);
            float lat = Lateral(p, i);
            float dy = p.y - Point(i, Mathf.Clamp(lat, -DeckHalf, DeckHalf)).y;
            if (Mathf.Abs(lat) > BarrierLat || dy < -1.5f || dy > 3f) return false;
            int r = Wrap(i - 4);
            pos = _c[r] + _normal[r] * 0.15f;
            yaw = Mathf.Atan2(_fwd[r].x, _fwd[r].z) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Metres right of the centre line at row <paramref name="i"/>.</summary>
        public static float Lateral(Vector3 p, int i)
        {
            Build();
            i = Wrap(i);
            Vector3 r = _right[i];
            r.y = 0f;
            return Vector3.Dot(p - _c[i], r.normalized);
        }

        // ------------------------------------------------------------------ construction

        private static Vector3[] Build()
        {
            if (_c != null) return _c;
            int n = Plan.Length;
            var th = new float[n];
            var tl = new float[n];
            var d1s = new Vector2[n];
            var d2s = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var p = Plan[(i + n - 1) % n];
                var v = Plan[i];
                var q = Plan[(i + 1) % n];
                Vector2 d1 = new Vector2(v.x - p.x, v.z - p.z).normalized, d2 = new Vector2(q.x - v.x, q.z - v.z).normalized;
                th[i] = Mathf.Atan2(d1.x * d2.y - d1.y * d2.x, Vector2.Dot(d1, d2));
                tl[i] = v.r * Mathf.Tan(Mathf.Abs(th[i]) * 0.5f);
                d1s[i] = d1;
                d2s[i] = d2;
            }

            // 2D line: arc of corner 0, straight, arc of corner 1, ... straight back to the start.
            var pts = new List<Vector2>();
            var arcS0 = new int[n];
            var arcS1 = new int[n];
            for (int i = 0; i < n; i++)
            {
                var v = Plan[i];
                Vector2 vp = new(v.x, v.z);
                Vector2 a = vp - d1s[i] * tl[i], b = vp + d2s[i] * tl[i];
                if (pts.Count > 0) Line(pts, pts[pts.Count - 1], a);
                else pts.Add(a);
                arcS0[i] = pts.Count - 1;
                if (Mathf.Abs(th[i]) > 1e-4f && v.r > 0.01f)
                {
                    Vector2 left = new(-d1s[i].y, d1s[i].x);
                    float sg = Mathf.Sign(th[i]);
                    Vector2 cen = a + left * v.r * sg;
                    float a0 = Mathf.Atan2(a.y - cen.y, a.x - cen.x);
                    int k = Mathf.Max(2, Mathf.CeilToInt(v.r * Mathf.Abs(th[i]) / 1f));
                    for (int j = 1; j <= k; j++)
                    {
                        float ang = a0 + th[i] * j / k;
                        pts.Add(cen + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * v.r);
                    }
                }
                arcS1[i] = pts.Count - 1;
            }
            int before = pts.Count;
            Line(pts, pts[pts.Count - 1], pts[0]);
            if (pts.Count > before) pts.RemoveAt(pts.Count - 1); // closing point = the first one

            int m = pts.Count;
            _s = new float[m];
            for (int i = 1; i < m; i++) _s[i] = _s[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
            _length = _s[m - 1] + Vector2.Distance(pts[m - 1], pts[0]);

            // Corners, apexes, heights and banking.
            _corners = new List<Corner>();
            var apex = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s0 = _s[arcS0[i]], s1 = _s[arcS1[i]];
                apex[i] = (s0 + s1) * 0.5f;
                _corners.Add(new Corner
                {
                    Vertex = i, S0 = s0, S1 = s1, Apex = apex[i], Radius = Plan[i].r, TurnDeg = th[i] * Mathf.Rad2Deg,
                    Bank = Plan[i].bank, Sign = th[i] >= 0f ? 1 : -1,
                });
            }
            _cornerCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(th[i]) * Mathf.Rad2Deg < 6f) continue;
                var prev = _corners[(i + n - 1) % n];
                bool joined = i > 0 && Mathf.Abs(prev.TurnDeg) >= 6f && prev.Sign == _corners[i].Sign && _corners[i].S0 - prev.S1 < 1.5f;
                if (!joined) _cornerCount++;
            }

            // Banked corners of the same hand close together (a hairpin made of two corners) roll as one (no dip in the roll / lift between them).
            var g0 = new float[n];
            var g1 = new float[n];
            for (int i = 0; i < n; i++) { g0[i] = _corners[i].S0; g1[i] = _corners[i].S1; }
            for (int i = 0; i + 1 < n; i++)
            {
                var a = _corners[i];
                var b = _corners[i + 1];
                if (a.Bank > 0f && b.Bank > 0f && a.Sign == b.Sign && b.S0 - a.S1 < 2f * Lead + 4f) { g1[i] = g1[i + 1]; g0[i + 1] = g0[i]; } // 27.11: also two corners a short straight apart (the lift dipped in between: a bump)
            }

            _c = new Vector3[m];
            _bank = new float[m];
            _curv = new float[m];
            for (int i = 0; i < m; i++)
            {
                float s = _s[i];
                float h = HeightAt(s, apex), roll = 0f, lift = 0f;
                for (int k = 0; k < n; k++)
                {
                    if (Plan[k].bank <= 0f) continue;
                    float w = BankWeight(s, g0[k], g1[k]);
                    if (w <= 0f) continue;
                    float b = Plan[k].bank * _corners[k].Sign * w;
                    if (Mathf.Abs(b) > Mathf.Abs(roll)) roll = b;
                    float need = Mathf.Max(0f, DeckHalf * Mathf.Sin(Plan[k].bank * Mathf.Deg2Rad) + 0.06f - Plan[k].h);
                    lift = Mathf.Max(lift, need * w);
                }
                _bank[i] = roll;
                _c[i] = new Vector3(pts[i].x, GroundY + h + lift, pts[i].y);
            }
            for (int k = 0; k < n; k++)
                if (Plan[k].r > 0.01f)
                    for (int i = arcS0[k] + 1; i <= arcS1[k]; i++) _curv[i] = _corners[k].Sign / Plan[k].r;

            _fwd = new Vector3[m];
            _right = new Vector3[m];
            _normal = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                Vector3 f = (_c[(i + 1) % m] - _c[(i + m - 1) % m]).normalized;
                Vector3 hr = Vector3.Cross(Vector3.up, new Vector3(f.x, 0f, f.z)).normalized;
                float b = _bank[i] * Mathf.Deg2Rad;
                Vector3 r = hr * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                r = (r - f * Vector3.Dot(r, f)).normalized;
                _fwd[i] = f;
                _right[i] = r;
                _normal[i] = Vector3.Cross(f, r).normalized;
            }

            // Flags: the viaducts (any raised road), the paddock gap, the start row. No tunnel since 27.11.
            _tunnel = new bool[m];
            _span = new bool[m];
            _openL = new bool[m];
            _openR = new bool[m];
            int last = n - 1;
            for (int i = 0; i < m; i++)
            {
                Vector3 p = _c[i];
                bool straight = i > arcS1[last] && Mathf.Abs(p.x - StartX) < 0.5f;
                _openR[i] = straight && p.z > PitZ0 && p.z < PitZ1;
                if (straight && p.z <= StartZ) _startRow = i;
                _span[i] = p.y >= ViaductFrom;
            }
            return _c;
        }

        private static void Line(List<Vector2> pts, Vector2 from, Vector2 to)
        {
            float len = Vector2.Distance(from, to);
            if (len < 0.05f) return; // the arcs touch (hairpin made of two corners)
            int k = Mathf.Max(1, Mathf.CeilToInt(len / 1f));
            for (int j = 1; j <= k; j++) pts.Add(Vector2.Lerp(from, to, j / (float)k));
        }

        /// <summary>Road height at <paramref name="s"/>: eased (cosine) between the corner apexes.</summary>
        private static float HeightAt(float s, float[] apex)
        {
            int n = apex.Length;
            for (int i = 0; i < n; i++)
            {
                float s0 = apex[i], s1 = i + 1 < n ? apex[i + 1] : apex[0] + _length;
                float ss = s >= s0 ? s : s + _length;
                if (ss < s0 || ss > s1) continue;
                float u = s1 > s0 ? (ss - s0) / (s1 - s0) : 0f;
                float h0 = Plan[i].h, h1 = Plan[(i + 1) % n].h;
                return h0 + (h1 - h0) * (1f - Mathf.Cos(Mathf.PI * u)) * 0.5f;
            }
            return Plan[0].h;
        }

        /// <summary>0..1: full roll from <see cref="Inside"/> m into the arc (s0..s1) to as far before its end, eased in / out over the <see cref="Lead"/> m around.</summary>
        private static float BankWeight(float s, float s0, float s1)
        {
            float mid = (s0 + s1) * 0.5f, half = (s1 - s0) * 0.5f;
            float x = Mathf.Abs(DeltaS(mid, s));
            float outside = x - half; // < 0 inside the arc
            if (outside <= -Inside) return 1f;
            if (outside >= Lead) return 0f;
            float u = (outside + Inside) / (Lead + Inside);
            return 1f - u * u * (3f - 2f * u);
        }
    }
}
