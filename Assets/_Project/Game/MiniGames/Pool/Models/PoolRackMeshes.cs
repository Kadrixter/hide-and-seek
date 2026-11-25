using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Meshes of the ball racks: a cross-section profile swept along a rounded polygon. The path is the outline of
    /// the ball-centre polygon grown by a radius, so every corner is an arc concentric with the corner ball and any
    /// offset of the profile stays a clean parallel curve. Profile points are (o, y): o outwards from the path, y up;
    /// a point given twice is a hard crease. Normals come from the swept surface itself (so a height warp such as a
    /// finger scallop shades right), UVs run along the path (u) and the profile (v) in metres times a scale.
    /// </summary>
    public static class PoolRackMeshes
    {
        /// <summary>One path station: <see cref="Q"/> on the ball-centre polygon (XZ as x, y), <see cref="N"/> the outward normal.</summary>
        public struct Station
        {
            public Vector2 Q, N;
            public Station(Vector2 q, Vector2 n) { Q = q; N = n; }
        }

        static Vector2 EdgeNormal(Vector2 a, Vector2 b, Vector2 centroid)
        {
            Vector2 d = (b - a).normalized;
            Vector2 n = new Vector2(d.y, -d.x);
            return Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f ? -n : n;
        }

        static Vector2 Centroid(Vector2[] poly)
        {
            Vector2 c = Vector2.zero;
            foreach (var p in poly) c += p;
            return c / poly.Length;
        }

        static void Corner(List<Station> into, Vector2 v, Vector2 nIn, Vector2 nOut, int arcSegs)
        {
            float a0 = Mathf.Atan2(nIn.y, nIn.x) * Mathf.Rad2Deg, a1 = Mathf.Atan2(nOut.y, nOut.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(a0, a1);
            for (int k = 0; k <= arcSegs; k++)
            {
                float a = (a0 + delta * k / arcSegs) * Mathf.Deg2Rad;
                into.Add(new Station(v, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
            }
        }

        /// <summary>Closed rounded outline of <paramref name="poly"/> (convex, any winding): corner arcs plus straights split every <paramref name="step"/> m.</summary>
        public static List<Station> Closed(Vector2[] poly, int arcSegs, float step)
        {
            var c = Centroid(poly);
            int n = poly.Length;
            var list = new List<Station>();
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = poly[(i + n - 1) % n], v = poly[i], next = poly[(i + 1) % n];
                Vector2 nIn = EdgeNormal(prev, v, c), nOut = EdgeNormal(v, next, c);
                Corner(list, v, nIn, nOut, arcSegs);
                int parts = Mathf.Max(1, Mathf.CeilToInt((next - v).magnitude / step));
                for (int k = 1; k < parts; k++) list.Add(new Station(Vector2.Lerp(v, next, k / (float)parts), nOut));
            }
            return list;
        }

        /// <summary>Open path round corner <paramref name="i"/> of <paramref name="poly"/>, running <paramref name="ext"/> m along both sides.</summary>
        public static List<Station> CornerOnly(Vector2[] poly, int i, int arcSegs, float ext, int extSteps)
        {
            var c = Centroid(poly);
            int n = poly.Length;
            Vector2 prev = poly[(i + n - 1) % n], v = poly[i], next = poly[(i + 1) % n];
            Vector2 nIn = EdgeNormal(prev, v, c), nOut = EdgeNormal(v, next, c);
            Vector2 dIn = (v - prev).normalized, dOut = (next - v).normalized;
            var list = new List<Station>();
            for (int k = extSteps; k >= 1; k--) list.Add(new Station(v - dIn * (ext * k / extSteps), nIn));
            Corner(list, v, nIn, nOut, arcSegs);
            for (int k = 1; k <= extSteps; k++) list.Add(new Station(v + dOut * (ext * k / extSteps), nOut));
            return list;
        }

        /// <summary>
        /// Sweep <paramref name="profile"/> along <paramref name="path"/> at <paramref name="offset"/> from the polygon
        /// (the profile's o = 0 sits at that offset). <paramref name="yWarp"/> (station, o, y) may move a point's height.
        /// </summary>
        public static Mesh Sweep(string name, List<Station> path, bool closed, IList<Vector2> profile, float offset,
            Func<Station, float, float, float> yWarp = null, float uvScale = 6f)
        {
            int ns = path.Count, np = profile.Count;
            int cols = closed ? ns + 1 : ns;
            Vector3 Pos(int j, int k)
            {
                var st = path[closed ? ((j % ns) + ns) % ns : Mathf.Clamp(j, 0, ns - 1)];
                Vector2 pr = profile[k];
                Vector2 xz = st.Q + st.N * (offset + pr.x);
                float y = yWarp != null ? yWarp(st, pr.x, pr.y) : pr.y;
                return new Vector3(xz.x, y, xz.y);
            }
            // Arc length along the path (at the profile's o = 0) and along the profile.
            var su = new float[cols];
            for (int j = 1; j < cols; j++) su[j] = su[j - 1] + (Pos(j, 0) - Pos(j - 1, 0)).magnitude;
            var sv = new float[np];
            for (int k = 1; k < np; k++) sv[k] = sv[k - 1] + (profile[k] - profile[k - 1]).magnitude;

            var v = new List<Vector3>(cols * np);
            var nn = new List<Vector3>(v.Capacity);
            var uv = new List<Vector2>(v.Capacity);
            for (int j = 0; j < cols; j++)
            {
                int jp = closed ? j - 1 : Mathf.Max(0, j - 1), jn = closed ? j + 1 : Mathf.Min(ns - 1, j + 1);
                var st = path[j % ns];
                for (int k = 0; k < np; k++)
                {
                    Vector3 p = Pos(j, k);
                    Vector3 dPath = Pos(jn, k) - Pos(jp, k);
                    bool creaseBefore = k > 0 && profile[k - 1] == profile[k];
                    bool creaseAfter = k < np - 1 && profile[k + 1] == profile[k];
                    int kp = k > 0 && !creaseBefore ? k - 1 : k, kn = k < np - 1 && !creaseAfter ? k + 1 : k;
                    Vector3 dProf = Pos(j, kn) - Pos(j, kp);
                    Vector3 nrm = Vector3.Cross(dPath, dProf).normalized;
                    // Reference outward normal from the 2D profile (clockwise profile: tangent turned left).
                    Vector2 t2 = profile[kn] - profile[kp];
                    Vector2 n2 = new Vector2(-t2.y, t2.x);
                    Vector3 refN = new Vector3(st.N.x * n2.x, n2.y, st.N.y * n2.x);
                    if (Vector3.Dot(nrm, refN) < 0f) nrm = -nrm;
                    if (nrm.sqrMagnitude < 0.5f) nrm = refN.normalized;
                    v.Add(p);
                    nn.Add(nrm);
                    uv.Add(new Vector2(su[j] * uvScale, sv[k] * uvScale));
                }
            }
            var tri = new List<int>();
            for (int j = 0; j < cols - 1; j++)
                for (int k = 0; k < np - 1; k++)
                {
                    if (profile[k] == profile[k + 1]) continue;
                    int a = j * np + k, b = a + np;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b);
                    tri.Add(a + 1); tri.Add(b + 1); tri.Add(b);
                }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        // ================================================================ profile helpers

        /// <summary>Adds <paramref name="p"/> (twice when it is a crease).</summary>
        public static void Pt(List<Vector2> into, float o, float y, bool crease = false)
        {
            into.Add(new Vector2(o, y));
            if (crease) into.Add(new Vector2(o, y));
        }

        /// <summary>Arc points (degrees; may run either way), end points included.</summary>
        public static void Arc(List<Vector2> into, float co, float cy, float r, float a0, float a1, int n)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                into.Add(new Vector2(co + r * Mathf.Cos(a), cy + r * Mathf.Sin(a)));
            }
        }
    }
}
