using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Procedural textures of the fire extinguisher: the wrap-around label (name, three-step pictogram instruction,
    /// fire classes A B C, fine print, barcode) and the pressure gauge dial (red - green - red arc, ticks, "bar").
    /// Painted pixel by pixel with <see cref="CanFont"/>; built once and shared by every copy of the model.
    /// </summary>
    public static class ExtinguisherArt
    {
        static Texture2D label, gauge;

        sealed class Canvas
        {
            public readonly int W, H;
            readonly Color32[] px;

            public Canvas(int w, int h, Color bg)
            {
                W = w; H = h;
                px = new Color32[w * h];
                Color32 c = bg;
                for (int i = 0; i < px.Length; i++) px[i] = c;
            }

            public void Plot(int x, int y, Color c, float a = 1f)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                px[i] = a >= 1f ? (Color32)c : (Color32)Color.Lerp(px[i], c, a);
            }

            public void Rect(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = Mathf.RoundToInt(y0 * H); y < Mathf.RoundToInt(y1 * H); y++)
                for (int x = Mathf.RoundToInt(x0 * W); x < Mathf.RoundToInt(x1 * W); x++) Plot(x, y, c);
            }

            public void Frame(float x0, float y0, float x1, float y1, int t, Color c)
            {
                int ax = Mathf.RoundToInt(x0 * W), ay = Mathf.RoundToInt(y0 * H), bx = Mathf.RoundToInt(x1 * W), by = Mathf.RoundToInt(y1 * H);
                for (int y = ay; y < by; y++)
                for (int x = ax; x < bx; x++)
                    if (x < ax + t || x >= bx - t || y < ay + t || y >= by - t) Plot(x, y, c);
            }

            /// <summary>Anti-aliased disc / ring (r in pixels, hole = inner radius in pixels).</summary>
            public void Disc(float cx, float cy, float r, Color c, float hole = 0f)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r + 0.5f - d);
                    if (hole > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - hole + 0.5f));
                    if (a > 0f) Plot(x, y, c, a);
                }
            }

            /// <summary>Thick anti-aliased line in pixels.</summary>
            public void Line(float ax, float ay, float bx, float by, float w, Color c)
            {
                Vector2 a = new(ax, ay), b = new(bx, by);
                int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - w), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + w);
                int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - w), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + w);
                Vector2 ab = b - a;
                float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = (p - (a + ab * t)).magnitude;
                    float al = Mathf.Clamp01(w * 0.5f + 0.5f - d);
                    if (al > 0f) Plot(x, y, c, al);
                }
            }

            public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
            {
                int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                int y0 = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new(x + 0.5f, y + 0.5f);
                    float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Plot(x, y, col);
                }
            }

            static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

            /// <summary>Text centred on (cx, y = baseline bottom), in pixels.</summary>
            public void Text(string s, int cx, int y, int sx, int sy, Color c)
            {
                int x = cx - CanFont.Width(s, sx) / 2;
                CanFont.Draw(s, x, y, sx, sy, (a, b) => Plot(a, b, c));
            }

            public Texture2D Finish(string name, bool mips = true)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, mips) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                t.SetPixels32(px);
                t.Apply(mips, true);
                return t;
            }
        }

        static readonly Color Paper = new(0.95f, 0.94f, 0.9f), Ink = new(0.1f, 0.1f, 0.11f), Red = new(0.78f, 0.07f, 0.05f);
        static readonly Color Grey = new(0.55f, 0.55f, 0.55f);

        /// <summary>Wrap-around label (u = around the tank, v = up).</summary>
        public static Texture2D Label
        {
            get
            {
                if (label != null) return label;
                var c = new Canvas(288, 384, Paper);
                int W = c.W;
                // Title band.
                c.Rect(0f, 0.82f, 1f, 1f, Red);
                c.Text("GAŚNICA", W / 2, 330, 5, 6, Color.white);
                c.Rect(0f, 0.812f, 1f, 0.82f, Ink);
                c.Text("PROSZKOWA GP-6X ABC", W / 2, 292, 2, 3, Ink);

                // Three-step instruction: 1 pull the pin, 2 aim at the base of the fire, 3 squeeze the lever.
                for (int i = 0; i < 3; i++)
                {
                    float x0 = 0.04f + i * 0.32f, x1 = x0 + 0.28f;
                    c.Frame(x0, 0.5f, x1, 0.74f, 2, Ink);
                    int cx = Mathf.RoundToInt((x0 + x1) * 0.5f * W), cy = Mathf.RoundToInt(0.63f * c.H);
                    c.Disc(Mathf.RoundToInt(x0 * W) + 10, Mathf.RoundToInt(0.74f * c.H) - 11, 7, Red);
                    c.Text((i + 1).ToString(), Mathf.RoundToInt(x0 * W) + 10, Mathf.RoundToInt(0.74f * c.H) - 15, 1, 1, Color.white);
                    if (i == 0)
                    {
                        // ring of the pin pulled out, arrow to the right
                        c.Rect(x0 + 0.03f, 0.6f, x0 + 0.12f, 0.66f, Grey);
                        c.Disc(cx + 4, cy, 12, Ink, 8);
                        c.Line(cx + 18, cy, cx + 34, cy, 3, Red);
                        c.Tri(new Vector2(cx + 38, cy), new Vector2(cx + 30, cy + 7), new Vector2(cx + 30, cy - 7), Red);
                    }
                    else if (i == 1)
                    {
                        // nozzle aimed at the flame
                        c.Line(cx - 30, cy + 14, cx - 6, cy - 2, 5, Ink);
                        c.Tri(new Vector2(cx + 4, cy - 26), new Vector2(cx + 30, cy - 26), new Vector2(cx + 17, cy + 10), new Color(0.95f, 0.45f, 0.08f));
                        c.Tri(new Vector2(cx + 9, cy - 26), new Vector2(cx + 25, cy - 26), new Vector2(cx + 17, cy - 4), new Color(1f, 0.82f, 0.2f));
                        for (int k = 0; k < 4; k++) c.Disc(cx - 2 + k * 4, cy - 6 - k * 3, 2.2f, Grey);
                    }
                    else
                    {
                        // lever pressed down onto the handle
                        c.Line(cx - 28, cy - 6, cx + 24, cy - 6, 5, Ink);
                        c.Line(cx - 28, cy + 4, cx + 24, cy + 12, 5, Ink);
                        c.Line(cx, cy + 30, cx, cy + 16, 3, Red);
                        c.Tri(new Vector2(cx, cy + 10), new Vector2(cx - 7, cy + 19), new Vector2(cx + 7, cy + 19), Red);
                    }
                }
                c.Text("WYCIĄGNIJ", Mathf.RoundToInt(0.18f * W), 178, 1, 1, Ink);
                c.Text("CELUJ", Mathf.RoundToInt(0.5f * W), 178, 1, 1, Ink);
                c.Text("NACIŚNIJ", Mathf.RoundToInt(0.82f * W), 178, 1, 1, Ink);

                // Fire classes.
                var cls = new[] { ("A", new Color(0.1f, 0.45f, 0.2f)), ("B", new Color(0.75f, 0.1f, 0.08f)), ("C", new Color(0.12f, 0.3f, 0.65f)) };
                for (int i = 0; i < 3; i++)
                {
                    float x0 = 0.16f + i * 0.24f;
                    c.Rect(x0, 0.27f, x0 + 0.2f, 0.42f, cls[i].Item2);
                    c.Text(cls[i].Item1, Mathf.RoundToInt((x0 + 0.1f) * W), 120, 4, 4, Color.white);
                }
                // Fine print, barcode, temperature range.
                for (int i = 0; i < 6; i++) c.Rect(0.06f, 0.2f - i * 0.025f, 0.94f - (i % 3) * 0.13f, 0.21f - i * 0.025f, Grey);
                c.Text("-30 +60", Mathf.RoundToInt(0.3f * W), 12, 1, 1, Ink);
                for (int i = 0; i < 26; i++)
                    if ((i * 7 + 3) % 5 != 0) c.Rect(0.6f + i * 0.012f, 0.02f, 0.6f + i * 0.012f + ((i % 3 == 0) ? 0.008f : 0.004f), 0.07f, Ink);
                label = c.Finish("ExtLabel");
                return label;
            }
        }

        /// <summary>Gauge dial: a 270 degree scale from bottom-left to bottom-right, green "OK" band in the middle.</summary>
        public static Texture2D Gauge
        {
            get
            {
                if (gauge != null) return gauge;
                var c = new Canvas(128, 128, new Color(0.96f, 0.96f, 0.93f));
                float cx = 63.5f, cy = 63.5f;
                // Coloured arc: red (empty) - green - red (over-filled).
                for (int k = 0; k <= 270; k += 1)
                {
                    float t = k / 270f;
                    Color col = t < 0.22f ? new Color(0.85f, 0.1f, 0.08f) : t < 0.78f ? new Color(0.1f, 0.62f, 0.22f) : new Color(0.85f, 0.1f, 0.08f);
                    float a = (225f - k) * Mathf.Deg2Rad;
                    for (float r = 40f; r < 50f; r += 0.5f) c.Plot(Mathf.RoundToInt(cx + Mathf.Cos(a) * r), Mathf.RoundToInt(cy + Mathf.Sin(a) * r), col);
                }
                for (int k = 0; k <= 10; k++)
                {
                    float a = (225f - k * 27f) * Mathf.Deg2Rad;
                    float r0 = k % 5 == 0 ? 30f : 35f;
                    c.Line(cx + Mathf.Cos(a) * r0, cy + Mathf.Sin(a) * r0, cx + Mathf.Cos(a) * 52f, cy + Mathf.Sin(a) * 52f, k % 5 == 0 ? 3f : 1.6f, Ink);
                }
                c.Text("BAR", 64, 30, 2, 2, Ink);
                c.Text("0", 34, 20, 2, 2, Ink);
                c.Text("15", 96, 20, 2, 2, Ink);
                c.Disc(cx, cy, 63, new Color(0.2f, 0.2f, 0.22f), 60.5f);
                gauge = c.Finish("ExtGauge");
                return gauge;
            }
        }

        /// <summary>Needle angle as seen on the dial (degrees, counter-clockwise from straight up) for a charge of 0..1:
        /// full = upper part of the green band, empty = the start of the red one.</summary>
        public static float NeedleAngle(float charge) => Mathf.Lerp(125f, -60f, Mathf.Clamp01(charge));
    }
}
