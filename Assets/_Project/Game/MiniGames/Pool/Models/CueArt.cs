using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Procedural textures of the cues (made once per key, kept for the session). Everything is drawn on the cue's own
    /// lathe UVs: u = around (0..1, must tile), v = along the cue (metres or 0..1 over a panel), so wood grain always
    /// runs along the length. This file: periodic noise, wood species, the panel painter (2x2 supersampled inlays),
    /// the tip, dash rings, mother of pearl and the bumper logo.
    /// </summary>
    public static partial class CueArt
    {
        static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Get(string key, Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = "Cue_" + key;
            Cache[key] = t;
            return t;
        }

        /// <summary>Texture filled row by row in parallel (pure functions only, no Unity API inside <paramref name="px"/>).</summary>
        public static Texture2D Fill(int w, int h, bool linear, Func<int, int, Color> px)
        {
            var data = new Color32[w * h];
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++) data[y * w + x] = px(x, y);
            });
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            t.SetPixels32(data);
            t.Apply(true, true);
            return t;
        }

        // ================================================================ noise (periodic in x)

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        static int Mod(int a, int p) => ((a % p) + p) % p;

        /// <summary>Smooth value noise 0..1, periodic in x with <paramref name="periodX"/> cells (and in y when <paramref name="periodY"/> &gt; 0).</summary>
        public static float Noise(float x, float y, int periodX, int seed, int periodY = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, periodX), xb = Mod(x0 + 1, periodX);
            if (periodY > 0) { int y1 = Mod(y0 + 1, periodY); y0 = Mod(y0, periodY); return Lerp4(xa, xb, y0, y1, fx, fy, seed); }
            return Lerp4(xa, xb, y0, y0 + 1, fx, fy, seed);
        }

        static float Lerp4(int xa, int xb, int y0, int y1, float fx, float fy, int seed)
        {
            float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed), c = Hash(xa, y1, seed), d = Hash(xb, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal noise 0..1 at (u * period, y); stays periodic over u = 0..1.</summary>
        public static float Fbm(float u, float y, int period, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(u * period, y, period, seed + o * 31) * amp;
                norm += amp;
                amp *= 0.5f;
                period *= 2;
                y *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Random 0..1 for an integer cell (u cells wrap with <paramref name="period"/>).</summary>
        public static float Rand(int x, int y, int period, int seed) => Hash(Mod(x, period), y, seed);

        static float Frac(float x) => x - Mathf.Floor(x);

        // ================================================================ wood

        /// <summary>Look of one wood species. Lines = growth lines around the cue (integer, so u tiles).</summary>
        public sealed class Wood
        {
            public Color Light, Dark;
            public int Lines = 24, Seed;
            public float Warp = 1f, LineAmt = 0.5f, LineSharp = 4f, Pores = 0.12f, Curl, CurlAmt = 0.1f, Eyes, Streak = 0.12f;
            public Color StreakTint = Color.white;
        }

        /// <summary>Colour of <paramref name="w"/> at u (around) and z (metres along the cue).</summary>
        public static Color WoodAt(Wood w, float u, float z)
        {
            float warp = (Fbm(u, z * 5f, 3, w.Seed, 3) - 0.5f) * w.Warp * 3f + (Noise(u * 16f, z * 40f, 16, w.Seed + 7) - 0.5f) * 0.35f * w.Warp;
            float gp = u * w.Lines + warp, r = Frac(gp);
            // Every growth line gets its own strength (and the next one half of it near the edge), so the grain is not a barcode.
            int li = Mathf.FloorToInt(gp + 0.5f);
            float strength = 0.3f + 0.7f * Rand(li, 0, w.Lines, w.Seed + 51);
            float line = Mathf.Pow(Mathf.Abs(r - 0.5f) * 2f, w.LineSharp) * strength;
            int pp = w.Lines * 10;
            float pore = (Noise(u * pp, z * 70f, pp, w.Seed + 3) - 0.5f) * w.Pores * 2f;
            pore += (Noise(u * pp * 2, z * 260f, pp * 2, w.Seed + 5) - 0.5f) * w.Pores;
            Color c = Color.Lerp(w.Light, w.Dark, Mathf.Clamp01(line * w.LineAmt + pore));
            if (w.Curl > 0f)
            {
                // Flame / curl: bright and dark bands across the grain that wobble around the cue.
                float ph = z * w.Curl + (Fbm(u, z * 6f, 2, w.Seed + 11, 2) - 0.5f) * 3f + Mathf.Sin(u * Mathf.PI * 2f) * 0.4f;
                c *= 1f + Mathf.Sin(ph * Mathf.PI * 2f) * w.CurlAmt;
            }
            if (w.Eyes > 0f) c = Color.Lerp(c, w.Dark * 0.85f, BirdsEye(w, u, z));
            float st = Fbm(u, z * 2.5f, 2, w.Seed + 17, 3);
            c = Color.Lerp(c, c * w.StreakTint, Mathf.Clamp01((st - 0.45f) * 3f)) * (1f - w.Streak + w.Streak * 2f * st);
            c.a = 1f;
            return c;
        }

        /// <summary>Birdseye figure: small dark rings with a pale centre, scattered by cell.</summary>
        static float BirdsEye(Wood w, float u, float z)
        {
            const int cellsU = 22;
            float cu = u * cellsU, cz = z * 230f;
            int iu = Mathf.FloorToInt(cu), iz = Mathf.FloorToInt(cz);
            float best = 0f;
            for (int du = -1; du <= 1; du++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    int x = iu + du, y = iz + dz;
                    if (Rand(x, y, cellsU, w.Seed + 41) > w.Eyes) continue;
                    float ox = x + 0.2f + Rand(x, y, cellsU, w.Seed + 42) * 0.6f, oy = y + 0.2f + Rand(x, y, cellsU, w.Seed + 43) * 0.6f;
                    float dx = (cu - ox) * 1.1f, dy = (cz - oy) * 1.25f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    // A small dark eye with a soft halo, slightly stretched across the grain.
                    float size = 0.16f + Rand(x, y, cellsU, w.Seed + 44) * 0.12f;
                    float eye = Mathf.Pow(Mathf.Clamp01(1f - d / size), 1.6f) * 0.6f + Mathf.Clamp01(1f - d / (size * 2.4f)) * 0.16f;
                    best = Mathf.Max(best, eye);
                }
            return best;
        }

        /// <summary>Wood texture covering z0..z1 of the cue once (v = 0..1 over that span, tiles seamlessly in u only).</summary>
        public static Texture2D WoodTex(string key, Wood w, int width, int height, float z0, float z1) => Get(key, () =>
            Fill(width, height, false, (x, y) => WoodAt(w, (x + 0.5f) / width, z0 + (y + 0.5f) / height * (z1 - z0))));

        // ================================================================ panels

        /// <summary>Layer id at a point of a panel (u around, z metres along the cue); -1 = background.</summary>
        public delegate int LayerFn(float u, float z);
        public delegate Color ColorFn(int layer, float u, float z);

        /// <summary>
        /// Texture covering z0..z1 of the cue (v = 0..1 over that span). <paramref name="layer"/> is sampled on a lattice
        /// through the pixel corners (shared by neighbouring pixels) with <paramref name="su"/> extra steps across each
        /// pixel in u (long, nearly axial edges such as splice points need them); every pixel averages its lattice
        /// samples, so inlay edges come out smooth. Colours from <paramref name="color"/> at the pixel centre.
        /// </summary>
        public static Texture2D Panel(string key, int w, int h, float z0, float z1, LayerFn layer, ColorFn color, int su = 1) => Get(key, () =>
        {
            int cw = w * su + 1;
            var lay = new int[cw * (h + 1)];
            Parallel.For(0, h + 1, y =>
            {
                float z = z0 + y / (float)h * (z1 - z0);
                for (int x = 0; x < cw; x++) lay[y * cw + x] = layer(x / (float)(w * su), z);
            });
            return Fill(w, h, false, (x, y) =>
            {
                float u = (x + 0.5f) / w, z = z0 + (y + 0.5f) / h * (z1 - z0);
                int first = lay[y * cw + x * su];
                Color c0 = color(first, u, z);
                int l1 = int.MinValue, l2 = int.MinValue;
                Color c1 = default, c2 = default, sum = Color.clear;
                for (int row = 0; row < 2; row++)
                    for (int k = 0; k <= su; k++)
                    {
                        int l = lay[(y + row) * cw + x * su + k];
                        if (l == first) { sum += c0; continue; }
                        if (l1 == int.MinValue || l == l1) { if (l1 != l) { l1 = l; c1 = color(l, u, z); } sum += c1; continue; }
                        if (l2 != l) { l2 = l; c2 = color(l, u, z); }
                        sum += c2;
                    }
                sum /= 2f * (su + 1);
                sum.a = 1f;
                return sum;
            });
        });

        // ================================================================ small parts

        /// <summary>Mother of pearl: milky white with pink / green / blue flashes in wavy bands.</summary>
        public static Color PearlAt(float u, float z)
        {
            float a = Fbm(u, z * 90f, 12, 5, 3), b = Fbm(u, z * 160f, 20, 9, 2);
            float band = Mathf.Sin((a * 4f + b * 1.5f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            Color c = Color.Lerp(new Color(0.93f, 0.90f, 0.86f), new Color(0.98f, 0.97f, 0.95f), b);
            c = Color.Lerp(c, band > 0.5f ? new Color(0.92f, 0.82f, 0.86f) : new Color(0.80f, 0.90f, 0.88f), Mathf.Abs(band - 0.5f) * 0.8f);
            c = Color.Lerp(c, new Color(0.78f, 0.82f, 0.95f), Mathf.Clamp01((a - 0.6f) * 3f) * 0.5f);
            c.a = 1f;
            return c;
        }

        /// <summary>Tip: laminated leather layers on the side (v 0..0.62), blue chalk on the dome (v 0.62..1).</summary>
        public static Texture2D Tip(bool fresh) => Get("tip" + fresh, () => Fill(64, 256, false, (x, y) =>
        {
            float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 256f;
            Color chalk = new Color(0.16f, 0.43f, 0.80f) * (0.82f + 0.3f * Fbm(u, v * 18f, 8, 3, 3));
            float n = Fbm(u, v * 40f, 16, 7, 2);
            if (v < 0.62f)
            {
                // 8 layers: tan / brown leather with thin dark glue lines between them.
                float lv = v / 0.62f * 8f, f = Frac(lv);
                int layer = Mathf.FloorToInt(lv);
                Color leather = (layer & 1) == 0 ? new Color(0.40f, 0.29f, 0.20f) : new Color(0.33f, 0.23f, 0.15f);
                leather *= 0.85f + 0.3f * n;
                if (f < 0.13f) leather = new Color(0.12f, 0.08f, 0.05f);
                // Chalk rubbed over the upper edge of the side.
                float rub = Mathf.Clamp01((v - 0.42f) / 0.2f) * (0.4f + 0.6f * Fbm(u, v * 12f, 6, 13, 2));
                return Color.Lerp(leather, chalk, rub * (fresh ? 0.9f : 0.6f));
            }
            // Dome: chalk with a few worn patches showing the leather.
            float worn = fresh ? 0f : Mathf.Clamp01((Fbm(u, v * 9f, 5, 21, 3) - 0.62f) * 4f);
            return Color.Lerp(chalk, new Color(0.38f, 0.30f, 0.26f), worn * 0.6f);
        }));

        /// <summary>Dash ring: <paramref name="count"/> blocks of a / b around the cue (v = 0..1 over the ring).</summary>
        public static Texture2D Dashes(string key, int count, Color a, Color b, Color edge) => Get("dash_" + key, () => Fill(256, 16, false, (x, y) =>
        {
            float u = (x + 0.5f) / 256f * count, f = Frac(u);
            Color c = Mathf.FloorToInt(u) % 2 == 0 ? a : b;
            if (f < 0.06f || f > 0.94f || y == 0 || y == 15) c = Color.Lerp(c, edge, 0.6f);
            return c;
        }));

        public static Texture2D Pearl => Get("pearl", () => Fill(256, 256, false, (x, y) => PearlAt((x + 0.5f) / 256f, (y + 0.5f) / 256f * 0.05f)));
    }
}
