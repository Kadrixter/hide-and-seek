using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Procedural textures of the board-game tables (made once, kept for the whole session): wood grain (neutral, tinted
    /// by the material colour), the chess board (maple / walnut squares with alternating grain, inlay line, frame with
    /// a-h / 1-8), the tic-tac-toe slate with the chalk grid, a stool leather.
    /// </summary>
    public static class BoardArt
    {
        /// <summary>Board texture layout: 64 px frame + 8 x 112 px squares + 64 px frame = 1024 px.</summary>
        public const int BoardPx = 1024, FramePx = 64, SquarePx = 112;
        public const float SquareSize = 0.08f;
        public const float BoardSize = BoardPx * SquareSize / SquarePx;

        static Texture2D _grainFine, _grainTable, _board, _boardAlt, _slate, _leather, _felt;

        public static Texture2D GrainFine => _grainFine != null ? _grainFine : (_grainFine = Grain(256, 9f, 0.16f, 11));
        public static Texture2D GrainTable => _grainTable != null ? _grainTable : (_grainTable = Grain(512, 14f, 0.22f, 23));
        public static Texture2D Board => _board != null ? _board : (_board = MakeBoard(false));
        public static Texture2D BoardAlt => _boardAlt != null ? _boardAlt : (_boardAlt = MakeBoard(true));
        public static Texture2D Slate => _slate != null ? _slate : (_slate = MakeSlate());
        public static Texture2D Leather => _leather != null ? _leather : (_leather = MakeLeather());
        public static Texture2D Felt => _felt != null ? _felt : (_felt = Grain(64, 2f, 0.05f, 5));

        static Texture2D New(int w, int h, string name, bool mips = true) =>
            new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>Grain value 0..1 at (x along the grain, y across it): growth rings bent by noise plus fine pores.</summary>
        public static float GrainAt(float x, float y, float rings, int seed)
        {
            float warp = Mathf.PerlinNoise(x * 1.3f + seed, y * 4f + seed * 0.7f) * 2.2f + Mathf.PerlinNoise(x * 5f, y * 9f + seed) * 0.35f;
            float r = Frac(y * rings + warp);
            float line = Mathf.SmoothStep(0f, 1f, Mathf.Abs(r - 0.5f) * 2f);
            line = Mathf.Pow(line, 6f);
            float pores = Mathf.PerlinNoise(x * 60f + seed, y * 220f) * 0.5f + Mathf.PerlinNoise(x * 140f, y * 500f + seed) * 0.5f;
            return Mathf.Clamp01(0.82f - line * 0.38f - (pores - 0.5f) * 0.22f + Mathf.PerlinNoise(x * 0.8f + seed, y * 0.8f) * 0.18f);
        }

        static Texture2D Grain(int size, float rings, float contrast, int seed)
        {
            var t = New(size, size, "BoardGrain" + size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float g = GrainAt(x / (float)size, y / (float)size, rings, seed);
                    float v = 1f - contrast + contrast * g / 0.82f;
                    byte b = (byte)(Mathf.Clamp01(v) * 255f);
                    px[y * size + x] = new Color32(b, (byte)(b * 0.985f), (byte)(b * 0.97f), 255);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        // ================================================================ chess board

        static Texture2D MakeBoard(bool alt)
        {
            int n = BoardPx;
            var t = New(n, n, alt ? "ChessBoardAlt" : "ChessBoard");
            var px = new Color[n * n];
            Color light = alt ? new Color(0.90f, 0.86f, 0.74f) : new Color(0.87f, 0.72f, 0.50f);
            Color dark = alt ? new Color(0.30f, 0.42f, 0.30f) : new Color(0.40f, 0.24f, 0.13f);
            Color frame = alt ? new Color(0.20f, 0.12f, 0.08f) : new Color(0.25f, 0.14f, 0.08f);
            Color inlay = alt ? new Color(0.82f, 0.70f, 0.42f) : new Color(0.93f, 0.82f, 0.60f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int bx = x - FramePx, by = y - FramePx;
                    Color c;
                    if (bx >= 0 && by >= 0 && bx < 8 * SquarePx && by < 8 * SquarePx)
                    {
                        int f = bx / SquarePx, r = by / SquarePx;
                        bool isDark = ((f + r) & 1) == 0;
                        bool across = ((f + r) & 1) == 1; // alternate grain direction square by square
                        float u = x / (float)n, v = y / (float)n;
                        float g = across ? GrainAt(v * 3f + f * 0.37f, u * 0.9f + r * 0.21f, 11f, 3 + f * 8 + r)
                                         : GrainAt(u * 3f + r * 0.41f, v * 0.9f + f * 0.17f, 11f, 70 + f * 8 + r);
                        c = (isDark ? dark : light) * (0.78f + 0.3f * g);
                        // Hairline joints between the squares.
                        int lx = bx % SquarePx, ly = by % SquarePx;
                        if (lx == 0 || ly == 0) c *= 0.8f;
                    }
                    else
                    {
                        float u = x / (float)n, v = y / (float)n;
                        bool horizontal = Mathf.Min(y, n - 1 - y) < Mathf.Min(x, n - 1 - x);
                        float g = GrainAt(horizontal ? u * 2f : v * 2f, horizontal ? v * 0.6f : u * 0.6f, 9f, 41);
                        c = frame * (0.75f + 0.35f * g);
                        // Inlay strip around the playing field.
                        int d = Mathf.Max(Mathf.Max(-bx, -by), Mathf.Max(bx - 8 * SquarePx + 1, by - 8 * SquarePx + 1));
                        if (d >= 6 && d <= 10) c = inlay * (0.85f + 0.2f * g);
                        // Mitred corners: a darker joint along the diagonals.
                        if (Mathf.Abs(Mathf.Min(x, n - 1 - x) - Mathf.Min(y, n - 1 - y)) < 1 && Mathf.Min(x, n - 1 - x) < FramePx - 10) c *= 0.75f;
                    }
                    c.a = 1f;
                    px[y * n + x] = c;
                }
            // Coordinates on the frame: readable from seat A (bottom / left) and turned round for seat B (top / right).
            Color ink = alt ? new Color(0.85f, 0.74f, 0.45f) : new Color(0.92f, 0.82f, 0.58f);
            for (int i = 0; i < 8; i++)
            {
                int cx = FramePx + i * SquarePx + SquarePx / 2;
                Glyph(px, n, (char)('A' + i), cx, FramePx / 2 - 6, ink, false);
                Glyph(px, n, (char)('A' + i), cx, n - FramePx / 2 + 6, ink, true);
                int cy = FramePx + i * SquarePx + SquarePx / 2;
                Glyph(px, n, (char)('1' + i), FramePx / 2 - 6, cy, ink, false);
                Glyph(px, n, (char)('1' + i), n - FramePx / 2 + 6, cy, ink, true);
            }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        /// <summary>5x7 glyph, 4 px per dot, soft edges, centred on (cx, cy); <paramref name="flip"/> turns it 180 degrees.</summary>
        static void Glyph(Color[] px, int n, char ch, int cx, int cy, Color ink, bool flip)
        {
            const int s = 4;
            int w = CanFont.W * s, h = CanFont.H * s;
            for (int y = -2; y < h + 2; y++)
                for (int x = -2; x < w + 2; x++)
                {
                    // Distance-ish coverage from the 4 nearest dots for smooth, slightly engraved strokes.
                    float cov = 0f;
                    for (int gy = 0; gy < CanFont.H; gy++)
                        for (int gx = 0; gx < CanFont.W; gx++)
                        {
                            if (!CanFont.On(char.ToUpperInvariant(ch), gx, gy)) continue;
                            float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - (gx + 0.5f) * s) - s * 0.5f);
                            float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - (gy + 0.5f) * s) - s * 0.5f);
                            cov = Mathf.Max(cov, 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.4f));
                        }
                    if (cov <= 0f) continue;
                    int px0 = flip ? cx + w / 2 - x : cx - w / 2 + x;
                    int py0 = flip ? cy - h / 2 + y : cy + h / 2 - y;
                    if (px0 < 0 || py0 < 0 || px0 >= n || py0 >= n) continue;
                    int i = py0 * n + px0;
                    px[i] = Color.Lerp(px[i], ink, cov * 0.9f);
                }
        }

        // ================================================================ slate, leather

        public const int SlatePx = 512;

        static Texture2D MakeSlate() => MakeSlate(SlatePx, SlatePx);

        static Texture2D _slateSized;

        /// <summary>Slate background of a given size (made once).</summary>
        public static Texture2D SlateSized(int w, int h)
        {
            if (_slateSized == null || _slateSized.width != w || _slateSized.height != h) _slateSized = MakeSlate(w, h);
            return _slateSized;
        }

        static Texture2D MakeSlate(int w, int h)
        {
            var t = New(w, h, "TttSlate");
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)w;
                    float g = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.5f + Mathf.PerlinNoise(u * 40f, v * 40f) * 0.25f + Mathf.PerlinNoise(u * 150f, v * 150f) * 0.25f;
                    // Old wiped chalk: pale cloudy smears.
                    float smear = Mathf.Clamp01(Mathf.PerlinNoise(u * 3f + 7f, v * 1.4f) * 1.6f - 0.85f) * 0.16f;
                    px[y * w + x] = Color.Lerp(new Color(0.11f, 0.13f, 0.13f) * (0.85f + 0.3f * g), new Color(0.75f, 0.76f, 0.74f), smear);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }

        static Texture2D MakeLeather()
        {
            int n = 256;
            var t = New(n, n, "StoolLeather");
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n;
                    float cell = Mathf.PerlinNoise(u * 48f, v * 48f), fine = Mathf.PerlinNoise(u * 160f + 3f, v * 160f);
                    float g = 0.86f + (cell - 0.5f) * 0.18f + (fine - 0.5f) * 0.08f;
                    px[y * n + x] = new Color(g, g, g, 1f);
                }
            t.SetPixels(px);
            t.Apply(true);
            return t;
        }
    }
}
