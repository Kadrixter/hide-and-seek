using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Runtime mesh builder of the pool table (2026-11-25). Every vertex carries an explicit normal and triangles are
    /// turned to agree with it, so the callers never think about winding. Shapes: a (d, y) profile swept along a plan
    /// path with mitred joints (d = outward = right-hand side of the walking direction), a prism with a rounded top edge
    /// from a plan polygon, flat polygons, thin tubes along 3D polylines. Plan points are (x, z) stored in Vector2.
    /// Baked meshes are cached by key for the whole session (the geometry does not depend on the style).
    /// </summary>
    internal sealed class PoolTableMesh
    {
        public struct Row { public Vector2 P, N; public float V; }
        public struct Col { public Vector2 P, Off, N; public float U; }

        static readonly Dictionary<string, Mesh> Cache = new();
        readonly List<Vector3> _v = new();
        readonly List<Vector3> _n = new();
        readonly List<Vector2> _uv = new();
        readonly List<int> _t = new();

        public static bool TryGet(string key, out Mesh m) => Cache.TryGetValue(key, out m) && m != null;

        public int Vert(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(p);
            _n.Add(n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up);
            _uv.Add(uv);
            return _v.Count - 1;
        }

        /// <summary>Triangle facing the way its vertex normals point (degenerate ones are dropped).</summary>
        public void Tri(int a, int b, int c)
        {
            Vector3 g = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
            if (g.sqrMagnitude < 1e-20f) return;
            if (Vector3.Dot(g, _n[a] + _n[b] + _n[c]) < 0f) (b, c) = (c, b);
            _t.Add(a); _t.Add(b); _t.Add(c);
        }

        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        public Mesh Bake(string key)
        {
            var m = new Mesh { name = key };
            if (_v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            Cache[key] = m;
            return m;
        }

        // ================================================================ 2D helpers

        public static float Area(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 c = p[i], d = p[(i + 1) % p.Count];
                a += c.x * d.y - d.x * c.y;
            }
            return a * 0.5f;
        }

        /// <summary>Copy of the polygon, counter-clockwise in (x, z) (its outward side is then on the right).</summary>
        public static List<Vector2> Ccw(IList<Vector2> p)
        {
            var r = new List<Vector2>(p);
            if (Area(r) < 0f) r.Reverse();
            return r;
        }

        /// <summary>Arc points (degrees) appended to <paramref name="into"/>; a first point equal to the list's last is skipped.</summary>
        public static void Arc(List<Vector2> into, Vector2 c, float r, float a0, float a1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (into.Count > 0 && (into[into.Count - 1] - p).sqrMagnitude < 1e-12f) continue;
                into.Add(p);
            }
        }

        /// <summary>
        /// Profile points to rows: the normal is the walking direction turned counter-clockwise (so walk an open profile
        /// from the top inner end outwards and down, a closed one clockwise); corners sharper than
        /// <paramref name="creaseDeg"/> become two rows at the same spot (no strip between them).
        /// </summary>
        public static List<Row> Rows(IList<Vector2> pts, float creaseDeg)
        {
            var rows = new List<Row>(pts.Count + 8);
            int n = pts.Count;
            float v = 0f;
            Vector2 SegN(int i)
            {
                Vector2 t = (pts[i + 1] - pts[i]).normalized;
                return new Vector2(-t.y, t.x);
            }
            for (int i = 0; i < n; i++)
            {
                if (i > 0) v += Vector2.Distance(pts[i], pts[i - 1]);
                if (i == 0) { rows.Add(new Row { P = pts[i], N = SegN(0), V = v }); continue; }
                if (i == n - 1) { rows.Add(new Row { P = pts[i], N = SegN(i - 1), V = v }); continue; }
                Vector2 a = SegN(i - 1), b = SegN(i);
                if (Vector2.Angle(a, b) > creaseDeg)
                {
                    rows.Add(new Row { P = pts[i], N = a, V = v });
                    rows.Add(new Row { P = pts[i], N = b, V = v });
                }
                else rows.Add(new Row { P = pts[i], N = (a + b).normalized, V = v });
            }
            return rows;
        }

        static Vector2 EdgeN(IList<Vector2> p, int i, int n)
        {
            Vector2 e = (p[(i + 1) % n] - p[i]).normalized;
            return new Vector2(e.y, -e.x);
        }

        /// <summary>
        /// Columns of a plan path: Off = mitred offset per metre of outward distance, N = outward shading normal (split in
        /// two columns at a corner sharper than <paramref name="creaseDeg"/>), U = path length.
        /// </summary>
        public static List<Col> Cols(IList<Vector2> path, bool closed, float creaseDeg)
        {
            int n = path.Count;
            var cols = new List<Col>(n + 8);
            int count = closed ? n + 1 : n;
            float u = 0f;
            for (int k = 0; k < count; k++)
            {
                int i = k % n;
                if (k > 0) u += Vector2.Distance(path[i], path[(k - 1) % n]);
                bool hasPrev = closed || i > 0, hasNext = closed || i < n - 1;
                Vector2 nPrev = hasPrev ? EdgeN(path, (i + n - 1) % n, n) : Vector2.zero;
                Vector2 nNext = hasNext ? EdgeN(path, i, n) : Vector2.zero;
                if (!hasPrev) nPrev = nNext;
                if (!hasNext) nNext = nPrev;
                Vector2 m = nPrev + nNext;
                m = m.sqrMagnitude < 1e-8f ? nNext : m.normalized;
                Vector2 off = m / Mathf.Max(0.3f, Vector2.Dot(m, nNext));
                if (Vector2.Angle(nPrev, nNext) > creaseDeg)
                {
                    if (k > 0) cols.Add(new Col { P = path[i], Off = off, N = nPrev, U = u });
                    if (k < count - 1) cols.Add(new Col { P = path[i], Off = off, N = nNext, U = u });
                }
                else cols.Add(new Col { P = path[i], Off = off, N = m, U = u });
            }
            return cols;
        }

        /// <summary>Closed polygon moved outwards by <paramref name="d"/> (negative = inwards), mitred corners.</summary>
        public static List<Vector2> Offset(IList<Vector2> poly, float d)
        {
            var cols = Cols(poly, true, 181f);
            var r = new List<Vector2>(poly.Count);
            for (int i = 0; i < poly.Count; i++) r.Add(cols[i].P + cols[i].Off * d);
            return r;
        }

        /// <summary>Reverses an open path unless the right-hand side of its middle edge points along <paramref name="want"/>(mid).</summary>
        public static List<Vector2> OrientRight(List<Vector2> path, Func<Vector2, Vector2> want)
        {
            int m = Mathf.Max(0, path.Count / 2 - 1);
            Vector2 e = path[m + 1] - path[m], mid = (path[m] + path[m + 1]) * 0.5f;
            if (Vector2.Dot(new Vector2(e.y, -e.x), want(mid)) < 0f) path.Reverse();
            return path;
        }

        // ================================================================ shapes

        /// <summary>Profile (d, y) swept along a plan path; UV = (path length, profile length) * <paramref name="uvScale"/>.</summary>
        public void Sweep(IList<Vector2> path, bool closed, IList<Vector2> profile, Vector2 uvScale, float pathCrease = 30f, float profCrease = 40f, bool capEnds = false)
        {
            var cols = Cols(path, closed, pathCrease);
            var rows = Rows(profile, profCrease);
            int rc = rows.Count;
            var idx = new int[cols.Count * rc];
            for (int j = 0; j < cols.Count; j++)
            {
                var c = cols[j];
                for (int i = 0; i < rc; i++)
                {
                    var r = rows[i];
                    Vector2 q = c.P + c.Off * r.P.x;
                    idx[j * rc + i] = Vert(new Vector3(q.x, r.P.y, q.y), new Vector3(c.N.x * r.N.x, r.N.y, c.N.y * r.N.x),
                        new Vector2(c.U * uvScale.x, r.V * uvScale.y));
                }
            }
            for (int j = 0; j + 1 < cols.Count; j++)
            {
                if ((cols[j].P - cols[j + 1].P).sqrMagnitude < 1e-14f) continue;
                for (int i = 0; i + 1 < rc; i++)
                {
                    if ((rows[i].P - rows[i + 1].P).sqrMagnitude < 1e-14f) continue;
                    Quad(idx[j * rc + i], idx[(j + 1) * rc + i], idx[(j + 1) * rc + i + 1], idx[j * rc + i + 1]);
                }
            }
            if (!capEnds || closed) return;
            var loop = new List<Vector2>();
            foreach (var p in profile)
                if (loop.Count == 0 || (loop[loop.Count - 1] - p).sqrMagnitude > 1e-14f) loop.Add(p);
            if (loop.Count > 2 && (loop[0] - loop[loop.Count - 1]).sqrMagnitude < 1e-14f) loop.RemoveAt(loop.Count - 1);
            for (int e = 0; e < 2; e++)
            {
                var c = e == 0 ? cols[0] : cols[cols.Count - 1];
                Vector2 t = e == 0 ? path[0] - path[1] : path[path.Count - 1] - path[path.Count - 2];
                t.Normalize();
                Polygon(loop, q => { Vector2 p = c.P + c.Off * q.x; return new Vector3(p.x, q.y, p.y); }, new Vector3(t.x, 0f, t.y), uvScale);
            }
        }

        /// <summary>Flat polygon given in its own 2D coordinates, placed in 3D by <paramref name="place"/>, facing <paramref name="normal"/>.</summary>
        public void Polygon(IList<Vector2> poly, Func<Vector2, Vector3> place, Vector3 normal, Vector2 uvScale)
        {
            var tris = BoardMeshes.EarClip(poly);
            int s = _v.Count;
            foreach (var q in poly) Vert(place(q), normal, new Vector2(q.x * uvScale.x, q.y * uvScale.y));
            for (int i = 0; i + 2 < tris.Count; i += 3) Tri(s + tris[i], s + tris[i + 1], s + tris[i + 2]);
        }

        /// <summary>Horizontal plan polygon at height <paramref name="y"/>; planar UV along <paramref name="uAxis"/> unless <paramref name="uv"/> is given.</summary>
        public void Cap(IList<Vector2> poly, float y, bool up, Vector2 uvScale, Vector2 uAxis, Func<Vector2, Vector2> uv = null)
        {
            var tris = BoardMeshes.EarClip(poly);
            Vector2 vAxis = new Vector2(-uAxis.y, uAxis.x);
            int s = _v.Count;
            foreach (var q in poly)
                Vert(new Vector3(q.x, y, q.y), up ? Vector3.up : Vector3.down,
                    uv != null ? uv(q) : new Vector2(Vector2.Dot(q, uAxis) * uvScale.x, Vector2.Dot(q, vAxis) * uvScale.y));
            for (int i = 0; i + 2 < tris.Count; i += 3) Tri(s + tris[i], s + tris[i + 1], s + tris[i + 2]);
        }

        /// <summary>Solid from a plan polygon between y0 and y1, its top edge rounded by <paramref name="round"/>.</summary>
        public void Prism(IList<Vector2> poly, float y0, float y1, float round, Vector2 uvScale, Vector2 uAxis, bool bottom = false, Func<Vector2, Vector2> topUv = null)
        {
            var p = Ccw(poly);
            var prof = new List<Vector2>();
            if (round > 0f) Arc(prof, new Vector2(-round, y1 - round), round, 90f, 0f, 3);
            else prof.Add(new Vector2(0f, y1));
            prof.Add(new Vector2(0f, y0));
            Sweep(p, true, prof, uvScale, 30f, 50f);
            Cap(round > 0f ? Offset(p, -round) : p, y1, true, uvScale, uAxis, topUv);
            if (bottom) Cap(p, y0, false, uvScale, uAxis);
        }

        /// <summary>Round tube along a 3D polyline (radius r0 at the start to r1 at the end), parallel-transported frame.</summary>
        public void Tube(IList<Vector3> pts, float r0, float r1, int sides, bool closed = false)
        {
            int n = pts.Count;
            int rings = closed ? n + 1 : n;
            Vector3 T(int i) => closed
                ? (pts[(i + 1) % n] - pts[(i + n - 1) % n]).normalized
                : (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 t0 = T(0);
            Vector3 a = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            int start = _v.Count;
            float len = 0f;
            for (int k = 0; k < rings; k++)
            {
                int i = k % n;
                if (k > 0) len += Vector3.Distance(pts[i], pts[(k - 1) % n]);
                Vector3 t = T(i);
                a = (a - t * Vector3.Dot(a, t)).normalized;
                Vector3 b = Vector3.Cross(t, a);
                float r = Mathf.Lerp(r0, r1, k / (float)Mathf.Max(1, rings - 1));
                for (int s = 0; s < sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                    Vert(pts[i] + dir * r, dir, new Vector2(len * 0.9f, s / (float)sides * 0.02f));
                }
            }
            for (int k = 0; k + 1 < rings; k++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start + k * sides + s, i1 = start + k * sides + (s + 1) % sides;
                    Quad(i0, i1, i1 + sides, i0 + sides);
                }
        }
    }
}
