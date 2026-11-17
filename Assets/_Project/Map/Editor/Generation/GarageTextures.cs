using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Generates every texture of the parking garage procedurally and saves them as PNG assets.
    /// All generators are seeded and therefore deterministic.
    /// </summary>
    internal static class GarageTextures
    {
        public const string Folder = "Assets/_Project/Map/Textures";

        private enum Kind
        {
            Albedo,
            AlbedoAlpha,
            AlbedoAlphaClamp,
            CutoutAlpha,
            CutoutAlphaClamp,
            Normal
        }

        private static readonly List<(string path, Kind kind)> Written = new List<(string, Kind)>();

        public static void GenerateAll()
        {
            Written.Clear();
            Directory.CreateDirectory(FullPath(Folder));

            FloorConcrete();
            WallConcrete();
            TileFloor();
            Hazard();
            Paint();
            Metal();
            PaintedMetal();
            Wood();
            Cardboard();
            WireMesh();
            Shutter();
            Arrow();
            Hatch();
            Puddle();
            Crack();
            OilStain();
            Grate();
            Glyphs();
            Tarp();
            Flood();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var (path, kind) in Written)
                Configure(path, kind);
        }

        public static Texture2D Load(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
            if (t == null) throw new InvalidOperationException($"Texture {name} missing");
            return t;
        }

        // ------------------------------------------------------------------ helpers

        public static string FullPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        private static void Save(string name, int w, int h, Color[] px, Kind kind)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, kind == Kind.Normal);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(FullPath(path), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Written.Add((path, kind));
        }

        private static void Configure(string path, Kind kind)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) throw new InvalidOperationException("No importer for " + path);
            bool alpha = kind != Kind.Albedo && kind != Kind.Normal;
            bool clamp = kind == Kind.AlbedoAlphaClamp || kind == Kind.CutoutAlphaClamp;
            bool cutout = kind == Kind.CutoutAlpha || kind == Kind.CutoutAlphaClamp;
            imp.textureType = kind == Kind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = kind != Kind.Normal;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = alpha;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.mipMapsPreserveCoverage = cutout;
            imp.alphaTestReferenceValue = 0.5f;
            imp.anisoLevel = 8;
            imp.filterMode = FilterMode.Trilinear;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static int Wrap(int a, int n) => ((a % n) + n) % n;

        private static float WrapDist(float a, float b, int n)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, n - d);
        }

        private static float Range(Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        private static Color[] NormalFromHeight(float[] h, int w, int hh, float strength, bool wrap = true)
        {
            var px = new Color[w * hh];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
            {
                int xl = wrap ? Wrap(x - 1, w) : Mathf.Max(0, x - 1);
                int xr = wrap ? Wrap(x + 1, w) : Mathf.Min(w - 1, x + 1);
                int yd = wrap ? Wrap(y - 1, hh) : Mathf.Max(0, y - 1);
                int yu = wrap ? Wrap(y + 1, hh) : Mathf.Min(hh - 1, y + 1);
                float dx = h[y * w + xr] - h[y * w + xl];
                float dy = h[yu * w + x] - h[yd * w + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return px;
        }

        /// <summary>Random-walk crack drawn into tone/height buffers (wrapping).</summary>
        private static void DrawCrack(float[] tone, float[] height, int n, Random rng, float x, float y, float angle,
            int steps, float width, float darken, float depth, int branchDepth)
        {
            for (int i = 0; i < steps; i++)
            {
                angle += Range(rng, -0.35f, 0.35f);
                x += Mathf.Cos(angle) * 1.5f;
                y += Mathf.Sin(angle) * 1.5f;
                int r = Mathf.CeilToInt(width + 1);
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    int px = Wrap(Mathf.RoundToInt(x) + ox, n), py = Wrap(Mathf.RoundToInt(y) + oy, n);
                    int idx = py * n + px;
                    if (d <= width)
                    {
                        tone[idx] = Mathf.Min(tone[idx], tone[idx] * darken);
                        height[idx] -= depth * 0.3f;
                    }
                    else if (d <= width + 2.5f)
                    {
                        tone[idx] *= 0.985f;
                    }
                }
                if (branchDepth > 0 && rng.NextDouble() < 0.012)
                    DrawCrack(tone, height, n, rng, x, y, angle + Range(rng, -1.2f, 1.2f), steps / 3,
                        Mathf.Max(0.5f, width * 0.6f), darken, depth * 0.6f, branchDepth - 1);
            }
        }

        private static void Blob(float[] tone, int n, TileNoise nz, float cx, float cy, float radius, float strength,
            bool wrap = true)
        {
            int r = Mathf.CeilToInt(radius * 1.4f);
            for (int oy = -r; oy <= r; oy++)
            for (int ox = -r; ox <= r; ox++)
            {
                int px = Mathf.RoundToInt(cx) + ox, py = Mathf.RoundToInt(cy) + oy;
                if (wrap) { px = Wrap(px, n); py = Wrap(py, n); }
                else if (px < 0 || py < 0 || px >= n || py >= n) continue;
                float d = Mathf.Sqrt(ox * ox + oy * oy) / radius;
                float e = nz.Fbm(px / (float)n, py / (float)n, 16, 3);
                float f = Smooth(1.1f, 0.35f, d + (e - 0.5f) * 0.8f);
                tone[py * n + px] *= 1f - strength * f;
            }
        }

        // ------------------------------------------------------------------ generators

        /// <summary>1024px = 4 m. Stained, cracked slab with saw-cut joints along the tile border.</summary>
        private static void FloorConcrete()
        {
            const int n = 1024;
            var nz = new TileNoise(101);
            var rng = new Random(101);
            var tone = new float[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float large = nz.Fbm(u, v, 3, 4);
                float mid = nz.Fbm(u + 0.31f, v + 0.77f, 14, 3);
                float fine = nz.Fbm(u, v, 110, 2);
                float white = nz.Hash01(x, y);
                int i = y * n + x;
                tone[i] = 0.38f + (large - 0.5f) * 0.22f + (mid - 0.5f) * 0.10f + (fine - 0.5f) * 0.08f + (white - 0.5f) * 0.05f;
                h[i] = mid * 0.35f + fine * 0.45f + white * 0.2f;
            }

            for (int s = 0; s < 10; s++)
                Blob(tone, n, nz, Range(rng, 0, n), Range(rng, 0, n), Range(rng, 50, 210), Range(rng, 0.05f, 0.17f));
            // oil drips: clusters of small dark spots
            for (int c = 0; c < 5; c++)
            {
                float cx = Range(rng, 0, n), cy = Range(rng, 0, n);
                int count = rng.Next(6, 16);
                for (int k = 0; k < count; k++)
                    Blob(tone, n, nz, cx + Range(rng, -60, 60), cy + Range(rng, -60, 60), Range(rng, 4, 20), Range(rng, 0.15f, 0.35f));
            }
            // aggregate speckles
            for (int k = 0; k < 7000; k++)
            {
                int cx = rng.Next(n), cy = rng.Next(n);
                int r = rng.Next(1, 3);
                float delta = Range(rng, -0.09f, 0.07f);
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r * r) continue;
                    int i = Wrap(cy + oy, n) * n + Wrap(cx + ox, n);
                    tone[i] += delta;
                    h[i] += 0.25f;
                }
            }
            // tyre tracks (periodic so it tiles)
            for (int t = 0; t < 2; t++)
            {
                float y0 = Range(rng, 0, n);
                float amp = Range(rng, 10, 40);
                int k = rng.Next(1, 3);
                for (int x = 0; x < n; x++)
                {
                    float yc = y0 + amp * Mathf.Sin(2f * Mathf.PI * k * x / n);
                    for (int side = 0; side < 2; side++)
                    {
                        float ys = yc + side * 170f;
                        for (int oy = -14; oy <= 14; oy++)
                        {
                            int py = Wrap(Mathf.RoundToInt(ys) + oy, n);
                            float prof = 1f - Mathf.Abs(oy) / 14f;
                            float nn = nz.Fbm(x / (float)n, py / (float)n, 64, 2);
                            tone[py * n + x] *= 1f - 0.07f * prof * nn;
                        }
                    }
                }
            }
            for (int c = 0; c < 5; c++)
                DrawCrack(tone, h, n, rng, Range(rng, 0, n), Range(rng, 0, n), Range(rng, 0, 6.28f), rng.Next(150, 420),
                    Range(rng, 0.6f, 1.3f), 0.5f, 2.5f, 2);
            // saw-cut joints along the border => 4 m joint grid
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int dx = Mathf.Min(x, n - 1 - x), dy = Mathf.Min(y, n - 1 - y);
                int d = Mathf.Min(dx, dy);
                int i = y * n + x;
                if (d < 2) { tone[i] *= 0.45f; h[i] -= 1.2f; }
                else if (d < 12) tone[i] *= 0.96f + d * 0.003f;
            }

            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float t = Mathf.Clamp01(tone[i]);
                px[i] = new Color(t * 1.02f, t * 1.0f, t * 0.95f, 1f);
            }
            Save("Floor_Concrete", n, n, px, Kind.Albedo);
            Save("Floor_Concrete_N", n, n, NormalFromHeight(h, n, n, 3.0f), Kind.Normal);
        }

        /// <summary>512px = 3 m. Board-formed concrete with seams, tie holes and water streaks.</summary>
        private static void WallConcrete()
        {
            const int n = 512;
            var nz = new TileNoise(202);
            var rng = new Random(202);
            var tone = new float[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float large = nz.Fbm(u, v, 3, 3);
                float mid = nz.Fbm(u + 0.5f, v, 12, 3);
                float fine = nz.Fbm(u, v, 80, 2);
                float streak = nz.Fbm(u, v, 48, 3, 2, 0.6f);
                float white = nz.Hash01(x, y);
                int i = y * n + x;
                tone[i] = 0.52f + (large - 0.5f) * 0.16f + (mid - 0.5f) * 0.08f + (fine - 0.5f) * 0.06f + (white - 0.5f) * 0.04f;
                tone[i] -= Mathf.Pow(streak, 3f) * 0.22f;
                h[i] = mid * 0.3f + fine * 0.5f + white * 0.2f;
                // board seams every 128px (0.75 m), panel joints every 256px
                int sy = y % 128, sx = x % 256;
                if (sy < 2) { tone[i] *= 0.72f; h[i] -= 0.8f; }
                else if (sy < 5) h[i] += 0.2f;
                if (sx < 2) { tone[i] *= 0.8f; h[i] -= 0.6f; }
                // board grain: subtle horizontal variation per board
                float board = nz.Hash01(y / 128, 999);
                tone[i] += (board - 0.5f) * 0.05f;
            }
            // tie holes
            for (int py = 64; py < n; py += 128)
            for (int px0 = 40; px0 < n; px0 += 128)
            {
                for (int oy = -7; oy <= 7; oy++)
                for (int ox = -7; ox <= 7; ox++)
                {
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    int i = Wrap(py + oy, n) * n + Wrap(px0 + ox, n);
                    if (d < 4.5f) { tone[i] *= 0.4f; h[i] -= 1.5f; }
                    else if (d < 7f) { tone[i] *= 1.05f; h[i] += 0.2f; }
                }
            }
            // bug holes
            for (int k = 0; k < 900; k++)
            {
                int cx = rng.Next(n), cy = rng.Next(n);
                int r = rng.Next(1, 3);
                for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > r * r) continue;
                    int i = Wrap(cy + oy, n) * n + Wrap(cx + ox, n);
                    tone[i] *= 0.7f;
                    h[i] -= 0.5f;
                }
            }
            for (int s = 0; s < 4; s++)
                Blob(tone, n, nz, Range(rng, 0, n), Range(rng, 0, n), Range(rng, 30, 90), Range(rng, 0.05f, 0.12f));
            DrawCrack(tone, h, n, rng, Range(rng, 0, n), Range(rng, 0, n), 1.3f, 160, 0.7f, 0.6f, 2f, 1);

            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float t = Mathf.Clamp01(tone[i]);
                px[i] = new Color(t * 0.98f, t, t * 1.02f, 1f);
            }
            Save("Wall_Concrete", n, n, px, Kind.Albedo);
            Save("Wall_Concrete_N", n, n, NormalFromHeight(h, n, n, 2.2f), Kind.Normal);
        }

        /// <summary>512px = 1.2 m. 2x2 grimy ceramic floor tiles for the elevator lobby.</summary>
        private static void TileFloor()
        {
            const int n = 512;
            var nz = new TileNoise(303);
            var px = new Color[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                int gx = x % 256, gy = y % 256;
                int d = Mathf.Min(Mathf.Min(gx, 255 - gx), Mathf.Min(gy, 255 - gy));
                float tileTone = 0.55f + (nz.Hash01(x / 256, y / 256) - 0.5f) * 0.06f;
                float dirt = nz.Fbm(u, v, 6, 4);
                float t = tileTone - dirt * 0.18f + (nz.Hash01(x, y) - 0.5f) * 0.03f;
                int i = y * n + x;
                if (d < 4) { t = 0.18f + dirt * 0.05f; h[i] = 0f; }
                else h[i] = 1f;
                px[i] = new Color(t * 1.0f, t * 0.97f, t * 0.9f, 1f);
            }
            Save("Tile_Floor", n, n, px, Kind.Albedo);
            Save("Tile_Floor_N", n, n, NormalFromHeight(h, n, n, 0.8f), Kind.Normal);
        }

        /// <summary>256px = 0.5 m. Worn yellow/black diagonal hazard stripes.</summary>
        private static void Hazard()
        {
            const int n = 256;
            var nz = new TileNoise(404);
            var px = new Color[n * n];
            var yellow = new Color(0.95f, 0.70f, 0.05f);
            var black = new Color(0.05f, 0.05f, 0.05f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                bool stripe = ((x + y) % 128) < 64;
                Color c = stripe ? yellow : black;
                float wear = nz.Fbm(u, v, 12, 4) + (nz.Fbm(u, v, 48, 2) - 0.5f) * 0.3f;
                if (wear > 0.7f)
                {
                    float g = 0.4f + nz.Hash01(x, y) * 0.06f;
                    c = Color.Lerp(c, new Color(g, g, g * 0.97f), Smooth(0.7f, 0.75f, wear));
                }
                float dirt = nz.Fbm(u + 0.3f, v, 4, 3);
                c *= 1f - dirt * 0.35f;
                c *= 0.97f + nz.Hash01(x, y) * 0.06f;
                c.a = 1f;
                px[y * n + x] = c;
            }
            Save("Hazard_Stripes", n, n, px, Kind.Albedo);
        }

        /// <summary>256px = 1 m. Worn white road paint (tinted per material).</summary>
        private static void Paint()
        {
            const int n = 256;
            var nz = new TileNoise(505);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float wear = nz.Fbm(u, v, 12, 4) + (nz.Fbm(u, v, 64, 2) - 0.5f) * 0.4f;
                float t = 0.9f - nz.Fbm(u, v, 16, 3) * 0.12f + (nz.Hash01(x, y) - 0.5f) * 0.05f;
                float worn = Smooth(0.69f, 0.74f, wear);
                float g = 0.42f;
                var c = new Color(Mathf.Lerp(t, g, worn), Mathf.Lerp(t, g, worn), Mathf.Lerp(t, g * 0.97f, worn), 1f);
                px[y * n + x] = c;
            }
            Save("Paint_Worn", n, n, px, Kind.Albedo);
        }

        /// <summary>512px = 1 m. Brushed galvanised steel with scratches.</summary>
        private static void Metal()
        {
            const int n = 512;
            var nz = new TileNoise(606);
            var rng = new Random(606);
            var tone = new float[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float brush = nz.Fbm(u, v, 2, 256, 3);
                float large = nz.Fbm(u, v, 3, 3);
                float spangle = nz.Fbm(u, v, 24, 2);
                int i = y * n + x;
                tone[i] = 0.55f + (brush - 0.5f) * 0.14f + (large - 0.5f) * 0.12f + (spangle - 0.5f) * 0.06f;
                h[i] = brush * 0.5f;
            }
            for (int k = 0; k < 140; k++)
            {
                float x = Range(rng, 0, n), y = Range(rng, 0, n), a = Range(rng, 0, 6.28f);
                int len = rng.Next(10, 80);
                float delta = Range(rng, -0.12f, 0.15f);
                for (int s = 0; s < len; s++)
                {
                    int i = Wrap(Mathf.RoundToInt(y + Mathf.Sin(a) * s), n) * n + Wrap(Mathf.RoundToInt(x + Mathf.Cos(a) * s), n);
                    tone[i] += delta;
                    h[i] -= 0.3f;
                }
            }
            for (int s = 0; s < 6; s++)
                Blob(tone, n, nz, Range(rng, 0, n), Range(rng, 0, n), Range(rng, 20, 70), Range(rng, 0.1f, 0.25f));
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float t = Mathf.Clamp01(tone[i]);
                px[i] = new Color(t * 0.98f, t, t * 1.03f, 1f);
            }
            Save("Metal_Brushed", n, n, px, Kind.Albedo);
            Save("Metal_Brushed_N", n, n, NormalFromHeight(h, n, n, 1.2f), Kind.Normal);
        }

        /// <summary>256px = 1 m. Light painted metal with grime, chips and rust (tinted per material).</summary>
        private static void PaintedMetal()
        {
            const int n = 256;
            var nz = new TileNoise(707);
            var rng = new Random(707);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float t = 0.86f + (nz.Fbm(u, v, 3, 3) - 0.5f) * 0.12f + (nz.Hash01(x, y) - 0.5f) * 0.03f;
                float grime = nz.Fbm(u, v, 4, 16, 3);
                t -= grime * 0.25f * (1f - v * 0.6f);
                var c = new Color(t, t, t, 1f);
                float chip = nz.Fbm(u + 0.2f, v + 0.6f, 10, 3);
                if (chip > 0.7f) c = Color.Lerp(c, new Color(0.38f, 0.26f, 0.18f), Smooth(0.7f, 0.74f, chip));
                px[y * n + x] = c;
            }
            // rust drips
            for (int k = 0; k < 8; k++)
            {
                int x0 = rng.Next(n), y0 = rng.Next(n);
                int len = rng.Next(20, 90);
                for (int s = 0; s < len; s++)
                for (int w = -1; w <= 1; w++)
                {
                    int i = Wrap(y0 - s, n) * n + Wrap(x0 + w, n);
                    float f = 0.5f * (1f - s / (float)len);
                    px[i] = Color.Lerp(px[i], new Color(0.45f, 0.25f, 0.12f, 1f), f);
                }
            }
            Save("Painted_Metal", n, n, px, Kind.Albedo);
        }

        /// <summary>256px = 1 m. Rough pallet / crate wood with plank seams.</summary>
        private static void Wood()
        {
            const int n = 256;
            var nz = new TileNoise(808);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                int plank = y / 32;
                float grain = nz.Fbm(u, v, 3, 64, 3);
                float plankTone = nz.Hash01(plank, 77);
                float t = 0.8f + (grain - 0.5f) * 0.35f + (plankTone - 0.5f) * 0.2f - nz.Fbm(u, v, 4, 3) * 0.15f;
                if (y % 32 < 2) t *= 0.45f;
                var c = new Color(0.66f * t, 0.5f * t, 0.33f * t, 1f);
                px[y * n + x] = c;
            }
            Save("Wood", n, n, px, Kind.Albedo);
        }

        private static void Cardboard()
        {
            const int n = 256;
            var nz = new TileNoise(909);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float t = 0.85f + (nz.Fbm(u, v, 5, 3) - 0.5f) * 0.18f + Mathf.Sin(x * 0.8f) * 0.015f;
                var c = new Color(0.66f * t, 0.5f * t, 0.33f * t, 1f);
                int ty = y % 128;
                if (ty > 56 && ty < 72) c = new Color(0.72f * t, 0.6f * t, 0.42f * t, 1f); // tape
                px[y * n + x] = c;
            }
            Save("Cardboard", n, n, px, Kind.Albedo);
        }

        /// <summary>256px = 0.5 m. Diamond chain-link mesh with alpha cut-out.</summary>
        private static void WireMesh()
        {
            const int n = 256;
            const int p = 32;
            var nz = new TileNoise(1010);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d1 = Wrap(x + y, p), d2 = Wrap(x - y, p);
                bool wire = d1 < 3 || d2 < 3;
                float t = 0.62f + (nz.Hash01(x, y) - 0.5f) * 0.1f;
                px[y * n + x] = new Color(t, t, t * 1.03f, wire ? 1f : 0f);
            }
            Save("Wire_Mesh", n, n, px, Kind.CutoutAlpha);
        }

        /// <summary>256px = 1 m. Ribbed roller shutter slats.</summary>
        private static void Shutter()
        {
            const int n = 256;
            var nz = new TileNoise(1111);
            var px = new Color[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float t01 = (y % 32) / 32f;
                float shade = 0.5f + 0.18f * Mathf.Sin(t01 * Mathf.PI * 2f);
                if (t01 < 0.07f) shade = 0.18f;
                shade -= nz.Fbm(u, v, 4, 16, 3) * 0.2f;
                int i = y * n + x;
                h[i] = t01 < 0.07f ? 0f : Mathf.Sin(t01 * Mathf.PI);
                px[i] = new Color(shade * 0.95f, shade, shade * 1.02f, 1f);
            }
            Save("Shutter", n, n, px, Kind.Albedo);
            Save("Shutter_N", n, n, NormalFromHeight(h, n, n, 2f), Kind.Normal);
        }

        /// <summary>256x512 painted floor arrow pointing to +V.</summary>
        private static void Arrow()
        {
            const int w = 256, hh = 512;
            var nz = new TileNoise(1212);
            var px = new Color[w * hh];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = false;
                if (y >= 16 && y < 300 && x >= 92 && x < 164) inside = true;
                if (y >= 300 && y < 496)
                {
                    float halfW = 110f * (1f - (y - 300f) / 196f);
                    if (Mathf.Abs(x - 128f) <= halfW) inside = true;
                }
                float wear = nz.Fbm(x / (float)w, y / (float)hh, 16, 32, 3);
                float a = inside && wear < 0.72f ? 1f : 0f;
                float t = 0.92f - nz.Hash01(x, y) * 0.06f;
                px[y * w + x] = new Color(t, t, t, a);
            }
            Save("Arrow", w, hh, px, Kind.CutoutAlphaClamp);
        }

        /// <summary>256px = 1 m. Diagonal hatching (no-parking zones), alpha cut-out.</summary>
        private static void Hatch()
        {
            const int n = 256;
            var nz = new TileNoise(1313);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                bool stripe = ((x + y) % 128) < 40;
                float wear = nz.Fbm(u, v, 12, 4) + (nz.Fbm(u, v, 48, 2) - 0.5f) * 0.3f;
                float t = 0.9f - nz.Hash01(x, y) * 0.06f;
                px[y * n + x] = new Color(t, t, t, stripe && wear < 0.71f ? 1f : 0f);
            }
            Save("Hatch", n, n, px, Kind.CutoutAlpha);
        }

        private static void Puddle()
        {
            const int n = 512;
            var nz = new TileNoise(1414);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float shape = nz.Fbm(u, v, 4, 4) * 0.9f + (1f - d) * 0.85f - 0.8f;
                float a = Smooth(0f, 0.06f, shape) * (0.75f + 0.25f * Smooth(0.05f, 0.25f, shape));
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            Save("Puddle", n, n, px, Kind.AlbedoAlphaClamp);
        }

        private static void Crack()
        {
            const int n = 512;
            var rng = new Random(1515);
            var tone = new float[n * n];
            var dummy = new float[n * n];
            for (int i = 0; i < tone.Length; i++) tone[i] = 1f;
            for (int c = 0; c < 3; c++)
                DrawCrack(tone, dummy, n, rng, n * 0.5f + Range(rng, -30, 30), n * 0.5f + Range(rng, -30, 30),
                    Range(rng, 0, 6.28f), 130, 1.4f, 0.0f, 1f, 3);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float edge = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y)) / 40f;
                float a = (1f - tone[i]) * Mathf.Clamp01(edge);
                px[i] = new Color(0.05f, 0.05f, 0.05f, Mathf.Clamp01(a * 1.2f));
            }
            Save("Crack", n, n, px, Kind.AlbedoAlphaClamp);
        }

        private static void OilStain()
        {
            const int n = 256;
            var nz = new TileNoise(1616);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float shape = (1f - d) + (nz.Fbm(u, v, 5, 4) - 0.5f) * 0.7f;
                float a = Smooth(0.1f, 0.35f, shape) * 0.75f;
                float rim = Smooth(0.08f, 0.14f, shape) * (1f - Smooth(0.14f, 0.22f, shape));
                a = Mathf.Clamp01(a + rim * 0.3f);
                px[y * n + x] = new Color(0.07f, 0.06f, 0.05f, a);
            }
            Save("Oil_Stain", n, n, px, Kind.AlbedoAlphaClamp);
        }

        private static void Grate()
        {
            const int n = 128;
            var nz = new TileNoise(1717);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool frame = x < 8 || y < 8 || x >= n - 8 || y >= n - 8;
                bool bar = (x % 16) < 7;
                float t = 0.28f + (nz.Hash01(x, y) - 0.5f) * 0.08f - nz.Fbm(x / (float)n, y / (float)n, 4, 3) * 0.1f;
                Color c = frame || bar ? new Color(t * 1.05f, t * 0.95f, t * 0.85f, 1f) : new Color(0.015f, 0.015f, 0.015f, 1f);
                px[y * n + x] = c;
            }
            Save("Grate", n, n, px, Kind.Albedo);
        }

        /// <summary>256px = 1.5 m. Plastic tarp with folds and creases.</summary>
        private static void Tarp()
        {
            const int n = 256;
            var nz = new TileNoise(1919);
            var px = new Color[n * n];
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float folds = nz.Fbm(u, v, 3, 6, 3);
                float crease = Mathf.Abs(nz.Fbm(u + 0.3f, v, 5, 2, 2) - 0.5f) * 2f;
                int i = y * n + x;
                h[i] = folds * 1.5f - Mathf.Pow(1f - crease, 8f) * 0.4f;
                float t = 0.8f + (folds - 0.5f) * 0.35f - nz.Fbm(u, v, 8, 3) * 0.12f;
                if ((x % 128) < 2 || (y % 128) < 2) t *= 0.85f;
                px[i] = new Color(t, t, t, 1f);
            }
            Save("Tarp", n, n, px, Kind.Albedo);
            Save("Tarp_N", n, n, NormalFromHeight(h, n, n, 4f), Kind.Normal);
        }

        /// <summary>512px clamp. Large standing-water sheet: mostly opaque with a lobed, soft shoreline.</summary>
        private static void Flood()
        {
            const int n = 512;
            var nz = new TileNoise(2020);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float shape = 1f - d * 1.15f + (nz.Fbm(u, v, 3, 4) - 0.5f) * 0.55f;
                float a = Smooth(0f, 0.1f, shape) * (0.82f + 0.12f * nz.Fbm(u, v, 24, 2));
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            Save("Flood", n, n, px, Kind.AlbedoAlphaClamp);
        }

        private static void Glyphs()
        {
            var cov = PixelFont.RenderAtlas();
            var nz = new TileNoise(1818);
            int w = PixelFont.AtlasWidth, hh = PixelFont.AtlasHeight;
            var clean = new Color[w * hh];
            var worn = new Color[w * hh];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float a = cov[i];
                clean[i] = new Color(1f, 1f, 1f, a);
                float wear = nz.Fbm(x / (float)w, y / (float)hh, 64, 32, 3);
                float t = 0.92f - nz.Hash01(x, y) * 0.08f;
                worn[i] = new Color(t, t, t, a > 0.5f && wear < 0.71f ? 1f : 0f);
            }
            Save("Glyphs", w, hh, clean, Kind.CutoutAlphaClamp);
            Save("Glyphs_Worn", w, hh, worn, Kind.CutoutAlphaClamp);
        }
    }
}
