using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Procedural textures of the lobby vending machine (no lettering): printed side panels, lit header, the light
    /// box behind the shelves, glass streaks, worn paint, contactless reader icon and the 7-segment price display.
    /// </summary>
    public static class VendingMachineArt
    {
        static Texture2D side, header, backWall, glass, paint, reader;

        static readonly Color Red = new Color(0.62f, 0.05f, 0.07f), DeepRed = new Color(0.3f, 0.02f, 0.04f);
        static readonly Color Cream = new Color(1f, 0.95f, 0.86f);

        static Texture2D Make(int w, int h, System.Func<int, int, Color> f, string name, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, anisoLevel = 4 };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; }

        /// <summary>Side panel print: red gradient, cream wave, rising bubbles, three big cans, light sweep, wear.</summary>
        public static Texture2D Side => side ??= Make(256, 512, (x, y) =>
        {
            float u = x / 255f, v = y / 511f;
            Color c = Color.Lerp(DeepRed, Red, Mathf.SmoothStep(0f, 1f, v * 0.9f + u * 0.25f));
            // Light sweep.
            float sweep = Mathf.Exp(-Mathf.Pow((u + v * 0.6f - 0.75f) * 5f, 2f));
            c = Color.Lerp(c, new Color(0.95f, 0.3f, 0.25f), sweep * 0.35f);
            // Wave band through the lower third.
            float w = 0.3f + 0.06f * Mathf.Sin(u * 7f + 0.8f);
            if (Mathf.Abs(v - w) < 0.035f) c = Cream;
            else if (Mathf.Abs(v - w + 0.06f) < 0.008f) c = Color.Lerp(Cream, c, 0.4f);
            // Three tilted cans (rounded rectangles) with a highlight stripe and a lid.
            for (int i = 0; i < 3; i++)
            {
                Vector2 ctr = new Vector2(0.28f + i * 0.24f, 0.55f + 0.08f * Mathf.Sin(i * 2.1f));
                float ang = (i - 1) * 0.22f;
                Vector2 d = new Vector2(u, v * 2f) - new Vector2(ctr.x, ctr.y * 2f);
                Vector2 q = new Vector2(d.x * Mathf.Cos(ang) + d.y * Mathf.Sin(ang), -d.x * Mathf.Sin(ang) + d.y * Mathf.Cos(ang));
                Vector2 e = new Vector2(Mathf.Abs(q.x) - 0.075f, Mathf.Abs(q.y) - 0.2f);
                float sd = new Vector2(Mathf.Max(e.x, 0f), Mathf.Max(e.y, 0f)).magnitude + Mathf.Min(Mathf.Max(e.x, e.y), 0f) - 0.03f;
                if (sd < 0f)
                {
                    Color body = i == 0 ? new Color(0.9f, 0.85f, 0.15f) : i == 1 ? new Color(0.15f, 0.55f, 0.95f) : new Color(0.95f, 0.45f, 0.08f);
                    float shade = 0.65f + 0.35f * Mathf.Cos((q.x / 0.105f) * 1.4f);
                    c = body * shade;
                    if (q.y > 0.17f) c = new Color(0.78f, 0.8f, 0.84f) * shade;      // lid / neck
                    if (Mathf.Abs(q.x + 0.04f) < 0.008f && q.y < 0.17f) c = Color.Lerp(c, Color.white, 0.6f); // highlight
                    if (Mathf.Abs(q.y) < 0.05f && q.y < 0.17f) c = Color.Lerp(c, Cream, 0.75f);               // label band
                    c.a = 1f;
                }
                else if (sd < 0.012f) c = Color.Lerp(c, Color.black, 0.5f);
            }
            // Bubbles.
            for (int b = 0; b < 26; b++)
            {
                Vector2 p = new Vector2(Hash(b, 1) * 256f, 60f + Hash(b, 2) * 440f);
                float r = 3f + Hash(b, 3) * 9f;
                float dd = Vector2.Distance(new Vector2(x, y), p);
                if (dd < r && dd > r - 1.8f) c = Color.Lerp(c, Cream, 0.8f);
                else if (dd < r * 0.35f && dd > 0f && x < p.x && y > p.y) c = Color.Lerp(c, Cream, 0.5f);
            }
            // Wear: speckles and a few scratches.
            float n = Hash(x, y);
            if (n > 0.996f) c = Color.Lerp(c, new Color(0.7f, 0.7f, 0.7f), 0.6f);
            if (Mathf.Abs((x + y * 0.31f) % 97f - 50f) < 0.5f && Hash(x / 9, y / 9) > 0.8f) c = Color.Lerp(c, Color.white, 0.25f);
            c.a = 1f;
            return c;
        }, "VendSideArt");

        /// <summary>Lit header (emissive): cool gradient, bubbles and a row of can silhouettes, no text.</summary>
        public static Texture2D Header => header ??= Make(512, 96, (x, y) =>
        {
            float u = x / 511f, v = y / 95f;
            Color c = Color.Lerp(new Color(0.05f, 0.25f, 0.65f), new Color(0.35f, 0.85f, 1f), v * 0.8f + 0.1f * Mathf.Sin(u * 9f));
            for (int i = 0; i < 9; i++)
            {
                float cx = 0.06f + i * 0.11f, cy = 0.5f + (i % 2 == 0 ? 0.04f : -0.04f);
                float dx = (u - cx) * 512f, dy = (v - cy) * 96f;
                if (Mathf.Abs(dx) < 11f && Mathf.Abs(dy) < 30f)
                {
                    float shade = 0.7f + 0.3f * Mathf.Cos(dx / 11f * 1.4f);
                    c = (dy > 24f ? new Color(0.85f, 0.88f, 0.92f) : Color.HSVToRGB(i / 9f, 0.75f, 1f)) * shade;
                    if (Mathf.Abs(dy) < 6f && dy < 24f) c = Color.Lerp(c, Cream, 0.8f);
                }
            }
            float b = Mathf.Sin(u * 80f + Mathf.Sin(v * 13f) * 3f);
            if (b > 0.985f && Hash(x / 6, y / 6) > 0.5f) c = Color.Lerp(c, Color.white, 0.6f);
            if (v < 0.06f || v > 0.94f) c = new Color(0.9f, 0.95f, 1f);
            c.a = 1f;
            return c;
        }, "VendHeaderArt");

        /// <summary>Light box behind the shelves: bright diffuser, falls off to the edges, faint vertical ribs.</summary>
        public static Texture2D BackWall => backWall ??= Make(128, 256, (x, y) =>
        {
            float u = x / 127f - 0.5f, v = y / 255f - 0.5f;
            float l = 1f - 0.55f * (u * u * 2.5f + v * v * 1.2f);
            l *= 0.94f + 0.06f * Mathf.Cos(x * 0.9f);
            return new Color(0.82f * l, 0.92f * l, 1f * l, 1f);
        }, "VendBackWall");

        /// <summary>Glass: R = diagonal wipe streaks, G = finger smudges (near the handle and at chest height).</summary>
        public static Texture2D Glass => glass ??= Make(256, 256, (x, y) =>
        {
            float u = x / 255f, v = y / 255f;
            float streak = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((u * 0.8f + v * 0.5f) * 9f + Mathf.Sin(v * 5f) * 0.8f), 6f) * Mathf.PerlinNoise(u * 4f, v * 3f) * 0.8f;
            float smudge = 0f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = new Vector2(0.75f + Hash(i, 7) * 0.22f, 0.35f + Hash(i, 8) * 0.3f);
                float d = Vector2.Distance(new Vector2(u, v), p);
                smudge += Mathf.Clamp01(1f - d / 0.035f) * (0.5f + 0.5f * Mathf.Sin(d * 600f));
            }
            return new Color(Mathf.Clamp01(streak), Mathf.Clamp01(smudge * 0.6f), 0f, 1f);
        }, "VendGlass", TextureWrapMode.Repeat);

        /// <summary>Paint wear for the cabinet: fine orange peel, light scuffs.</summary>
        public static Texture2D Paint => paint ??= Make(128, 128, (x, y) =>
        {
            float n = 0.93f + 0.07f * Hash(x, y) + 0.04f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
            if (Hash(x / 3, y / 17) > 0.985f) n += 0.12f;
            return new Color(n, n, n, 1f);
        }, "VendPaint", TextureWrapMode.Repeat);

        /// <summary>Contactless reader: black glass with the wave arcs.</summary>
        public static Texture2D Reader => reader ??= Make(64, 64, (x, y) =>
        {
            float dx = x - 18f, dy = y - 32f, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Abs(Mathf.Atan2(dy, dx));
            Color c = new Color(0.04f, 0.045f, 0.05f, 1f);
            if (a < 0.75f && dx > 0f)
                for (int i = 1; i <= 4; i++) if (Mathf.Abs(r - i * 9f) < 1.6f) c = new Color(0.85f, 0.88f, 0.9f, 1f);
            return c;
        }, "VendReader");

        // ------------------------------------------------------------------ 7-segment display

        static readonly int[] Seg = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

        /// <summary>Redraws a 4-digit 7-segment display; '-' = middle bar, ' ' = blank, '.' after a digit = decimal point.</summary>
        public static void DrawDisplay(Texture2D t, string text, Color on)
        {
            int w = t.width, h = t.height;
            var px = new Color[w * h];
            Color off = new Color(on.r * 0.02f, on.g * 0.03f, on.b * 0.02f, 1f), bg = new Color(0.01f, 0.03f, 0.015f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            int digit = 0;
            for (int i = 0; i < text.Length && digit < 4; i++)
            {
                char ch = text[i];
                if (ch == '.') continue;
                int mask = ch == '-' ? 0x40 : ch >= '0' && ch <= '9' ? Seg[ch - '0'] : 0;
                bool dot = i + 1 < text.Length && text[i + 1] == '.';
                int ox = 4 + digit * (w - 8) / 4, dw = (w - 8) / 4 - 5, dh = h - 6, oy = 3;
                void Bar(int seg, int x0, int y0, int x1, int y1)
                {
                    Color c = (mask & (1 << seg)) != 0 ? on : off;
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) if (x >= 0 && y >= 0 && x < w && y < h) px[y * w + x] = c;
                }
                int t2 = 2, mid = oy + dh / 2;
                Bar(0, ox + t2, oy + dh - t2, ox + dw - t2, oy + dh);          // a
                Bar(1, ox + dw - t2, mid + 1, ox + dw, oy + dh - t2 - 1);      // b
                Bar(2, ox + dw - t2, oy + t2 + 1, ox + dw, mid - 1);           // c
                Bar(3, ox + t2, oy, ox + dw - t2, oy + t2);                    // d
                Bar(4, ox, oy + t2 + 1, ox + t2, mid - 1);                     // e
                Bar(5, ox, mid + 1, ox + t2, oy + dh - t2 - 1);                // f
                Bar(6, ox + t2, mid - 1, ox + dw - t2, mid + 1);               // g
                Color dc = dot ? on : off;
                for (int y = oy; y < oy + 2; y++) for (int x = ox + dw + 2; x < ox + dw + 4 && x < w; x++) px[y * w + x] = dc;
                digit++;
            }
            t.SetPixels(px);
            t.Apply(false);
        }
    }
}
