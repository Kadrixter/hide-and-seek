using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Textures of the wrecked Golf (seeded, deterministic): tileable dust, the scrape mask of the right front, the
    /// spider-web crack of the windscreen and the finger-written "speed &lt;3" (see WreckTextures.Writing).
    /// Saved next to the garage textures as Wreck_*.png.
    /// </summary>
    internal static partial class WreckTextures
    {
        /// <summary>Bump when a generator changes: the PNGs are rewritten once.</summary>
        private const int Version = 5;

        private static readonly List<string> Written = new List<string>();

        public static void Ensure()
        {
            string stamp = GarageTextures.FullPath(GarageTextures.Folder + "/Wreck_Version.txt");
            bool fresh = File.Exists(stamp) && File.ReadAllText(stamp).Trim() == Version.ToString();
            foreach (var n in new[] { "Wreck_Dust", "Wreck_Scrape", "Wreck_Crack", "Wreck_Write" })
                if (!File.Exists(GarageTextures.FullPath($"{GarageTextures.Folder}/{n}.png"))) fresh = false;
            if (fresh) return;
            Written.Clear();
            Dust();
            Scrape();
            Crack();
            Writing();
            File.WriteAllText(stamp, Version.ToString());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var p in Written) Configure(p);
            Debug.Log("[Wreck] textures generated (v" + Version + ")");
        }

        private static void Save(string name, int w, int h, Color[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{GarageTextures.Folder}/{name}.png";
            File.WriteAllBytes(GarageTextures.FullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Written.Add(path);
        }

        /// <summary>All wreck textures are data masks: linear, with alpha, repeat for the dust and clamp for the rest.</summary>
        private static void Configure(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = false;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.wrapMode = path.Contains("Wreck_Dust") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
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

        // ------------------------------------------------------------------ dust

        private static void Dust()
        {
            const int n = 512;
            var nz = new TileNoise(7101);
            var nz2 = new TileNoise(7102);
            var rng = new Random(7103);
            var speck = new float[n * n];
            for (int k = 0; k < 900; k++)
            {
                int cx = rng.Next(n), cy = rng.Next(n);
                float r = 0.6f + (float)rng.NextDouble() * 1.8f, a = 0.4f + (float)rng.NextDouble() * 0.6f;
                for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    int i = ((cy + dy + n) % n) * n + (cx + dx + n) % n;
                    speck[i] = Mathf.Max(speck[i], a * Smooth(r + 0.8f, r - 0.3f, d));
                }
            }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                float grain = nz.Fbm(u, v, 64, 3) * 0.7f + nz2.Hash01(x, y) * 0.3f;
                float blotch = Smooth(0.25f, 0.8f, nz.Fbm(u, v, 3, 5, 0.55f));
                // streaks: fast across, slow down the face
                float streak = nz2.Fbm(u, v, 48, 3, 3, 0.5f);
                streak = Smooth(0.3f, 0.75f, streak);
                px[y * n + x] = new Color(grain, blotch, streak, speck[y * n + x]);
            }
            Save("Wreck_Dust", n, n, px);
        }

        // ------------------------------------------------------------------ scrapes (right front wing and door)

        /// <summary>u runs from the front door (0) to the nose (1), v from the sill (0) to the shoulder (1).</summary>
        private static void Scrape()
        {
            const int w = 1024, h = 512;
            var nz = new TileNoise(7201);
            var rng = new Random(7202);
            var scratch = new float[w * h];
            for (int k = 0; k < 300; k++)
            {
                float u0 = Mathf.Pow((float)rng.NextDouble(), 0.6f), v0 = 0.15f + (float)rng.NextDouble() * 0.75f;
                float len = 0.04f + (float)rng.NextDouble() * 0.35f * u0;
                float ang = ((float)rng.NextDouble() - 0.5f) * 0.25f + (v0 - 0.5f) * 0.15f;
                float inten = 0.35f + (float)rng.NextDouble() * 0.65f;
                Line(scratch, w, h, u0 * w, v0 * h, (u0 - len) * w, (v0 - len * Mathf.Tan(ang) * 2f) * h, 0.7f + (float)rng.NextDouble() * 0.9f, inten);
            }
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                // the damage grows towards the nose and sits in the middle band of the flank
                float zone = Smooth(0.18f, 0.62f, u + (nz.Fbm(u, v, 4, 3) - 0.5f) * 0.3f)
                             * Smooth(0.02f, 0.22f, v) * Smooth(1.0f, 0.78f, v + (nz.Fbm(u * 2f, v, 3, 3) - 0.5f) * 0.2f);
                // dragged texture: stretched along u
                float drag = nz.Fbm(u * 0.25f, v, 6, 40, 4, 0.55f);
                float blot = nz.Fbm(u, v, 8, 6, 4, 0.5f);
                float bare = Smooth(0.6f, 0.66f, drag * 0.62f + blot * 0.38f + zone * 0.16f - 0.06f) * zone;
                float scuff = Mathf.Clamp01(zone * (0.2f + blot * 0.8f) * Smooth(0.42f, 0.62f, drag + 0.05f));
                float s = scratch[y * w + x] * Mathf.Clamp01(zone * 2.2f + 0.15f);
                // ragged edges: nothing in the outermost texels (clamped sampling)
                float edge = Smooth(0f, 0.02f, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)));
                px[y * w + x] = new Color(bare * edge, Mathf.Max(scuff - bare, 0f) * edge, s * edge, 1f);
            }
            Save("Wreck_Scrape", w, h, px);
        }

        /// <summary>Anti-aliased line of width wpx into a float mask (max blend).</summary>
        internal static void Line(float[] m, int w, int h, float x0, float y0, float x1, float y1, float wpx, float a)
        {
            int xa = Mathf.Max(0, (int)(Mathf.Min(x0, x1) - wpx - 2)), xb = Mathf.Min(w - 1, (int)(Mathf.Max(x0, x1) + wpx + 2));
            int ya = Mathf.Max(0, (int)(Mathf.Min(y0, y1) - wpx - 2)), yb = Mathf.Min(h - 1, (int)(Mathf.Max(y0, y1) + wpx + 2));
            var A = new Vector2(x0, y0);
            var D = new Vector2(x1 - x0, y1 - y0);
            float L2 = Mathf.Max(D.sqrMagnitude, 1e-6f);
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                var P = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(P - A, D) / L2);
                float d = (P - (A + D * t)).magnitude;
                float v = a * Smooth(wpx * 0.5f + 0.7f, wpx * 0.5f - 0.3f, d);
                int i = y * w + x;
                if (v > m[i]) m[i] = v;
            }
        }

        // ------------------------------------------------------------------ windscreen cracks

        /// <summary>u across the screen (left to right), v up the slope (base 0 .. roof 1).</summary>
        private static void Crack()
        {
            const int n = 1024;
            var rng = new Random(7301);
            var nz = new TileNoise(7302);
            var lines = new float[n * n];
            void Web(Vector2 c, int rays, float reach, float width, int rings)
            {
                var ends = new List<Vector2>[rays];
                for (int r = 0; r < rays; r++)
                {
                    ends[r] = new List<Vector2> { c };
                    float a = r * Mathf.PI * 2f / rays + ((float)rng.NextDouble() - 0.5f) * 0.35f;
                    var p = c;
                    float len = reach * (0.55f + (float)rng.NextDouble() * 0.6f);
                    int steps = 14;
                    for (int s = 0; s < steps; s++)
                    {
                        a += ((float)rng.NextDouble() - 0.5f) * 0.32f;
                        var q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (len / steps);
                        float wd = width * Mathf.Lerp(1.5f, 0.6f, s / (float)steps);
                        Line(lines, n, n, p.x, p.y, q.x, q.y, wd, 0.9f);
                        // a side branch now and then
                        if (rng.NextDouble() < 0.18)
                        {
                            float b = a + (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + (float)rng.NextDouble() * 0.5f);
                            var e = q + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * len * (0.05f + (float)rng.NextDouble() * 0.12f);
                            Line(lines, n, n, q.x, q.y, e.x, e.y, wd * 0.7f, 0.7f);
                        }
                        p = q;
                        ends[r].Add(p);
                    }
                }
                // concentric rings joining neighbouring rays (not every gap)
                for (int k = 1; k <= rings; k++)
                {
                    int idx = Mathf.Min(14, Mathf.RoundToInt(k * 14f / (rings + 1) * (0.25f + 0.75f * k / rings)));
                    for (int r = 0; r < rays; r++)
                    {
                        if (rng.NextDouble() < 0.25 + k * 0.08) continue;
                        var a0 = ends[r][Mathf.Min(idx, ends[r].Count - 1)];
                        var b0 = ends[(r + 1) % rays][Mathf.Min(idx + rng.Next(-1, 2), ends[(r + 1) % rays].Count - 1)];
                        var mid = (a0 + b0) * 0.5f + (a0 + b0 - 2f * c).normalized * (a0 - b0).magnitude * 0.12f;
                        Line(lines, n, n, a0.x, a0.y, mid.x, mid.y, width * 0.8f, 0.75f);
                        Line(lines, n, n, mid.x, mid.y, b0.x, b0.y, width * 0.8f, 0.75f);
                    }
                }
            }
            var main = new Vector2(0.66f * n, 0.4f * n);
            var second = new Vector2(0.27f * n, 0.62f * n);
            Web(main, 26, n * 0.75f, 2.2f, 6);
            Web(second, 14, n * 0.3f, 1.6f, 3);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n;
                var p = new Vector2(x, y);
                float d1 = (p - main).magnitude / n, d2 = (p - second).magnitude / n;
                float jag = (nz.Fbm(u, v, 24, 3) - 0.5f) * 0.03f;
                float crushed = Smooth(0.075f, 0.02f, d1 + jag) * (0.6f + 0.4f * nz.Fbm(u, v, 64, 2)) + Smooth(0.035f, 0.008f, d2 + jag) * 0.7f;
                float haze = Smooth(0.45f, 0.05f, d1) * 0.6f + Smooth(0.18f, 0.02f, d2) * 0.3f;
                float edge = Smooth(0f, 0.015f, Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)));
                px[y * n + x] = new Color(lines[y * n + x] * edge, Mathf.Clamp01(crushed) * edge, haze * edge, 1f);
            }
            Save("Wreck_Crack", n, n, px);
        }
    }
}
