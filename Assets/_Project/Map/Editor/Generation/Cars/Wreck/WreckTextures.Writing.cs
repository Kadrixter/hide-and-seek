using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// "speed &lt;3" written with a finger in the dust: hand-drawn strokes (lower-case, uneven, rising to the right),
    /// rasterised as a finger track: R = dust wiped off, G = dust pushed into ridges along both edges, B = the skin lines
    /// left inside the track (parallel to the stroke). 2048 x 768 for 1.3 x 0.46 m on the right rear door.
    /// </summary>
    internal static partial class WreckTextures
    {
        public const string Message = "speed <3";

        // glyph strokes in em (baseline 0, x-height 1), width per glyph
        private static readonly Dictionary<char, (float w, Vector2[][] strokes)> Glyphs = new Dictionary<char, (float, Vector2[][])>
        {
            ['s'] = (0.72f, new[] { V(0.64f, 0.86f, 0.42f, 1.0f, 0.14f, 0.9f, 0.13f, 0.64f, 0.42f, 0.5f, 0.64f, 0.3f, 0.55f, 0.05f, 0.27f, -0.02f, 0.04f, 0.14f) }),
            ['p'] = (0.84f, new[] { V(0.1f, 1.02f, 0.11f, 0.4f, 0.07f, -0.68f), V(0.12f, 0.74f, 0.36f, 1.0f, 0.7f, 0.86f, 0.79f, 0.47f, 0.57f, 0.08f, 0.26f, 0.04f, 0.11f, 0.22f) }),
            ['e'] = (0.8f, new[] { V(0.08f, 0.5f, 0.42f, 0.54f, 0.68f, 0.6f, 0.68f, 0.84f, 0.44f, 1.0f, 0.15f, 0.86f, 0.05f, 0.46f, 0.2f, 0.08f, 0.5f, -0.01f, 0.74f, 0.16f) }),
            ['d'] = (0.86f, new[] { V(0.68f, 0.74f, 0.44f, 0.98f, 0.14f, 0.86f, 0.04f, 0.45f, 0.2f, 0.05f, 0.5f, 0.02f, 0.7f, 0.3f), V(0.71f, 1.72f, 0.72f, 0.8f, 0.76f, -0.02f) }),
            ['<'] = (0.74f, new[] { V(0.7f, 1.16f, 0.36f, 0.82f, 0.08f, 0.56f, 0.4f, 0.26f, 0.73f, -0.06f) }),
            ['3'] = (0.82f, new[] { V(0.08f, 1.26f, 0.42f, 1.5f, 0.74f, 1.32f, 0.64f, 0.96f, 0.3f, 0.8f), V(0.3f, 0.8f, 0.72f, 0.62f, 0.8f, 0.24f, 0.5f, -0.03f, 0.08f, 0.1f) }),
        };

        private static Vector2[] V(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return p;
        }

        private static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }

        private static void Writing()
        {
            const int W = 2048, H = 768;
            const float em = 225f;               // px per em (x-height about 14 cm)
            const float finger = 36f;            // finger track width in px (about 23 mm)
            var rng = new Random(7401);
            var nz = new TileNoise(7402);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // per pixel: best normalised distance to a track, signed offset across it
            var dn = new float[W * H];
            var across = new float[W * H];
            for (int i = 0; i < dn.Length; i++) dn[i] = 9f;

            float total = 0f;
            foreach (char ch in Message) total += (ch == ' ' ? 0.5f : Glyphs[ch].w) + 0.16f;
            float x0 = (W - total * em) * 0.5f + 20f, base0 = H * 0.28f;
            float slope = Mathf.Tan(4.5f * Mathf.Deg2Rad);  // the line climbs to the right
            float pen = 0f;
            foreach (char ch in Message)
            {
                if (ch == ' ') { pen += 0.5f + 0.16f; continue; }
                var (gw, strokes) = Glyphs[ch];
                float rot = R(-7f, 6f) * Mathf.Deg2Rad, sc = R(0.9f, 1.1f), lift = R(-0.09f, 0.09f), lean = R(0.05f, 0.18f);
                var gOrigin = new Vector2(x0 + pen * em, base0 + (pen * em) * slope + lift * em);
                foreach (var s in strokes)
                {
                    // jitter the control points, then a smooth Catmull-Rom path through them
                    var cp = new Vector2[s.Length];
                    for (int i = 0; i < s.Length; i++)
                    {
                        var q = s[i] + new Vector2(R(-0.045f, 0.045f), R(-0.045f, 0.045f));
                        q.x += q.y * lean;                       // italic lean
                        q = new Vector2(q.x * Mathf.Cos(rot) - q.y * Mathf.Sin(rot), q.x * Mathf.Sin(rot) + q.y * Mathf.Cos(rot));
                        cp[i] = gOrigin + q * (sc * em);
                    }
                    var path = new List<Vector2>();
                    for (int i = 0; i < cp.Length - 1; i++)
                    {
                        Vector2 a = cp[Mathf.Max(i - 1, 0)], b = cp[i], c = cp[i + 1], d = cp[Mathf.Min(i + 2, cp.Length - 1)];
                        int steps = Mathf.Max(2, Mathf.CeilToInt((c - b).magnitude / 3f));
                        for (int k = 0; k < steps; k++) path.Add(CatmullRom(a, b, c, d, k / (float)steps));
                    }
                    path.Add(cp[cp.Length - 1]);
                    float press0 = R(1.05f, 1.2f), press1 = R(0.75f, 0.9f), wob = R(0f, 6.28f);
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        float t = i / (float)(path.Count - 1);
                        // pressing harder where the finger lands, lifting at the end, a little uneven on the way
                        float wd = finger * Mathf.Lerp(press0, press1, t) * (1f + 0.07f * Mathf.Sin(t * 9f + wob));
                        Track(dn, across, W, H, path[i], path[i + 1], wd * 0.5f);
                    }
                }
                pen += gw + 0.16f;
            }

            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float e = dn[i] + (nz.Fbm(u, v, 96, 36, 3, 0.5f) - 0.5f) * 0.22f;   // ragged edge of the dust
                float wiped = Smooth(1.0f, 0.82f, e);
                float ridge = Smooth(0.92f, 1.06f, e) * (1f - Smooth(1.12f, 1.55f, e)) * (0.55f + 0.7f * nz.Fbm(u, v, 128, 48, 2, 0.5f));
                float lines = wiped * (0.5f + 0.5f * Mathf.Sin(across[i] * Mathf.PI * 2f / 5.5f)) * (0.4f + 0.6f * nz.Fbm(u, v, 64, 24, 2, 0.5f));
                px[i] = new Color(wiped, Mathf.Clamp01(ridge), lines, 1f);
            }
            Save("Wreck_Write", W, H, px);
        }

        /// <summary>One piece of the finger track (half width r): keeps the nearest track per pixel.</summary>
        private static void Track(float[] dn, float[] across, int w, int h, Vector2 a, Vector2 b, float r)
        {
            float m = r * 1.7f;
            int xa = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) - m)), xb = Mathf.Min(w - 1, (int)(Mathf.Max(a.x, b.x) + m));
            int ya = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) - m)), yb = Mathf.Min(h - 1, (int)(Mathf.Max(a.y, b.y) + m));
            Vector2 d = b - a;
            float L2 = Mathf.Max(d.sqrMagnitude, 1e-6f);
            Vector2 nrm = new Vector2(-d.y, d.x).normalized;
            for (int y = ya; y <= yb; y++)
            for (int x = xa; x <= xb; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / L2);
                var c = a + d * t;
                float q = (p - c).magnitude / r;
                int i = y * w + x;
                if (q < dn[i])
                {
                    dn[i] = q;
                    across[i] = Vector2.Dot(p - c, nrm);
                }
            }
        }
    }
}
