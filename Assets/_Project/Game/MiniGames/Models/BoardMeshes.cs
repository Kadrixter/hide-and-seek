using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Procedural meshes for the lobby board-game tables: turned (lathe) solids around +Y with arc-length UVs,
    /// bevelled extrusions of a side silhouette with a width that may vary over the outline (knight head, table
    /// feet), annular sectors (rook battlements) and a slab with a fully textured top. Cached by key.
    /// </summary>
    public static partial class BoardMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        public static bool TryGet(string key, out Mesh m) => Cache.TryGetValue(key, out m);

        static Mesh Store(string key, Mesh m)
        {
            m.name = key;
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        // ================================================================ profile helpers

        /// <summary>Points of an arc (degrees, counter-clockwise in the (r, y) plane), first and last included.</summary>
        public static void Arc(List<Vector2> into, float cx, float cy, float r, float a0, float a1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                into.Add(new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a)));
            }
        }

        // ================================================================ lathe around +Y

        /// <summary>
        /// Surface of revolution around +Y. <paramref name="profile"/>: (radius, height) from the bottom centre up to the
        /// top; a point given twice makes a crease. UV: u around, v = arc length (in metres times <paramref name="vScale"/>).
        /// </summary>
        public static Mesh Lathe(string key, IList<Vector2> profile, int segments = 40, float vScale = 4f)
        {
            key = "lathe_" + key + "_" + segments;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            int rows = profile.Count;
            var pn = new Vector2[rows];
            var arc = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                if (i > 0) arc[i] = arc[i - 1] + Vector2.Distance(profile[i], profile[i - 1]);
                bool creaseBefore = i > 0 && profile[i - 1] == profile[i];
                bool creaseAfter = i < rows - 1 && profile[i + 1] == profile[i];
                Vector2 a = i > 0 && !creaseBefore ? (profile[i] - profile[i - 1]).normalized : Vector2.zero;
                Vector2 b = i < rows - 1 && !creaseAfter ? (profile[i + 1] - profile[i]).normalized : Vector2.zero;
                Vector2 t = a + b;
                if (t.sqrMagnitude < 1e-8f) t = a.sqrMagnitude > 0f ? a : b;
                t.Normalize();
                // Profile runs upwards on the outside: the outward normal is the tangent turned clockwise.
                pn[i] = new Vector2(t.y, -t.x);
            }
            var v = new List<Vector3>((segments + 1) * rows);
            var n = new List<Vector3>(v.Capacity);
            var uv = new List<Vector2>(v.Capacity);
            for (int s = 0; s <= segments; s++)
            {
                float ang = s / (float)segments * Mathf.PI * 2f, c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                for (int i = 0; i < rows; i++)
                {
                    Vector2 p = profile[i];
                    v.Add(new Vector3(c * p.x, p.y, sn * p.x));
                    n.Add(new Vector3(c * pn[i].x, pn[i].y, sn * pn[i].x).normalized);
                    uv.Add(new Vector2(s / (float)segments, arc[i] * vScale));
                }
            }
            var tri = new List<int>();
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < rows - 1; i++)
                {
                    if (profile[i] == profile[i + 1]) continue;
                    int k = s * rows + i, k2 = k + rows;
                    tri.Add(k); tri.Add(k + 1); tri.Add(k2);
                    tri.Add(k + 1); tri.Add(k2 + 1); tri.Add(k2);
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateTangents();
            return Store(key, m);
        }

        /// <summary>Flips triangles whose geometric normal disagrees with the vertex normals.</summary>
        public static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(g, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.triangles = t;
        }

        // ================================================================ annular sector (battlements)

        /// <summary>Block between radii <paramref name="r0"/>..<paramref name="r1"/>, angles a0..a1 (degrees), heights y0..y1.</summary>
        public static Mesh Sector(string key, float r0, float r1, float a0, float a1, float y0, float y1, int n = 6)
        {
            key = "sector_" + key;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var b = new MeshDraft();
            Vector3 P(float r, float a, float y) => new Vector3(r * Mathf.Cos(a * Mathf.Deg2Rad), y, r * Mathf.Sin(a * Mathf.Deg2Rad));
            Vector3 Dir(float a) => new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0f, Mathf.Sin(a * Mathf.Deg2Rad));
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n), c = Mathf.Lerp(a0, a1, (i + 1) / (float)n), mid = (a + c) * 0.5f;
                b.Quad(P(r1, a, y0), P(r1, a, y1), P(r1, c, y1), P(r1, c, y0), Dir(mid));   // outside
                b.Quad(P(r0, c, y0), P(r0, c, y1), P(r0, a, y1), P(r0, a, y0), -Dir(mid));  // inside
                b.Quad(P(r0, a, y1), P(r0, c, y1), P(r1, c, y1), P(r1, a, y1), Vector3.up); // top
            }
            Vector3 side0 = Vector3.Cross(Vector3.up, Dir(a0)), side1 = Vector3.Cross(Dir(a1), Vector3.up);
            b.Quad(P(r0, a0, y0), P(r0, a0, y1), P(r1, a0, y1), P(r1, a0, y0), side0);
            b.Quad(P(r1, a1, y0), P(r1, a1, y1), P(r0, a1, y1), P(r0, a1, y0), side1);
            return Store(key, b.ToMesh());
        }

        // ================================================================ textured slab

        /// <summary>Flat box (x, z footprint, height h, bottom at y = 0): the top shows the whole texture (u = +x, v = +z), the sides a thin strip of it.</summary>
        public static Mesh Slab(string key, float sx, float sz, float h)
        {
            key = "slab_" + key;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            float x = sx * 0.5f, z = sz * 0.5f;
            var b = new MeshDraft();
            b.QuadUv(new Vector3(-x, h, -z), new Vector3(-x, h, z), new Vector3(x, h, z), new Vector3(x, h, -z),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), Vector3.up);
            const float e = 0.004f;
            b.QuadUv(new Vector3(-x, 0, -z), new Vector3(-x, h, -z), new Vector3(x, h, -z), new Vector3(x, 0, -z),
                new Vector2(0, 0), new Vector2(0, e), new Vector2(1, e), new Vector2(1, 0), Vector3.back);
            b.QuadUv(new Vector3(x, 0, z), new Vector3(x, h, z), new Vector3(-x, h, z), new Vector3(-x, 0, z),
                new Vector2(1, 1), new Vector2(1, 1 - e), new Vector2(0, 1 - e), new Vector2(0, 1), Vector3.forward);
            b.QuadUv(new Vector3(-x, 0, z), new Vector3(-x, h, z), new Vector3(-x, h, -z), new Vector3(-x, 0, -z),
                new Vector2(0, 1), new Vector2(e, 1), new Vector2(e, 0), new Vector2(0, 0), Vector3.left);
            b.QuadUv(new Vector3(x, 0, -z), new Vector3(x, h, -z), new Vector3(x, h, z), new Vector3(x, 0, z),
                new Vector2(1, 0), new Vector2(1 - e, 0), new Vector2(1 - e, 1), new Vector2(1, 1), Vector3.right);
            return Store(key, b.ToMesh());
        }

        /// <summary>Box of size <paramref name="size"/> centred on the origin with rounded edges (see VendingMeshes).</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int n = 5) => VendingMeshes.RoundedBox(size, radius, n);

        /// <summary>Small mesh builder: flat quads facing a given outward direction.</summary>
        sealed class MeshDraft
        {
            readonly List<Vector3> _v = new();
            readonly List<Vector3> _n = new();
            readonly List<Vector2> _uv = new();
            readonly List<int> _t = new();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) =>
                QuadUv(a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), outward);

            public void QuadUv(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
            {
                Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
                int s = _v.Count;
                _v.Add(a); _v.Add(b); _v.Add(c); _v.Add(d);
                _uv.Add(ua); _uv.Add(ub); _uv.Add(uc); _uv.Add(ud);
                bool flip = Vector3.Dot(nrm, outward) < 0f;
                for (int i = 0; i < 4; i++) _n.Add(flip ? -nrm : nrm);
                if (!flip) { _t.Add(s); _t.Add(s + 1); _t.Add(s + 2); _t.Add(s); _t.Add(s + 2); _t.Add(s + 3); }
                else { _t.Add(s); _t.Add(s + 2); _t.Add(s + 1); _t.Add(s); _t.Add(s + 3); _t.Add(s + 2); }
            }

            public Mesh ToMesh()
            {
                var m = new Mesh();
                m.SetVertices(_v); m.SetNormals(_n); m.SetUVs(0, _uv); m.SetTriangles(_t, 0);
                m.RecalculateTangents();
                return m;
            }
        }
    }
}
