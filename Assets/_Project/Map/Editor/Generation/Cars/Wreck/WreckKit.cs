using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Small building blocks of the wreck: smooth 3D noise, tubes along paths (hoses, wires, cables), shards.</summary>
    internal static class WreckKit
    {
        // ------------------------------------------------------------------ noise

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2147483647u + 0x9E3779B9u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Smooth value noise in 0..1.</summary>
        public static float Noise(Vector3 p)
        {
            int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
            float fx = p.x - xi, fy = p.y - yi, fz = p.z - zi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float L(int dx, int dy, int dz) => Hash(xi + dx, yi + dy, zi + dz);
            float x00 = Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), x10 = Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx);
            float x01 = Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), x11 = Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        /// <summary>Two-octave signed noise (about -0.5..0.5).</summary>
        public static float Noise2(Vector3 p) => (Noise(p) - 0.5f) * 0.7f + (Noise(p * 2.13f + new Vector3(5.1f, 1.7f, 3.3f)) - 0.5f) * 0.3f;

        public static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ tubes

        /// <summary>Catmull-Rom path through the points, about <paramref name="step"/> metres apart.</summary>
        public static List<Vector3> Spline(IList<Vector3> pts, float step = 0.02f)
        {
            var o = new List<Vector3>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[Mathf.Max(i - 1, 0)], b = pts[i], c = pts[i + 1], d = pts[Mathf.Min(i + 2, pts.Count - 1)];
                int n = Mathf.Max(1, Mathf.CeilToInt((c - b).magnitude / step));
                for (int k = 0; k < n; k++)
                {
                    float t = k / (float)n, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3));
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        /// <summary>Round tube along a path (parallel-transported frame, open ends unless caps). r may vary per point.</summary>
        public static void Tube(MeshBuilder mb, IList<Vector3> path, float r, Material m, int sides = 8, bool caps = true,
            System.Func<float, float> radiusAt = null)
        {
            int n = path.Count;
            if (n < 2) return;
            var T = new Vector3[n];
            for (int i = 0; i < n; i++) T[i] = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 nrm = Vector3.Cross(T[0], Mathf.Abs(T[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var ring = new Vector3[n, sides + 1];
            var rn = new Vector3[n, sides + 1];
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    var q = Quaternion.FromToRotation(T[i - 1], T[i]);
                    nrm = (q * nrm).normalized;
                }
                Vector3 bin = Vector3.Cross(T[i], nrm);
                float rr = radiusAt != null ? r * radiusAt(i / (float)(n - 1)) : r;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    Vector3 d = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    ring[i, s] = path[i] + d * rr;
                    rn[i, s] = d;
                }
            }
            for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
                mb.Quad(ring[i, s], ring[i, s + 1], ring[i + 1, s + 1], ring[i + 1, s], rn[i, s], rn[i, s + 1], rn[i + 1, s + 1], rn[i + 1, s], m);
            if (!caps) return;
            for (int s = 0; s < sides; s++)
            {
                mb.Tri(path[0], ring[0, s], ring[0, s + 1], -T[0], -T[0], -T[0], m);
                mb.Tri(path[n - 1], ring[n - 1, s], ring[n - 1, s + 1], T[n - 1], T[n - 1], T[n - 1], m);
            }
        }

        /// <summary>Corrugated rubber boot / bellows between a and b.</summary>
        public static void Bellows(MeshBuilder mb, Vector3 a, Vector3 b, float r0, float r1, int folds, Material m)
        {
            var prof = new List<Vector2>();
            float len = (b - a).magnitude;
            for (int i = 0; i <= folds * 2; i++)
            {
                float t = i / (float)(folds * 2);
                float r = Mathf.Lerp(r0, r1, t) * (i % 2 == 0 ? 1f : 0.82f);
                prof.Add(new Vector2(r, t * len));
            }
            prof.Insert(0, new Vector2(0f, 0f));
            prof.Add(new Vector2(0f, len));
            CarWheels.Lathe(mb, a, b - a, prof.ToArray(), 12, m, 60f);
        }

        // ------------------------------------------------------------------ shards and chips

        /// <summary>Thin irregular plate (a broken piece): random convex-ish outline, thickness t, lying in the plane of n.</summary>
        public static void Chip(MeshBuilder mb, Vector3 c, Vector3 n, float size, float t, Material m, System.Random rng, int corners = 5)
        {
            n.Normalize();
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(n, u);
            float rot = (float)rng.NextDouble() * Mathf.PI * 2f;
            var top = new Vector3[corners];
            var bot = new Vector3[corners];
            for (int i = 0; i < corners; i++)
            {
                float a = rot + i * Mathf.PI * 2f / corners + ((float)rng.NextDouble() - 0.5f) * 0.9f;
                float r = size * (0.45f + (float)rng.NextDouble() * 0.55f);
                Vector3 d = (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r;
                top[i] = c + d + n * (t * 0.5f);
                bot[i] = c + d - n * (t * 0.5f);
            }
            Vector3 ct = c + n * (t * 0.5f), cb = c - n * (t * 0.5f);
            for (int i = 0; i < corners; i++)
            {
                int j = (i + 1) % corners;
                mb.Tri(ct, top[i], top[j], n, n, n, m);
                mb.Tri(cb, bot[j], bot[i], -n, -n, -n, m);
                Vector3 side = Vector3.Cross(top[j] - top[i], n).normalized;
                if (Vector3.Dot(side, top[i] - ct) < 0f) side = -side;
                mb.Quad(top[i], top[j], bot[j], bot[i], side, side, side, side, m);
            }
        }

        /// <summary>Tempered-glass crumbs: small cubes scattered round c within radius on a surface with normal n.</summary>
        public static void Crumbs(MeshBuilder mb, Vector3 c, Vector3 n, float radius, int count, Material m, System.Random rng,
            float size = 0.011f)
        {
            n.Normalize();
            Vector3 u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(n, u);
            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = radius * Mathf.Sqrt((float)rng.NextDouble());
                float s = size * (0.5f + (float)rng.NextDouble());
                Vector3 p = c + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r + n * (s * 0.35f);
                var q = Quaternion.LookRotation(n, u) * Quaternion.Euler((float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f);
                mb.Box(p, new Vector3(s, s * 0.8f, s * 0.55f), m, q);
            }
        }
    }
}
