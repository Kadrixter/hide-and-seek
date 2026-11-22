using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Paper atlas for the crumpled balls, 2 x 2 quadrants: 0 lined notebook page (blue lines, red margin, a few
    /// pencil words), 1 squared maths paper with a sum, 2 yellow sticky note with blue handwriting, 3 newspaper
    /// (headline, columns of text, a grey photo). Fibres, smudges and pale crease lines on all. Shared crinkle normal map.
    /// </summary>
    internal static partial class TrashArt
    {
        public enum PaperKind { Lined = 0, Squared = 1, Note = 2, News = 3 }

        const int PaperN = 1024, Quad = 512;

        /// <summary>Uv of the centre of a quadrant.</summary>
        public static Vector2 PaperCentre(PaperKind k) => new Vector2(((int)k & 1) * 0.5f + 0.25f, ((int)k >> 1) * 0.5f + 0.25f);

        static void MakePaper()
        {
            const int n = PaperN;
            var px = new Color32[n * n];
            Rows(n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    int q = (x / Quad) + 2 * (y / Quad);
                    int lx = x % Quad, ly = y % Quad;
                    Color c = q switch
                    {
                        0 => Lined(lx, ly),
                        1 => Squared(lx, ly),
                        2 => Note(lx, ly),
                        _ => News(lx, ly),
                    };
                    // Fibres and soft smudges, pale crease lines where the sheet was folded before.
                    float f = Noise(x * 0.9f, y * 0.25f, 81) * 0.5f + Noise(x * 0.25f, y * 0.9f, 83) * 0.5f;
                    float sm = Fbm(x / 60f, y / 60f, 87);
                    float crease = Mathf.Max(Line(lx, ly, 0.3f, 0.9f, 170f), Line(lx, ly, -1.2f, 0.4f, 330f));
                    c *= 0.96f + 0.06f * f - 0.07f * Mathf.Clamp01(sm * 2f - 1.1f);
                    c *= 1f - 0.08f * crease;
                    px[y * n + x] = C32(c, 1f);
                }
            });
            _paperP = Albedo(px, n, n, "TrashPaper");

            // Crinkles: many short random ridges and valleys.
            const int m = 512;
            var h = new float[m * m];
            var rnd = new System.Random(91);
            for (int s = 0; s < 700; s++)
            {
                float x0 = (float)rnd.NextDouble() * m, y0 = (float)rnd.NextDouble() * m, a = (float)rnd.NextDouble() * Mathf.PI;
                float len = 10f + (float)rnd.NextDouble() * 50f, sign = rnd.NextDouble() < 0.5 ? -1f : 1f, amp = 0.3f + (float)rnd.NextDouble() * 0.7f;
                for (int k = 0; k <= (int)len; k++)
                {
                    float t = k / len;
                    int cx = (int)(x0 + Mathf.Cos(a) * k), cy = (int)(y0 + Mathf.Sin(a) * k);
                    for (int o = -2; o <= 2; o++)
                    {
                        int xx = ((cx - (int)(Mathf.Sin(a) * o)) % m + m) % m, yy = ((cy + (int)(Mathf.Cos(a) * o)) % m + m) % m;
                        h[yy * m + xx] += sign * amp * Mathf.Sin(t * Mathf.PI) * (1f - Mathf.Abs(o) / 3f);
                    }
                }
            }
            Rows(m, y => { for (int x = 0; x < m; x++) h[y * m + x] += Noise(x * 0.2f, y * 0.2f, 93) * 0.6f; });
            _paperNP = NormalFrom(h, m, m, 0.9f, "TrashPaper_N", true);
        }

        /// <summary>0..1 closeness to a straight line through the quadrant (slope, offset, crossing x).</summary>
        static float Line(int x, int y, float slope, float off, float x0)
        {
            float d = Mathf.Abs((y - Quad * off * 0.5f) - slope * (x - x0)) / Mathf.Sqrt(1f + slope * slope);
            return Mathf.Clamp01(1f - d / 1.5f);
        }

        static Color Ink(Color paper, Color ink, float k) => Color.Lerp(paper, ink, Mathf.Clamp01(k));

        /// <summary>Hand-written "words": a pen line wandering up and down (loops of varying size and pace, tall
        /// letters now and then) in short runs along text lines.</summary>
        static float Scribble(int x, int y, float lineGap, float baseY, int seed, float x0, float x1)
        {
            if (x < x0 || x > x1) return 0f;
            float row = (y - baseY) / lineGap;
            int r = Mathf.RoundToInt(row);
            float dy = (row - r) * lineGap;
            if (r < 0 || Hash(r, 0, seed) < 0.2f) return 0f;
            float lineEnd = x0 + (x1 - x0) * (0.45f + 0.55f * Hash(r, 1, seed));
            if (x > lineEnd) return 0f;
            // Words of 3..9 letters with gaps between them.
            float wpos = (x - x0) / 46f + Hash(r, 2, seed);
            int word = Mathf.FloorToInt(wpos);
            float wx = wpos - word;
            if (wx > 0.55f + 0.35f * Hash(word, r, seed + 1)) return 0f;
            float phase = x * 0.8f + 9f * Noise(x * 0.05f, r * 5.3f, seed + 2);
            float amp = 1.6f + 2.6f * Noise(x * 0.13f, r * 2.7f, seed + 3);
            float tall = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(x * 0.23f + r * 1.7f + word * 2.3f)), 30f) * 8f;
            float pen = Mathf.Sin(phase) * amp + Mathf.Sin(phase * 0.47f + word) * amp * 0.45f + Mathf.Max(0f, Mathf.Sin(phase)) * tall + Mathf.Sin(x * 0.05f + r) * 1.2f;
            // Steep parts of the line are thinner in pixels: widen by the slope.
            float slope = Mathf.Abs(Mathf.Cos(phase) * amp * 0.8f) + 1f;
            return 0.8f * Mathf.Clamp01(1.2f - Mathf.Abs(dy - pen - 6f) / (0.9f * slope));
        }

        static Color Lined(int x, int y)
        {
            Color c = new Color(0.94f, 0.94f, 0.91f);
            float ly = Mathf.Repeat(y, 34f);
            if (ly < 1.6f) c = Ink(c, new Color(0.55f, 0.68f, 0.88f), 0.85f);
            if (Mathf.Abs(x - 92) < 1.4f) c = Ink(c, new Color(0.9f, 0.35f, 0.38f), 0.9f);
            float s = Scribble(x, y, 34f, 4f, 5, 104f, 500f);
            return Ink(c, new Color(0.32f, 0.32f, 0.35f), s * 0.8f);
        }

        static Color Squared(int x, int y)
        {
            Color c = new Color(0.95f, 0.95f, 0.93f);
            if (Mathf.Repeat(x, 22f) < 1.1f || Mathf.Repeat(y, 22f) < 1.1f) c = Ink(c, new Color(0.62f, 0.72f, 0.82f), 0.75f);
            float s = Scribble(x, y, 44f, 10f, 9, 40f, 300f);
            return Ink(c, new Color(0.12f, 0.2f, 0.55f), s);
        }

        static Color Note(int x, int y)
        {
            Color c = new Color(0.99f, 0.88f, 0.36f);
            if (y > 470) c *= 0.94f; // glue strip
            float s = Scribble(x, y, 52f, 20f, 13, 40f, 470f);
            return Ink(c, new Color(0.08f, 0.15f, 0.55f), s);
        }

        static Color News(int x, int y)
        {
            Color c = new Color(0.86f, 0.84f, 0.78f);
            Color ink = new Color(0.16f, 0.16f, 0.17f);
            // Headline: letters made of a few strokes (bars and stems), not solid blocks.
            if (y > 436 && y < 472 && x > 24 && x < 470)
            {
                int letter = (x - 24) / 26;
                int lx = (x - 24) % 26, ly = y - 436;
                if (lx > 20 || Hash(letter, 9, 101) < 0.12f) return c;
                bool stemL = lx < 5, stemR = lx > 15 && Hash(letter, 1, 101) > 0.35f, stemM = lx > 8 && lx < 12 && Hash(letter, 2, 101) > 0.7f;
                bool barT = ly > 29 && Hash(letter, 3, 101) > 0.4f, barM = ly > 15 && ly < 20 && Hash(letter, 4, 101) > 0.5f, barB = ly < 5 && Hash(letter, 5, 101) > 0.45f;
                return stemL || stemR || stemM || barT || barM || barB ? Ink(c, ink, 0.92f) : c;
            }
            // Small photo top-left, columns of small text everywhere else.
            if (x > 24 && x < 182 && y > 300 && y < 415)
            {
                float p = Fbm(x / 30f, y / 30f, 103);
                return Color.Lerp(new Color(0.42f, 0.41f, 0.4f), new Color(0.78f, 0.77f, 0.73f), p);
            }
            int colIdx = (x - 24) / 158;
            float cx = (x - 24) % 158;
            if (x < 24 || cx > 146 || y < 20 || y > 415 || (x < 190 && y > 292)) return c;
            int line = y / 9;
            if (y % 9 > 4) return c;
            float shift = Hash(line, colIdx, 105) * 30f;
            int word = (int)((cx + shift) / 14f);
            float wx = (cx + shift) % 14f;
            bool letterOn = wx < 11f && Hash(word, line, 107) > 0.08f && Noise(x * 1.3f, y * 1.3f, 109) > 0.35f;
            return letterOn ? Ink(c, ink, 0.55f) : c;
        }
    }
}
