using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Procedural meshes of the boombox: lathe surfaces around +Z (speaker cones, surrounds, dust caps, trim rings,
    /// knobs, tweeters) and tubes along a path (handle, antenna). Smooth normals; meshes are cached by key.
    /// </summary>
    public static class BoomboxMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>
        /// Surface of revolution around the local +Z axis. <paramref name="profile"/>: (radius, z) in order; a point given
        /// twice makes a hard crease there. Normals point to the right of the profile direction in the (r, z) plane,
        /// so a profile that runs from the axis outwards while z falls faces +Z (the front).
        /// </summary>
        public static Mesh Lathe(string key, IList<Vector2> profile, int segments = 64)
        {
            key = "lathe_" + key + "_" + segments;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            int rows = profile.Count;
            var pn = new Vector2[rows];
            for (int i = 0; i < rows; i++)
            {
                bool creaseBefore = i > 0 && profile[i - 1] == profile[i];
                bool creaseAfter = i < rows - 1 && profile[i + 1] == profile[i];
                Vector2 a = i > 0 && !creaseBefore ? (profile[i] - profile[i - 1]).normalized : Vector2.zero;
                Vector2 b = i < rows - 1 && !creaseAfter ? (profile[i + 1] - profile[i]).normalized : Vector2.zero;
                if (creaseAfter) b = Vector2.zero;
                if (creaseBefore) a = Vector2.zero;
                Vector2 t = a + b;
                if (t.sqrMagnitude < 1e-8f) t = a.sqrMagnitude > 0f ? a : b;
                t.Normalize();
                pn[i] = new Vector2(-t.y, t.x);
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
                    v.Add(new Vector3(c * p.x, sn * p.x, p.y));
                    n.Add(new Vector3(c * pn[i].x, sn * pn[i].x, pn[i].y).normalized);
                    uv.Add(new Vector2(s / (float)segments, i / (float)Mathf.Max(1, rows - 1)));
                }
            }
            var tri = new List<int>();
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < rows - 1; i++)
                {
                    if (profile[i] == profile[i + 1]) continue;
                    int k = s * rows + i, k2 = k + rows;
                    tri.Add(k); tri.Add(k2); tri.Add(k + 1);
                    tri.Add(k + 1); tri.Add(k2); tri.Add(k2 + 1);
                }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            FixWinding(m);
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Round tube of <paramref name="radius"/> along <paramref name="path"/> with flat end caps.</summary>
        public static Mesh Tube(string key, IList<Vector3> path, float radius, int ring = 14)
        {
            key = "tube_" + key + "_" + ring;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            Vector3 prevNormal = Vector3.zero;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tan = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                // Parallel transport of the ring frame: no twisting along the bends.
                Vector3 nrm = i == 0 ? Vector3.Cross(tan, Mathf.Abs(tan.z) < 0.9f ? Vector3.forward : Vector3.right).normalized
                                     : Vector3.ProjectOnPlane(prevNormal, tan).normalized;
                prevNormal = nrm;
                Vector3 bin = Vector3.Cross(tan, nrm);
                for (int r = 0; r <= ring; r++)
                {
                    float a = r / (float)ring * Mathf.PI * 2f;
                    Vector3 d = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    v.Add(path[i] + d * radius);
                    n.Add(d);
                }
                if (i == 0) continue;
                int p = (i - 1) * (ring + 1), q = i * (ring + 1);
                for (int r = 0; r < ring; r++)
                {
                    t.Add(p + r); t.Add(q + r); t.Add(q + r + 1);
                    t.Add(p + r); t.Add(q + r + 1); t.Add(p + r + 1);
                }
            }
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? 0 : path.Count - 1;
                Vector3 outward = e == 0 ? (path[0] - path[1]).normalized : (path[i] - path[i - 1]).normalized;
                int centre = v.Count;
                v.Add(path[i]); n.Add(outward);
                int baseRing = i * (ring + 1);
                for (int r = 0; r <= ring; r++) { v.Add(v[baseRing + r]); n.Add(outward); }
                for (int r = 0; r < ring; r++) { t.Add(centre); t.Add(centre + 1 + r); t.Add(centre + 2 + r); }
            }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            FixWinding(m);
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Points of a circular arc in the XY plane (centre, radius, from / to angle in degrees).</summary>
        public static void Arc(List<Vector3> into, Vector3 centre, float radius, float fromDeg, float toDeg, int steps)
        {
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)steps) * Mathf.Deg2Rad;
                var p = centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                if (into.Count == 0 || (into[into.Count - 1] - p).sqrMagnitude > 1e-10f) into.Add(p);
            }
        }

        /// <summary>Quarter-ellipse from <paramref name="from"/> to <paramref name="to"/> in (r, z): rounded rims and domes.
        /// radialFirst: leaves <paramref name="from"/> moving along r, else along z.</summary>
        public static void Round(List<Vector2> into, Vector2 from, Vector2 to, int steps, bool radialFirst)
        {
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 0.5f;
                Vector2 p = radialFirst
                    ? new Vector2(from.x + (to.x - from.x) * Mathf.Sin(a), from.y + (to.y - from.y) * (1f - Mathf.Cos(a)))
                    : new Vector2(from.x + (to.x - from.x) * (1f - Mathf.Cos(a)), from.y + (to.y - from.y) * Mathf.Sin(a));
                if (into.Count == 0 || (into[into.Count - 1] - p).sqrMagnitude > 1e-12f) into.Add(p);
            }
        }

        /// <summary>Flips triangles whose geometric normal disagrees with the vertex normals.</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(g, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            m.triangles = t;
        }
    }
}
