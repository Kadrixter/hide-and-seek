using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Raised parts on the nose, tail and flanks of the parked cars: outlines projected onto the panels like
    /// <see cref="CarLoft.ProjectedPatch"/>, but with side walls going back into the panel, so lamps, grille frames,
    /// slats, trims and plate surrounds are solid pieces with a real edge and shadow line, not paint.
    /// </summary>
    internal abstract partial class ParkedCar
    {
        /// <summary>Outline step shared with ProjectedPatch (same densified points: walls meet the face exactly).</summary>
        private static float ReliefStep => 0.03f * CarLoft.PatchCoarse;

        /// <summary>Surface points and normals of outline points (x signed by <paramref name="side"/>), pulled inwards
        /// when they miss the body like ProjectedPatch does.</summary>
        private void ProjectPoints(Vector2[] pts, float dir, float side, float minNz, out Vector3[] p, out Vector3[] n)
        {
            int cnt = pts.Length;
            p = new Vector3[cnt];
            n = new Vector3[cnt];
            var ok = new bool[cnt];
            int any = -1;
            for (int i = 0; i < cnt; i++)
            {
                Vector3 q = Vector3.zero, qn = Vector3.forward * dir;
                bool hit = false;
                for (int t = 0; t < 40 && !hit; t++)
                    hit = Body.Project(pts[i].x * side * (1f - t * 0.01f), pts[i].y, dir, out q, out qn) && Mathf.Abs(qn.z) >= minNz;
                p[i] = q;
                n[i] = qn;
                ok[i] = hit;
                if (hit) any = i;
            }
            if (any < 0) return;
            // a point that missed the body takes its last good neighbour (never the origin)
            for (int k = 1; k <= cnt; k++)
            {
                int i = (any + k) % cnt, prev = (any + k - 1) % cnt;
                if (!ok[i]) { p[i] = p[prev]; n[i] = n[prev]; }
            }
        }

        /// <summary>
        /// Solid raised part: the outline's face <paramref name="lift"/> proud of the panel, its walls running back
        /// <paramref name="sink"/> into it.
        /// </summary>
        protected void Relief(Vector2[] outline, float dir, float side, float lift, Material top, Material wall, float minNz = 0.3f,
            float sink = 0.006f)
        {
            var o = CarLoft.Densify(outline, ReliefStep);
            Body.ProjectedPatch(Mb, o, dir, side, lift, top, 4, minNz);
            Walls(o, dir, side, -sink, lift, wall, minNz, false);
        }

        /// <summary>Ring standing proud round an outline (lamp bezel, grille surround): face, outer and inner walls.</summary>
        protected void Frame(Vector2[] outline, float width, float dir, float side, float lift, Material top, Material wall,
            float minNz = 0.3f)
        {
            var inner = CarLoft.Densify(outline, ReliefStep);
            var outer = Offset(inner, width);
            ProjectPoints(inner, dir, side, minNz, out var pi, out var ni);
            ProjectPoints(outer, dir, side, minNz, out var po, out var no);
            int n = inner.Length;
            for (int i = 0; i < n; i++)
            {
                int k = (i + 1) % n;
                Mb.Quad(pi[i] + ni[i] * lift, pi[k] + ni[k] * lift, po[k] + no[k] * lift, po[i] + no[i] * lift, ni[i], ni[k], no[k], no[i], top);
            }
            Walls(outer, dir, side, -0.004f, lift, wall, minNz, false);
            Walls(inner, dir, side, -0.004f, lift, wall, minNz, true);
        }

        /// <summary>Side walls along a closed outline between two lifts; facing away from (or into) the outline.</summary>
        private void Walls(Vector2[] o, float dir, float side, float lo, float hi, Material m, float minNz, bool inward)
        {
            ProjectPoints(o, dir, side, minNz, out var p, out var n);
            int cnt = o.Length;
            Vector3 cen = Vector3.zero;
            foreach (var q in p) cen += q;
            cen /= cnt;
            var wn = new Vector3[cnt];
            for (int i = 0; i < cnt; i++)
            {
                Vector3 t = p[(i + 1) % cnt] - p[(i + cnt - 1) % cnt];
                Vector3 w = Vector3.Cross(t, n[i]);
                if (Vector3.Dot(w, p[i] - cen) < 0f) w = -w;
                wn[i] = (inward ? -w : w).normalized;
            }
            for (int i = 0; i < cnt; i++)
            {
                int k = (i + 1) % cnt;
                Mb.Quad(p[i] + n[i] * lo, p[k] + n[k] * lo, p[k] + n[k] * hi, p[i] + n[i] * hi, wn[i], wn[k], wn[k], wn[i], m);
            }
        }

        /// <summary>
        /// Bar lying on the nose / tail from a to b (outline coordinates, x signed by side): a front face and two side
        /// faces, <paramref name="thick"/> wide, standing <paramref name="lift"/> proud. Grille slats, mesh bars.
        /// </summary>
        protected void Bar(Vector2 a, Vector2 b, float thick, float dir, float side, float lift, Material m, int seg = 3, float minNz = 0.25f)
        {
            Vector2 d = (b - a).normalized, perp = new Vector2(-d.y, d.x) * (thick * 0.5f);
            var pts = new Vector2[(seg + 1) * 2];
            for (int k = 0; k <= seg; k++)
            {
                Vector2 c = Vector2.Lerp(a, b, k / (float)seg);
                pts[k * 2] = c + perp;
                pts[k * 2 + 1] = c - perp;
            }
            ProjectPoints(pts, dir, side, minNz, out var p, out var n);
            for (int k = 0; k < seg; k++)
            {
                int a0 = k * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                Mb.Quad(p[a0] + n[a0] * lift, p[b0] + n[b0] * lift, p[b1] + n[b1] * lift, p[a1] + n[a1] * lift, n[a0], n[b0], n[b1], n[a1], m);
                Vector3 s0 = (p[a0] - p[a1]).normalized, s1 = (p[b0] - p[b1]).normalized;
                Mb.Quad(p[a0] - n[a0] * 0.003f, p[b0] - n[b0] * 0.003f, p[b0] + n[b0] * lift, p[a0] + n[a0] * lift, s0, s1, s1, s0, m);
                Mb.Quad(p[a1] - n[a1] * 0.003f, p[b1] - n[b1] * 0.003f, p[b1] + n[b1] * lift, p[a1] + n[a1] * lift, -s0, -s1, -s1, -s0, m);
            }
        }

        /// <summary>Outline offset by d along its edge normals (constant width, unlike CarLoft.Grow).</summary>
        protected static Vector2[] Offset(Vector2[] o, float d)
        {
            int n = o.Length;
            float area = 0f;
            for (int i = 0; i < n; i++) { Vector2 a = o[i], b = o[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            float s = area >= 0f ? 1f : -1f; // counter-clockwise: outward normal is (dy, -dx)
            var r = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 e0 = (o[i] - o[(i + n - 1) % n]).normalized, e1 = (o[(i + 1) % n] - o[i]).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x) * s, n1 = new Vector2(e1.y, -e1.x) * s;
                Vector2 m = (n0 + n1).normalized;
                float cos = Mathf.Max(0.5f, Vector2.Dot(m, n1));
                r[i] = o[i] + m * (d / cos);
            }
            return r;
        }

        /// <summary>
        /// Raised band on a flank between z0 and z1 and heights y0..y1 (sill trims, rubbing strips): face lifted along
        /// the panel normal, walls along the top edge and both ends.
        /// </summary>
        protected void SideBand(float side, float z0, float z1, float y0, float y1, float lift, Material m, int nz = 8)
        {
            var top = new Vector3[nz + 1];
            var bot = new Vector3[nz + 1];
            var nt = new Vector3[nz + 1];
            var nb = new Vector3[nz + 1];
            for (int k = 0; k <= nz; k++)
            {
                float z = Mathf.Lerp(z0, z1, k / (float)nz);
                (top[k], nt[k]) = Side(z, y1, side);
                (bot[k], nb[k]) = Side(z, y0, side);
            }
            for (int k = 0; k < nz; k++)
            {
                Mb.Quad(bot[k] + nb[k] * lift, bot[k + 1] + nb[k + 1] * lift, top[k + 1] + nt[k + 1] * lift, top[k] + nt[k] * lift,
                    nb[k], nb[k + 1], nt[k + 1], nt[k], m);
                Vector3 up = Vector3.Cross(top[k + 1] - top[k], nt[k]);
                if (up.y < 0f) up = -up;
                up.Normalize();
                Mb.Quad(top[k] - nt[k] * 0.003f, top[k + 1] - nt[k + 1] * 0.003f, top[k + 1] + nt[k + 1] * lift, top[k] + nt[k] * lift, up, up, up, up, m);
            }
            foreach (int k in new[] { 0, nz })
            {
                Vector3 e = new Vector3(0f, 0f, k == 0 ? Mathf.Sign(z0 - z1) : Mathf.Sign(z1 - z0));
                Mb.Quad(bot[k] - nb[k] * 0.003f, top[k] - nt[k] * 0.003f, top[k] + nt[k] * lift, bot[k] + nb[k] * lift, e, e, e, e, m);
            }
        }

        /// <summary>
        /// Wheel arch lip (paint) or plastic cladding (SUV): a band round the arch rising from the wing to a rounded
        /// crest <paramref name="lift"/> proud, rolling over the arch edge and tucking back under it. Fades out at
        /// both lower ends.
        /// </summary>
        protected void ArchTrim(Arch a, float side, float reach, float lift, Material m)
        {
            const int n = 20;
            float yBot = Body.SectionAt(a.Z)[CarShapes.BUnder].y;
            float r0 = a.R + reach;
            float s0 = Mathf.Asin(Mathf.Clamp((yBot + 0.03f - a.Y) / r0, -1f, 1f));
            float[] rr = { r0, a.R + reach * 0.45f, a.R + 0.006f, a.R - 0.002f };
            float[] lf = { 0f, 0.85f, 1f, 0.75f };
            var P = new Vector3[n + 1, 5];
            var N = new Vector3[n + 1, 5];
            Vector3 X = Vector3.right * side;
            for (int i = 0; i <= n; i++)
            {
                float th = Mathf.Lerp(s0, Mathf.PI - s0, i / (float)n);
                Vector3 rad = new Vector3(0f, Mathf.Sin(th), Mathf.Cos(th));
                float f = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((a.Y + rad.y * a.R - yBot - 0.02f) / 0.12f));
                for (int k = 0; k < 4; k++)
                {
                    var (p, pn) = Side(a.Z + rad.z * rr[k], a.Y + rad.y * rr[k], side);
                    P[i, k] = p + pn * (lift * lf[k] * f + (k == 0 ? 0.0008f : 0.0015f)); // never flush: no z-fighting at the ends
                    N[i, k] = k == 0 ? pn : k == 1 ? (pn + rad * 0.3f).normalized : k == 2 ? (pn - rad * 0.2f).normalized : (X * 0.4f - rad).normalized;
                }
                // the sheet's edge returning inwards under the lip
                P[i, 4] = P[i, 3] - X * (lift * f + 0.008f) - rad * 0.004f;
                N[i, 4] = -rad;
            }
            for (int i = 0; i < n; i++)
            for (int k = 0; k < 4; k++)
                Mb.Quad(P[i, k], P[i + 1, k], P[i + 1, k + 1], P[i, k + 1], N[i, k], N[i + 1, k], N[i + 1, k + 1], N[i, k + 1], m);
        }

        /// <summary>
        /// Smooth surface from a grid of points (rows x columns, columns closed round), normals from central
        /// differences turned to agree with <paramref name="outward"/>(point).
        /// </summary>
        protected void Grid(Vector3[,] P, Material m, System.Func<Vector3, Vector3> outward)
        {
            int rows = P.GetLength(0), cols = P.GetLength(1);
            var N = new Vector3[rows, cols];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Vector3 du = P[r, (c + 1) % cols] - P[r, (c + cols - 1) % cols];
                Vector3 dv = P[Mathf.Min(rows - 1, r + 1), c] - P[Mathf.Max(0, r - 1), c];
                Vector3 nn = Vector3.Cross(du, dv);
                if (Vector3.Dot(nn, outward(P[r, c])) < 0f) nn = -nn;
                N[r, c] = nn.sqrMagnitude > 1e-14f ? nn.normalized : outward(P[r, c]).normalized;
            }
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols; c++)
            {
                int c1 = (c + 1) % cols;
                Mb.Quad(P[r, c], P[r, c1], P[r + 1, c1], P[r + 1, c], N[r, c], N[r, c1], N[r + 1, c1], N[r + 1, c], m);
            }
        }
    }
}
