using System.Collections.Generic;
using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>5-door glazing of the wreck: rear door glass with its fixed quarter light under the wide C-pillar, the
    /// right front door glass smashed out (crumbs left in the seal), seals, belt mouldings, door seams and handles.</summary>
    internal sealed partial class GolfWreck
    {
        private static float EdgeH(float z) => 1f - Mathf.Pow(Mathf.InverseLerp(QE, QA, z), 2.2f);
        private static float EdgeZ(float h) => Mathf.Lerp(QE, QA, Mathf.Pow(Mathf.Clamp01(1f - h), 1f / 2.2f));

        private Vector2 YAt(float z, float h0, float h1)
        {
            var g = GlassY(z);
            return new Vector2(Mathf.Lerp(g.x, g.y, h0), Mathf.Lerp(g.x, g.y, h1));
        }

        private void Seams()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                // front door seams only on the right: the left door is open
                if (s > 0f) { SeamAt(s, DF, 0.36f, 0.95f); SeamAt(s, DB, 0.36f, 0.99f); }
                else SeamAt(s, DB - 0.06f, 0.36f, 0.99f);
                // rear door: rear edge down from the belt, then round the wheel arch to the sill
                var path = new List<Vector2>();
                for (int k = 0; k <= 6; k++) path.Add(new Vector2(QA + 0.005f, Mathf.Lerp(0.985f, 0.7f, k / 6f)));
                for (int k = 1; k <= 12; k++)
                {
                    float a = Mathf.Lerp(Mathf.PI * 0.42f, Mathf.PI * 0.05f, k / 12f);
                    path.Add(new Vector2(RearArch.Z + Mathf.Cos(a) * 0.43f, RearArch.Y + Mathf.Sin(a) * 0.43f));
                }
                SeamPath(path, s);
                // sill line under the doors
                Body.PatchY(Mb, s, RB, s > 0 ? DF : DB, t => new Vector2(SillY - 0.003f, SillY + 0.002f), BLip, BShoulder + 1, 0.0012f, Seam, 12, 1);
                // rocker strip (GTI: body-coloured sill with a black lower edge)
                Body.PatchY(Mb, s, -0.92f, 0.84f, t => new Vector2(0.2f, 0.235f), BLip, BShoulder, 0.003f, Lib["Wreck_Plastic"], 16, 2);
                // handles: front (right only, the left one is on the open door) and rear
                if (s > 0f) { var (hp, hn) = Side(DB + 0.12f, 0.87f, s); CarKit.Handle(Mb, hp, hn, Paint, 0.12f); }
                var (rp, rn) = Side(RB + 0.14f, 0.87f, s);
                CarKit.Handle(Mb, rp, rn, Paint, 0.12f);
            }
            // fuel flap (right rear wing)
            var (fp, fn) = Side(-1.55f, 0.84f, 1f);
            CarWheels.Lathe(Mb, fp, fn, new[] { new Vector2(0.065f, 0.0005f), new Vector2(0.06f, 0.0012f), new Vector2(0f, 0.0012f) }, 18, Seam);
            CarWheels.Lathe(Mb, fp + fn * 0.0013f, fn, new[] { new Vector2(0.058f, 0f), new Vector2(0f, 0f) }, 18, Paint);
        }

        private void SeamAt(float s, float z, float y0, float y1) =>
            Body.PatchY(Mb, s, z - 0.0025f, z + 0.0025f, t => new Vector2(y0, y1), BLip, BShoulder + 1, 0.0012f, Seam, 1, 8);

        /// <summary>Panel gap along a (z, y) path on the side of the body.</summary>
        private void SeamPath(List<Vector2> zy, float s, float width = 0.005f)
        {
            for (int k = 0; k < zy.Count - 1; k++)
            {
                var (a, na) = Side(zy[k].x, zy[k].y, s);
                var (b, nb) = Side(zy[k + 1].x, zy[k + 1].y, s);
                Vector3 d = (b - a).normalized;
                Vector3 wa = Vector3.Cross(na, d).normalized * (width * 0.5f), wb = Vector3.Cross(nb, d).normalized * (width * 0.5f);
                Mb.Quad(a - wa + na * 0.0012f, a + wa + na * 0.0012f, b + wb + nb * 0.0012f, b - wb + nb * 0.0012f, na, na, nb, nb, Seam);
            }
        }

        private void Glasses()
        {
            var lining = Lib["Car_Headliner"];
            foreach (float s in new[] { -1f, 1f })
            {
                // C-pillar skin over the arched edge of the quarter light (body colour), lining inside
                System.Func<float, float> zA = t => Mathf.Lerp(QE, QA, t), zB = t => Mathf.Lerp(QA, DV0, t);
                Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), 0f, EdgeH(zA(t))), CSeal, CGlassTop, 0f, Paint, 64, 8);
                Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), 0f, EdgeH(zA(t))), CSeal, CGlassTop, -0.02f, lining, 64, 8, true);
                foreach (bool inner in new[] { false, true })
                {
                    Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), EdgeH(zA(t)), 1f), CSeal, CGlassTop, -Recess, Glass, 64, 4, inner);
                    Cabin.PatchY(Mb, s, QA, DV0, t => YAt(zB(t), 0f, 1f), CSeal, CGlassTop, -Recess, Glass, 6, 4, inner);
                    Cabin.PatchY(Mb, s, RG0, RG1, t => YAt(Mathf.Lerp(RG0, RG1, t), 0f, 1f), CSeal, CGlassTop, -Recess, Glass, 24, 4, inner);
                }
                var rim = new List<Vector2>();
                for (int i = 0; i <= 8; i++) rim.Add(new Vector2(Mathf.Lerp(DV0, QE, i / 8f), 0.995f));
                for (int i = 1; i <= 40; i++) { float h = 1f - i / 40f; rim.Add(new Vector2(EdgeZ(h), Mathf.Clamp(h, 0.005f, 0.995f))); }
                for (int i = 1; i <= 6; i++) rim.Add(new Vector2(Mathf.Lerp(QA, DV0, i / 6f), 0.005f));
                GlassRim(rim, s, new Vector2(-1.05f, 0.5f));
                GlassRim(Frame(RG0, RG1), s, new Vector2((RG0 + RG1) * 0.5f, 0.5f));
                BeltMoulding(s, QA + 0.02f, RG1 - 0.01f);
                if (s > 0f)
                {
                    // right front: the glass is gone, only its seal and a few crumbs in the slot
                    GlassRim(Frame(FG0, DF + 0.02f), s, new Vector2(0.4f, 0.5f));
                    BeltMoulding(s, FG0 + 0.01f, DF - 0.03f);
                    var rng = new System.Random(311);
                    for (int k = 0; k < 9; k++)
                    {
                        float z = Mathf.Lerp(FG0 + 0.05f, DF - 0.1f, (float)rng.NextDouble());
                        var (p, n) = Cabin.OnSideAtY(z, GlassY(z).x + 0.004f, CSeal, CGlassTop, s);
                        float h = 0.012f + (float)rng.NextDouble() * 0.035f;
                        var up = Vector3.up;
                        Vector3 q0 = p - n * Recess + Vector3.forward * 0.02f, q1 = p - n * Recess - Vector3.forward * 0.018f;
                        Mb.Tri(q0, q1, (q0 + q1) * 0.5f + up * h + Vector3.forward * (float)(rng.NextDouble() - 0.5) * 0.02f, n, n, n, Glass);
                        Mb.Tri(q1, q0, (q0 + q1) * 0.5f + up * h, -n, -n, -n, Glass);
                    }
                }
            }
            Wipers(0.92f, 0.1f, 0.55f, 0.06f);
            // the passenger-side wiper arm is bent up off the glass (side photo)
            var (wp, wn) = Cabin.OnTopAtX(0.92f, 0.66f, CRoofEdge, CCentre, 1f);
            CarKit.Wiper(Mb, wp + wn * 0.008f, (new Vector3(-0.6f, 0f, -0.3f) + wn * 0.3f).normalized, wn, 0.5f);
            // rear-view mirror with an air freshener tree on a string
            var mc = new Vector3(0f, 1.33f, 0.2f);
            CarBlob.Blob(Mb, mc, Quaternion.Euler(12f, 0, 0), new Vector3(0.11f, 0.03f, 0.012f), 0.4f, Lib["Wreck_Plastic"], 0.5f, 1f, 14, 8);
            Mb.Cylinder(mc + new Vector3(0, 0.02f, 0.01f), mc + new Vector3(0, 0.07f, 0.05f), 0.007f, 6, Lib["Wreck_Plastic"]);
            Mb.Cylinder(mc + new Vector3(0.05f, -0.02f, 0f), mc + new Vector3(0.055f, -0.13f, 0.005f), 0.0012f, 4, Lib["Text_White"]);
            var tree = new[] { new Vector2(0f, 0.05f), new Vector2(0.018f, 0.02f), new Vector2(0.01f, 0.02f), new Vector2(0.026f, -0.015f),
                new Vector2(0.006f, -0.015f), new Vector2(0.006f, -0.035f), new Vector2(-0.006f, -0.035f), new Vector2(-0.006f, -0.015f),
                new Vector2(-0.026f, -0.015f), new Vector2(-0.01f, 0.02f), new Vector2(-0.018f, 0.02f) };
            var tc = mc + new Vector3(0.056f, -0.18f, 0.006f);
            var rot = Quaternion.Euler(0, 30f, 4f);
            for (int i = 1; i < tree.Length - 1; i++)
                foreach (float f in new[] { 1f, -1f })
                    Mb.Tri(tc + rot * new Vector3(tree[0].x, tree[0].y, 0.001f * f), tc + rot * new Vector3(tree[i].x, tree[i].y, 0.001f * f),
                        tc + rot * new Vector3(tree[i + 1].x, tree[i + 1].y, 0.001f * f), rot * Vector3.forward * f, rot * Vector3.forward * f, rot * Vector3.forward * f, Lib["Wreck_Freshener"]);
        }

        /// <summary>Rectangular glass opening z0..z1 as (z, height fraction) round the frame.</summary>
        private static List<Vector2> Frame(float z0, float z1)
        {
            var rim = new List<Vector2>();
            for (int i = 0; i <= 30; i++) rim.Add(new Vector2(Mathf.Lerp(z0, z1, i / 30f), 0.995f));
            for (int i = 1; i <= 4; i++) rim.Add(new Vector2(z1, Mathf.Lerp(0.995f, 0.005f, i / 4f)));
            for (int i = 1; i <= 30; i++) rim.Add(new Vector2(Mathf.Lerp(z1, z0, i / 30f), 0.005f));
            for (int i = 1; i <= 4; i++) rim.Add(new Vector2(z0, Mathf.Lerp(0.005f, 0.995f, i / 4f)));
            return rim;
        }

        /// <summary>Recess wall and rubber seal round a glass opening given as (z, glass-height fraction).</summary>
        private void GlassRim(List<Vector2> rim, float s, Vector2 centre)
        {
            var (cp, _) = Cabin.OnSideAtY(centre.x, YAt(centre.x, centre.y, centre.y).x, CSeal, CGlassTop, s);
            int m = rim.Count;
            var P = new Vector3[m];
            var N = new Vector3[m];
            var W = new Vector3[m];
            for (int i = 0; i < m; i++)
                (P[i], N[i]) = Cabin.OnSideAtY(rim[i].x, YAt(rim[i].x, rim[i].y, rim[i].y).x, CSeal, CGlassTop, s);
            for (int i = 0; i < m; i++)
            {
                Vector3 tan = P[Mathf.Min(i + 1, m - 1)] - P[Mathf.Max(i - 1, 0)];
                Vector3 w = Vector3.Cross(N[i], tan).normalized;
                if (Vector3.Dot(w, cp - P[i]) < 0f) w = -w;
                W[i] = w;
            }
            for (int i = 0; i < m - 1; i++)
            {
                Vector3 wa = W[i], wb = W[i + 1];
                Mb.Quad(P[i], P[i + 1], P[i + 1] - N[i + 1] * Recess, P[i] - N[i] * Recess, wa, wb, wb, wa, Seam);
                Mb.Quad(P[i] - wa * 0.006f + N[i] * 0.0015f, P[i + 1] - wb * 0.006f + N[i + 1] * 0.0015f,
                    P[i + 1] + wb * 0.008f + N[i + 1] * 0.0015f, P[i] + wa * 0.008f + N[i] * 0.0015f, N[i], N[i + 1], N[i + 1], N[i], Seam);
            }
        }

        /// <summary>Black rubber belt moulding under the side glass.</summary>
        private void BeltMoulding(float s, float z0, float z1)
        {
            var prof = new[] { new Vector2(0.026f, 0f), new Vector2(0.024f, 0.004f), new Vector2(0.012f, 0.0065f), new Vector2(0.003f, 0.006f),
                new Vector2(-0.001f, 0.0035f), new Vector2(-0.002f, 0f) };
            const int n = 40;
            var P = new Vector3[n + 1, prof.Length];
            var H = new Vector3[n + 1, prof.Length];
            for (int i = 0; i <= n; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)n);
                float end = Mathf.Clamp01(Mathf.Min(z - z0, z1 - z) / 0.02f);
                float y0 = GlassY(z).x;
                for (int k = 0; k < prof.Length; k++)
                {
                    var (p, nn) = Cabin.OnSideAtY(z, y0 - prof[k].x, CBelt, CGlassTop, s);
                    P[i, k] = p + nn * (prof[k].y * end + 0.0005f);
                    H[i, k] = nn;
                }
            }
            Grid(P, H, Trim);
        }

        /// <summary>Quad grid with smooth normals from the grid itself, oriented along the hint normals.</summary>
        private void Grid(Vector3[,] P, Vector3[,] hint, Material m)
        {
            int a = P.GetLength(0), b = P.GetLength(1);
            var N = new Vector3[a, b];
            for (int i = 0; i < a; i++)
            for (int k = 0; k < b; k++)
            {
                Vector3 du = P[Mathf.Min(i + 1, a - 1), k] - P[Mathf.Max(i - 1, 0), k];
                Vector3 dv = P[i, Mathf.Min(k + 1, b - 1)] - P[i, Mathf.Max(k - 1, 0)];
                Vector3 nn = Vector3.Cross(du, dv);
                if (nn.sqrMagnitude < 1e-12f) nn = hint[i, k];
                N[i, k] = Vector3.Dot(nn, hint[i, k]) < 0f ? -nn.normalized : nn.normalized;
            }
            for (int i = 0; i < a - 1; i++)
            for (int k = 0; k < b - 1; k++)
                Mb.Quad(P[i, k], P[i + 1, k], P[i + 1, k + 1], P[i, k + 1], N[i, k], N[i + 1, k], N[i + 1, k + 1], N[i, k + 1], m);
        }
    }
}
