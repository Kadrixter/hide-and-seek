using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Single-stroke vector font as a signed distance field, for printing on the pool balls and the chalk wrapper:
    /// digits 0-9 and the letters A C H K L O P R. Glyphs are built from line segments and circular arcs with a round
    /// pen, in a box 0.62 wide and 1 high (glyph heights); "9" is "6" turned upside down and a lone 6 / 9 gets an
    /// underline, like on real balls. Distances are in glyph heights, negative inside the ink.
    /// </summary>
    public static class PoolBallGlyphs
    {
        public const float Advance = 0.62f, Gap = 0.06f, Stroke = 0.19f;
        const float S = 0f, A = 1f; // ops: segment (x0 y0 x1 y1), arc (cx cy r deg0 deg1, counter-clockwise)
        const float B = 0.085f, T = 0.915f, L = 0.085f, R = 0.535f;

        static readonly Dictionary<char, float[]> Glyphs = new()
        {
            ['0'] = new[] { A, 0.31f, 0.69f, 0.225f, 0f, 180f, A, 0.31f, 0.31f, 0.225f, 180f, 360f, S, L, 0.31f, L, 0.69f, S, R, 0.31f, R, 0.69f },
            ['1'] = new[] { S, 0.40f, B, 0.40f, T, S, 0.40f, T, 0.17f, 0.75f },
            ['2'] = new[] { A, 0.31f, 0.685f, 0.23f, -35f, 165f, S, 0.498f, 0.553f, L, B, S, L, B, 0.55f, B },
            ['3'] = new[] { A, 0.30f, 0.70f, 0.215f, -90f, 150f, A, 0.30f, 0.31f, 0.225f, -150f, 90f, S, 0.22f, 0.51f, 0.30f, 0.51f },
            ['4'] = new[] { S, 0.42f, T, 0.065f, 0.33f, S, 0.065f, 0.33f, 0.56f, 0.33f, S, 0.42f, T, 0.42f, B },
            ['5'] = new[] { S, 0.51f, T, 0.14f, T, S, 0.14f, T, 0.115f, 0.545f, S, 0.115f, 0.545f, 0.19f, 0.54f, A, 0.31f, 0.33f, 0.245f, -145f, 120f },
            ['6'] = new[] { A, 0.31f, 0.31f, 0.225f, 0f, 360f, S, L, 0.31f, L, 0.665f, A, 0.335f, 0.665f, 0.25f, 45f, 180f },
            ['7'] = new[] { S, L, T, 0.55f, T, S, 0.55f, T, 0.22f, B },
            ['8'] = new[] { A, 0.31f, 0.715f, 0.20f, 0f, 360f, A, 0.31f, 0.30f, 0.215f, 0f, 360f },
            ['A'] = new[] { S, 0.04f, B, 0.31f, T, S, 0.31f, T, 0.58f, B, S, 0.15f, 0.36f, 0.47f, 0.36f },
            ['C'] = new[] { A, 0.33f, 0.665f, 0.25f, 20f, 180f, S, 0.08f, 0.335f, 0.08f, 0.665f, A, 0.33f, 0.335f, 0.25f, 180f, 340f },
            ['H'] = new[] { S, L, B, L, T, S, R, B, R, T, S, L, 0.5f, R, 0.5f },
            ['K'] = new[] { S, L, B, L, T, S, 0.54f, T, L, 0.38f, S, 0.25f, 0.56f, 0.55f, B },
            ['L'] = new[] { S, L, T, L, B, S, L, B, 0.53f, B },
            ['O'] = new[] { A, 0.31f, 0.69f, 0.225f, 0f, 180f, A, 0.31f, 0.31f, 0.225f, 180f, 360f, S, L, 0.31f, L, 0.69f, S, R, 0.31f, R, 0.69f },
            ['P'] = new[] { S, L, B, L, T, S, L, T, 0.30f, T, S, L, 0.495f, 0.30f, 0.495f, A, 0.30f, 0.705f, 0.21f, -90f, 90f },
            ['R'] = new[] { S, L, B, L, T, S, L, T, 0.30f, T, S, L, 0.495f, 0.30f, 0.495f, A, 0.30f, 0.705f, 0.21f, -90f, 90f, S, 0.30f, 0.495f, 0.54f, B },
        };

        /// <summary>Width of <paramref name="text"/> in glyph heights.</summary>
        public static float Width(string text) => text.Length == 0 ? 0f : text.Length * Advance + (text.Length - 1) * Gap;

        static bool Underlined(string text) => text == "6" || text == "9";

        /// <summary>Half height of the inked block (glyphs plus a possible underline) in glyph heights.</summary>
        public static float HalfHeight(string text) => Underlined(text) ? 0.6f : 0.5f;

        /// <summary>
        /// Signed distance (glyph heights) from <paramref name="p"/> to the ink of <paramref name="text"/> centred on the
        /// origin (+y up). <paramref name="stroke"/> is the pen width.
        /// </summary>
        public static float Distance(string text, Vector2 p, float stroke = Stroke)
        {
            float w = Width(text);
            bool under = Underlined(text);
            // A lone 6 / 9 is lifted so that the glyph plus its underline sit centred.
            float lift = under ? 0.1f : 0f;
            float best = 1e9f;
            for (int i = 0; i < text.Length; i++)
            {
                Vector2 q = new Vector2(p.x + w * 0.5f - i * (Advance + Gap), p.y + 0.5f - lift);
                char c = text[i];
                if (c == '9') { q = new Vector2(Advance - q.x, 1f - q.y); c = '6'; }
                if (!Glyphs.TryGetValue(c, out var ops)) continue;
                best = Mathf.Min(best, GlyphDistance(ops, q));
            }
            best -= stroke * 0.5f;
            if (under)
            {
                Vector2 q = new Vector2(p.x + w * 0.5f, p.y + 0.5f - lift);
                best = Mathf.Min(best, Segment(q, new Vector2(0.11f, -0.13f), new Vector2(0.51f, -0.13f)) - stroke * 0.36f);
            }
            return best;
        }

        static float GlyphDistance(float[] ops, Vector2 q)
        {
            float best = 1e9f;
            int i = 0;
            while (i < ops.Length)
            {
                if (ops[i] == S)
                {
                    best = Mathf.Min(best, Segment(q, new Vector2(ops[i + 1], ops[i + 2]), new Vector2(ops[i + 3], ops[i + 4])));
                    i += 5;
                }
                else
                {
                    best = Mathf.Min(best, Arc(q, new Vector2(ops[i + 1], ops[i + 2]), ops[i + 3], ops[i + 4], ops[i + 5]));
                    i += 6;
                }
            }
            return best;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Distance to a circular arc from <paramref name="a0"/> to <paramref name="a1"/> degrees (counter-clockwise).</summary>
        static float Arc(Vector2 p, Vector2 c, float r, float a0, float a1)
        {
            Vector2 d = p - c;
            float span = a1 - a0;
            if (span >= 359.9f) return Mathf.Abs(d.magnitude - r);
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float rel = Mathf.Repeat(ang - a0, 360f);
            if (rel <= span) return Mathf.Abs(d.magnitude - r);
            Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
            Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
            return Mathf.Min((p - e0).magnitude, (p - e1).magnitude);
        }
    }
}
