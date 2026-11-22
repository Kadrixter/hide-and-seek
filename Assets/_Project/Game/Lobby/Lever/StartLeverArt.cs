using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Procedural textures of the start lever: hazard stripes and printed label plates (5x7 font).</summary>
    public static class StartLeverArt
    {
        static Texture2D hazard;

        /// <summary>Yellow / black 45 degree stripes, one stripe pair per tile (tile it along the strip).</summary>
        public static Texture2D Hazard
        {
            get
            {
                if (hazard != null) return hazard;
                const int n = 64;
                hazard = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "LeverHazard", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
                var yellow = new Color(0.95f, 0.72f, 0.08f);
                var black = new Color(0.05f, 0.05f, 0.05f);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool y0 = ((x + y) % n) < n / 2;
                    // Worn paint: a little noise on the edges.
                    float wear = Mathf.PerlinNoise(x * 0.31f, y * 0.29f) * 0.12f;
                    hazard.SetPixel(x, y, (y0 ? yellow : black) * (0.94f + wear));
                }
                hazard.Apply(true);
                return hazard;
            }
        }

        /// <summary>A label plate: text lines centred, <paramref name="ink"/> on <paramref name="paper"/>, with a border.</summary>
        public static Texture2D Label(string[] lines, int width, int height, int scale, Color paper, Color ink)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "LeverLabel", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color[width * height];
            for (int i = 0; i < px.Length; i++) px[i] = paper;
            void Plot(int x, int y) { if (x >= 0 && y >= 0 && x < width && y < height) px[y * width + x] = ink; }
            // Border inset by 3 px.
            for (int x = 3; x < width - 3; x++) for (int k = 0; k < 2; k++) { Plot(x, 3 + k); Plot(x, height - 4 - k); }
            for (int y = 3; y < height - 3; y++) for (int k = 0; k < 2; k++) { Plot(3 + k, y); Plot(width - 4 - k, y); }
            int lineH = (CanFont.H + 4) * scale;
            int total = lines.Length * lineH - 4 * scale;
            int top = height / 2 + total / 2;
            for (int i = 0; i < lines.Length; i++)
            {
                int w = CanFont.Width(lines[i], scale);
                int baseY = top - (i + 1) * lineH + 4 * scale;
                CanFont.Draw(lines[i], (width - w) / 2, baseY, scale, scale, Plot);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Big arrow pointing down, <paramref name="ink"/> on <paramref name="paper"/>.</summary>
        public static Texture2D ArrowDown(Color paper, Color ink)
        {
            const int w = 64, h = 96;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "LeverArrow", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool shaft = y > 40 && Mathf.Abs(x - w / 2) < 9;
                bool head = y <= 44 && Mathf.Abs(x - w / 2) < y * 0.7f + 1;
                tex.SetPixel(x, y, shaft || head ? ink : paper);
            }
            tex.Apply(true);
            return tex;
        }
    }
}
