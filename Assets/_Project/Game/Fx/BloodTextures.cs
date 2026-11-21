using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Procedural blood textures, built once at runtime: irregular splats (metaball blob + lobes + satellite drops +
    /// streaks), a wall run, a shoe print and raw flesh. Each comes with a normal map from its thickness, so the
    /// glossy shader shows a raised, wet edge.
    /// </summary>
    public static class BloodTextures
    {
        /// <summary>Splat <paramref name="size"/> px. <paramref name="detail"/> 0..1 = how many lobes / drops around it.</summary>
        public static Texture2D Splat(int seed, int size, float detail, out Texture2D normal)
        {
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // Metaballs in -1..1 space: (x, y, r).
            var balls = new System.Collections.Generic.List<Vector3> { new Vector3(R(-0.05f, 0.05f), R(-0.05f, 0.05f), R(0.36f, 0.44f)) };
            int lobes = Mathf.RoundToInt(R(4f, 8f) * detail) + 1;
            for (int i = 0; i < lobes; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(0.2f, 0.42f);
                balls.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, R(0.1f, 0.22f)));
            }
            int streaks = Mathf.RoundToInt(R(1f, 4f) * detail);
            for (int s = 0; s < streaks; s++)
            {
                float a = R(0f, Mathf.PI * 2f);
                float r0 = R(0.08f, 0.12f);
                for (float d = 0.35f; d < R(0.6f, 0.85f); d += 0.05f)
                {
                    r0 *= 0.88f;
                    balls.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, r0));
                }
                balls.Add(new Vector3(Mathf.Cos(a) * R(0.78f, 0.9f), Mathf.Sin(a) * R(0.78f, 0.9f), R(0.035f, 0.06f)));
            }
            int drops = Mathf.RoundToInt(R(8f, 18f) * detail);
            for (int i = 0; i < drops; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(0.5f, 0.9f);
                balls.Add(new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, R(0.015f, 0.05f)));
            }

            var h = new float[size * size];
            float nOff = R(0f, 100f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float f = 0f;
                foreach (var b in balls)
                {
                    float dx = u - b.x, dy = v - b.y;
                    f += b.z * b.z / (dx * dx + dy * dy + 1e-4f);
                }
                // Wobbly edge.
                f *= 0.9f + 0.2f * Mathf.PerlinNoise(nOff + u * 4f, nOff + v * 4f);
                float edge = Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) * 12f); // never touch the texture border
                h[y * size + x] = f * edge;
            }
            return Build(h, size, size, 1f, 0.9f, 6f, out normal);
        }

        /// <summary>Vertical trickle: thin at the top, bulging drop at the bottom.</summary>
        public static Texture2D Run(out Texture2D normal)
        {
            const int W = 16, H = 128;
            var h = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f;
                float v = (y + 0.5f) / H; // 0 bottom .. 1 top
                float width = Mathf.Lerp(0.35f, 0.75f, v) + 0.1f * Mathf.Sin(v * 23f);
                float body = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.05f, width));
                // Bulb at the bottom end.
                float bv = (v - 0.07f) / 0.07f;
                float bulb = Mathf.Clamp01(1f - (u * u / 0.8f + bv * bv));
                float top = Mathf.Clamp01((1f - v) * 10f);
                float val = Mathf.Max(body * (v > 0.1f ? 1f : 0f), bulb * 1.3f) * top;
                h[y * W + x] = val * 1.6f;
            }
            return Build(h, W, H, 0.3f, 0.95f, 4f, out normal);
        }

        /// <summary>Shoe sole: front pad and heel with tread lines (toe at +v).</summary>
        public static Texture2D Foot(out Texture2D normal)
        {
            const int W = 32, H = 80;
            var h = new float[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H * 2f - 1f;
                float fx = (u + 0.06f) / 0.86f, fy = (v - 0.3f) / 0.62f;
                float front = 1f - (fx * fx + fy * fy);
                float hx = u / 0.72f, hy = (v + 0.68f) / 0.3f;
                float heel = 1f - (hx * hx + hy * hy);
                float sole = Mathf.Max(front, heel);
                float tread = (Mathf.Sin(v * 46f) > -0.35f ? 1f : 0.35f) * (Mathf.Sin(u * 30f + v * 8f) > -0.6f ? 1f : 0.5f);
                float patchy = 0.55f + 0.6f * Mathf.PerlinNoise(u * 3.1f + 11f, v * 2.3f + 5f);
                h[y * W + x] = Mathf.Clamp01(sole * 5f) * tread * patchy * 1.3f;
            }
            return Build(h, W, H, 0.6f, 0.75f, 2f, out normal);
        }

        /// <summary>Raw flesh for stumps (fully opaque, lumpy).</summary>
        public static Texture2D Flesh(out Texture2D normal)
        {
            const int S = 32;
            var h = new float[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                h[y * S + x] = 1f + 0.8f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f) + 0.4f * Mathf.PerlinNoise(x * 0.9f + 7f, y * 0.9f + 3f);
            var tex = Build(h, S, S, 1f, 1f, 3f, out normal);
            // Bone in the middle: a lighter dot.
            var px = tex.GetPixels32();
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2f - 1f, v = (y + 0.5f) / S * 2f - 1f;
                byte a = 255;
                if (u * u + v * v < 0.07f) px[y * S + x] = new Color32(255, 235, 220, a);
                else px[y * S + x].a = a;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Height field -> albedo (RGB shade: thick = darker, alpha = coverage) and a linear normal map (RG = xy).
        /// </summary>
        private static Texture2D Build(float[] h, int w, int hgt, float threshold, float softness, float bump, out Texture2D normal)
        {
            var tex = new Texture2D(w, hgt, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "BloodAlbedo" };
            normal = new Texture2D(w, hgt, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = "BloodNormal" };
            var col = new Color32[w * hgt];
            var nrm = new Color32[w * hgt];
            var t = new float[w * hgt];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold * softness, threshold * (2f - softness), h[i]));
            }
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float cov = t[i];
                float thick = Mathf.Clamp01((h[i] - threshold) * 0.45f);
                float shade = Mathf.Lerp(1.05f, 0.62f, thick); // thick centre darker, thin edge a brighter red
                byte s = (byte)(Mathf.Clamp01(shade) * 255f);
                col[i] = new Color32(s, s, s, (byte)(cov * 255f));

                float hl = H(t, w, hgt, x - 1, y), hr = H(t, w, hgt, x + 1, y);
                float hd = H(t, w, hgt, x, y - 1), hu = H(t, w, hgt, x, y + 1);
                var n = new Vector3((hl - hr) * bump, (hd - hu) * bump, 1f).normalized;
                nrm[i] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
            tex.SetPixels32(col);
            tex.Apply(true);
            normal.SetPixels32(nrm);
            normal.Apply(true);
            return tex;
        }

        private static float H(float[] t, int w, int h, int x, int y)
        {
            x = Mathf.Clamp(x, 0, w - 1);
            y = Mathf.Clamp(y, 0, h - 1);
            return t[y * w + x];
        }
    }
}
