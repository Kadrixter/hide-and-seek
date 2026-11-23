using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Finds surfaces that can z-fight: triangles of different materials (or renderers) that face the same way, lie
    /// within <c>tolerance</c> of each other along the normal and overlap. With the player camera's near plane of 3 cm
    /// and a 24-bit depth buffer, two surfaces 2 mm apart already flicker 30 m away, so the default tolerance is 4 mm.
    /// Spatially hashed (normal x plane x 2 m cells), so the whole garage (chunked meshes with many sub-meshes) is fast.
    /// Works in the editor (map / menu scenes) and in the player (-testzfight, rooms built at runtime). Sub-meshes of one
    /// renderer count as separate surfaces only where <c>perSubmesh</c> says so (the garage chunks: floor + paint in one
    /// mesh); props like cars are one surface each, their own coplanar trim would only drown the report.
    /// </summary>
    public static class ZFightScan
    {
        public struct Pair
        {
            public Renderer A, B;
            public int SubA, SubB;
            public float Area, Gap;
            public Vector3 At, Normal;
        }

        private struct Tri
        {
            public int Owner;            // renderer * 64 + sub-mesh
            public Vector3 A, B, C, N;
            public float D;
        }

        private const float Cell = 2f;

        public static List<Pair> Find(IList<Renderer> renderers, float tolerance = 0.004f, float minArea = 2e-4f, System.Func<Renderer, bool> perSubmesh = null)
        {
            var tris = new List<Tri>();
            for (int ri = 0; ri < renderers.Count; ri++) Collect(renderers[ri], ri, tris, perSubmesh == null || perSubmesh(renderers[ri]));

            // Buckets: quantized normal + plane slab + in-plane cell. A triangle goes into every cell its box touches.
            var buckets = new Dictionary<(int, int, int, int, int, int), List<int>>();
            for (int i = 0; i < tris.Count; i++)
            {
                var t = tris[i];
                var nk = NormalKey(t.N);
                Basis(nk, out var u, out var w);
                float u0 = Mathf.Min(Vector3.Dot(t.A, u), Mathf.Min(Vector3.Dot(t.B, u), Vector3.Dot(t.C, u)));
                float u1 = Mathf.Max(Vector3.Dot(t.A, u), Mathf.Max(Vector3.Dot(t.B, u), Vector3.Dot(t.C, u)));
                float w0 = Mathf.Min(Vector3.Dot(t.A, w), Mathf.Min(Vector3.Dot(t.B, w), Vector3.Dot(t.C, w)));
                float w1 = Mathf.Max(Vector3.Dot(t.A, w), Mathf.Max(Vector3.Dot(t.B, w), Vector3.Dot(t.C, w)));
                int cu0 = Mathf.FloorToInt(u0 / Cell), cu1 = Mathf.FloorToInt(u1 / Cell);
                int cw0 = Mathf.FloorToInt(w0 / Cell), cw1 = Mathf.FloorToInt(w1 / Cell);
                if ((cu1 - cu0 + 1) * (cw1 - cw0 + 1) > 900) continue; // absurdly large: skip
                int slab = Mathf.FloorToInt(t.D / (tolerance * 2f));
                for (int cu = cu0; cu <= cu1; cu++)
                for (int cw = cw0; cw <= cw1; cw++)
                {
                    var key = (nk.x, nk.y, nk.z, slab, cu, cw);
                    if (!buckets.TryGetValue(key, out var l)) buckets[key] = l = new List<int>();
                    l.Add(i);
                }
            }

            var seen = new HashSet<long>();
            var acc = new Dictionary<(int, int), Pair>();
            foreach (var kv in buckets)
            {
                var list = kv.Value;
                var k = kv.Key;
                buckets.TryGetValue((k.Item1, k.Item2, k.Item3, k.Item4 + 1, k.Item5, k.Item6), out var up);
                for (int a = 0; a < list.Count; a++)
                {
                    int i = list[a];
                    for (int b = a + 1; b < list.Count + (up?.Count ?? 0); b++)
                    {
                        int j = b < list.Count ? list[b] : up[b - list.Count];
                        Test(tris, i, j, tolerance, minArea, seen, acc);
                    }
                }
            }

            var result = new List<Pair>();
            foreach (var kv in acc)
            {
                var p = kv.Value;
                p.A = renderers[kv.Key.Item1 / 64];
                p.B = renderers[kv.Key.Item2 / 64];
                p.SubA = kv.Key.Item1 % 64;
                p.SubB = kv.Key.Item2 % 64;
                result.Add(p);
            }
            result.Sort((x, y) => y.Area.CompareTo(x.Area));
            return result;
        }

        private static void Test(List<Tri> tris, int i, int j, float tol, float minArea, HashSet<long> seen, Dictionary<(int, int), Pair> acc)
        {
            var ti = tris[i];
            var tj = tris[j];
            if (ti.Owner == tj.Owner) return;
            if (Vector3.Dot(ti.N, tj.N) < 0.995f) return;
            float gap = Mathf.Abs(ti.D - tj.D);
            if (gap > tol) return;
            long id = i < j ? ((long)i << 32) | (uint)j : ((long)j << 32) | (uint)i;
            if (!seen.Add(id)) return;
            float area = OverlapArea(ti, tj, out Vector3 at);
            if (area < minArea) return;
            var key = ti.Owner < tj.Owner ? (ti.Owner, tj.Owner) : (tj.Owner, ti.Owner);
            acc.TryGetValue(key, out var p);
            if (p.Area <= 0f) { p.At = at; p.Normal = ti.N; p.Gap = gap; }
            p.Area += area;
            p.Gap = Mathf.Min(p.Gap, gap);
            acc[key] = p;
        }

        private static void Collect(Renderer r, int ri, List<Tri> tris, bool splitSubmeshes)
        {
            Mesh m = null;
            if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf)) m = mf.sharedMesh;
            if (m == null || !m.isReadable || !r.enabled || !r.gameObject.activeInHierarchy) return;
            var v = m.vertices;
            var tr = r.transform.localToWorldMatrix;
            for (int s = 0; s < m.subMeshCount && s < 64; s++)
            {
                if (m.GetTopology(s) != MeshTopology.Triangles) continue;
                var idx = m.GetTriangles(s);
                for (int i = 0; i + 2 < idx.Length; i += 3)
                {
                    Vector3 a = tr.MultiplyPoint3x4(v[idx[i]]), b = tr.MultiplyPoint3x4(v[idx[i + 1]]), c = tr.MultiplyPoint3x4(v[idx[i + 2]]);
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f) continue;
                    n.Normalize();
                    tris.Add(new Tri { Owner = ri * 64 + (splitSubmeshes ? s : 0), A = a, B = b, C = c, N = n, D = Vector3.Dot(n, a) });
                }
            }
        }

        private static Vector3Int NormalKey(Vector3 n) => new Vector3Int(Mathf.RoundToInt(n.x * 40f), Mathf.RoundToInt(n.y * 40f), Mathf.RoundToInt(n.z * 40f));

        private static void Basis(Vector3Int k, out Vector3 u, out Vector3 w)
        {
            Vector3 n = new Vector3(k.x, k.y, k.z).normalized;
            u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            w = Vector3.Cross(n, u);
        }

        /// <summary>Area both triangles cover (8x8 samples over the overlap of their boxes in the plane).</summary>
        private static float OverlapArea(Tri a, Tri b, out Vector3 at)
        {
            Vector3 n = a.N;
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 w = Vector3.Cross(n, u);
            Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w));
            var a0 = P(a.A); var a1 = P(a.B); var a2 = P(a.C);
            var b0 = P(b.A); var b1 = P(b.B); var b2 = P(b.C);
            var lo = Vector2.Max(Vector2.Min(a0, Vector2.Min(a1, a2)), Vector2.Min(b0, Vector2.Min(b1, b2)));
            var hi = Vector2.Min(Vector2.Max(a0, Vector2.Max(a1, a2)), Vector2.Max(b0, Vector2.Max(b1, b2)));
            at = (a.A + a.B + a.C) / 3f;
            if (hi.x <= lo.x || hi.y <= lo.y) return 0f;
            int inside = 0;
            Vector2 first = default;
            for (int ix = 0; ix < 8; ix++)
            for (int iy = 0; iy < 8; iy++)
            {
                var q = new Vector2(Mathf.Lerp(lo.x, hi.x, (ix + 0.5f) / 8f), Mathf.Lerp(lo.y, hi.y, (iy + 0.5f) / 8f));
                if (!In(q, a0, a1, a2) || !In(q, b0, b1, b2)) continue;
                if (inside++ == 0) first = q;
            }
            if (inside > 0) at = u * first.x + w * first.y + n * a.D;
            return (hi.x - lo.x) * (hi.y - lo.y) * inside / 64f;
        }

        private static bool In(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            return (s1 >= 0 && s2 >= 0 && s3 >= 0) || (s1 <= 0 && s2 <= 0 && s3 <= 0);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>One log line per pair (worst first): names, materials, gap, overlap area, where.</summary>
        public static string Describe(Pair p)
        {
            static string Name(Renderer r, int sub)
            {
                string n = r.name;
                var t = r.transform;
                for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; n = t.name + "/" + n; }
                var mats = r.sharedMaterials;
                string m = sub < mats.Length && mats[sub] != null ? mats[sub].name : "?";
                return $"{n}[{m}]";
            }
            return $"{Name(p.A, p.SubA)} <-> {Name(p.B, p.SubB)} gap={p.Gap * 1000f:0.0}mm area={p.Area:0.000}m2 at {p.At:F2} n={p.Normal:F2}";
        }
    }
}
