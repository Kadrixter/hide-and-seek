using UnityEngine;

namespace HideAndSeek.Game.Gfx
{
    /// <summary>
    /// Post effects on the camera that draws last to the screen (picked by <see cref="GfxPostDriver"/>: normally the
    /// first-person hands camera, which clears only depth, so its input already holds the world): soft bloom of
    /// bright lamps / LEDs, then the "Styl obrazu" colour grade (one pass, <see cref="GfxSettings.EffectiveLook"/>), then
    /// FXAA or SMAA 1x. Hardware MSAA is a quality setting and needs nothing here.
    /// Disabled (no extra copy of the frame) when neither bloom, a look nor a post filter is on.
    /// </summary>
    [DisallowMultipleComponent]
    public class GfxPost : MonoBehaviour
    {
        private static Material _fxaa, _smaa, _bloom;
        /// <summary>Look shaders by <see cref="GfxSettings.LookNames"/> index (0 = none, stays null).</summary>
        private static readonly Material[] _looks = new Material[5];
        private static readonly string[] LookShaders = { null, "Shaders/HnsLookFilm", "Shaders/HnsLookCctv", "Shaders/HnsLookCold", "Shaders/HnsLookSodium" };
        private static Texture2D _area, _search;
        private static bool _loaded, _missing;
        private readonly RenderTexture[] _pyramid = new RenderTexture[8];

        private static readonly int AreaTexId = Shader.PropertyToID("_AreaTex");
        private static readonly int SearchTexId = Shader.PropertyToID("_SearchTex");
        private static readonly int BlendTexId = Shader.PropertyToID("_BlendTex");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        /// <summary>Bloom strength and HDR threshold (only parts brighter than white glow).</summary>
        public static float BloomIntensity = 0.2f, BloomThreshold = 1.5f, BloomKnee = 0.5f;

        public static bool Wanted => GfxSettings.PostFilter(GfxSettings.EffectiveAa) != 0 || GfxSettings.EffectiveBloom || GfxSettings.EffectiveLook != 0;

        /// <summary>True when the look shader <paramref name="look"/> loaded and runs on this GPU (0 = none is always ready).</summary>
        public static bool LookReady(int look)
        {
            Load();
            return look <= 0 || (look < _looks.Length && _looks[look] != null);
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _fxaa = Mat("Shaders/HnsFxaa");
            _smaa = Mat("Shaders/HnsSmaa");
            _bloom = Mat("Shaders/HnsBloom");
            for (int i = 1; i < _looks.Length; i++)
            {
                _looks[i] = Mat(LookShaders[i]);
                if (_looks[i] == null) Debug.LogWarning($"[Gfx] look shader missing or unsupported: {LookShaders[i]}");
            }
            var a = Resources.Load<TextAsset>("Gfx/SmaaArea");
            var s = Resources.Load<TextAsset>("Gfx/SmaaSearch");
            if (a != null && s != null)
            {
                _area = new Texture2D(160, 560, TextureFormat.RG16, false, true) { name = "SmaaArea", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                _area.LoadRawTextureData(a.bytes);
                _area.Apply(false, true);
                _search = new Texture2D(64, 16, TextureFormat.R8, false, true) { name = "SmaaSearch", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                _search.LoadRawTextureData(s.bytes);
                _search.Apply(false, true);
            }
            _missing = _fxaa == null || _smaa == null || _bloom == null || _area == null;
            if (_missing) Debug.LogWarning($"[Gfx] post shaders missing: fxaa {_fxaa != null}, smaa {_smaa != null}, bloom {_bloom != null}, lut {_area != null}");

            static Material Mat(string path)
            {
                var sh = Resources.Load<Shader>(path);
                return sh != null && sh.isSupported ? new Material(sh) { hideFlags = HideFlags.DontSave } : null;
            }
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst) => Process(src, dst, GfxSettings.EffectiveLook);

        /// <summary>
        /// The whole chain from a rendered frame to <paramref name="dst"/>: bloom, look <paramref name="look"/>, FXAA / SMAA
        /// (current settings). Also called directly by the -testlooks capture (batch mode renders no screen).
        /// </summary>
        public void Process(RenderTexture src, RenderTexture dst, int look)
        {
            Load();
            int filter = GfxSettings.PostFilter(GfxSettings.EffectiveAa);
            bool bloom = GfxSettings.EffectiveBloom && _bloom != null;
            Material lookMat = look > 0 && look < _looks.Length ? _looks[look] : null;
            RenderTexture cur = src, withBloom = null, graded = null;
            NanScan.Inspect(src, "frame");
            if (bloom)
            {
                withBloom = RenderTexture.GetTemporary(src.width, src.height, 0, src.format, RenderTextureReadWrite.Linear);
                Bloom(src, withBloom);
                cur = withBloom;
                NanScan.Inspect(withBloom, "bloomed");
            }
            if (lookMat != null)
            {
                // Display values 0..1 from here on; same format as the frame (8-bit sRGB or half float).
                graded = RenderTexture.GetTemporary(src.width, src.height, 0, src.format, RenderTextureReadWrite.Default);
                graded.filterMode = FilterMode.Bilinear;
                Graphics.Blit(cur, graded, lookMat, 0);
                cur = graded;
            }
            if (filter == 2 && _smaa != null && _area != null) Smaa(cur, dst);
            else if (filter == 1 && _fxaa != null) Graphics.Blit(cur, dst, _fxaa, 0);
            else Graphics.Blit(cur, dst);
            if (withBloom != null) RenderTexture.ReleaseTemporary(withBloom);
            if (graded != null) RenderTexture.ReleaseTemporary(graded);
        }

        private static void Smaa(RenderTexture src, RenderTexture dst)
        {
            int w = src.width, h = src.height;
            var edges = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.RG16, RenderTextureReadWrite.Linear);
            var weights = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            edges.filterMode = FilterMode.Bilinear;
            weights.filterMode = FilterMode.Bilinear;
            // The edge pass discards pixels without an edge: start from black.
            var prev = RenderTexture.active;
            RenderTexture.active = edges;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
            Graphics.Blit(src, edges, _smaa, 0);
            _smaa.SetTexture(AreaTexId, _area);
            _smaa.SetTexture(SearchTexId, _search);
            Graphics.Blit(edges, weights, _smaa, 1);
            _smaa.SetTexture(BlendTexId, weights);
            Graphics.Blit(src, dst, _smaa, 2);
            RenderTexture.ReleaseTemporary(edges);
            RenderTexture.ReleaseTemporary(weights);
        }

        private void Bloom(RenderTexture src, RenderTexture dst)
        {
            float knee = BloomThreshold * BloomKnee + 1e-5f;
            _bloom.SetVector(ThresholdId, new Vector4(BloomThreshold, BloomThreshold - knee, knee * 2f, 0.25f / knee));
            _bloom.SetFloat(IntensityId, BloomIntensity);
            var fmt = src.format == RenderTextureFormat.ARGBHalf || src.format == RenderTextureFormat.ARGBFloat || src.format == RenderTextureFormat.DefaultHDR
                ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            int w = src.width / 2, h = src.height / 2, n = 0;
            RenderTexture last = src;
            while (n < _pyramid.Length && w >= 8 && h >= 8)
            {
                _pyramid[n] = RenderTexture.GetTemporary(w, h, 0, fmt, RenderTextureReadWrite.Linear);
                _pyramid[n].filterMode = FilterMode.Bilinear;
                Graphics.Blit(last, _pyramid[n], _bloom, n == 0 ? 0 : 1);
                last = _pyramid[n];
                n++;
                w /= 2;
                h /= 2;
                if (n >= 6) break;
            }
            for (int i = n - 1; i > 0; i--) Graphics.Blit(_pyramid[i], _pyramid[i - 1], _bloom, 2);
            if (n > 0)
            {
                NanScan.Inspect(_pyramid[0], "pyramid");
                _bloom.SetTexture(BloomTexId, _pyramid[0]);
                Graphics.Blit(src, dst, _bloom, 3);
            }
            else Graphics.Blit(src, dst);
            for (int i = 0; i < n; i++)
            {
                RenderTexture.ReleaseTemporary(_pyramid[i]);
                _pyramid[i] = null;
            }
        }
    }
}
