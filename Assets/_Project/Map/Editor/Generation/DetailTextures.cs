using System.IO;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Procedural textures of the map details added on 23.11: dust noise for the forgotten cars, fluid leak shapes,
    /// fabrics of the car covers (fitted cover, canvas, crinkled foil). Seeded, deterministic, written next to the
    /// other garage textures (<see cref="GarageTextures.Folder"/>).
    /// </summary>
    internal static class DetailTextures
    {
        public const int LeakShapes = 3;

        public static void GenerateAll()
        {
            DustNoise();
            LeakAtlas();
            Fabric("Cover_Fabric", 2301, 0.82f, 0.9f, 48, 0.06f, 0.5f);
            Fabric("Canvas", 2302, 0.78f, 0.85f, 24, 0.12f, 1.4f);
            Foil();
            Cobweb();
        }

        /// <summary>
        /// 256px corner cobweb (cut-out): threads fanning out from the corner (0,0) to the far edges, sagging
        /// cross threads between them, a few broken ones. Alpha = thread.
        /// </summary>
        private static void Cobweb()
        {
            const int n = 256;
            var rnd = new System.Random(2350);
            var a = new float[n * n];
            int spokes = 9;
            var ang = new float[spokes];
            for (int k = 0; k < spokes; k++) ang[k] = Mathf.Lerp(4f, 86f, k / (spokes - 1f)) + R(rnd, -3f, 3f);
            void Line(Vector2 p0, Vector2 p1, float w)
            {
                int steps = (int)(Vector2.Distance(p0, p1) * 2f) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(p0, p1, s / (float)steps);
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = (int)p.x + dx, y = (int)p.y + dy;
                        if (x < 0 || y < 0 || x >= n || y >= n) continue;
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), p);
                        a[y * n + x] = Mathf.Max(a[y * n + x], Mathf.Clamp01(w - d));
                    }
                }
            }
            Vector2 Spoke(int k, float r) => new Vector2(Mathf.Cos(ang[k] * Mathf.Deg2Rad), Mathf.Sin(ang[k] * Mathf.Deg2Rad)) * r;
            for (int k = 0; k < spokes; k++) Line(Vector2.zero, Spoke(k, R(rnd, 230f, 330f)), 1.3f);
            for (float r = 22f; r < 250f; r += R(rnd, 14f, 24f))
                for (int k = 0; k + 1 < spokes; k++)
                {
                    if (rnd.NextDouble() < 0.12) continue; // broken bits
                    Vector2 p0 = Spoke(k, r), p1 = Spoke(k + 1, r * R(rnd, 0.95f, 1.05f));
                    Vector2 mid = (p0 + p1) * 0.5f * 0.94f;
                    Line(p0, mid, 1.1f);
                    Line(mid, p1, 1.1f);
                }
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.85f, 0.85f, 0.82f, a[i] > 0.3f ? 1f : 0f);
            Save("Cobweb", n, px, n, true, true, false, true);
        }

        public static Texture2D Load(string name) => GarageTextures.Load(name);

        // ------------------------------------------------------------------ dust

        /// <summary>256px tile (about 1.25 m on the cars). R fine grain, G soft blotches, B vertical streaks. Linear data.</summary>
        private static void DustNoise()
        {
            const int n = 256;
            var nz = new TileNoise(2310);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float grain = 0.55f * nz.Hash01(x, y) + 0.45f * nz.Fbm(u, v, 64, 2);
                float blot = Smooth(0.3f, 0.75f, nz.Fbm(u + 0.37f, v + 0.11f, 4, 5, 0.55f));
                // streaks: noise stretched along v (rain-like runs down the doors), plus dry drip ends
                float streak = nz.Fbm(u * 1f, v, 24, 2, 3, 0.5f);
                streak = Smooth(0.35f, 0.8f, streak) * (0.6f + 0.4f * nz.Fbm(u, v + 0.5f, 6, 3));
                px[y * n + x] = new Color(grain, blot, streak, 1f);
            }
            Save("Dust_Noise", n, px, n, false, false);
        }

        // ------------------------------------------------------------------ leaks

        /// <summary>
        /// Atlas of <see cref="LeakShapes"/> leak shapes side by side, each 256 x 512 (1.6 x 3.2 m on the floor):
        /// a pool under the engine (v 0.05..0.4) and a run towards the aisle along +v that thins out; R = film
        /// thickness, G = noise. Linear data, clamped.
        /// </summary>
        private static void LeakAtlas()
        {
            const int w = 256, h = 512;
            var px = new Color[w * LeakShapes * h];
            for (int s = 0; s < LeakShapes; s++)
            {
                var one = Leak(s, w, h);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w * LeakShapes + s * w + x] = one[y * w + x];
            }
            Save("Leak_Atlas", w * LeakShapes, px, h, false, true);
        }

        private static Color[] Leak(int variant, int w, int h)
        {
            var nz = new TileNoise(2320 + variant * 7);
            var rnd = new System.Random(2330 + variant);
            var px = new Color[w * h];
            // main pool + 2-4 satellite drops
            var blobs = new System.Collections.Generic.List<Vector3>();
            blobs.Add(new Vector3(0.5f + R(rnd, -0.06f, 0.06f), 0.2f, R(rnd, 0.21f, 0.27f)));
            int sats = 2 + rnd.Next(3);
            for (int i = 0; i < sats; i++)
                blobs.Add(new Vector3(0.5f + R(rnd, -0.22f, 0.22f), R(rnd, 0.06f, 0.4f), R(rnd, 0.05f, 0.11f)));
            // the run: a wandering path from the pool to the far end
            float[] pathX = new float[h];
            float px0 = blobs[0].x, drift = R(rnd, -0.15f, 0.15f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                pathX[y] = px0 + drift * Mathf.Max(0f, v - 0.3f) + (nz.Fbm(0.3f, v, 1, 6, 3) - 0.5f) * 0.18f;
            }
            float runEnd = R(rnd, 0.72f, 0.93f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float edge = (nz.Fbm(u, v, 8, 16, 4) - 0.5f) * 0.09f;
                float pool = 0f;
                foreach (var b in blobs)
                {
                    float dx = u - b.x, dy = (v - b.y) * 2f; // texture is 1:2, distances in the same units
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    pool = Mathf.Max(pool, 1f - (d + edge) / b.z);
                }
                // run: width shrinks with distance
                float run = 0f;
                if (v > blobs[0].y && v < runEnd)
                {
                    float k = Mathf.InverseLerp(blobs[0].y, runEnd, v);
                    float width = Mathf.Lerp(0.11f, 0.03f, k) * (0.75f + 0.6f * nz.Fbm(0.7f, v, 1, 12, 2));
                    float d = Mathf.Abs(u - pathX[y]) + edge * 0.4f;
                    run = (1f - d / width) * Mathf.Lerp(0.8f, 0.35f, k);
                }
                // keep a dry margin so the clamped edges never show
                float margin = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float th = Mathf.Max(Mathf.Clamp01(pool) * (0.55f + 0.45f * Mathf.Clamp01(pool * 1.6f)), Mathf.Clamp01(run));
                th = Mathf.Clamp01(th * (0.85f + 0.3f * nz.Fbm(u, v, 16, 32, 2))) * Mathf.Clamp01(margin * 25f);
                px[y * w + x] = new Color(th, nz.Fbm(u, v, 4, 8, 3), 0f, 1f);
            }
            return px;
        }

        // ------------------------------------------------------------------ fabrics

        /// <summary>256px fabric weave (greyscale albedo) + normal map: fine threads, soft mottling and creases.</summary>
        private static void Fabric(string name, int seed, float baseTone, float threadTone, int threads, float mottle, float creaseDepth)
        {
            const int n = 256;
            var nz = new TileNoise(seed);
            var px = new Color[n * n];
            var hgt = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float wu = Mathf.Sin(u * threads * Mathf.PI * 2f), wv = Mathf.Sin(v * threads * Mathf.PI * 2f);
                float weave = 0.5f + 0.25f * wu * Mathf.Sign(wv) + 0.25f * wv * Mathf.Sign(wu);
                float crease = Mathf.Abs(nz.Fbm(u + 0.2f, v, 3, 3, 3) - 0.5f) * 2f;
                float t = baseTone + (weave - 0.5f) * (threadTone - baseTone) - nz.Fbm(u, v, 4, 4) * mottle
                          - Mathf.Pow(1f - crease, 10f) * 0.08f;
                int i = y * n + x;
                px[i] = new Color(t, t, t, 1f);
                hgt[i] = weave * 0.15f + nz.Fbm(u, v, 6, 3) * creaseDepth - Mathf.Pow(1f - crease, 10f) * 0.3f * creaseDepth;
            }
            Save(name, n, px, n, true, false);
            Save(name + "_N", n, Normal(hgt, n, 3f), n, false, false, true);
        }

        /// <summary>256px crinkled plastic foil: albedo near white with milky creases, strong normal map.</summary>
        private static void Foil()
        {
            const int n = 256;
            var nz = new TileNoise(2340);
            var px = new Color[n * n];
            var hgt = new float[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                // crinkles: ridges of several rotated noises
                float r1 = 1f - Mathf.Abs(nz.Fbm(u, v, 6, 4) - 0.5f) * 2f;
                float r2 = 1f - Mathf.Abs(nz.Fbm(u + 0.31f, v + 0.77f, 11, 3) - 0.5f) * 2f;
                float ridge = Mathf.Pow(r1, 6f) * 0.7f + Mathf.Pow(r2, 8f) * 0.5f;
                int i = y * n + x;
                hgt[i] = ridge + nz.Fbm(u, v, 3, 3) * 0.6f;
                float t = 0.86f + ridge * 0.14f - nz.Fbm(u, v, 5, 3) * 0.08f;
                px[i] = new Color(t, t, t, 0.35f + ridge * 0.4f);
            }
            Save("Foil", n, px, n, true, false, false, true);
            Save("Foil_N", n, Normal(hgt, n, 6f), n, false, false, true);
        }

        // ------------------------------------------------------------------ helpers

        private static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Color[] Normal(float[] h, int n, float strength)
        {
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                float dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                var v = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                px[y * n + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
            }
            return px;
        }

        private static void Save(string name, int w, Color[] px, int h, bool srgb, bool clamp, bool normal = false, bool alpha = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{GarageTextures.Folder}/{name}.png";
            File.WriteAllBytes(GarageTextures.FullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            imp.alphaIsTransparency = alpha;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.filterMode = FilterMode.Trilinear;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }
    }
}
