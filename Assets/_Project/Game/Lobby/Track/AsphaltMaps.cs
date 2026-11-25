using System.Threading.Tasks;
using HideAndSeek.Game.Gfx;
using UnityEngine;
using N = HideAndSeek.Game.AsphaltNoise;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The drift track's asphalt, generated at runtime (worker thread) for the graphics profile: one 6 m tile of
    /// albedo (dark binder with angular stone chips, repaired patches, stains, a few thin cracks), normal
    /// map (from the same height field) and smoothness (polished stones, rough cracks), all seamless. Ultra = 2048 px
    /// (3 mm per pixel) plus a fine 512 px detail normal map repeated 8x; Wysoka = 1024; Średnia = 512; Niska = 256 and
    /// a softer normal map (a custom profile goes by its texture setting). Re-made when the profile changes
    /// (<see cref="GfxSettings.Changed"/>). The maps go onto the shared asphalt material of the passage and the pad.
    /// </summary>
    public static class AsphaltMaps
    {
        public const float TileMetres = 6f;
        private const int Seed = 9127;

        private sealed class Result { public int Size; public Color32[] Albedo, Normal, Gloss, Detail; public float Ms; }

        private static Material _mat;
        private static int _shownSize, _wantSize;
        private static Task<Result> _job;
        private static bool _hooked;

        /// <summary>Texture size for the current graphics settings.</summary>
        /// <summary>Tests: this size instead of the one for the settings (0 = settings).</summary>
        public static int TestSize;

        /// <summary>Tests: re-make the maps for <see cref="TestSize"/> / the settings now.</summary>
        public static void Refresh() { if (_mat != null) Request(SizeForSettings()); }

        public static int SizeForSettings()
        {
            if (TestSize > 0) return TestSize;
            int preset = GfxSettings.CurrentPreset();
            if (preset >= 0) return preset switch { 3 => 2048, 2 => 1024, 1 => 512, _ => 256 };
            return GfxSettings.TextureLevel switch { 0 => 1024, 1 => 512, _ => 256 };
        }

        /// <summary>Size of the maps on the material now (tests).</summary>
        public static int ShownSize => _shownSize;

        /// <summary>Starts using <paramref name="m"/>: a quick small set now, the full set from a worker thread.</summary>
        public static void Attach(Material m)
        {
            if (m != _mat)
            {
                // A new lobby load: its material starts with the placeholders; give it the last maps made.
                _mat = m;
                _shownSize = 0;
                if (_last[0] != null) Assign(_lastSize, _last[0], _last[1], _last[2], _last[3]);
            }
            if (!_hooked) { GfxSettings.Changed += OnGfxChanged; _hooked = true; }
            if (_shownSize == 0) Upload(Make(128));
            _wantSize = 0;
            Request(SizeForSettings());
        }

        private static readonly Texture2D[] _last = new Texture2D[4];
        private static int _lastSize;

        private static void OnGfxChanged()
        {
            if (_mat != null) Request(SizeForSettings());
        }

        private static void Request(int size)
        {
            if (size == _shownSize || size == _wantSize) return;
            _wantSize = size;
            _job = Task.Run(() => Make(size));
        }

        /// <summary>Main thread, every frame: uploads a finished set.</summary>
        public static void Tick()
        {
            if (_job == null || !_job.IsCompleted) return;
            var job = _job;
            _job = null;
            if (job.IsFaulted) { Debug.LogWarning($"[Track] asphalt generation failed: {job.Exception?.GetBaseException().Message}"); return; }
            var r = job.Result;
            if (r.Size != _wantSize) return;
            Upload(r);
        }

        private static void Upload(Result r)
        {
            if (_mat == null) return;
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < _last.Length; i++) if (_last[i] != null) Object.Destroy(_last[i]);
            _last[0] = Tex(r.Albedo, r.Size, false, "AsphaltAlbedo");
            _last[1] = Tex(r.Normal, r.Size, true, "AsphaltNormal");
            _last[2] = Tex(r.Gloss, r.Size, true, "AsphaltGloss");
            _last[3] = r.Detail != null ? Tex(r.Detail, 512, true, "AsphaltDetailNormal") : null;
            _lastSize = r.Size;
            Assign(r.Size, _last[0], _last[1], _last[2], _last[3]);
            Debug.Log($"[Track] asphalt maps {r.Size} px (generated in {r.Ms:0} ms, upload {(Time.realtimeSinceStartup - t0) * 1000f:0} ms)");
        }

        private static void Assign(int size, Texture2D albedo, Texture2D normal, Texture2D gloss, Texture2D detail)
        {
            _mat.SetTexture("_MainTex", albedo);
            _mat.SetTexture("_BumpMap", normal);
            _mat.SetTexture("_MetallicGlossMap", gloss);
            if (detail != null) _mat.SetTexture("_DetailNormalMap", detail);
            _mat.SetFloat("_DetailNormalMapScale", detail != null ? 0.7f : 0f);
            _mat.SetTextureScale("_DetailAlbedoMap", new Vector2(8f, 8f));
            _mat.SetFloat("_BumpScale", size >= 1024 ? 1f : 0.7f);
            _mat.SetFloat("_GlossMapScale", 1f);
            _shownSize = size;
        }

        private static Texture2D Tex(Color32[] px, int size, bool linear, string name)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 16 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Builds one set (any thread).</summary>
        private static Result Make(int size)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int n = size * size;
            var height = new float[n];
            var albedo = new Color32[n];
            var gloss = new Color32[n];
            System.Threading.Tasks.Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float macro = N.Fbm(u, v, 2, 3, Seed);                 // big tone changes
                    float grain = N.Fbm(u, v, 128, 3, Seed + 11, 0.55f);  // binder / sand
                    // Aggregate: angular chips packed in the binder (warped cells, ~2.6 cm), some covered by binder.
                    const int C = 230;
                    float ws = 0.45f / C;
                    float xu = u + (N.Value(u, v, C * 2, Seed + 31) - 0.5f) * ws, xv = v + (N.Value(u, v, C * 2, Seed + 37) - 0.5f) * ws;
                    N.Cells(xu, xv, C, Seed + 23, out float s1, out float s2, out float sid);
                    float edge = (s2 - s1) * C;
                    float expo = N.Smooth(0.3f, 0.45f, sid) * N.Smooth(0.05f, 0.22f, edge);
                    float fine = N.Value(u, v, C * 6, Seed + 43);
                    float shade = 0.25f + 0.22f * N.Hash((int)(sid * 1e6f), 0, 5) + 0.05f * (fine - 0.5f);
                    float dome = Mathf.Clamp01(edge * 2.5f);
                    float patch = N.Smooth(0.62f, 0.66f, N.Fbm(u, v, 3, 2, Seed + 41));
                    float stain = N.Smooth(0.55f, 0.75f, N.Fbm(u, v, 4, 3, Seed + 53)) * 0.6f;
                    // Cracks: a few thin wobbly cell borders where a low mask allows.
                    float wu = u + (N.Value(u, v, 24, Seed + 61) - 0.5f) * 0.012f, wv = v + (N.Value(u, v, 24, Seed + 67) - 0.5f) * 0.012f;
                    N.Cells(wu, wv, 5, Seed + 71, out float f1, out float f2);
                    float mask = N.Smooth(0.66f, 0.74f, N.Value(u, v, 3, Seed + 79));
                    float crack = (1f - N.Smooth(0.0007f, 0.0018f, f2 - f1)) * mask * (1f - patch);

                    float tone = 0.27f + 0.05f * (macro - 0.5f) + 0.08f * (grain - 0.5f); // sRGB
                    tone = tone * (1f - 0.1f * patch) * (1f - 0.22f * stain);
                    tone += (shade - tone) * expo * 0.9f;
                    tone *= 1f - 0.55f * crack;
                    int i = y * size + x;
                    albedo[i] = new Color32(B(tone), B(tone), B(tone * 1.04f), 255);
                    height[i] = 0.3f * grain + 0.7f * expo * (0.55f + 0.45f * dome) + 0.15f * fine * expo - 1.1f * crack - 0.06f * patch;
                    float g = 0.12f + 0.18f * expo - 0.08f * crack + 0.05f * patch + 0.06f * stain;
                    gloss[i] = new Color32(0, 0, 0, B(g));
                }
            });
            var r = new Result { Size = size, Albedo = albedo, Gloss = gloss, Normal = Normals(height, size, size >= 1024 ? 4.5f : 3f) };
            if (size >= 2048) r.Detail = DetailNormal(512);
            r.Ms = (float)sw.Elapsed.TotalMilliseconds;
            return r;
        }

        /// <summary>Fine grain for the detail slot (Ultra): tiny stones and pores.</summary>
        private static Color32[] DetailNormal(int size)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                h[y * size + x] = 0.6f * N.Fbm(u, v, 48, 3, Seed + 101, 0.6f) + 0.4f * N.Smooth(0.6f, 0.75f, N.Value(u, v, 90, Seed + 103));
            }
            return Normals(h, size, 3f);
        }

        /// <summary>Tangent-space normals from a wrapping height field (strength per tile pixel).</summary>
        private static Color32[] Normals(float[] h, int size, float strength)
        {
            var o = new Color32[size * size];
            float k = strength * size / 1024f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int xl = (x + size - 1) % size, xr = (x + 1) % size, yd = (y + size - 1) % size, yu = (y + 1) % size;
                float dx = (h[y * size + xr] - h[y * size + xl]) * k, dy = (h[yu * size + x] - h[yd * size + x]) * k;
                float nx = -dx, ny = -dy, nz = 1f;
                float len = (float)System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                o[y * size + x] = new Color32(B(nx / len * 0.5f + 0.5f), B(ny / len * 0.5f + 0.5f), B(nz / len * 0.5f + 0.5f), 255);
            }
            return o;
        }

        private static byte B(float v) => (byte)(v <= 0f ? 0 : v >= 1f ? 255 : (int)(v * 255f + 0.5f));
    }
}
