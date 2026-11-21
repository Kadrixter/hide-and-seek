using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural soda can artwork: 8 made-up brands, each with its own colours, background pattern, logo,
    /// back panel (ingredients, deposit mark, barcode) and an embossed height map turned into a normal map.
    /// Label UV: u goes round the can (front centred at u = 0.75, i.e. facing local -Z), v goes up.
    /// </summary>
    public static class SodaCanArt
    {
        public const int BrandCount = 8;
        const int LW = 512, LH = 256, LidN = 512;

        struct Brand
        {
            public string Name, Sub, Flavour;
            public Color Bg, Bg2, Ink, Accent;
        }

        static readonly Brand[] Brands =
        {
            new Brand { Name = "FIZZ", Sub = "COLA", Flavour = "KLASYCZNA", Bg = C(0.78f, 0.06f, 0.07f), Bg2 = C(0.5f, 0.02f, 0.04f), Ink = C(1f, 0.97f, 0.92f), Accent = C(0.1f, 0.08f, 0.08f) },
            new Brand { Name = "LEMONIX", Sub = "CYTRYNA", Flavour = "Z LIMONKĄ", Bg = C(0.98f, 0.86f, 0.12f), Bg2 = C(0.55f, 0.8f, 0.12f), Ink = C(0.1f, 0.32f, 0.08f), Accent = C(1f, 1f, 0.85f) },
            new Brand { Name = "NOCNA", Sub = "ZMIANA", Flavour = "ENERGY 32MG", Bg = C(0.05f, 0.05f, 0.06f), Bg2 = C(0.12f, 0.13f, 0.14f), Ink = C(0.45f, 1f, 0.2f), Accent = C(0.45f, 1f, 0.2f) },
            new Brand { Name = "MALINA", Sub = "GAZOWANA", Flavour = "OWOCOWA", Bg = C(0.92f, 0.3f, 0.55f), Bg2 = C(0.6f, 0.08f, 0.3f), Ink = C(1f, 1f, 1f), Accent = C(1f, 0.75f, 0.85f) },
            new Brand { Name = "ORANŻ", Sub = "SŁOŃCE", Flavour = "POMARAŃCZA", Bg = C(1f, 0.52f, 0.06f), Bg2 = C(0.95f, 0.78f, 0.12f), Ink = C(0.35f, 0.1f, 0.02f), Accent = C(1f, 0.9f, 0.55f) },
            new Brand { Name = "MRÓZ", Sub = "ICE MINT", Flavour = "MIĘTA LODOWA", Bg = C(0.55f, 0.88f, 0.95f), Bg2 = C(0.1f, 0.42f, 0.7f), Ink = C(1f, 1f, 1f), Accent = C(0.85f, 0.97f, 1f) },
            new Brand { Name = "KWAS", Sub = "CHLEBOWY", Flavour = "TRADYCYJNY", Bg = C(0.42f, 0.24f, 0.1f), Bg2 = C(0.25f, 0.13f, 0.05f), Ink = C(0.98f, 0.82f, 0.4f), Accent = C(0.85f, 0.62f, 0.2f) },
            new Brand { Name = "TURBO", Sub = "WINOGRONO", Flavour = "BEZ CUKRU", Bg = C(0.42f, 0.14f, 0.7f), Bg2 = C(0.2f, 0.05f, 0.38f), Ink = C(1f, 0.92f, 0.2f), Accent = C(1f, 1f, 1f) },
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        static Texture2D[] labels = new Texture2D[BrandCount], normals = new Texture2D[BrandCount];
        static Texture2D lid, lidNormal;

        /// <summary>Main colour of a brand (for the shelf tint and the thrown can).</summary>
        public static Color BrandColor(int brand) => Brands[Mod(brand)].Bg;

        static int Mod(int b) => ((b % BrandCount) + BrandCount) % BrandCount;

        public static Texture2D Label(int brand) { Ensure(brand); return labels[Mod(brand)]; }
        public static Texture2D LabelNormal(int brand) { Ensure(brand); return normals[Mod(brand)]; }

        public static Texture2D Lid { get { EnsureLid(); return lid; } }
        public static Texture2D LidNormal { get { EnsureLid(); return lidNormal; } }

        // ------------------------------------------------------------------ label

        static void Ensure(int brand)
        {
            int b = Mod(brand);
            if (labels[b] != null) return;
            var br = Brands[b];
            var px = new Color[LW * LH];
            var h = new float[LW * LH];
            var rng = new System.Random(1234 + b * 77);

            // Background pattern.
            for (int y = 0; y < LH; y++)
            for (int x = 0; x < LW; x++)
                px[y * LW + x] = Pattern(b, br, x, y);

            void Plot(int x, int y, Color c, float height)
            {
                x = ((x % LW) + LW) % LW;
                if (y < 0 || y >= LH) return;
                px[y * LW + x] = c;
                h[y * LW + x] = Mathf.Max(h[y * LW + x], height);
            }
            void Rect(int x0, int y0, int x1, int y1, Color c, float height)
            {
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Plot(x, y, c, height);
            }
            void Text(string s, int cx, int y, int sx, int sy, Color c, float height, Color? outline = null)
            {
                int x = cx - CanFont.Width(s, sx) / 2;
                if (outline.HasValue)
                    for (int ox = -1; ox <= 1; ox++) for (int oy = -1; oy <= 1; oy++)
                        CanFont.Draw(s, x + ox * Mathf.Max(1, sx / 2), y + oy * Mathf.Max(1, sy / 3), sx, sy, (a, bb) => Plot(a, bb, outline.Value, height * 0.6f));
                CanFont.Draw(s, x, y, sx, sy, (a, bb) => Plot(a, bb, c, height));
            }

            // Unprinted rims (the can's own aluminium band shows through a thin dark line).
            Rect(0, 0, LW, 10, br.Bg2 * 0.75f, 0f);
            Rect(0, LH - 10, LW, LH, br.Bg2 * 0.75f, 0f);
            Rect(0, 10, LW, 12, br.Accent, 0.3f);
            Rect(0, LH - 12, LW, LH - 10, br.Accent, 0.3f);

            // Front (u = 0.75): logo, embossed.
            int front = LW * 3 / 4;
            int sxName = Mathf.Clamp(150 / Mathf.Max(1, CanFont.Width(br.Name, 1)), 2, 5);
            Text(br.Name, front, 124, sxName, sxName * 2 + 2, br.Ink, 1f, br.Accent * 0.35f + Color.black * 0.65f);
            Text(br.Sub, front, 90, 2, 3, br.Ink, 0.7f);
            Rect(front - 48, 84, front + 48, 86, br.Ink, 0.5f);
            Text(br.Flavour, front, 64, 1, 2, br.Accent, 0.4f);
            Text("330 ML", front, 24, 2, 2, br.Ink, 0.6f);
            // Small round seal "NOWOSC" / "ORYGINAL" next to the logo.
            int sealX = front - 72, sealY = 44;
            for (int y = -16; y <= 16; y++) for (int x = -16; x <= 16; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d <= 16f) Plot(sealX + x, sealY + y, d > 13.5f ? br.Ink : br.Accent, d > 13.5f ? 0.8f : 0.5f);
            }
            Text(b % 2 == 0 ? "NOWY" : "TOP", sealX, sealY - 3, 1, 1, br.Bg2, 0.6f);

            // Side: vertical brand repeat (both sides of the front).
            for (int s = -1; s <= 1; s += 2)
            {
                int sx = front + s * 112;
                for (int i = 0; i < br.Name.Length; i++)
                {
                    string ch = br.Name[i].ToString();
                    Text(ch, sx, 200 - i * 22, 2, 2, br.Accent, 0.35f);
                }
            }

            // Back (u = 0.25): ingredients panel, deposit mark, barcode.
            int back = LW / 4;
            Color paper = new Color(0.96f, 0.95f, 0.92f), dark = new Color(0.12f, 0.12f, 0.13f);
            Rect(back - 62, 58, back + 62, 214, paper, 0.15f);
            Rect(back - 62, 212, back + 62, 214, dark, 0.15f);
            Text("SKŁAD:", back - 34, 200, 1, 1, dark, 0.2f);
            for (int line = 0; line < 7; line++)
            {
                int x = back - 56;
                while (x < back + 54)
                {
                    int w = rng.Next(4, 14);
                    if (x + w > back + 56) w = back + 56 - x;
                    Rect(x, 188 - line * 7, x + w, 191 - line * 7, dark * 1.6f, 0.15f);
                    x += w + rng.Next(2, 4);
                }
            }
            Text("100 ML: " + (b == 2 ? "46" : (38 + b * 3).ToString()) + " KCAL", back, 128, 1, 1, dark, 0.2f);
            // Deposit (Polish can deposit): round arrows + KAUCJA 0.50 ZL.
            for (int a = 0; a < 360; a += 3)
            {
                if (a % 120 > 100) continue;
                float r = a * Mathf.Deg2Rad;
                for (int t = 0; t < 3; t++)
                    Plot(back - 40 + Mathf.RoundToInt(Mathf.Cos(r) * (9 + t)), 107 + Mathf.RoundToInt(Mathf.Sin(r) * (9 + t)), new Color(0.1f, 0.45f, 0.2f), 0.4f);
            }
            Text("KAUCJA", back + 10, 110, 1, 1, dark, 0.2f);
            Text("0.50 ZŁ", back + 10, 100, 1, 1, dark, 0.2f);
            // Barcode.
            int bx = back - 44;
            while (bx < back + 44)
            {
                int w = rng.Next(1, 4);
                if (rng.NextDouble() < 0.55) Rect(bx, 72, bx + w, 92, dark, 0f);
                bx += w + rng.Next(1, 3);
            }
            Text("590" + (1000 + b * 1111) + "0" + (31 + b), back, 62, 1, 1, dark, 0.1f);
            Text("SCHŁODŹ PRZED PICIEM", back, 36, 1, 1, br.Ink, 0.3f);
            Text("NAJLEPSZE DO: 11.2027", back, 24, 1, 1, br.Ink, 0.15f);

            labels[b] = Make(px, LW, LH, "CanLabel_" + br.Name, false);
            normals[b] = NormalFrom(h, LW, LH, 2.2f, true, "CanLabelN_" + br.Name);
        }

        static Color Pattern(int b, in Brand br, int x, int y)
        {
            float u = x / (float)LW, v = y / (float)LH;
            Color c = Color.Lerp(br.Bg2, br.Bg, Mathf.SmoothStep(0f, 1f, v * 1.2f));
            float fu = u - 0.75f; // distance from the front, -0.75..0.25
            switch (b)
            {
                case 0: // cola: white wave ribbon
                {
                    float w = 0.38f + 0.12f * Mathf.Sin(u * Mathf.PI * 4f);
                    if (Mathf.Abs(v - w) < 0.035f) c = br.Ink;
                    else if (Mathf.Abs(v - w + 0.06f) < 0.012f) c = br.Accent;
                    break;
                }
                case 1: // lemon: citrus slices
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 ctr = new Vector2((i * 0.17f + 0.08f) * LW, (i % 2 == 0 ? 0.24f : 0.8f) * LH);
                        Vector2 d = new Vector2(x, y) - ctr;
                        float r = d.magnitude;
                        if (r < 26f)
                        {
                            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                            c = r > 22f ? new Color(0.98f, 0.95f, 0.5f) : (Mathf.Repeat(ang, 45f) < 4f || r < 3f) ? new Color(1f, 1f, 0.85f) : new Color(1f, 0.92f, 0.3f);
                        }
                    }
                    break;
                }
                case 2: // energy: diagonal stripes + lightning bolt
                {
                    if (Mathf.Repeat(x + y * 0.8f, 40f) < 6f) c = br.Bg2 * 1.6f;
                    float bu = fu * LW, bv = y - 128f;
                    float zig = Mathf.Repeat(bv, 60f) < 30f ? Mathf.Repeat(bv, 30f) : 30f - Mathf.Repeat(bv, 30f);
                    if (Mathf.Abs(bu - 118f - zig * 0.9f) < 7f && Mathf.Abs(bv) < 110f) c = br.Accent;
                    if (Mathf.Abs(bu + 118f - zig * 0.9f) < 7f && Mathf.Abs(bv) < 110f) c = br.Accent;
                    break;
                }
                case 3: // raspberry: polka dots
                {
                    float gx = Mathf.Repeat(x + (y / 22 % 2) * 11, 22f) - 11f, gy = Mathf.Repeat(y, 22f) - 11f;
                    if (gx * gx + gy * gy < 20f) c = br.Accent;
                    break;
                }
                case 4: // orange: sunburst from the front
                {
                    float ang = Mathf.Atan2(y - 128f, fu * LW);
                    if (Mathf.Repeat(ang * Mathf.Rad2Deg, 20f) < 8f) c = Color.Lerp(c, br.Bg2, 0.6f);
                    break;
                }
                case 5: // ice mint: snowflakes
                {
                    int cx = (x / 32) * 32 + 16, cy = (y / 32) * 32 + 16 + ((x / 32) % 2) * 8;
                    int dx = x - cx, dy = y - cy;
                    if ((dx == 0 || dy == 0 || dx == dy || dx == -dy) && Mathf.Abs(dx) < 6 && Mathf.Abs(dy) < 6 && ((x / 32 + y / 32) % 3 != 0))
                        c = Color.Lerp(c, br.Accent, 0.85f);
                    break;
                }
                case 6: // kvass: retro horizontal bands + wheat ears
                {
                    if (Mathf.Abs(v - 0.2f) < 0.02f || Mathf.Abs(v - 0.82f) < 0.02f) c = br.Accent;
                    float wx = Mathf.Repeat(x, 48f) - 24f, wy = y - 40f;
                    if (Mathf.Abs(wy) < 18f && Mathf.Abs(wx - wy * 0.1f) < 1.2f) c = br.Accent;
                    if (Mathf.Abs(wy) < 16f && Mathf.Repeat(wy, 6f) < 3f && Mathf.Abs(Mathf.Abs(wx) - 3f) < 1.5f) c = br.Accent;
                    break;
                }
                case 7: // grape: racing checker band
                {
                    if (v > 0.12f && v < 0.26f) c = ((x / 12 + y / 12) % 2 == 0) ? br.Accent : br.Bg2 * 0.6f;
                    if (Mathf.Abs(v - 0.88f) < 0.015f) c = br.Ink;
                    break;
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ lid

        static void EnsureLid()
        {
            if (lid != null) return;
            var px = new Color[LidN * LidN];
            var h = new float[LidN * LidN];
            float c = (LidN - 1) * 0.5f;
            for (int y = 0; y < LidN; y++)
            for (int x = 0; x < LidN; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c, r = Mathf.Sqrt(dx * dx + dy * dy);
                float g = 0.76f + 0.012f * Mathf.Sin(r * 140f) + 0.03f * Mathf.PerlinNoise(x * 0.22f, y * 0.012f);
                float height = r > 0.93f ? 1f : r > 0.86f ? 0.2f : 0.55f;  // rolled rim, countersink, panel
                if (Mathf.Abs(r - 0.62f) < 0.012f) height -= 0.25f;        // engraved ring
                // Score line of the opening (towards +V = front of the can), engraved.
                float ox = dx / 0.3f, oy = (dy + 0.42f) / 0.36f;
                float oval = Mathf.Sqrt(ox * ox + oy * oy);
                if (Mathf.Abs(oval - 1f) < 0.06f) height -= 0.35f;
                px[y * LidN + x] = new Color(g, g + 0.01f, g + 0.02f, 1f);
                h[y * LidN + x] = height;
            }
            // Text engraved along an arc between the engraved ring and the countersink (letters' tops point outwards).
            void EngraveArc(string s, float centreDeg, float radius)
            {
                const int sc = 2;
                float step = (CanFont.W + 1) * sc / radius * Mathf.Rad2Deg;
                float start = centreDeg + step * (s.Length - 1) * 0.5f;
                for (int i = 0; i < s.Length; i++)
                {
                    float ang = (start - i * step) * Mathf.Deg2Rad;
                    Vector2 rad = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)), tan = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
                    for (int gy = CanFont.MarkTop; gy <= CanFont.MarkBottom; gy++)
                    for (int gx = 0; gx < CanFont.W; gx++)
                    {
                        if (!CanFont.On(s[i], gx, gy)) continue;
                        for (int a2 = 0; a2 < sc; a2++)
                        for (int b2 = 0; b2 < sc; b2++)
                        {
                            float tx = (gx - CanFont.W * 0.5f) * sc + a2, ty = (CanFont.H - 1 - gy - CanFont.H * 0.5f) * sc + b2;
                            Vector2 q = new Vector2(c, c) + rad * (radius + ty) + tan * tx;
                            int x = Mathf.RoundToInt(q.x), y = Mathf.RoundToInt(q.y);
                            if (x < 0 || y < 0 || x >= LidN || y >= LidN) continue;
                            h[y * LidN + x] -= 0.35f;
                            px[y * LidN + x] *= 0.8f;
                        }
                    }
                }
            }
            // Left and right of the tab, readable from the outside of the rim.
            EngraveArc("RECYKLING", 180f, c * 0.74f);
            EngraveArc("330 ML", 0f, c * 0.74f);
            lid = Make(px, LidN, LidN, "CanLid", false);
            lidNormal = NormalFrom(h, LidN, LidN, 5f, false, "CanLidN");
        }

        // ------------------------------------------------------------------ helpers

        static Texture2D Make(Color[] px, int w, int h, string name, bool linear)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear) { name = name, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Tangent-space normal map (RGB, A = 1 so UnpackNormal works without DXT5nm swizzle).</summary>
        static Texture2D NormalFrom(float[] h, int w, int hh, float strength, bool wrapX, string name)
        {
            var px = new Color[w * hh];
            float H(int x, int y)
            {
                x = wrapX ? ((x % w) + w) % w : Mathf.Clamp(x, 0, w - 1);
                y = Mathf.Clamp(y, 0, hh - 1);
                return h[y * w + x];
            }
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = (H(x - 1, y) - H(x + 1, y)) * strength;
                float ny = (H(x, y - 1) - H(x, y + 1)) * strength;
                var n = new Vector3(nx, ny, 1f).normalized;
                px[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return Make(px, w, hh, name, true);
        }
    }
}
