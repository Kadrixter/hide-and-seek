using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Procedural surface textures for the security rooms (generated once per run, cached): painted concrete, rough
    /// crate planks, desk laminate, linoleum tiles, brushed metal and seat fabric. Each comes with a matching normal map.
    /// </summary>
    public static class SecurityTextures
    {
        public enum Kind { Concrete, Planks, Laminate, Linoleum, Metal, Fabric, Cork }

        private static readonly Dictionary<Kind, (Texture2D albedo, Texture2D normal)> Cache = new();

        public static Texture2D Albedo(Kind k) => Get(k).albedo;
        public static Texture2D Normal(Kind k) => Get(k).normal;

        // ---------------------------------------------------------------- noise

        private static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        public static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable-enough fractal noise in 0..1.</summary>
        public static float Fbm(float x, float y, int octaves)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                s += Noise(x, y) * amp; norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return s / norm;
        }

        // Periodic noise (wraps every `period` cells) so materials tile without seams.
        private static float PNoise(float x, float y, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period;
            int x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0), Hash(x1, y0), fx), Mathf.Lerp(Hash(x0, y1), Hash(x1, y1), fx), fy);
        }

        private static float PFbm(float u, float v, int basePeriod, int octaves)
        {
            float s = 0f, amp = 0.5f, norm = 0f;
            int p = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                s += PNoise(u * p, v * p, p) * amp; norm += amp;
                p *= 2; amp *= 0.5f;
            }
            return s / norm;
        }

        // ---------------------------------------------------------------- textures

        private static (Texture2D albedo, Texture2D normal) Get(Kind k)
        {
            if (Cache.TryGetValue(k, out var t) && t.albedo != null) return t;
            const int n = 256;
            var col = new Color[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                Sample(k, u, v, out col[y * n + x], out h[y * n + x]);
            }
            var albedo = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Sec_" + k, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            albedo.SetPixels(col);
            albedo.Apply(true, false);
            var normal = NormalFrom(h, n, k == Kind.Planks || k == Kind.Concrete ? 3.5f : 1.6f, "Sec_" + k + "_N");
            albedo.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            normal.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            t = (albedo, normal);
            Cache[k] = t;
            return t;
        }

        private static void Sample(Kind k, float u, float v, out Color c, out float height)
        {
            switch (k)
            {
                case Kind.Concrete:
                {
                    // Painted block wall: faint block joints, roller marks, stains running down.
                    float n = PFbm(u, v, 4, 5);
                    float pores = PNoise(u * 96f, v * 96f, 96) > 0.9f ? 0.045f : 0f;
                    float jy = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f), row = Mathf.Floor(v * 4f);
                    float jx = Mathf.Abs(Mathf.Repeat(u * 2f + row * 0.5f, 1f) - 0.5f);
                    float joint = Mathf.Max(Mathf.SmoothStep(0.488f, 0.5f, jy), Mathf.SmoothStep(0.492f, 0.5f, jx));
                    float stain = Mathf.Clamp01(PFbm(u * 1f, v * 0.25f, 3, 3) * 2.2f - 1.1f);
                    float g = 0.62f + (n - 0.5f) * 0.14f - pores - joint * 0.1f - stain * 0.16f;
                    c = new Color(g * 0.98f, g, g * 0.96f);
                    height = n * 0.6f - pores * 2f - joint * 1.2f;
                    break;
                }
                case Kind.Planks:
                {
                    // Rough sawn planks running horizontally (u), 5 per tile, grain, knots, dark gaps.
                    float pv = v * 5f; int plank = Mathf.FloorToInt(pv); float fv = pv - plank;
                    float shift = Hash(plank, 7) * 3f;
                    float grain = PNoise(u * 4f + shift, fv * 40f + plank * 13f, 4) * 0.5f + Mathf.Sin((u * 16f + PNoise(u * 8f, pv * 2f, 8) * 3f) * Mathf.PI * 2f + plank) * 0.12f;
                    float knotD = Vector2.Distance(new Vector2(Mathf.Repeat(u + Hash(plank, 3), 1f), fv), new Vector2(0.5f, 0.5f));
                    float knot = Hash(plank, 9) > 0.45f ? Mathf.Clamp01(1f - knotD * 9f) : 0f;
                    float gap = Mathf.SmoothStep(0.04f, 0.0f, Mathf.Min(fv, 1f - fv));
                    float tone = 0.85f + Hash(plank, 1) * 0.25f;
                    float dirt = PFbm(u, v, 3, 3);
                    var wood = new Color(0.62f, 0.45f, 0.27f) * tone * (0.82f + grain * 0.3f) * (1f - knot * 0.55f) * (0.85f + dirt * 0.25f);
                    c = Color.Lerp(wood, new Color(0.12f, 0.08f, 0.05f), gap * 0.9f);
                    c.a = 1f;
                    height = grain * 0.5f - gap * 2.5f - knot * 0.6f;
                    break;
                }
                case Kind.Laminate:
                {
                    float grain = PNoise(u * 2f, v * 60f, 2) * 0.6f + PNoise(u * 8f, v * 120f, 8) * 0.4f;
                    float wear = PFbm(u, v, 2, 4);
                    float g = 0.36f + grain * 0.08f + (wear - 0.5f) * 0.06f;
                    c = new Color(g * 1.05f, g * 0.97f, g * 0.88f);
                    height = grain * 0.2f;
                    break;
                }
                case Kind.Linoleum:
                {
                    // 4x4 tiles, alternating shade, speckles, scuffs and grime in the joints.
                    int tx = Mathf.FloorToInt(u * 4f), ty = Mathf.FloorToInt(v * 4f);
                    float fu = u * 4f - tx, fv = v * 4f - ty;
                    float joint = Mathf.SmoothStep(0.03f, 0.0f, Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv)));
                    float speck = PNoise(u * 128f, v * 128f, 128) > 0.8f ? 0.1f : 0f;
                    float scuff = Mathf.Clamp01(PFbm(u, v, 4, 4) * 2.4f - 1.2f);
                    float baseG = ((tx + ty) & 1) == 0 ? 0.5f : 0.44f;
                    float g = baseG - speck + (Hash(tx, ty) - 0.5f) * 0.04f - scuff * 0.16f - joint * 0.2f;
                    c = new Color(g * 0.95f, g, g * 0.9f);
                    height = -joint * 1.2f - speck;
                    break;
                }
                case Kind.Metal:
                {
                    float brush = PNoise(u * 2f, v * 128f, 2) * 0.5f + PNoise(u * 4f, v * 64f, 4) * 0.5f;
                    float scratch = PNoise(u * 64f, v * 8f, 8) > 0.9f ? 0.08f : 0f;
                    float g = 0.6f + (brush - 0.5f) * 0.14f + scratch;
                    c = new Color(g, g, g * 1.02f);
                    height = brush * 0.15f;
                    break;
                }
                case Kind.Cork:
                {
                    float n = PNoise(u * 96f, v * 96f, 96) * 0.6f + PNoise(u * 32f, v * 32f, 32) * 0.4f;
                    c = new Color(0.62f, 0.44f, 0.27f) * (0.75f + n * 0.4f);
                    c.a = 1f;
                    height = n * 0.8f;
                    break;
                }
                default: // Fabric: woven
                {
                    float wx = Mathf.Sin(u * 256f * Mathf.PI), wy = Mathf.Sin(v * 256f * Mathf.PI);
                    float weave = 0.5f + 0.25f * wx * wy;
                    float n = PFbm(u, v, 8, 3);
                    float g = 0.55f + (weave - 0.5f) * 0.25f + (n - 0.5f) * 0.15f;
                    c = new Color(g, g, g);
                    height = weave;
                    break;
                }
            }
        }

        private static Texture2D NormalFrom(float[] h, int n, float bump, string name)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float l = h[y * n + (x + n - 1) % n], r = h[y * n + (x + 1) % n];
                float d = h[((y + n - 1) % n) * n + x], u = h[((y + 1) % n) * n + x];
                var v = new Vector3((l - r) * bump, (d - u) * bump, 1f).normalized;
                px[y * n + x] = new Color32((byte)((v.x * 0.5f + 0.5f) * 255f), (byte)((v.y * 0.5f + 0.5f) * 255f), (byte)((v.z * 0.5f + 0.5f) * 255f), 255);
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }
    }
}
