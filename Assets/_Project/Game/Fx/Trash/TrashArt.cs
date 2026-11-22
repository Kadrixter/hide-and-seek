using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Procedural textures of the trash bin (made once, shared): perforated anthracite powder-coated steel (round punched
    /// holes in a staggered grid, plain bands top and bottom with pressed grooves, brushed streaks, metal flakes,
    /// scratches down to bare steel, a coffee drip, dust and scuffs at the foot), black liner bag with stretch wrinkles.
    /// Albedo alpha = smoothness mask. Papers are in TrashArt.Paper, background computing and upload in TrashArt.Pixels.
    /// </summary>
    internal static partial class TrashArt
    {
        const int MetalW = 1024, MetalH = 1024;
        static Texture2D _blob;

        /// <summary>Soft round contact-shadow blob (alpha), clamped.</summary>
        public static Texture2D Blob
        {
            get
            {
                if (_blob != null) return _blob;
                const int n = 64;
                var px = new Color32[n * n];
                Rows(n, y =>
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude / (n * 0.5f);
                        float a = Mathf.Clamp01(1f - d);
                        px[y * n + x] = new Color32(0, 0, 0, (byte)(a * a * (3f - 2f * a) * 255f));
                    }
                });
                _blob = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "TrashBlob", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                _blob.SetPixels32(px);
                _blob.Apply(true, true);
                return _blob;
            }
        }

        // ------------------------------------------------------------------ noise

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>Value noise 0..1, periodic in x with period <paramref name="px"/> cells (0 = not periodic).</summary>
        static float Noise(float x, float y, int s, int px = 0)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ix, x1 = ix + 1;
            if (px > 0) { x0 = ((x0 % px) + px) % px; x1 = ((x1 % px) + px) % px; }
            float a = Hash(x0, iy, s), b = Hash(x1, iy, s), c = Hash(x0, iy + 1, s), d = Hash(x1, iy + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int s, int px = 0)
        {
            float v = 0f, amp = 0.5f, tot = 0f;
            for (int o = 0; o < 4; o++)
            {
                v += Noise(x, y, s + o * 17, px) * amp;
                tot += amp; x *= 2f; y *= 2f; px *= 2; amp *= 0.5f;
            }
            return v / tot;
        }

        /// <summary>Runs one pixel row per call on all cores (pure maths only, no Unity API inside).</summary>
        static void Rows(int h, System.Action<int> row) => System.Threading.Tasks.Parallel.For(0, h, row);

        static Color32 C32(Color c, float a) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        // ------------------------------------------------------------------ perforated metal

        static void MakeMetal()
        {
            const int w = MetalW, h = MetalH;
            var col = new Color[w * h];
            var gloss = new float[w * h];
            var hgt = new float[w * h];
            Color paint = new Color(0.075f, 0.08f, 0.088f), steel = new Color(0.5f, 0.5f, 0.52f), dust = new Color(0.27f, 0.25f, 0.22f);
            const float bandLo = 0.034f, bandHi = 0.322f, holeR = 0.0029f, pitchY = 0.0088f;
            const int perRow = 48;
            float H = TrashBinMesh.H;
            Rows(h, y =>
            {
                float yy = (y + 0.5f) / h * H;                    // metres up the wall
                float circ = Mathf.PI * TrashBinMesh.R(yy);        // metres across the texture at this height
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = (x + 0.5f) / w;
                    // Paint: fine orange-peel noise, horizontal brushed streaks, sparse metal flakes.
                    float n = Fbm(u * 64f, yy * 300f, 3, 64);
                    float brush = Noise(u * 6f, yy * 2400f, 7, 6) * 0.6f + Noise(u * 23f, yy * 5200f, 9, 23) * 0.4f;
                    float flake = Hash(x, y, 21) > 0.985f ? 0.05f : 0f;
                    Color c = paint * (0.9f + 0.16f * n + 0.1f * brush) + new Color(flake, flake, flake * 1.05f);
                    float g = 0.82f + 0.14f * brush - 0.1f * n;
                    float ht = 0.15f * n + 0.06f * brush;

                    // Pressed grooves at the band edges.
                    float gr = Mathf.Min(Mathf.Abs(yy - (bandLo - 0.004f)), Mathf.Abs(yy - (bandHi + 0.006f)));
                    if (gr < 0.0016f) { float k = 1f - gr / 0.0016f; ht -= 0.9f * k * k; c *= 1f - 0.25f * k; }

                    // Staggered round holes (round in metres: the texture is squeezed less at the narrow bottom).
                    if (yy > bandLo + 0.002f && yy < bandHi)
                    {
                        float fr = (yy - bandLo - 0.0055f) / pitchY;
                        int row = Mathf.RoundToInt(fr);
                        float best = 1f;
                        for (int dr = -1; dr <= 1; dr++)
                        {
                            int rr = row + dr;
                            if (rr < 0) continue;
                            float cy = bandLo + 0.0055f + rr * pitchY;
                            if (cy > bandHi - 0.004f) continue;
                            float off = (rr & 1) * 0.5f;
                            float col01 = u * perRow - off;
                            float cx = (Mathf.Round(col01) + off) / perRow;
                            float dx = (u - cx) * circ, dy = yy - cy;
                            best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
                        }
                        if (best < holeR + 0.0012f)
                        {
                            float edge = Mathf.InverseLerp(holeR + 0.00025f, holeR - 0.00025f, best); // 1 inside the hole
                            float lip = Mathf.Clamp01(1f - Mathf.Abs(best - holeR - 0.0005f) / 0.0007f); // punched rim
                            ht += 0.35f * lip - 2.2f * edge;
                            c = Color.Lerp(c * (1f + 0.5f * lip), new Color(0.012f, 0.012f, 0.014f), edge);
                            g = Mathf.Lerp(g + 0.1f * lip, 0.05f, edge);
                        }
                    }
                    col[i] = c; gloss[i] = g; hgt[i] = ht;
                }
            });
            Scratches(col, gloss, hgt, w, h, steel);
            // Coffee drip from the top band, dust and scuffs at the foot, greasy hand marks under the rim.
            Rows(h, y =>
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float u = (x + 0.5f) / w;
                    float foot = Mathf.Clamp01(1f - v / 0.14f);
                    float dn = Fbm(u * 40f, v * 30f, 31, 40);
                    float dk = Mathf.Clamp01(foot * foot * (0.5f + dn) * 0.9f);
                    col[i] = Color.Lerp(col[i], dust, dk * 0.55f);
                    gloss[i] *= 1f - 0.8f * dk;
                    float grease = Mathf.Clamp01((v - 0.86f) / 0.1f) * Mathf.Clamp01(Fbm(u * 12f, v * 20f, 41, 12) * 2.2f - 1.15f);
                    gloss[i] *= 1f - 0.5f * grease;
                    float du = Mathf.Abs(u - 0.31f) * w, dripLen = 0.87f - 0.2f * Noise(u * 300f, 0f, 51);
                    float drip = du < 2.4f && v < 0.9f && v > dripLen ? (1f - du / 2.4f) * Mathf.InverseLerp(dripLen, dripLen + 0.04f, v) : 0f;
                    if (drip > 0f) { col[i] = Color.Lerp(col[i], new Color(0.07f, 0.045f, 0.025f), drip * 0.8f); gloss[i] = Mathf.Lerp(gloss[i], 1f, drip); }
                }
            });
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = C32(col[i], gloss[i]);
            _metalP = Albedo(px, w, h, "TrashBinMetal");
            _metalNP = NormalFrom(hgt, w, h, 2.2f, "TrashBinMetal_N", false);
        }

        /// <summary>Thin scratches: some through the paint to bright bare steel, many just scuffing the coat.</summary>
        static void Scratches(Color[] col, float[] gloss, float[] hgt, int w, int h, Color steel)
        {
            var rnd = new System.Random(77);
            for (int s = 0; s < 90; s++)
            {
                bool deep = s % 3 == 0;
                float u0 = (float)rnd.NextDouble();
                // Most marks near the top band (hands, things thrown in) and the foot (kicks, the vacuum cleaner).
                float r = (float)rnd.NextDouble();
                float v0 = r < 0.45f ? 0.84f + 0.13f * (float)rnd.NextDouble() : r < 0.8f ? 0.02f + 0.16f * (float)rnd.NextDouble() : (float)rnd.NextDouble();
                float ang = (float)(rnd.NextDouble() * 2.0 - 1.0) * (v0 < 0.2f ? 0.35f : 1.4f);
                float len = (deep ? 12f : 25f) + (float)rnd.NextDouble() * (deep ? 40f : 90f);
                float dx = Mathf.Cos(ang) * len, dy = Mathf.Sin(ang) * len;
                float x0 = u0 * w, y0 = v0 * h;
                int steps = Mathf.CeilToInt(len);
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    float fade = Mathf.Sin(t * Mathf.PI);
                    int px = ((Mathf.RoundToInt(x0 + dx * t) % w) + w) % w, py = Mathf.RoundToInt(y0 + dy * t + Mathf.Sin(t * 9f + s) * 1.5f);
                    if (py < 0 || py >= h) continue;
                    int i = py * w + px;
                    if (deep) { col[i] = Color.Lerp(col[i], steel, 0.85f * fade); gloss[i] = 0.75f; hgt[i] -= 0.5f * fade; }
                    else { col[i] = Color.Lerp(col[i], col[i] * 1.9f + new Color(0.02f, 0.02f, 0.02f), 0.5f * fade); gloss[i] *= 0.55f; hgt[i] -= 0.12f * fade; }
                }
            }
        }

        // ------------------------------------------------------------------ liner bag

        static void MakeBag()
        {
            const int n = 256;
            var px = new Color32[n * n];
            var hgt = new float[n * n];
            Rows(n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    // Stretch wrinkles run mostly along the bag (v), plus a few crumples.
                    float wr = Fbm(u * 18f, v * 3f, 61, 18);
                    float cr = Fbm(u * 8f + wr, v * 8f, 67, 8);
                    float ridge = 1f - Mathf.Abs(wr * 2f - 1f);
                    hgt[y * n + x] = ridge * 1.6f + cr * 0.6f;
                    float shade = 0.85f + 0.3f * ridge;
                    px[y * n + x] = C32(new Color(0.03f, 0.031f, 0.034f) * shade, 0.75f + 0.25f * ridge);
                }
            });
            _bagP = Albedo(px, n, n, "TrashBag");
            _bagNP = NormalFrom(hgt, n, n, 1.2f, "TrashBag_N", true);
        }
    }
}
