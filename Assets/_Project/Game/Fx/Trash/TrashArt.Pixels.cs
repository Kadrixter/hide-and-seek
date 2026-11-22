using System.Threading.Tasks;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The bin / bag / paper textures take ~0.5 s of maths: it runs on worker threads, started when the game boots
    /// (<see cref="Prewarm"/>), so the lobby never waits for it; the main thread only uploads the finished pixels.
    /// </summary>
    internal static partial class TrashArt
    {
        sealed class Pix
        {
            public Color32[] Px;
            public int W, H;
            public bool Linear;
            public string Name;
        }

        static Pix _metalP, _metalNP, _bagP, _bagNP, _paperP, _paperNP;
        static Texture2D _metal, _metalN, _bag, _bagN, _paper, _paperN;
        static Task _job;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Prewarm()
        {
            if (_job != null) return;
            _job = Task.Run(() =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    MakeMetal();
                    MakeBag();
                    MakePaper();
                    Debug.Log($"[TrashBin] textures computed in the background in {sw.ElapsedMilliseconds} ms");
                }
                catch (System.Exception e) { Debug.LogException(e); }
            });
        }

        /// <summary>True when the pixels are computed (or failed: then the materials go without textures).</summary>
        public static bool Ready { get { Prewarm(); return _job.IsCompleted; } }

        public static Texture2D Metal => Get(ref _metal, ref _metalP);
        public static Texture2D MetalNormal => Get(ref _metalN, ref _metalNP);
        public static Texture2D Bag => Get(ref _bag, ref _bagP);
        public static Texture2D BagNormal => Get(ref _bagN, ref _bagNP);
        public static Texture2D Paper => Get(ref _paper, ref _paperP);
        public static Texture2D PaperNormal => Get(ref _paperN, ref _paperNP);

        static Texture2D Get(ref Texture2D tex, ref Pix p)
        {
            if (tex != null) return tex;
            if (!Ready) _job.Wait();
            if (p == null) return null;
            tex = new Texture2D(p.W, p.H, TextureFormat.RGBA32, true, p.Linear)
            {
                name = p.Name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8,
            };
            tex.SetPixels32(p.Px);
            tex.Apply(true, true);
            p = null; // pixels live on the GPU now
            return tex;
        }

        static Pix Albedo(Color32[] px, int w, int h, string name) => new Pix { Px = px, W = w, H = h, Name = name };

        /// <summary>Tangent-space normal map from a height field (wraps in x; clamps in y unless <paramref name="wrapY"/>).</summary>
        static Pix NormalFrom(float[] hgt, int w, int h, float bump, string name, bool wrapY)
        {
            var px = new Color32[w * h];
            Rows(h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int yd = wrapY ? (y + h - 1) % h : Mathf.Max(0, y - 1), yu = wrapY ? (y + 1) % h : Mathf.Min(h - 1, y + 1);
                    float l = hgt[y * w + (x + w - 1) % w], r = hgt[y * w + (x + 1) % w];
                    float d = hgt[yd * w + x], u = hgt[yu * w + x];
                    var v = new Vector3((l - r) * bump, (d - u) * bump, 1f).normalized;
                    px[y * w + x] = new Color32((byte)((v.x * 0.5f + 0.5f) * 255f), (byte)((v.y * 0.5f + 0.5f) * 255f), (byte)((v.z * 0.5f + 0.5f) * 255f), 255);
                }
            });
            return new Pix { Px = px, W = w, H = h, Linear = true, Name = name };
        }
    }
}
