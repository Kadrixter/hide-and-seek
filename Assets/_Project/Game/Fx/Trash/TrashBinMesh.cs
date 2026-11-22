using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Geometry of the perforated office bin (origin = centre of the bottom, +Y up): tapered wall with real thickness
    /// (outer + inner surface), rolled rim, bevelled foot, floor; a slightly oval shape and two dents. The black liner
    /// bag hugs the inside and is folded over the rim, hanging unevenly, slipped back inside on one side (the rim shows
    /// there). The bag closes the bin at <see cref="FillY"/> with a lumpy bottom the papers sit on.
    /// Wall uv: u = angle / 180 degrees (texture repeats twice round), v = height / <see cref="H"/>.
    /// </summary>
    internal static class TrashBinMesh
    {
        public const float H = 0.38f, Rb = 0.124f, Rt = 0.152f, Wall = 0.0025f, RimR = 0.0055f, FillY = 0.29f;
        const int Cols = 64;

        /// <summary>Outer radius of the (round) wall at height y.</summary>
        public static float R(float y) => Rb + (Rt - Rb) * Mathf.Clamp01(y / H);

        static float Ang(int i) => i * Mathf.PI * 2f / Cols;
        static Vector3 Round(float r, float a, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);

        /// <summary>Inward push of the wall (dents, slightly oval sheet), metres.</summary>
        static float Dent(float a, float y, out float k)
        {
            float env = Mathf.Clamp01((y - 0.03f) / 0.04f) * Mathf.Clamp01((H - 0.02f - y) / 0.04f);
            float d1 = Gauss(a, 2.15f, y, 0.2f, 0.036f), d2 = Gauss(a, 4.4f, y, 0.075f, 0.022f);
            k = Mathf.Clamp01(d1 + d2 * 0.7f) * env;
            return (0.0075f * d1 + 0.0045f * d2) * env + 0.0016f * Mathf.Cos(2f * a + 0.4f) * Mathf.Clamp01(y / H);
        }

        static float Gauss(float a, float a0, float y, float y0, float s)
        {
            float da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, a0 * Mathf.Rad2Deg) * Mathf.Deg2Rad * Rt;
            float dy = y - y0;
            return Mathf.Exp(-(da * da + dy * dy) / (s * s));
        }

        public static Mesh Body()
        {
            var m = new TrashMesh();
            const int rows = 22;
            float y0 = 0.006f;
            float Y(int j) => Mathf.Lerp(y0, H, j / (float)rows);
            Vector2 WallUv(int i, int j, float y) => new Vector2(2f * i / Cols, y / H);

            // Outer wall (faces out).
            m.Grid(Cols, rows, (i, j) => { float a = Ang(i), y = Y(j); return Round(R(y) - Dent(a, y, out _), a, y); },
                (i, j) => WallUv(i, j, Y(j)),
                (i, j) =>
                {
                    float y = Y(j);
                    Dent(Ang(i), y, out float k);
                    return TrashMesh.Ao(Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, y / 0.06f)) * (1f - 0.3f * k));
                }, true);
            // Inner wall (faces the axis), from the floor up.
            float yi = 0.012f;
            float Yi(int j) => Mathf.Lerp(yi, H, j / (float)rows);
            m.Grid(Cols, rows, (i, j) => { float a = Ang(i), y = Yi(j); return Round(R(y) - Wall - Dent(a, y, out _), a, y); },
                (i, j) => WallUv(i, j, Yi(j)),
                (i, j) => TrashMesh.Ao(Mathf.Lerp(0.3f, 0.75f, Yi(j) / H)), false);

            // Rolled rim: a tube round the top edge (top band of the texture, no holes).
            const int tube = 10;
            float cr = Rt + RimR * 0.4f;
            m.Grid(Cols, tube, (i, j) =>
                {
                    float a = Ang(i), f = j * Mathf.PI * 2f / tube - Mathf.PI * 0.5f;
                    return Round(cr + Mathf.Cos(f) * RimR - Dent(a, H, out _), a, H + Mathf.Sin(f) * RimR);
                },
                (i, j) => new Vector2(2f * i / Cols, 0.975f + 0.02f * j / tube),
                (i, j) => TrashMesh.Ao(j > tube / 2 + 1 && j < tube - 1 ? 0.8f : 1f), true);

            // Bevelled foot ring touching the floor, and the inside floor.
            m.Grid(Cols, 1, (i, j) => { float a = Ang(i); return j == 0 ? Round(Rb - 0.005f, a, 0f) : Round(Rb, a, y0); },
                (i, j) => new Vector2(2f * i / Cols, 0.004f), (i, j) => TrashMesh.Ao(0.45f), true);
            m.Grid(Cols, 1, (i, j) => Round(j == 0 ? 0f : Rb - Wall, Ang(i), yi),
                (i, j) => new Vector2(2f * i / Cols, 0.01f), (i, j) => TrashMesh.Ao(0.3f), false);
            return m.ToMesh("TrashBinBody");
        }

        /// <summary>Liner bag. <paramref name="slip"/> = angle (rad) where it slid back inside, <paramref name="seed"/> varies the folds.</summary>
        public static Mesh Bag(float slip, int seed)
        {
            var m = new TrashMesh();
            float ph = seed * 1.37f;
            const int rows = 7;
            float rimTop = H + RimR + 0.0018f, cr = Rt + RimR * 0.4f;

            Vector3 P(int i, int j)
            {
                float a = Ang(i);
                // Over the rim (1) .. slipped inside (0), with a short transition.
                float d = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, slip * Mathf.Rad2Deg));
                float over = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 40f, d));
                float hang = 0.022f + 0.016f * Mathf.Sin(2f * a + ph) + 0.01f * Mathf.Sin(5f * a + ph * 2.3f) + 0.006f * Mathf.Sin(11f * a + ph);
                hang = Mathf.Max(0.01f, hang);
                float dn = Dent(a, H, out _);
                float fold = Mathf.Sin(13f * a + ph) * 0.6f + Mathf.Sin(29f * a + ph * 3.1f) * 0.4f;
                Vector2 rp; // (r, y)
                switch (j)
                {
                    case 0: rp = new Vector2(R(FillY - 0.02f) - Wall - 0.003f, FillY - 0.02f); break;
                    case 1: rp = new Vector2(R(H - 0.045f) - Wall - 0.0035f + 0.0015f * fold, H - 0.045f); break;
                    default:
                    {
                        Vector2 inside = new Vector2(R(H) - Wall - 0.004f + 0.002f * fold, Mathf.Lerp(H - 0.04f, H - 0.016f + 0.004f * fold, (j - 1) / 6f));
                        Vector2 outside = j switch
                        {
                            2 => new Vector2(R(H) - Wall - 0.0015f, H - 0.006f),
                            3 => new Vector2(cr - RimR * 0.2f, rimTop),
                            4 => new Vector2(cr + RimR + 0.0022f, H - 0.0015f),
                            5 => new Vector2(R(H - hang * 0.55f) + RimR * 0.9f + 0.003f + 0.002f * fold, H - hang * 0.55f),
                            _ => new Vector2(R(H - hang) + RimR * 0.7f + 0.006f + 0.005f * fold, H - hang + 0.003f * fold),
                        };
                        rp = Vector2.Lerp(inside, outside, over);
                        break;
                    }
                }
                return Round(rp.x - dn, a, rp.y);
            }
            m.Grid(Cols, rows - 1, P, (i, j) => new Vector2(4f * i / Cols, j * 0.12f),
                (i, j) => TrashMesh.Ao(j == 0 ? 0.3f : j == 1 ? 0.55f : j == 6 ? 0.85f : 1f), false);

            // Lumpy bottom of the bag (under the papers).
            const int rings = 4;
            float rIn = R(FillY - 0.02f) - Wall - 0.003f;
            m.Grid(Cols, rings, (i, j) =>
                {
                    float a = Ang(i), t = j / (float)rings;
                    float lump = 0.012f * Mathf.Sin(3f * a + ph) * Mathf.Sin(t * Mathf.PI) + 0.008f * (1f - t);
                    return Round(rIn * t, a, FillY - 0.02f + lump);
                },
                (i, j) => new Vector2(4f * i / Cols, j * 0.1f), (i, j) => TrashMesh.Ao(0.35f), false);
            return m.ToMesh("TrashBinBag");
        }
    }
}
