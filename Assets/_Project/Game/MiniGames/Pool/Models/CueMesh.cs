using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Meshes of the cues and the bridge: turned solids around +Z (CUE SPACE, see <see cref="PoolDims"/>) with a
    /// caller-chosen v coordinate per profile point (so wood grain runs along the cue and decal panels map 0..1 over
    /// their own length), plus a flat disc with planar UVs (bumper logo). Cached by key for the whole session.
    /// </summary>
    public static class CueMesh
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>
        /// Surface of revolution around +Z. <paramref name="profile"/> = (radius, z, v) running along the outside
        /// (z growing on the outer wall); a point given twice (same radius and z) makes a crease. u = 0..<paramref name="uRepeat"/>
        /// around (u = 0 on +Y), v taken from the profile. The ends stay open unless the profile reaches radius 0.
        /// </summary>
        public static Mesh Lathe(string key, IList<Vector3> profile, int segments = 40, float uRepeat = 1f)
        {
            key = "cuelathe_" + key + "_" + segments;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            int rows = profile.Count;
            var pn = new Vector2[rows];
            for (int i = 0; i < rows; i++)
            {
                Vector2 p = P(profile[i]);
                bool creaseBefore = i > 0 && P(profile[i - 1]) == p;
                bool creaseAfter = i < rows - 1 && P(profile[i + 1]) == p;
                Vector2 a = i > 0 && !creaseBefore ? (p - P(profile[i - 1])).normalized : Vector2.zero;
                Vector2 b = i < rows - 1 && !creaseAfter ? (P(profile[i + 1]) - p).normalized : Vector2.zero;
                Vector2 t = a + b;
                if (t.sqrMagnitude < 1e-10f) t = a.sqrMagnitude > 0f ? a : b;
                t.Normalize();
                // The profile runs along the outside: the outward normal is the (r, z) tangent turned clockwise.
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
                    Vector3 p = profile[i];
                    v.Add(new Vector3(-sn * p.x, c * p.x, p.y));
                    n.Add(new Vector3(-sn * pn[i].x, c * pn[i].x, pn[i].y).normalized);
                    uv.Add(new Vector2(s / (float)segments * uRepeat, p.z));
                }
            }
            var tri = new List<int>(segments * rows * 6);
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < rows - 1; i++)
                {
                    if (P(profile[i]) == P(profile[i + 1])) continue;
                    int k = s * rows + i, k2 = k + rows;
                    tri.Add(k); tri.Add(k + 1); tri.Add(k2);
                    tri.Add(k + 1); tri.Add(k2 + 1); tri.Add(k2);
                }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        static Vector2 P(Vector3 p) => new Vector2(p.x, p.y);

        /// <summary>Flat disc of radius <paramref name="r"/> at z = 0 facing -Z (or +Z), planar UVs 0..1 (u = +X seen from the front, v = +Y).</summary>
        public static Mesh Disc(string key, float r, int segments = 40, bool facePlusZ = false)
        {
            key = "cuedisc_" + key + "_" + segments + (facePlusZ ? "_p" : "_m");
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            float side = facePlusZ ? 1f : -1f; // seen from +Z, +X is on the viewer's left: mirror u so text reads right
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a), y = Mathf.Sin(a);
                v.Add(new Vector3(x * r, y * r, 0f));
                uv.Add(new Vector2(0.5f - side * x * 0.5f, 0.5f + y * 0.5f));
            }
            var n = new List<Vector3>();
            for (int i = 0; i < v.Count; i++) n.Add(facePlusZ ? Vector3.forward : Vector3.back);
            var tri = new List<int>();
            for (int s = 0; s < segments; s++) { tri.Add(0); tri.Add(s + 1); tri.Add(s + 2); }
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Appends a quarter-round (fillet) of radius <paramref name="rad"/> between a face and a side wall, n steps, v fixed.</summary>
        public static void Fillet(List<Vector3> into, float cr, float cz, float rad, float a0, float a1, int n, float v)
        {
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                into.Add(new Vector3(cr + rad * Mathf.Cos(a), cz + rad * Mathf.Sin(a), v));
            }
        }
    }
}
