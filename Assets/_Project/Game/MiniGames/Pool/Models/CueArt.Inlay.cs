using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    public static partial class CueArt
    {
        /// <summary>Signed shortest difference of two u values round the cue (-0.5..0.5).</summary>
        public static float WrapD(float a) => a - Mathf.Round(a);

        /// <summary>
        /// Inlaid points (the classic "4 points with veneers"): <see cref="Count"/> spikes round the cue starting at z =
        /// <see cref="Base"/> and ending in a needle tip at z = <see cref="Tip"/>, each wrapped in veneers (metres, inner first).
        /// A <see cref="Splice"/> is a full splice: below the base the whole cue is point wood (one-piece house cues).
        /// </summary>
        public sealed class Points
        {
            public int Count = 4;
            public float Offset, Base, Tip, BaseHalf = 0.7f, Curve = 1.2f;
            public float[] Veneers = new float[0];
            public bool Splice;
        }

        /// <summary>Layer at (u, z) of a set of points on a cue of circumference <paramref name="circ"/>: -1 outside, 0 point, 1.. veneer k.</summary>
        public static int PointLayer(Points p, float u, float z, float circ)
        {
            if (p.Splice && z < p.Base) return 0;
            float spacing = 1f / p.Count;
            float f = Mathf.Abs(WrapD((u - p.Offset) / spacing) * spacing); // fraction of the circle to the nearest point's centre line
            float t = Mathf.Max(0f, (z - p.Base) / (p.Tip - p.Base));
            float half = p.BaseHalf * spacing * 0.5f;
            float w = t < 1f ? half * Mathf.Pow(1f - t, p.Curve) : -half * (t - 1f) * 1.5f;
            float d = (f - w) * circ; // metres outside the point's edge (negative = inside)
            // Floating points: the veneers also run round the flat base.
            if (z < p.Base) d = Mathf.Max(d, p.Base - z);
            if (d < 0f) return 0;
            float acc = 0f;
            for (int i = 0; i < p.Veneers.Length; i++)
            {
                acc += p.Veneers[i];
                if (d < acc) return i + 1;
            }
            return -1;
        }

        /// <summary>Diamond centred on (cu, cz), half sizes in metres (round / along). Returns 0 inside, 1 in the border, -1 outside.</summary>
        public static int Diamond(float u, float z, float cu, float cz, float hw, float hh, float circ, float border = 0f)
        {
            float du = Mathf.Abs(WrapD(u - cu)) * circ, dz = Mathf.Abs(z - cz);
            float k = du / hw + dz / hh;
            if (k <= 1f) return 0;
            // Border measured roughly perpendicular to the edges.
            float scale = hw * hh / Mathf.Sqrt(hw * hw + hh * hh);
            return border > 0f && (k - 1f) * scale < border ? 1 : -1;
        }

        /// <summary>Ellipse centred on (cu, cz), radii in metres: 0 inside, 1 border, -1 outside.</summary>
        public static int Dot(float u, float z, float cu, float cz, float ru, float rz, float circ, float border = 0f)
        {
            float du = WrapD(u - cu) * circ, dz = z - cz;
            float a = du / ru, b = dz / rz, d = Mathf.Sqrt(a * a + b * b);
            if (d <= 1f) return 0;
            return border > 0f && (d - 1f) * Mathf.Min(ru, rz) < border ? 1 : -1;
        }

        /// <summary>Rounded rectangle (window) centred on (cu, cz), half sizes and corner radius in metres: 0 inside, 1 border, -1 outside.</summary>
        public static int Window(float u, float z, float cu, float cz, float hw, float hh, float corner, float circ, float border = 0f)
        {
            float du = Mathf.Abs(WrapD(u - cu)) * circ - (hw - corner), dz = Mathf.Abs(z - cz) - (hh - corner);
            float dx = Mathf.Max(du, 0f), dy = Mathf.Max(dz, 0f);
            float d = Mathf.Sqrt(dx * dx + dy * dy) + Mathf.Min(Mathf.Max(du, dz), 0f) - corner;
            if (d <= 0f) return 0;
            return d < border ? 1 : -1;
        }

        /// <summary>
        /// True on the strokes of <paramref name="text"/> written along the cue (reading towards +z, letters standing on
        /// the -u side), starting at z = <paramref name="z0"/>, centred round u = <paramref name="cu"/>; <paramref name="cell"/> = metres per font pixel.
        /// </summary>
        public static bool Text(string text, float u, float z, float cu, float z0, float cell, float circ)
        {
            float gzf = (z - z0) / cell;
            if (gzf < 0f) return false;
            int col = Mathf.FloorToInt(gzf), ch = col / (CanFont.W + 1), gx = col % (CanFont.W + 1);
            if (ch >= text.Length || gx >= CanFont.W) return false;
            int gy = Mathf.FloorToInt(-WrapD(u - cu) * circ / cell + CanFont.H * 0.5f);
            return gy >= 0 && gy < CanFont.H && CanFont.On(text[ch], gx, gy);
        }

        // ================================================================ bumper logo

        /// <summary>Fictional maker's medallion for the rubber bumper: a dark disc, a silver ring, "HS" and an 8-ball dot.</summary>
        public static Texture2D Logo => Get("logo", () => Fill(256, 256, false, (x, y) =>
        {
            float fx = (x + 0.5f) / 128f - 1f, fy = (y + 0.5f) / 128f - 1f, r = Mathf.Sqrt(fx * fx + fy * fy);
            Color rubber = new Color(0.07f, 0.07f, 0.075f) * (0.9f + 0.2f * Noise(fx * 40f + 40f, fy * 40f + 40f, 1000, 5));
            Color silver = new Color(0.78f, 0.78f, 0.8f);
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 40f) + Mathf.Clamp01(1f - Mathf.Abs(r - 0.7f) * 60f);
            Color c = Color.Lerp(rubber, silver, Mathf.Clamp01(ring));
            // Eight small studs between the rings.
            float ang = Mathf.Atan2(fy, fx) / (Mathf.PI * 2f) * 8f;
            float sd = new Vector2(WrapD(ang) * 0.76f * Mathf.PI * 2f / 8f, r - 0.76f).magnitude;
            if (sd < 0.03f) c = silver;
            // "HS" monogram, 5x7 font, 9 px per font pixel.
            const int s = 9;
            int tw = CanFont.Width("HS", s), tx = (x - (128 - tw / 2)), ty = (128 + CanFont.H * s / 2) - y;
            if (tx >= 0 && ty >= 0 && tx < tw && ty < CanFont.H * s)
            {
                int col = tx / s, ch = col / (CanFont.W + 1), gx = col % (CanFont.W + 1), gy = ty / s;
                if (gx < CanFont.W && CanFont.On("HS"[ch], gx, gy)) c = silver * 0.95f;
            }
            c.a = 1f;
            return c;
        }));
    }
}
