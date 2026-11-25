using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Mesh shapes of the ball return (2026-11-25), on top of <see cref="PoolTableMesh"/>: rods with rounded ends,
    /// rings round a 3D axis, flat steel straps bent along a polyline and small domed screw heads on any face.
    /// </summary>
    internal static class PoolReturnMesh
    {
        /// <summary>Round rod along a 3D polyline with half-sphere ends (no open tube ends from any angle).</summary>
        public static void Rod(PoolTableMesh b, IList<Vector3> pts, float r, int sides = 10)
        {
            if (pts.Count < 2) return;
            var p = new List<Vector3>(pts.Count + 8);
            var rad = new List<float>(pts.Count + 8);
            Vector3 t0 = (pts[0] - pts[1]).normalized, t1 = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
            const int capN = 3;
            for (int k = capN; k >= 1; k--)
            {
                float a = k / (float)capN * Mathf.PI * 0.5f;
                p.Add(pts[0] + t0 * (Mathf.Sin(a) * r));
                rad.Add(Mathf.Max(0.0002f, Mathf.Cos(a) * r));
            }
            foreach (var q in pts) { p.Add(q); rad.Add(r); }
            for (int k = 1; k <= capN; k++)
            {
                float a = k / (float)capN * Mathf.PI * 0.5f;
                p.Add(pts[pts.Count - 1] + t1 * (Mathf.Sin(a) * r));
                rad.Add(Mathf.Max(0.0002f, Mathf.Cos(a) * r));
            }
            Tube(b, p, rad, sides, false);
        }

        /// <summary>Closed ring of radius <paramref name="ringR"/> round <paramref name="axis"/> at <paramref name="c"/>, wire radius <paramref name="r"/>.</summary>
        public static void Ring(PoolTableMesh b, Vector3 c, Vector3 axis, float ringR, float r, int segs = 24, int sides = 8)
        {
            Basis(axis, out var u, out var v);
            var pts = new List<Vector3>(segs);
            var rad = new List<float>(segs);
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                pts.Add(c + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * ringR);
                rad.Add(r);
            }
            Tube(b, pts, rad, sides, true);
        }

        /// <summary>Tube with a radius per point, parallel-transported frame (closed = a loop).</summary>
        public static void Tube(PoolTableMesh b, IList<Vector3> pts, IList<float> rad, int sides, bool closed)
        {
            int n = pts.Count, rings = closed ? n + 1 : n;
            Vector3 T(int i) => closed
                ? (pts[(i + 1) % n] - pts[(i + n - 1) % n]).normalized
                : (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 t0 = T(0);
            Vector3 a = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var start = new int[rings];
            float len = 0f;
            for (int k = 0; k < rings; k++)
            {
                int i = k % n;
                if (k > 0) len += Vector3.Distance(pts[i], pts[(k - 1) % n]);
                Vector3 t = T(i);
                a = (a - t * Vector3.Dot(a, t)).normalized;
                Vector3 bb = Vector3.Cross(t, a);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = a * Mathf.Cos(ang) + bb * Mathf.Sin(ang);
                    int id = b.Vert(pts[i] + dir * rad[i], dir, new Vector2(len * 2f, s / (float)sides));
                    if (s == 0) start[k] = id;
                }
            }
            for (int k = 0; k + 1 < rings; k++)
                for (int s = 0; s < sides; s++)
                {
                    int i0 = start[k] + s, i1 = start[k] + (s + 1) % sides;
                    int j0 = start[k + 1] + s, j1 = start[k + 1] + (s + 1) % sides;
                    b.Quad(i0, i1, j1, j0);
                }
        }

        /// <summary>
        /// Flat strap (<paramref name="width"/> along <paramref name="across"/>, <paramref name="thick"/> thick) bent along a
        /// polyline that lies in the plane normal to <paramref name="across"/>; flat faces, square ends.
        /// </summary>
        public static void Strap(PoolTableMesh b, IList<Vector3> pts, Vector3 across, float width, float thick)
        {
            int n = pts.Count;
            if (n < 2) return;
            across.Normalize();
            var off = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 tp = i > 0 ? (pts[i] - pts[i - 1]).normalized : Vector3.zero;
                Vector3 tn = i < n - 1 ? (pts[i + 1] - pts[i]).normalized : Vector3.zero;
                Vector3 np = Vector3.Cross(across, tp), nn = Vector3.Cross(across, tn);
                if (i == 0) np = nn;
                if (i == n - 1) nn = np;
                Vector3 m = (np + nn).normalized;
                off[i] = m / Mathf.Max(0.35f, Vector3.Dot(m, nn));
            }
            float hw = width * 0.5f, ht = thick * 0.5f;
            for (int i = 0; i + 1 < n; i++)
            {
                Vector3 t = (pts[i + 1] - pts[i]).normalized, nrm = Vector3.Cross(across, t).normalized;
                Vector3 a0 = pts[i], a1 = pts[i + 1];
                Face(b, a0 + off[i] * ht - across * hw, a1 + off[i + 1] * ht - across * hw, a1 + off[i + 1] * ht + across * hw, a0 + off[i] * ht + across * hw, nrm);
                Face(b, a0 - off[i] * ht - across * hw, a1 - off[i + 1] * ht - across * hw, a1 - off[i + 1] * ht + across * hw, a0 - off[i] * ht + across * hw, -nrm);
                Face(b, a0 + off[i] * ht + across * hw, a1 + off[i + 1] * ht + across * hw, a1 - off[i + 1] * ht + across * hw, a0 - off[i] * ht + across * hw, across);
                Face(b, a0 + off[i] * ht - across * hw, a1 + off[i + 1] * ht - across * hw, a1 - off[i + 1] * ht - across * hw, a0 - off[i] * ht - across * hw, -across);
            }
            for (int e = 0; e < 2; e++)
            {
                int i = e == 0 ? 0 : n - 1;
                Vector3 t = e == 0 ? (pts[0] - pts[1]).normalized : (pts[n - 1] - pts[n - 2]).normalized;
                Face(b, pts[i] + off[i] * ht - across * hw, pts[i] + off[i] * ht + across * hw, pts[i] - off[i] * ht + across * hw, pts[i] - off[i] * ht - across * hw, t);
            }
        }

        static void Face(PoolTableMesh b, Vector3 a, Vector3 c, Vector3 d, Vector3 e, Vector3 n)
        {
            var uv = Vector2.zero;
            b.Quad(b.Vert(a, n, uv), b.Vert(c, n, new Vector2(1f, 0f)), b.Vert(d, n, Vector2.one), b.Vert(e, n, new Vector2(0f, 1f)));
        }

        /// <summary>Low domed screw head (radius <paramref name="r"/>) sitting on a face whose outward normal is <paramref name="axis"/>.</summary>
        public static void Dome(PoolTableMesh b, Vector3 c, Vector3 axis, float r, float h)
        {
            Basis(axis, out var u, out var v);
            axis.Normalize();
            const int seg = 12, rings = 3;
            int start = -1;
            for (int k = 0; k <= rings; k++)
            {
                float a = k / (float)rings * Mathf.PI * 0.5f, rr = Mathf.Max(0.0002f, r * Mathf.Cos(a)), hh = h * Mathf.Sin(a);
                for (int s = 0; s < seg; s++)
                {
                    float ang = s / (float)seg * Mathf.PI * 2f;
                    Vector3 dir = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                    int id = b.Vert(c + dir * rr + axis * hh, (dir * Mathf.Cos(a) * 0.6f + axis * (Mathf.Sin(a) + 0.4f)).normalized, Vector2.zero);
                    if (start < 0) start = id;
                }
            }
            for (int k = 0; k < rings; k++)
                for (int s = 0; s < seg; s++)
                {
                    int i0 = start + k * seg + s, i1 = start + k * seg + (s + 1) % seg;
                    b.Quad(i0, i1, i1 + seg, i0 + seg);
                }
        }

        /// <summary>Two unit vectors perpendicular to <paramref name="axis"/> and to each other.</summary>
        public static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            axis.Normalize();
            u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            v = Vector3.Cross(axis, u);
        }
    }
}
