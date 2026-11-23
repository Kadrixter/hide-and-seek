using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    public static partial class BoardMeshes
    {
        /// <summary>
        /// Solid made from a side silhouette: <paramref name="outline"/> is a simple polygon in the (z, y) plane, the
        /// solid spans x = ±<paramref name="halfWidth"/>(z, y) (so it may get thinner towards a snout or ears) and every
        /// edge is rounded with a quarter-circle bevel. Smooth normals all round, flat-ish caps with normals from the
        /// width gradient.
        /// </summary>
        public static Mesh Extrude(string key, IList<Vector2> outline, Func<Vector2, float> halfWidth, float bevel, int rows = 3, float uvScale = 10f)
        {
            key = "extrude_" + key;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var poly = new List<Vector2>(outline);
            if (SignedArea(poly) < 0f) poly.Reverse();
            int n = poly.Count;
            // Outward vertex normals (miter) of the CCW outline: the interior is on the left of each edge.
            var vn = new Vector2[n];
            var miter = new float[n];
            var arc = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i + n - 1) % n], cur = poly[i], next = poly[(i + 1) % n];
                Vector2 e0 = (cur - prev).normalized, e1 = (next - cur).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x), n1 = new Vector2(e1.y, -e1.x);
                Vector2 m = (n0 + n1);
                m = m.sqrMagnitude < 1e-6f ? n1 : m.normalized;
                vn[i] = m;
                miter[i] = Mathf.Min(1f / Mathf.Max(0.35f, Vector2.Dot(m, n1)), 2.2f);
                arc[i + 1] = arc[i] + Vector2.Distance(cur, next);
            }
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();

            // ---- side band: rows from the front cap edge (+x) round to the back cap edge (-x)
            int ringRows = (rows + 1) * 2;
            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                Vector2 p = poly[k];
                float hw = Mathf.Max(0.0005f, halfWidth(p));
                float b = Mathf.Min(bevel, hw * 0.8f);
                for (int r = 0; r < ringRows; r++)
                {
                    bool front = r <= rows;
                    int j = front ? r : ringRows - 1 - r;
                    float phi = j / (float)rows * Mathf.PI * 0.5f; // 0 at the cap edge, 90 deg on the side wall
                    float x = (hw - b) + b * Mathf.Cos(phi);
                    float inset = b - b * Mathf.Sin(phi);
                    Vector2 q = p - vn[k] * inset * miter[k];
                    float sx = front ? 1f : -1f;
                    v.Add(new Vector3(sx * x, q.y, q.x));
                    Vector2 n2 = vn[k] * Mathf.Sin(phi);
                    nn.Add(new Vector3(sx * Mathf.Cos(phi), n2.y, n2.x).normalized);
                    uv.Add(new Vector2(arc[i] * uvScale, (r / (float)(ringRows - 1)) * 2f * hw * uvScale));
                }
            }
            for (int i = 0; i < n; i++)
                for (int r = 0; r < ringRows - 1; r++)
                {
                    int a = i * ringRows + r, c = a + ringRows;
                    tri.Add(a); tri.Add(a + 1); tri.Add(c);
                    tri.Add(a + 1); tri.Add(c + 1); tri.Add(c);
                }

            // ---- caps (inset outline, ear-clipped)
            var inset2 = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float hw = Mathf.Max(0.0005f, halfWidth(poly[i]));
                inset2.Add(poly[i] - vn[i] * Mathf.Min(bevel, hw * 0.8f) * miter[i]);
            }
            var capTris = EarClip(inset2);
            const float d = 0.0005f;
            foreach (float sx in new[] { 1f, -1f })
            {
                int start = v.Count;
                foreach (var p in inset2)
                {
                    float hw = Mathf.Max(0.0005f, halfWidth(p));
                    v.Add(new Vector3(sx * hw, p.y, p.x));
                    // Width gradient tilts the cap normal (thinner towards the snout -> facing a little forward).
                    float gz = (halfWidth(p + new Vector2(d, 0)) - halfWidth(p - new Vector2(d, 0))) / (2f * d);
                    float gy = (halfWidth(p + new Vector2(0, d)) - halfWidth(p - new Vector2(0, d))) / (2f * d);
                    nn.Add(new Vector3(sx, -gy * 0.3f, -gz * 0.3f).normalized);
                    uv.Add(new Vector2(p.x * uvScale, p.y * uvScale));
                }
                for (int t = 0; t < capTris.Count; t += 3)
                {
                    tri.Add(start + capTris[t]); tri.Add(start + capTris[t + 1]); tri.Add(start + capTris[t + 2]);
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(v); mesh.SetNormals(nn); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            FixWinding(mesh);
            mesh.RecalculateTangents();
            return Store(key, mesh);
        }

        static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 c = p[i], d = p[(i + 1) % p.Count];
                a += c.x * d.y - d.x * c.y;
            }
            return a * 0.5f;
        }

        /// <summary>Ear-clipping triangulation of a simple CCW polygon (indices into <paramref name="p"/>).</summary>
        public static List<int> EarClip(IList<Vector2> p)
        {
            var idx = new List<int>();
            for (int i = 0; i < p.Count; i++) idx.Add(i);
            if (SignedArea(p) < 0f) idx.Reverse();
            var tris = new List<int>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 5000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    if (Cross(b - a, c - b) <= 1e-12f) continue; // reflex
                    bool inside = false;
                    for (int j = 0; j < idx.Count && !inside; j++)
                    {
                        int q = idx[j];
                        if (q == ia || q == ib || q == ic) continue;
                        inside = InTri(p[q], a, b, c);
                    }
                    if (inside) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // Degenerate leftovers: fan the rest rather than leave a hole.
                    for (int i = 1; i + 1 < idx.Count; i++) { tris.Add(idx[0]); tris.Add(idx[i]); tris.Add(idx[i + 1]); }
                    idx.Clear();
                }
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= 0f && d2 >= 0f && d3 >= 0f;
        }
    }
}
