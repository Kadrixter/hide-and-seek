using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Procedural textures of the bead score counter: the felt panel behind the wires with a gold scale (ticks under
    /// every bead, 5 / 10 / 15), the engraved brass "8-BALL" plaque and the 8-ball medallion on the crest. Made once.
    /// </summary>
    internal static class PoolScoreArt
    {
        static Texture2D _panel, _plaque, _medal;

        public static Texture2D Panel => _panel != null ? _panel : (_panel = MakePanel());
        public static Texture2D Plaque => _plaque != null ? _plaque : (_plaque = MakePlaque());
        public static Texture2D Medal => _medal != null ? _medal : (_medal = MakeMedal());

        static Texture2D New(int w, int h, string name) =>
            new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        /// <summary>Panel texture: u = 0..1 over x = +PanelHalfW..-PanelHalfW (left to right seen from the room), v = 0..1 over y = -PanelHalfH..PanelHalfH.</summary>
        static Texture2D MakePanel()
        {
            const int w = 1024;
            int h = Mathf.RoundToInt(w * PoolScoreModel.PanelHalfH / PoolScoreModel.PanelHalfW);
            var t = New(w, h, "ScorePanel");
            var px = new Color[w * h];
            Color felt = new Color(0.07f, 0.2f, 0.12f), gold = new Color(0.86f, 0.7f, 0.38f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    float n = Mathf.PerlinNoise(u * 300f, v * 70f) * 0.5f + Mathf.PerlinNoise(u * 900f + 3f, v * 210f) * 0.5f;
                    px[y * w + x] = felt * (0.85f + 0.3f * n);
                }
            int X(float mx) => Mathf.RoundToInt((0.5f - mx / PoolScoreModel.PanelHalfW * 0.5f) * w); // u runs towards -X
            int Y(float my) => Mathf.RoundToInt((my / PoolScoreModel.PanelHalfH * 0.5f + 0.5f) * h);
            // Double pinstripe border.
            Box(px, w, h, 10, 10, w - 11, h - 11, 2, gold, 0.9f);
            Box(px, w, h, 16, 16, w - 17, h - 17, 1, gold, 0.6f);
            for (int wire = 0; wire < PoolScoreModel.Wires; wire++)
            {
                int wy = Y(PoolScoreModel.WireY(wire));
                for (int k = 1; k <= PoolScoreModel.BeadsPerWire; k++)
                {
                    int cx = X(PoolScoreModel.ScoredX(k - 1));
                    bool big = k % 5 == 0;
                    int len = big ? 10 : 5;
                    Fill(px, w, h, cx - 1, wy - 25 - len, cx + 1, wy - 25, gold, 0.95f);
                    if (big) Text(px, w, h, k.ToString(), cx, wy - 25 - len - 18, 3, gold);
                }
                Text(px, w, h, wire == 0 ? "1" : "2", X(PoolScoreModel.PanelHalfW - 0.016f), wy + 33, 3, gold);
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakePlaque()
        {
            const int w = 512, h = 128;
            var t = New(w, h, "ScorePlaque");
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Brushed brass: fine horizontal streaks.
                    float s = Mathf.PerlinNoise(x * 0.02f, y * 1.7f) * 0.5f + Mathf.PerlinNoise(x * 0.06f + 9f, y * 3.1f) * 0.5f;
                    float g = 0.9f + 0.12f * s;
                    px[y * w + x] = new Color(g, g, g, 1f);
                }
            Color ink = new Color(0.16f, 0.12f, 0.08f);
            Box(px, w, h, 10, 10, w - 11, h - 11, 3, ink, 0.85f);
            Box(px, w, h, 18, 18, w - 19, h - 19, 1, ink, 0.6f);
            Text(px, w, h, "8-BALL", w / 2, h / 2, 10, ink);
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeMedal()
        {
            const int n = 256;
            var t = New(n, n, "ScoreMedal");
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    // Black ball with a soft highlight, the white circle in the middle (slightly up).
                    float hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.16f, 0.18f)) / 0.22f);
                    Color c = new Color(0.03f, 0.03f, 0.035f) + Color.white * (hl * hl * 0.35f);
                    float rc = Mathf.Sqrt(dx * dx + (dy - 0.02f) * (dy - 0.02f));
                    float a = Mathf.Clamp01((0.2f - rc) * n * 0.7f);
                    c = Color.Lerp(c, new Color(0.95f, 0.94f, 0.9f), a);
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            Text(px, n, n, "8", n / 2, n / 2 + 5, 11, new Color(0.03f, 0.03f, 0.035f));
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        // ================================================================ drawing

        static void Fill(Color[] px, int w, int h, int x0, int y0, int x1, int y1, Color c, float a)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                    px[y * w + x] = Color.Lerp(px[y * w + x], c, a);
        }

        static void Box(Color[] px, int w, int h, int x0, int y0, int x1, int y1, int th, Color c, float a)
        {
            Fill(px, w, h, x0, y0, x1, y0 + th - 1, c, a);
            Fill(px, w, h, x0, y1 - th + 1, x1, y1, c, a);
            Fill(px, w, h, x0, y0 + th, x0 + th - 1, y1 - th, c, a);
            Fill(px, w, h, x1 - th + 1, y0 + th, x1, y1 - th, c, a);
        }

        /// <summary><see cref="CanFont"/> text centred on (cx, cy) (y up), <paramref name="dot"/> px per font pixel, soft rounded dots.</summary>
        static void Text(Color[] px, int w, int h, string s, int cx, int cy, int dot, Color ink)
        {
            int tw = CanFont.Width(s, dot), th = CanFont.H * dot;
            int left = cx - tw / 2, top = cy + th / 2;
            for (int i = 0; i < s.Length; i++)
            {
                int gx0 = left + i * (CanFont.W + 1) * dot;
                for (int y = -2; y < th + 2; y++)
                    for (int x = -2; x < CanFont.W * dot + 2; x++)
                    {
                        float cov = 0f;
                        for (int gy = 0; gy < CanFont.H; gy++)
                            for (int gx = 0; gx < CanFont.W; gx++)
                            {
                                if (!CanFont.On(s[i], gx, gy)) continue;
                                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - (gx + 0.5f) * dot) - dot * 0.5f);
                                float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - (gy + 0.5f) * dot) - dot * 0.5f);
                                cov = Mathf.Max(cov, 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.2f));
                            }
                        if (cov <= 0f) continue;
                        int ppx = gx0 + x, ppy = top - y;
                        if (ppx < 0 || ppy < 0 || ppx >= w || ppy >= h) continue;
                        int k = ppy * w + ppx;
                        px[k] = Color.Lerp(px[k], ink, cov * 0.95f);
                    }
            }
        }
    }
}
