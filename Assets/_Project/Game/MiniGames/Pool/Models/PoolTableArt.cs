using System.Collections.Generic;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Procedural textures of the pool table (made once per session): the bed cloth (colour baked in, nap streaks along
    /// the length, soft tone changes, head string, head spot, foot spot sticker and a faint pencilled rack outline), a
    /// tileable nap for the cushions, long wood grain along U or along V, mother-of-pearl and the engraved maker's plate.
    /// </summary>
    internal static class PoolTableArt
    {
        public const int BedW = 2048, BedH = 1024;
        static readonly Dictionary<int, Texture2D> Beds = new(), Naps = new();
        static Texture2D _along, _up, _pearl, _plate;

        /// <summary>Cloth colour (sRGB): 0 green, 1 tournament blue, 2 burgundy, 3 (garage) racing red.</summary>
        public static Color ClothColor(int style) => style switch
        {
            3 => new Color(0.5f, 0.075f, 0.055f),
            1 => new Color(0.11f, 0.30f, 0.60f),
            2 => new Color(0.34f, 0.055f, 0.085f),
            _ => new Color(0.08f, 0.42f, 0.22f),
        };

        static Texture2D New(int w, int h, string name, TextureWrapMode wrap = TextureWrapMode.Repeat) =>
            new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        // ================================================================ cloth

        /// <summary>Whole bed (BedX x BedZ, u = +x, v = +z), colour and markings baked in.</summary>
        public static Texture2D Bed(int style)
        {
            if (Beds.TryGetValue(style, out var t) && t != null) return t;
            t = New(BedW, BedH, "PoolBed" + style, TextureWrapMode.Clamp);
            var px = new Color32[BedW * BedH];
            Color c = ClothColor(style);
            float sx = 2f * PoolTableGeom.BedX / BedW, sz = 2f * PoolTableGeom.BedZ / BedH;
            for (int y = 0; y < BedH; y++)
            {
                float wz = -PoolTableGeom.BedZ + (y + 0.5f) * sz;
                float row = (Hash(0, y, 3) - 0.5f) * 0.03f;
                for (int x = 0; x < BedW; x++)
                {
                    float wx = -PoolTableGeom.BedX + (x + 0.5f) * sx;
                    float k = 1f + row + (Hash(x, y, 7) - 0.5f) * 0.08f
                        + (Mathf.PerlinNoise(wx * 9f + 3.1f, wz * 240f + 7.7f) - 0.5f) * 0.06f
                        + (Mathf.PerlinNoise(wx * 1.6f + 11.3f, wz * 1.6f + 5.2f) - 0.5f) * 0.07f;
                    Color col = c * k;
                    float m = Marks(wx, wz, sx);
                    if (m > 0f) col = Color.Lerp(col, new Color(0.94f, 0.94f, 0.9f), m);
                    col.a = 1f;
                    px[y * BedW + x] = col;
                }
            }
            t.SetPixels32(px);
            t.Apply(true);
            Beds[style] = t;
            return t;
        }

        /// <summary>Opacity of the pale markings at (x, z); <paramref name="px"/> = pixel size for anti-aliasing.</summary>
        static float Marks(float x, float z, float px)
        {
            float a = 0f;
            Vector2 p = new Vector2(x, z);
            Vector2 foot = new Vector2(PoolDims.FootSpot.x, PoolDims.FootSpot.z), head = new Vector2(PoolDims.HeadSpot.x, PoolDims.HeadSpot.z);
            // Foot spot: a paper sticker a little smaller than a ball, with a printed ring.
            float df = Vector2.Distance(p, foot);
            if (df < 0.02f)
            {
                a = 0.93f * Edge(0.0115f - df, px);
                a *= 1f - 0.35f * Edge(0.0006f - Mathf.Abs(df - 0.0085f), px);
            }
            // Head spot (pencil dot) and the head string across the kitchen line.
            a = Mathf.Max(a, 0.55f * Edge(0.0025f - Vector2.Distance(p, head), px));
            if (Mathf.Abs(z) < PoolTableGeom.HalfW) a = Mathf.Max(a, 0.2f * Edge(0.0006f - Mathf.Abs(x - head.x), px));
            // Faint pencil outline of the rack round the foot spot (15 balls, apex on the spot).
            if (x > foot.x - 0.07f && x < foot.x + 0.3f && Mathf.Abs(z) < 0.17f)
            {
                float d = PoolDims.BallD, r = PoolDims.BallR, bx = foot.x + 4f * d * 0.8660254f + r * 1.7320508f;
                Vector2 apex = new Vector2(foot.x - 2f * r, 0f), b1 = new Vector2(bx, 2f * d + r), b2 = new Vector2(bx, -(2f * d + r));
                float dl = Mathf.Min(SegDist(p, apex, b1), Mathf.Min(SegDist(p, apex, b2), SegDist(p, b1, b2)));
                a = Mathf.Max(a, 0.1f * Edge(0.0005f - dl, px));
            }
            return a;
        }

        static float Edge(float inside, float px) => Mathf.Clamp01(inside / px + 0.5f);

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Tileable cloth nap for the cushions and the pocket edges (u along the nap), 1 tile = 25 cm.</summary>
        public static Texture2D Nap(int style)
        {
            if (Naps.TryGetValue(style, out var t) && t != null) return t;
            const int n = 256;
            t = New(n, n, "PoolNap" + style);
            var px = new Color32[n * n];
            Color c = ClothColor(style);
            for (int y = 0; y < n; y++)
            {
                float row = (Hash(0, y, 5) - 0.5f) * 0.03f, ph = Hash(1, y, 9) * 6.2832f;
                for (int x = 0; x < n; x++)
                {
                    float k = 1f + row + (Hash(x, y, 13) - 0.5f) * 0.08f + Mathf.Sin(x / (float)n * 6.2832f * 2f + ph) * 0.015f;
                    Color col = c * k;
                    col.a = 1f;
                    px[y * n + x] = col;
                }
            }
            t.SetPixels32(px);
            t.Apply(true);
            Naps[style] = t;
            return t;
        }

        // ================================================================ wood, pearl, plate

        /// <summary>Wood grain running along U (rails, apron), 1024 x 512 = 1.1 x 0.55 m, tiles both ways.</summary>
        public static Texture2D GrainAlong => _along != null ? _along : (_along = Grain(1024, 512, false, 31));
        /// <summary>Wood grain running along V (legs).</summary>
        public static Texture2D GrainUp => _up != null ? _up : (_up = Grain(512, 1024, true, 47));

        static Texture2D Grain(int w, int h, bool vertical, int seed)
        {
            var t = New(w, h, vertical ? "PoolGrainUp" : "PoolGrainAlong");
            var px = new Color32[w * h];
            int along = vertical ? h : w, across = vertical ? w : h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int ia = vertical ? y : x, ib = vertical ? x : y;
                    float a = ia / (float)along, b = ib / (float)across;
                    // Blend the last fifth of each axis with the wrapped-round start so the texture tiles.
                    float wa = Mathf.SmoothStep(0f, 1f, (a - 0.8f) / 0.2f), wb = Mathf.SmoothStep(0f, 1f, (b - 0.8f) / 0.2f);
                    float g = Wood(a, b, ib, seed);
                    if (wa > 0f) g = Mathf.Lerp(g, Wood(a - 1f, b, ib, seed), wa);
                    if (wb > 0f) g = Mathf.Lerp(g, Mathf.Lerp(Wood(a, b - 1f, ib, seed), Wood(a - 1f, b - 1f, ib, seed), wa), wb);
                    // Late wood and pores are a little warmer / more saturated than the light early wood.
                    float v = Mathf.Clamp01(g);
                    px[y * w + x] = new Color32((byte)(v * 255f), (byte)(v * v * 0.25f * 255f + v * 0.73f * 255f), (byte)(v * v * 0.4f * 255f + v * 0.52f * 255f), 255);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>
        /// Flat-sawn board value at (a along the grain, b across), 0..1: growth rings bent by noise with dark late wood,
        /// fine pore streaks (one random lane per texel row across the grain), a soft large-scale figure.
        /// </summary>
        static float Wood(float a, float b, int row, int seed)
        {
            float warp = Mathf.PerlinNoise(a * 2.1f + seed, b * 2.7f + seed * 0.37f) * 1.7f + Mathf.PerlinNoise(a * 6.5f + seed, b * 9f) * 0.28f;
            float r = Frac(b * 26f + warp);
            float late = Mathf.Pow(Mathf.SmoothStep(0f, 1f, Mathf.Abs(r - 0.5f) * 2f), 5f);
            float pore = Mathf.Clamp01((Mathf.PerlinNoise(a * 46f + seed, row * 0.71f) - 0.58f) * 3.5f);
            float fleck = Hash(row, Mathf.FloorToInt(a * 700f), seed) > 0.985f ? 0.08f : 0f;
            float figure = (Mathf.PerlinNoise(a * 1.3f + seed * 0.5f, b * 1.7f) - 0.5f) * 0.2f;
            return 0.9f - late * 0.28f - pore * 0.15f - fleck + figure;
        }

        /// <summary>Mother-of-pearl: milky white with pink and blue-green sheen.</summary>
        public static Texture2D Pearl
        {
            get
            {
                if (_pearl != null) return _pearl;
                const int n = 64;
                _pearl = New(n, n, "PoolPearl");
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float p = Mathf.PerlinNoise(x * 0.09f + 3f, y * 0.13f), q = Mathf.PerlinNoise(x * 0.21f + 7f, y * 0.17f + 2f);
                        Color c = Color.Lerp(new Color(0.95f, 0.94f, 0.9f), new Color(0.99f, 0.86f, 0.9f), p * 0.8f);
                        c = Color.Lerp(c, new Color(0.8f, 0.93f, 0.95f), Mathf.Clamp01(q * 1.4f - 0.5f));
                        c.a = 1f;
                        px[y * n + x] = c;
                    }
                _pearl.SetPixels(px);
                _pearl.Apply(true);
                return _pearl;
            }
        }

        public const int PlateW = 512, PlateH = 176;

        /// <summary>Brushed brass maker's plate (fictional maker) with an engraved double border and two lines of text.</summary>
        public static Texture2D Plate
        {
            get
            {
                if (_plate != null) return _plate;
                _plate = New(PlateW, PlateH, "PoolMakerPlate", TextureWrapMode.Clamp);
                var px = new Color[PlateW * PlateH];
                Color brass = new Color(0.9f, 0.74f, 0.44f), ink = new Color(0.17f, 0.11f, 0.05f);
                for (int y = 0; y < PlateH; y++)
                {
                    float row = (Hash(0, y, 21) - 0.5f) * 0.12f;
                    for (int x = 0; x < PlateW; x++)
                    {
                        Color c = brass * (0.92f + row + (Hash(x, y, 4) - 0.5f) * 0.05f);
                        c.a = 1f;
                        px[y * PlateW + x] = c;
                    }
                }
                Border(px, 8, 3, ink);
                Border(px, 15, 1, ink);
                Text(px, "H.S. BILLIARD CO.", 98, 4, ink);
                Text(px, "SEVEN FOOT - EST. 2026", 40, 3, ink);
                _plate.SetPixels(px);
                _plate.Apply(true);
                return _plate;
            }
        }

        static void Border(Color[] px, int inset, int width, Color ink)
        {
            for (int y = 0; y < PlateH; y++)
                for (int x = 0; x < PlateW; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, PlateW - 1 - x), Mathf.Min(y, PlateH - 1 - y));
                    if (d >= inset && d < inset + width) px[y * PlateW + x] = Color.Lerp(px[y * PlateW + x], ink, 0.85f);
                }
        }

        /// <summary>Centred text, bottom of the letters at <paramref name="y"/>, <paramref name="dot"/> px per font pixel.</summary>
        static void Text(Color[] px, string s, int y, int dot, Color ink)
        {
            int x0 = (PlateW - CanFont.Width(s, dot)) / 2;
            CanFont.Draw(s, x0, y, dot, dot, (x, yy) =>
            {
                if (x >= 0 && yy >= 0 && x < PlateW && yy < PlateH) px[yy * PlateW + x] = Color.Lerp(px[yy * PlateW + x], ink, 0.9f);
            });
        }
    }
}
