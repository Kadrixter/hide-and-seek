using UnityEngine;
using static HideAndSeek.Map.Editor.CarShapes;

namespace HideAndSeek.Map.Editor
{
    /// <summary>Golf IV side glass: quarter light under the wide C-pillar, recessed door glass, seals, belt moulding.</summary>
    internal sealed partial class VwGolf4
    {
        // quarter light: B-pillar at QB, the arched rear edge runs from the roof at QE down and forward to the belt at QA
        private const float QB = -0.47f, QA = -0.85f, QE = -1.3f, Recess = 0.012f;

        /// <summary>Glass-height fraction (0 belt .. 1 top) where the arched rear edge crosses z.</summary>
        private const float EdgePow = 2.2f;

        /// <summary>The arch leaves the roof line tangentially and meets the belt at about 60 degrees.</summary>
        private static float EdgeH(float z)
        {
            float t = Mathf.InverseLerp(QE, QA, z);
            return 1f - Mathf.Pow(t, EdgePow);
        }

        private static float EdgeZ(float h) => Mathf.Lerp(QE, QA, Mathf.Pow(Mathf.Clamp01(1f - h), 1f / EdgePow));

        private Vector2 YAt(float z, float h0, float h1)
        {
            var g = GlassY(z);
            return new Vector2(Mathf.Lerp(g.x, g.y, h0), Mathf.Lerp(g.x, g.y, h1));
        }

        /// <summary>
        /// The wide C-pillar is real body skin (the greenhouse surface itself, with its own curvature into the roof and
        /// the wing); the quarter glass sits 12 mm inside it and a black rubber return lines the whole opening.
        /// </summary>
        private void QuarterLight()
        {
            var lining = Lib["Car_Headliner"];
            foreach (float s in new[] { -1f, 1f })
            {
                System.Func<float, float> zA = t => Mathf.Lerp(QE, QA, t), zB = t => Mathf.Lerp(QA, QB, t);
                Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), 0f, EdgeH(zA(t))), CSeal, CGlassTop, 0f, Paint, 72, 8);
                Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), 0f, EdgeH(zA(t))), CSeal, CGlassTop, -0.02f, lining, 72, 8, true);
                foreach (bool inner in new[] { false, true })
                {
                    Cabin.PatchY(Mb, s, QE, QA, t => YAt(zA(t), EdgeH(zA(t)), 1f), CSeal, CGlassTop, -Recess, Glass, 72, 4, inner);
                    Cabin.PatchY(Mb, s, QA, QB, t => YAt(zB(t), 0f, 1f), CSeal, CGlassTop, -Recess, Glass, 8, 4, inner);
                }
                // opening perimeter (z, h): top edge, arch, belt, B-pillar
                var rim = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i <= 10; i++) rim.Add(new Vector2(Mathf.Lerp(QB, QE, i / 10f), 0.995f));
                for (int i = 1; i <= 48; i++) { float h = 1f - i / 48f; rim.Add(new Vector2(EdgeZ(h), Mathf.Clamp(h, 0.005f, 0.995f))); }
                for (int i = 1; i <= 8; i++) rim.Add(new Vector2(zB(i / 8f), 0.005f));
                GlassRim(rim, s, new Vector2(-0.75f, 0.5f));
            }
        }

        private const float DB = -0.42f, DF = 0.9f;

        /// <summary>Door glass set 12 mm into its frame like the quarter light, sealed all round, belt moulding under it.</summary>
        private void DoorGlass()
        {
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (bool inner in new[] { false, true })
                    Cabin.PatchY(Mb, s, DB, DF, t => YAt(Mathf.Lerp(DB, DF, t), 0f, 1f), CSeal, CGlassTop, -Recess, Glass, 48, 4, inner);
                var rim = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i <= 40; i++) rim.Add(new Vector2(Mathf.Lerp(DB, DF, i / 40f), 0.995f));
                for (int i = 1; i <= 4; i++) rim.Add(new Vector2(DF, Mathf.Lerp(0.995f, 0.005f, i / 4f)));
                for (int i = 1; i <= 40; i++) rim.Add(new Vector2(Mathf.Lerp(DF, DB, i / 40f), 0.005f));
                for (int i = 1; i <= 6; i++) rim.Add(new Vector2(DB, Mathf.Lerp(0.005f, 0.995f, i / 6f)));
                GlassRim(rim, s, new Vector2(0.2f, 0.5f));
                BeltMoulding(s, QA + 0.02f, DF - 0.03f);
            }
        }

        /// <summary>Recess wall (12 mm deep) and matt rubber seal round a glass opening given as (z, glass-height fraction).</summary>
        private void GlassRim(System.Collections.Generic.List<Vector2> rim, float s, Vector2 centre)
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
                // inward direction on the skin, perpendicular to the edge, smoothed per point
                Vector3 tan = P[Mathf.Min(i + 1, m - 1)] - P[Mathf.Max(i - 1, 0)];
                Vector3 w = Vector3.Cross(N[i], tan).normalized;
                if (Vector3.Dot(w, cp - P[i]) < 0f) w = -w;
                W[i] = w;
            }
            var seal = Lib["Car_Seam"];
            for (int i = 0; i < m - 1; i++)
            {
                Vector3 wa = W[i], wb = W[i + 1];
                Mb.Quad(P[i], P[i + 1], P[i + 1] - N[i + 1] * Recess, P[i] - N[i] * Recess, wa, wb, wb, wa, seal);
                // matt rubber seal laid over the joint along the curve (covers the row steps of skin and glass)
                Mb.Quad(P[i] - wa * 0.006f + N[i] * 0.0015f, P[i + 1] - wb * 0.006f + N[i + 1] * 0.0015f,
                    P[i + 1] + wb * 0.008f + N[i + 1] * 0.0015f, P[i] + wa * 0.008f + N[i] * 0.0015f, N[i], N[i + 1], N[i + 1], N[i], seal);
            }
        }

        /// <summary>Black belt moulding under the side glass: a rounded rubber lip standing proud of the belt trim.</summary>
        private void BeltMoulding(float s, float z0, float z1)
        {
            // profile (drop below the glass edge, stand-off from the skin)
            var prof = new[] { new Vector2(0.026f, 0f), new Vector2(0.024f, 0.004f), new Vector2(0.012f, 0.0065f), new Vector2(0.003f, 0.006f),
                new Vector2(-0.001f, 0.0035f), new Vector2(-0.002f, 0f) };
            const int n = 60;
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
