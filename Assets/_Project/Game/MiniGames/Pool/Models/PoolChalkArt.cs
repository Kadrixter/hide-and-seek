using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Textures of the chalk cube (made per wear step and kept): the blue chalk (grainy powder with pale specks, a
    /// pressed ring-and-star stamp that fades with use, and in the worn dish the circular scuffs of a twisting tip with
    /// a halo of loose powder) and the paper wrapper (four 22 mm sides on one strip: off-white paper, a navy band with
    /// gold rules, "PRO" / "CHALK" lettering and an 8-ball emblem on alternate sides, red diamonds, blue chalk dust
    /// rubbed in near the top edge with wear). No real brand anywhere.
    /// </summary>
    public static class PoolChalkArt
    {
        public const int TopPx = 512, SidePx = 256;
        public static readonly Color ChalkBlue = new(0.13f, 0.40f, 0.80f);
        static readonly Color Paper = new(0.94f, 0.93f, 0.89f), Navy = new(0.07f, 0.11f, 0.30f), Gold = new(0.86f, 0.68f, 0.28f),
            Red = new(0.80f, 0.10f, 0.09f), Black = new(0.04f, 0.04f, 0.045f);

        static readonly Dictionary<int, Texture2D> Tops = new(), Wrappers = new();

        /// <summary>Wear quantised to tenths (the step textures and meshes are cached by).</summary>
        public static int Step(float wear01) => Mathf.RoundToInt(Mathf.Clamp01(wear01) * 10f);

        /// <summary>The tip dish of a chalk worn to <paramref name="wear01"/>: centre (x, z from the cube's middle), radius, depth (m).</summary>
        public static (Vector2 centre, float radius, float depth) Dish(float wear01)
        {
            float w = Mathf.Clamp01(wear01);
            return (new Vector2(0.0007f, -0.0005f) * w, 0.0058f + 0.003f * w, 0.0042f * w);
        }

        static Texture2D New(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGB24, true, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        // ================================================================ chalk

        /// <summary>Chalk texture for the top (u, v = x, z across the cube) and the sides.</summary>
        public static Texture2D Top(float wear01)
        {
            int step = Step(wear01);
            if (Tops.TryGetValue(step, out var cached) && cached != null) return cached;
            float wear = step / 10f;
            var (dc, dr, depth) = Dish(wear);
            int n = TopPx;
            float size = PoolChalkModel.Size;
            var px = new Color32[n * n];
            float stamp = Mathf.Clamp01(1f - wear * 3.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                    float g = Mathf.PerlinNoise(u * 18f, v * 18f) * 0.5f + Mathf.PerlinNoise(u * 70f + 3f, v * 70f) * 0.35f + Hash(x, y) * 0.25f;
                    float lum = 0.9f + (g - 0.55f) * 0.22f;
                    Color c = ChalkBlue * lum;
                    // Pale specks of loose powder and a few dark grains.
                    float h = Hash(x * 7 + 1, y * 3 + 5);
                    if (h > 0.985f) c = Color.Lerp(c, new Color(0.62f, 0.78f, 0.95f), 0.6f);
                    else if (h < 0.01f) c *= 0.7f;
                    // Pressed stamp of a new cube: a ring and a five-point star.
                    Vector2 p = new Vector2((u - 0.5f) * size, (v - 0.5f) * size);
                    if (stamp > 0f)
                    {
                        float ring = Mathf.Abs(p.magnitude - 0.0072f) - 0.00028f;
                        float star = Star(p, 0.0042f, 0.45f);
                        float ink = Mathf.Max(Cover(ring, size / n), Cover(star, size / n));
                        c = Color.Lerp(c, c * 0.78f, ink * stamp);
                    }
                    // Worn dish: compacted darker chalk with circular scuffs, loose powder round the rim.
                    if (depth > 0f)
                    {
                        Vector2 q = p - dc;
                        float r = q.magnitude / dr;
                        if (r < 1.25f)
                        {
                            float ang = Mathf.Atan2(q.y, q.x);
                            float scuff = Mathf.PerlinNoise(r * 46f, ang * 1.6f + 10f) * 0.6f + Mathf.PerlinNoise(r * 120f, ang * 3f) * 0.4f;
                            float inside = Mathf.Clamp01((1f - r) * 8f);
                            Color dish = ChalkBlue * (0.74f + scuff * 0.22f);
                            c = Color.Lerp(c, dish, inside * Mathf.Clamp01(wear * 2f));
                            float halo = Mathf.Exp(-Mathf.Pow((r - 1.04f) * 12f, 2f)) * Mathf.Clamp01(Mathf.PerlinNoise(ang * 4f + 4f, r * 9f) * 1.6f - 0.4f);
                            c = Color.Lerp(c, new Color(0.50f, 0.68f, 0.92f), halo * 0.4f * wear);
                        }
                    }
                    px[y * n + x] = c;
                }
            var t = New(n, n, "PoolChalkTop" + step);
            t.SetPixels32(px);
            t.Apply(true, true);
            Tops[step] = t;
            return t;
        }

        // ================================================================ wrapper

        /// <summary>Wrapper strip: side k covers u in [k/4, (k+1)/4], v = height over the cube size.</summary>
        public static Texture2D Wrapper(float wear01)
        {
            int step = Step(wear01);
            if (Wrappers.TryGetValue(step, out var cached) && cached != null) return cached;
            float wear = step / 10f;
            int w = SidePx * 4, hgt = SidePx;
            float pxs = 1f / SidePx;
            var px = new Color32[w * hgt];
            for (int y = 0; y < hgt; y++)
                for (int x = 0; x < w; x++)
                {
                    int side = x / SidePx;
                    float s = (x % SidePx + 0.5f) / SidePx, v = (y + 0.5f) / hgt;
                    float fibre = Mathf.PerlinNoise(x * 0.9f, y * 0.12f) * 0.04f + Hash(x, y) * 0.03f;
                    Color c = Paper * (0.97f + fibre);
                    // Navy band with gold rules.
                    c = Color.Lerp(c, Navy, Cover(Mathf.Abs(v - 0.5f) - 0.2f, pxs));
                    c = Color.Lerp(c, Gold, Cover(Mathf.Abs(Mathf.Abs(v - 0.5f) - 0.235f) - 0.011f, pxs));
                    var sp = new Vector2(s - 0.5f, v);
                    if ((side & 1) == 0)
                    {
                        c = Color.Lerp(c, Paper, Cover(Text("CHALK", sp - new Vector2(0f, 0.5f), 0.2f, 0.2f), pxs));
                        c = Color.Lerp(c, Navy, Cover(Text("PRO", sp - new Vector2(0f, 0.85f), 0.105f, 0.2f), pxs));
                    }
                    else
                    {
                        Vector2 e = sp - new Vector2(0f, 0.5f);
                        c = Color.Lerp(c, Gold, Cover(e.magnitude - 0.152f, pxs));
                        c = Color.Lerp(c, Black, Cover(e.magnitude - 0.135f, pxs));
                        c = Color.Lerp(c, Paper, Cover(e.magnitude - 0.068f, pxs));
                        c = Color.Lerp(c, Black, Cover(Text("8", e, 0.085f, 0.2f), pxs));
                        foreach (float sx in new[] { -0.33f, 0.33f })
                            c = Color.Lerp(c, Gold, Cover(Star(e - new Vector2(sx, 0f), 0.06f, 0.45f), pxs));
                        for (int k = -1; k <= 1; k++)
                            c = Color.Lerp(c, Gold, Cover(Star(sp - new Vector2(k * 0.16f, 0.85f), 0.045f, 0.45f), pxs));
                    }
                    // A row of red diamonds under the band.
                    for (int k = -2; k <= 2; k++)
                    {
                        Vector2 d = sp - new Vector2(k * 0.17f, 0.15f);
                        float rh = (Mathf.Abs(d.x) / 0.045f + Mathf.Abs(d.y) / 0.06f - 1f) * 0.036f;
                        c = Color.Lerp(c, Red, Cover(rh, pxs));
                    }
                    // Chalk dust rubbed into the paper, mostly near the top edge.
                    float smear = Mathf.Clamp01(Mathf.PerlinNoise(x * 0.06f, y * 0.11f) * 1.8f - 0.55f);
                    float speck = Hash(x * 3 + 11, y * 5 + 2) > 0.93f ? 0.5f : 0f;
                    float dust = wear * (Mathf.Pow(Mathf.Clamp01((v - 0.62f) / 0.38f), 1.6f) * (0.25f + 0.6f * smear) + 0.05f * smear + speck * 0.25f * v);
                    c = Color.Lerp(c, ChalkBlue * 1.15f, Mathf.Clamp01(dust));
                    px[y * w + x] = c;
                }
            var t = New(w, hgt, "PoolChalkWrapper" + step);
            t.SetPixels32(px);
            t.Apply(true, true);
            Wrappers[step] = t;
            return t;
        }

        // ================================================================ shapes

        /// <summary>Coverage 0..1 of a signed distance <paramref name="d"/> (negative inside) for a pixel of size <paramref name="px"/>.</summary>
        static float Cover(float d, float px) => Mathf.Clamp01(0.5f - d / px);

        /// <summary>Signed distance to <paramref name="text"/> of glyph height <paramref name="h"/> centred on the origin.</summary>
        static float Text(string text, Vector2 p, float h, float stroke)
        {
            float half = PoolBallGlyphs.Width(text) * h * 0.5f + h;
            if (Mathf.Abs(p.x) > half || Mathf.Abs(p.y) > h * 1.2f) return 1f;
            return PoolBallGlyphs.Distance(text, p / h, stroke) * h;
        }

        /// <summary>Exact signed distance to a five-point star of outer radius <paramref name="r"/> (inner / outer ratio <paramref name="rf"/>).</summary>
        static float Star(Vector2 p, float r, float rf)
        {
            var k1 = new Vector2(0.809016994f, -0.587785252f);
            var k2 = new Vector2(-k1.x, k1.y);
            p.x = Mathf.Abs(p.x);
            p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
            p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
            p.x = Mathf.Abs(p.x);
            p.y -= r;
            Vector2 ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0f, 1f);
            float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
            return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
        }
    }
}
