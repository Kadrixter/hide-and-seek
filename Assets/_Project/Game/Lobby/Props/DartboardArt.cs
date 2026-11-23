using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Dartboard face texture (sisal board: black / cream segments, red / green doubles and trebles, wire, numbers ring).</summary>
    public static class DartboardArt
    {
        private static Texture2D _tex;

        public static Texture2D Make()
        {
            if (_tex != null) return _tex;
            const int n = 512;
            var px = new Color32[n * n];
            Color black = new(0.07f, 0.07f, 0.07f), cream = new(0.9f, 0.84f, 0.68f), red = new(0.75f, 0.1f, 0.08f), green = new(0.08f, 0.45f, 0.2f);
            Color wire = new(0.75f, 0.75f, 0.72f), surround = new(0.06f, 0.06f, 0.06f);
            // quad covers 2 * BoardR; regulation radii scaled to it
            float R = LobbyDarts.BoardR;
            float[] rings = { 0.00635f, 0.0159f, 0.099f, 0.107f, 0.162f, 0.170f };
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // texture u grows to the viewer's right (the quad is mirrored by the builder's placement)
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) * R;
                float a = Mathf.Atan2(u, v) * Mathf.Rad2Deg; // clockwise from the top
                int seg = Mathf.FloorToInt(Mathf.Repeat(a + 9f, 360f) / 18f) % 20;
                bool even = seg % 2 == 0;
                Color c;
                if (r > R) c = new Color(0, 0, 0, 0);
                else if (r <= rings[0]) c = red;
                else if (r <= rings[1]) c = green;
                else if (r > rings[5]) c = surround;
                else if (r >= rings[2] && r <= rings[3] || r >= rings[4]) c = even ? red : green;
                else c = even ? black : cream;
                // sisal fibre noise
                float fib = 0.92f + 0.08f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                c = new Color(c.r * fib, c.g * fib, c.b * fib, c.a);
                // spider wire: rings and segment lines
                float edgeA = Mathf.Abs(Mathf.Repeat(a + 9f, 18f) - 9f) - 9f; // 0 at a segment border
                float px1 = R * 2f / n;
                bool onRing = false;
                foreach (var rr in rings) if (Mathf.Abs(r - rr) < px1 * 0.8f) onRing = true;
                bool onLine = r > rings[1] && r < rings[5] && Mathf.Abs(edgeA) * Mathf.Deg2Rad * r < px1 * 0.7f;
                if (r <= rings[5] && (onRing || onLine)) c = wire;
                px[y * n + x] = c;
            }
            _tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Dartboard", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            _tex.SetPixels32(px);
            DrawNumbers(_tex, n, R);
            _tex.Apply(true);
            return _tex;
        }

        // 3x5 digit font for the number ring
        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111",
        };

        private static void DrawNumbers(Texture2D t, int n, float R)
        {
            int[] nums = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };
            float rr = 0.197f / R; // middle of the outer ring (0.17 .. 0.225)
            const int s = 4;      // pixel size of the font
            for (int i = 0; i < 20; i++)
            {
                float a = i * 18f * Mathf.Deg2Rad;
                int cx = (int)((Mathf.Sin(a) * rr * 0.5f + 0.5f) * n), cy = (int)((Mathf.Cos(a) * rr * 0.5f + 0.5f) * n);
                string str = nums[i].ToString();
                int w = str.Length * 4 * s - s;
                for (int k = 0; k < str.Length; k++)
                {
                    string g = Digits[str[k] - '0'];
                    for (int gy = 0; gy < 5; gy++)
                    for (int gx = 0; gx < 3; gx++)
                    {
                        if (g[gy * 3 + gx] != '1') continue;
                        for (int py = 0; py < s; py++)
                        for (int pxx = 0; pxx < s; pxx++)
                        {
                            int x = cx - w / 2 + k * 4 * s + gx * s + pxx, y = cy + 10 - gy * s - py;
                            if (x >= 0 && y >= 0 && x < n && y < n) t.SetPixel(x, y, new Color(0.92f, 0.92f, 0.88f));
                        }
                    }
                }
            }
        }
    }
}
