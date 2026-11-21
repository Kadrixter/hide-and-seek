using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Procedural meshes for the vending machine: rounded boxes and the wire spiral of a shelf slot.</summary>
    public static class VendingMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Box of <paramref name="size"/> centred on the origin with all edges rounded by <paramref name="radius"/>.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int n = 6)
        {
            string key = $"rb{size.x:F3}_{size.y:F3}_{size.z:F3}_{radius:F3}_{n}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            Vector3 half = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.99f);
            Vector3 inner = half - Vector3.one * radius;
            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // Each face: an (n+1)^2 grid on the unit cube surface, pushed onto the rounded shape.
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in fn)
            {
                Vector3 a = new Vector3(f.y, f.z, f.x), b = Vector3.Cross(f, a);
                int s = v.Count;
                for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    // Cluster samples towards the edges so the rounding is smooth with few rows.
                    float ui = Remap(i / (float)n), vj = Remap(j / (float)n);
                    Vector3 p = f + a * (ui * 2f - 1f) + b * (vj * 2f - 1f);
                    Vector3 q = Vector3.Scale(p, half);
                    Vector3 c = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                    Vector3 d = q - c;
                    Vector3 normal = d.sqrMagnitude > 1e-10f ? d.normalized : f;
                    v.Add(c + normal * radius);
                    nn.Add(normal);
                    uv.Add(new Vector2(ui, vj));
                }
                for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int k = s + i * (n + 1) + j;
                    t.Add(k); t.Add(k + 1); t.Add(k + n + 2);
                    t.Add(k); t.Add(k + n + 2); t.Add(k + n + 1);
                }
            }
            var m = new Mesh { name = "RoundedBox" };
            m.SetVertices(v); m.SetNormals(nn); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            FixWinding(m);
            m.RecalculateBounds();
            m.RecalculateTangents();
            Cache[key] = m;
            return m;
        }

        static float Remap(float x)
        {
            // Two thirds of the samples in the flat middle, the rest packed into the rounded rims.
            return x < 0.15f ? x / 0.15f * 0.02f : x > 0.85f ? 0.98f + (x - 0.85f) / 0.15f * 0.02f : 0.02f + (x - 0.15f) / 0.7f * 0.96f;
        }

        /// <summary>Flips triangles whose geometric normal disagrees with the vertex normals (keeps every face outward).</summary>
        static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 g = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(g, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f) { (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]); }
            }
            m.triangles = t;
        }

        /// <summary>
        /// Wire spiral of a slot: helix around +X (from 0 to <paramref name="length"/>), coil radius, wire radius, turns.
        /// The last half turn is bent down to the motor at x = length.
        /// </summary>
        public static Mesh Spiral(float length, float coil, float wire, int turns)
        {
            string key = $"sp{length:F3}_{coil:F3}_{wire:F4}_{turns}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            const int ring = 6;
            int steps = turns * 28;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int s = 0; s <= steps; s++)
            {
                float k = s / (float)steps, a = k * turns * Mathf.PI * 2f;
                Vector3 c = new Vector3(k * length, Mathf.Cos(a) * coil, Mathf.Sin(a) * coil);
                Vector3 tan = new Vector3(length / (turns * Mathf.PI * 2f), -Mathf.Sin(a) * coil, Mathf.Cos(a) * coil).normalized;
                Vector3 nrm = Vector3.Cross(tan, Vector3.right).normalized;
                Vector3 bin = Vector3.Cross(tan, nrm);
                for (int r = 0; r < ring; r++)
                {
                    float b = r * Mathf.PI * 2f / ring;
                    v.Add(c + (nrm * Mathf.Cos(b) + bin * Mathf.Sin(b)) * wire);
                }
                if (s == 0) continue;
                int p = (s - 1) * ring, q = s * ring;
                for (int r = 0; r < ring; r++)
                {
                    int r1 = (r + 1) % ring;
                    t.Add(p + r); t.Add(q + r); t.Add(q + r1);
                    t.Add(p + r); t.Add(q + r1); t.Add(p + r1);
                }
            }
            var m = new Mesh { name = "VendSpiral" };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            // The tube winding depends on the helix handedness: make the normals point away from the wire centre.
            var vv = m.vertices; var nn = m.normals; var tt = m.triangles;
            bool flip = false;
            {
                float k = 0.5f, a = k * turns * Mathf.PI * 2f;
                Vector3 c = new Vector3(k * length, Mathf.Cos(a) * coil, Mathf.Sin(a) * coil);
                int idx = (steps / 2) * ring;
                flip = Vector3.Dot(nn[idx], vv[idx] - c) < 0f;
            }
            if (flip)
            {
                for (int i = 0; i < tt.Length; i += 3) (tt[i + 1], tt[i + 2]) = (tt[i + 2], tt[i + 1]);
                m.triangles = tt;
                m.RecalculateNormals();
            }
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }
    }
}
