using System.Collections.Generic;
using HideAndSeek.Player;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The kart circuit's road (<see cref="DriftTrackCircuit"/>): the asphalt deck in 64 m chunks (each its own mesh
    /// collider; neighbouring chunks share their edge rows exactly, so there is no seam to trip a wheel), the concrete
    /// side walls under raised road down to the ground, the bridge slab where the climb crosses the diagonal, jersey
    /// barriers along both sides (red / white tops in 3 m sections, a steel rail on top where the road is raised, one
    /// tall box-section mesh collider) with the gap to the paddock (tyre bundles at its ends), white edge lines, flat red / white kerbs (paint
    /// only, no bump) and the checkered start / finish line.
    /// </summary>
    public static class DriftTrackRoad
    {
        private const int ChunkRows = 64;
        private const float Ground = -0.08f, LineIn = 5.8f, LineOut = 6.0f;
        private static readonly float[] Lats = { -DriftTrackCircuit.DeckHalf, -6f, -3f, 0f, 3f, 6f, DriftTrackCircuit.DeckHalf };

        public static void Build(Transform parent, Material asphalt)
        {
            var root = new GameObject("Circuit").transform;
            root.SetParent(parent, false);
            Deck(root, asphalt);
            Walls(root);
            Barriers(root);
            Paint(root);
        }

        // ------------------------------------------------------------------ deck

        private static void Deck(Transform root, Material asphalt)
        {
            var c = DriftTrackCircuit.Center;
            int n = c.Length;
            for (int r0 = 0, k = 0; r0 < n; r0 += ChunkRows, k++)
            {
                int r1 = Mathf.Min(r0 + ChunkRows, n); // row n = row 0 again (the loop closes)
                var b = new MeshBuf();
                int cols = Lats.Length;
                for (int r = r0; r <= r1; r++)
                {
                    int i = DriftTrackCircuit.Wrap(r);
                    float s = r == n ? DriftTrackCircuit.Length : DriftTrackCircuit.S[i];
                    foreach (float lat in Lats)
                        b.Vert(DriftTrackCircuit.Point(i, lat), DriftTrackCircuit.Normal[i], new Vector2(lat / AsphaltMaps.TileMetres, s / AsphaltMaps.TileMetres));
                }
                for (int r = 0; r < r1 - r0; r++)
                {
                    int i = DriftTrackCircuit.Wrap(r0 + r);
                    for (int j = 0; j + 1 < cols; j++)
                    {
                        int a = r * cols + j;
                        b.QuadIdx(a, a + 1, a + cols + 1, a + cols, DriftTrackCircuit.Normal[i]);
                    }
                }
                var mesh = b.ToMesh($"TrackRoad_{k:00}");
                mesh.RecalculateTangents();
                var go = TrackMats.Piece(root, $"Road_{k:00}", mesh, asphalt, false);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<FootstepSurface>().Type = SurfaceType.Concrete;
            }
        }

        // ------------------------------------------------------------------ walls under raised road, the bridge

        private static void Walls(Transform root)
        {
            var c = DriftTrackCircuit.Center;
            var span = DriftTrackCircuit.Span;
            var fwd = DriftTrackCircuit.Fwd;
            int n = c.Length;
            const float D = DriftTrackCircuit.BridgeDepth, W = DriftTrackCircuit.DeckHalf;
            var b = new MeshBuf();
            for (int i = 0; i < n; i++)
            {
                int j = DriftTrackCircuit.Wrap(i + 1);
                bool spanPair = span[i] || span[j];
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 ei = DriftTrackCircuit.Point(i, side * W), ej = DriftTrackCircuit.Point(j, side * W);
                    Vector3 outN = Flat(DriftTrackCircuit.Right[i]) * side;
                    if (!spanPair)
                    {
                        if (Mathf.Max(ei.y, ej.y) > 0.02f) b.Quad(ei, ej, AtY(ej, Ground), AtY(ei, Ground), outN);
                    }
                    else b.Quad(ei, ej, ej + Vector3.down * D, ei + Vector3.down * D, outN); // bridge fascia
                }
                if (spanPair)
                {
                    Vector3 li = DriftTrackCircuit.Point(i, -W), ri = DriftTrackCircuit.Point(i, W), lj = DriftTrackCircuit.Point(j, -W), rj = DriftTrackCircuit.Point(j, W);
                    b.Quad(li + Vector3.down * D, ri + Vector3.down * D, rj + Vector3.down * D, lj + Vector3.down * D, Vector3.down); // slab underside
                }
                // Abutment faces where the bridge span starts / ends.
                if (!span[i] && (span[j] || span[DriftTrackCircuit.Wrap(i - 1)]))
                {
                    Vector3 l = DriftTrackCircuit.Point(i, -W), r = DriftTrackCircuit.Point(i, W);
                    Vector3 nrm = Flat(fwd[i]) * (span[j] ? 1f : -1f);
                    b.Quad(AtY(l, Ground), AtY(r, Ground), r + Vector3.down * D, l + Vector3.down * D, nrm);
                }
            }
            var mesh = b.ToMesh("TrackSideWalls");
            var go = TrackMats.Piece(root, "SideWalls", mesh, TrackMats.Get("TrackConcreteWall", new Color(0.46f, 0.46f, 0.44f), 0.1f));
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ------------------------------------------------------------------ barriers

        private static readonly Vector2[] Jersey =
        {
            new(-0.31f, -0.05f), new(-0.31f, 0.08f), new(-0.12f, 0.32f), new(-0.1f, 0.78f),
            new(0.1f, 0.78f), new(0.12f, 0.32f), new(0.31f, 0.08f), new(0.31f, -0.05f),
        };
        private static readonly Vector2[] Cap = { new(-0.11f, 0.78f), new(-0.11f, 0.92f), new(0.11f, 0.92f), new(0.11f, 0.78f) };

        /// <summary>Collider height over the road: low on the ground, tall where a kart could drop off the raised road.</summary>
        public static float ColliderHeight(int i) => DriftTrackCircuit.Center[i].y > 0.4f ? 1.7f : 1.05f;

        private static void Barriers(Transform root)
        {
            int n = DriftTrackCircuit.Count;
            var concrete = new MeshBuf();
            var red = new MeshBuf();
            var white = new MeshBuf();
            var rail = new MeshBuf();
            var col = new MeshBuf();
            var tyres = new ProcMesh();
            foreach (int side in new[] { -1, 1 })
            {
                var open = side < 0 ? DriftTrackCircuit.OpenLeft : DriftTrackCircuit.OpenRight;
                for (int i = 0; i < n; i++)
                {
                    int j = DriftTrackCircuit.Wrap(i + 1);
                    if (open[i]) continue;
                    if (!open[j])
                    {
                        Sweep(concrete, i, j, side, Jersey);
                        Sweep(((int)(DriftTrackCircuit.S[i] / 3f) & 1) == 0 ? red : white, i, j, side, Cap);
                        float hi = ColliderHeight(i), hj = ColliderHeight(j);
                        Sweep(col, i, j, side, new[] { new Vector2(-0.31f, -0.3f), new Vector2(-0.31f, hi), new Vector2(0.31f, hi), new Vector2(0.31f, -0.3f) },
                            new[] { new Vector2(-0.31f, -0.3f), new Vector2(-0.31f, hj), new Vector2(0.31f, hj), new Vector2(0.31f, -0.3f) });
                        if (DriftTrackCircuit.Center[i].y > 1f && DriftTrackCircuit.Center[j].y > 1f)
                        {
                            Sweep(rail, i, j, side, new[] { new Vector2(-0.04f, 1.52f), new Vector2(-0.04f, 1.62f), new Vector2(0.04f, 1.62f), new Vector2(0.04f, 1.52f) });
                            Sweep(rail, i, j, side, new[] { new Vector2(-0.03f, 1.22f), new Vector2(-0.03f, 1.28f), new Vector2(0.03f, 1.28f), new Vector2(0.03f, 1.22f) });
                            if (i % 3 == 0)
                            {
                                Vector3 p = Base(i, side);
                                rail.Box(p + Vector3.up * 1.25f, Quaternion.LookRotation(Flat(DriftTrackCircuit.Fwd[i])), new Vector3(0.07f, 0.74f, 0.07f));
                            }
                        }
                    }
                    // End caps where a barrier run starts or stops (the paddock gap).
                    int h = DriftTrackCircuit.Wrap(i - 1);
                    if (open[j]) EndCap(concrete, col, tyres, i, side, 1f);
                    if (open[h]) EndCap(concrete, col, tyres, i, side, -1f);
                }
            }
            TrackMats.Piece(root, "CircuitBarriers", concrete.ToMesh("TrackCircuitBarriers"), TrackMats.Get("TrackConcrete", new Color(0.62f, 0.62f, 0.6f), 0.12f));
            TrackMats.Piece(root, "CircuitBarrierRed", red.ToMesh("TrackCircuitBarrierRed"), TrackMats.Get("TrackPaintRed", new Color(0.75f, 0.06f, 0.05f), 0.35f));
            TrackMats.Piece(root, "CircuitBarrierWhite", white.ToMesh("TrackCircuitBarrierWhite"), TrackMats.Get("TrackPaintWhite", new Color(0.9f, 0.9f, 0.88f), 0.35f));
            TrackMats.Piece(root, "GapTyres", tyres.ToMesh("TrackGapTyres"), DriftTrackProps.Rubber);
            TrackMats.Piece(root, "BridgeRails", rail.ToMesh("TrackBridgeRails"), TrackMats.Get("TrackGalvanised", new Color(0.5f, 0.52f, 0.54f), 0.55f, 0.6f));
            var colGo = new GameObject("CircuitBarrierCol");
            colGo.transform.SetParent(root, false);
            colGo.AddComponent<MeshCollider>().sharedMesh = col.ToMesh("TrackCircuitBarrierCol");
        }

        /// <summary>Foot of the barrier on side <paramref name="side"/> of row <paramref name="i"/> (on the banked deck).</summary>
        private static Vector3 Base(int i, int side) => DriftTrackCircuit.Point(i, side * DriftTrackCircuit.BarrierLat);

        /// <summary>Sweeps a closed-at-the-bottom profile (x = outwards, y = up from the barrier foot) from row i to row j; hard edges.</summary>
        private static void Sweep(MeshBuf b, int i, int j, int side, Vector2[] prof, Vector2[] profJ = null)
        {
            profJ ??= prof;
            Vector3 bi = Base(i, side), bj = Base(j, side);
            Vector3 oi = Flat(DriftTrackCircuit.Right[i]) * side, oj = Flat(DriftTrackCircuit.Right[j]) * side;
            for (int k = 0; k + 1 < prof.Length; k++)
            {
                Vector2 p0 = prof[k], p1 = prof[k + 1];
                Vector2 d = p1 - p0;
                Vector2 n2 = new Vector2(d.y, -d.x).normalized;
                Vector2 mid = (p0 + p1) * 0.5f - new Vector2(0f, (prof[0].y + prof[prof.Length / 2].y) * 0.5f);
                if (Vector2.Dot(n2, mid) < 0f) n2 = -n2;
                Vector3 nrm = (oi * n2.x + Vector3.up * n2.y).normalized;
                b.Quad(bi + oi * p0.x + Vector3.up * p0.y, bi + oi * p1.x + Vector3.up * p1.y,
                       bj + oj * profJ[k + 1].x + Vector3.up * profJ[k + 1].y, bj + oj * profJ[k].x + Vector3.up * profJ[k].y, nrm);
            }
        }

        /// <summary>Closes a barrier run at row <paramref name="i"/> (open towards <paramref name="dir"/>) and puts a tyre bundle in front of the end.</summary>
        private static void EndCap(MeshBuf concrete, MeshBuf col, ProcMesh tyres, int i, int side, float dir)
        {
            Vector3 bse = Base(i, side), o = Flat(DriftTrackCircuit.Right[i]) * side, f = Flat(DriftTrackCircuit.Fwd[i]) * dir;
            Vector3 P(Vector2 q) => bse + o * q.x + Vector3.up * q.y;
            Vector3 mid = P(new Vector2(0f, 0.4f));
            for (int k = 0; k + 1 < Jersey.Length; k++) concrete.Tri(mid, P(Jersey[k]), P(Jersey[k + 1]), f);
            concrete.Tri(mid, P(Jersey[Jersey.Length - 1]), P(Jersey[0]), f);
            float h = ColliderHeight(i);
            col.Quad(P(new Vector2(-0.31f, -0.3f)), P(new Vector2(0.31f, -0.3f)), P(new Vector2(0.31f, h)), P(new Vector2(-0.31f, h)), f);
            for (int k = 0; k < 2; k++)
            {
                Vector3 t = bse + f * (0.36f + k * 0.6f);
                for (int y = 0; y < 3; y++) DriftTrackProps.Tyre(tyres, t + Vector3.up * (0.12f + y * 0.22f));
            }
            col.Box(bse + f * 0.66f + Vector3.up * 0.3f, Quaternion.LookRotation(f), new Vector3(0.62f, 1.2f, 1.25f));
        }

        // ------------------------------------------------------------------ paint: edge lines, kerbs, start line

        private static void Paint(Transform root)
        {
            int n = DriftTrackCircuit.Count;
            var white = new MeshBuf();
            var black = new MeshBuf();
            var kerbRed = new MeshBuf();
            var kerbWhite = new MeshBuf();
            for (int i = 0; i < n; i++)
            {
                int j = DriftTrackCircuit.Wrap(i + 1);
                Strip(white, i, j, -LineOut, -LineIn, 0.006f);
                Strip(white, i, j, LineIn, LineOut, 0.006f);
            }
            foreach (var c in DriftTrackCircuit.Corners)
            {
                if (Mathf.Abs(c.TurnDeg) < 15f) continue;
                int inner = c.Sign > 0 ? -1 : 1;
                float mid = (c.S0 + c.S1) * 0.5f, half = (c.S1 - c.S0) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    int j = DriftTrackCircuit.Wrap(i + 1);
                    float s = DriftTrackCircuit.S[i];
                    var buf = ((int)(s / 1.2f) & 1) == 0 ? kerbRed : kerbWhite;
                    float x = DriftTrackCircuit.DeltaS(mid, s);
                    if (Mathf.Abs(x) <= half + 3f) Strip(buf, i, j, inner > 0 ? 4.9f : -6.25f, inner > 0 ? 6.25f : -4.9f, 0.012f);
                    if (x >= 0f && x <= half + 10f) Strip(buf, i, j, inner > 0 ? -6.25f : 4.9f, inner > 0 ? -4.9f : 6.25f, 0.012f); // exit kerb outside
                }
            }
            // Checkered start / finish line: three rows of 0.6 m squares across the straight.
            int sr = DriftTrackCircuit.StartRow;
            Vector3 sc = DriftTrackCircuit.Center[sr], sf = Flat(DriftTrackCircuit.Fwd[sr]), sx = Flat(DriftTrackCircuit.Right[sr]), up = DriftTrackCircuit.Normal[sr];
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 20; k++)
                {
                    Vector3 o = sc + sf * (-0.9f + row * 0.6f) + sx * (-6f + k * 0.6f) + up * 0.009f;
                    (((row + k) & 1) == 0 ? white : black).Quad(o, o + sx * 0.6f, o + sx * 0.6f + sf * 0.6f, o + sf * 0.6f, up);
                }
            TrackMats.Piece(root, "CircuitLines", white.ToMesh("TrackCircuitLines"), TrackMats.Get("TrackLineWhite", new Color(0.86f, 0.86f, 0.84f), 0.25f), false);
            TrackMats.Piece(root, "CircuitChecker", black.ToMesh("TrackCircuitChecker"), TrackMats.Get("TrackLineBlack", new Color(0.03f, 0.03f, 0.03f), 0.2f), false);
            TrackMats.Piece(root, "CircuitKerbRed", kerbRed.ToMesh("TrackCircuitKerbRed"), TrackMats.Get("TrackPaintRed", new Color(0.75f, 0.06f, 0.05f), 0.35f), false);
            TrackMats.Piece(root, "CircuitKerbWhite", kerbWhite.ToMesh("TrackCircuitKerbWhite"), TrackMats.Get("TrackPaintWhite", new Color(0.9f, 0.9f, 0.88f), 0.35f), false);
        }

        /// <summary>Flat strip on the deck between two lateral offsets, <paramref name="lift"/> over the asphalt.</summary>
        private static void Strip(MeshBuf b, int i, int j, float lat0, float lat1, float lift)
        {
            Vector3 ni = DriftTrackCircuit.Normal[i], nj = DriftTrackCircuit.Normal[j];
            b.Quad(DriftTrackCircuit.Point(i, lat0) + ni * lift, DriftTrackCircuit.Point(i, lat1) + ni * lift,
                   DriftTrackCircuit.Point(j, lat1) + nj * lift, DriftTrackCircuit.Point(j, lat0) + nj * lift, ni);
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v.normalized; }
        private static Vector3 AtY(Vector3 v, float y) { v.y = y; return v; }
    }

    /// <summary>Raw triangle list with explicit normals (faces oriented to the normal given).</summary>
    public sealed class MeshBuf
    {
        private readonly List<Vector3> _v = new();
        private readonly List<Vector3> _n = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<int> _t = new();

        public int VertexCount => _v.Count;

        public int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p);
            _n.Add(n);
            _uv.Add(uv);
            return _v.Count - 1;
        }

        /// <summary>Quad of existing vertices a-b-c-d (a loop), front side towards <paramref name="n"/>.</summary>
        public void QuadIdx(int a, int b, int c, int d, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]), n) < 0f) { (b, d) = (d, b); }
            _t.Add(a); _t.Add(b); _t.Add(c);
            _t.Add(a); _t.Add(c); _t.Add(d);
        }

        /// <summary>Quad a-b-c-d (a loop) with one normal; UVs in metres (projected).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.7f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
            Vector3 w = Mathf.Abs(n.y) > 0.7f ? Vector3.forward : Vector3.up;
            int i = Vert(a, n, new Vector2(Vector3.Dot(a, u), Vector3.Dot(a, w)));
            Vert(b, n, new Vector2(Vector3.Dot(b, u), Vector3.Dot(b, w)));
            Vert(c, n, new Vector2(Vector3.Dot(c, u), Vector3.Dot(c, w)));
            Vert(d, n, new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, w)));
            QuadIdx(i, i + 1, i + 2, i + 3, n);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
        {
            int i = Vert(a, n, Vector2.zero);
            Vert(b, n, Vector2.zero);
            Vert(c, n, Vector2.zero);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { _t.Add(i); _t.Add(i + 2); _t.Add(i + 1); }
            else { _t.Add(i); _t.Add(i + 1); _t.Add(i + 2); }
        }

        /// <summary>Oriented box (6 quads).</summary>
        public void Box(Vector3 center, Quaternion rot, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = rot * Vector3.right * h.x, Y = rot * Vector3.up * h.y, Z = rot * Vector3.forward * h.z;
            void F(Vector3 o, Vector3 a, Vector3 b) => Quad(center + o - a - b, center + o + a - b, center + o + a + b, center + o - a + b, o.normalized);
            F(X, Y, Z); F(-X, Y, Z); F(Y, X, Z); F(-Y, X, Z); F(Z, X, Y); F(-Z, X, Y);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
