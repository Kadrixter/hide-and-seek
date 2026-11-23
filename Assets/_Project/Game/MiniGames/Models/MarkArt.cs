using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Alpha masks of the board highlights (white RGB, shape in alpha): soft square, glow, dot, ring.</summary>
    public static class MarkArt
    {
        static Texture2D _square, _glow, _dot, _ring;

        public static Texture2D SoftSquare => _square != null ? _square : (_square = Make("MarkSquare", (x, y) =>
        {
            float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            float edge = Mathf.Clamp01((1f - d) / 0.12f);
            float inner = 0.55f + 0.45f * Mathf.Clamp01((d - 0.55f) / 0.4f);
            return edge * inner;
        }));

        public static Texture2D Glow => _glow != null ? _glow : (_glow = Make("MarkGlow", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
        }));

        public static Texture2D Dot => _dot != null ? _dot : (_dot = Make("MarkDot", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((0.3f - r) / 0.05f);
        }));

        public static Texture2D Ring => _ring != null ? _ring : (_ring = Make("MarkRing", (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((0.12f - Mathf.Abs(r - 0.8f)) / 0.04f);
        }));

        static Texture2D Make(string name, System.Func<float, float, float> alpha)
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f));
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
