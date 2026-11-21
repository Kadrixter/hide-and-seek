using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// A smooth car surface lofted through half cross-sections (right side, x >= 0) placed at ascending z stations,
    /// mirrored to the left. Normals are smoothed across the grid except along sharp ring indices (body creases) and
    /// sharp stations. The surface can be sampled to lay lamps, seams and vents exactly on the panels.
    /// Normal convention: cross(along section, along +z) points outwards, so sections go bottom -> top on the side.
    /// </summary>
    internal sealed partial class CarLoft
    {
        public readonly float[] Z;
        public readonly Vector2[][] S;
        public readonly HashSet<int> SharpRing = new HashSet<int>();
        public readonly HashSet<int> SharpStation = new HashSet<int>();
        private Vector3[,] _fn;

        public int Stations => Z.Length;
        public int Ring => S[0].Length;

        public CarLoft(IList<float> z, IList<Vector2[]> sections)
        {
            Z = new float[z.Count];
            S = new Vector2[z.Count][];
            for (int j = 0; j < z.Count; j++) { Z[j] = z[j]; S[j] = sections[j]; }
        }

        public Vector3 P(int j, int i) => new Vector3(S[j][i].x, S[j][i].y, Z[j]);

        private void Faces()
        {
            if (_fn != null) return;
            _fn = new Vector3[Stations - 1, Ring - 1];
            for (int j = 0; j < Stations - 1; j++)
            for (int i = 0; i < Ring - 1; i++)
            {
                Vector3 ds = P(j, i + 1) - P(j, i) + P(j + 1, i + 1) - P(j + 1, i);
                Vector3 dz = P(j + 1, i) - P(j, i) + P(j + 1, i + 1) - P(j, i + 1);
                _fn[j, i] = Vector3.Cross(ds, dz) * 0.25f;
            }
        }

        private Vector3 CornerNormal(int fj, int fi, int vj, int vi)
        {
            Vector3 sum = Vector3.zero;
            for (int gj = vj - 1; gj <= vj; gj++)
            for (int gi = vi - 1; gi <= vi; gi++)
            {
                if (gj < 0 || gi < 0 || gj >= Stations - 1 || gi >= Ring - 1) continue;
                if (gi != fi && SharpRing.Contains(vi)) continue;
                if (gj != fj && SharpStation.Contains(vj)) continue;
                sum += _fn[gj, gi];
            }
            if (S[vj][vi].x < 1e-4f) sum.x = 0f; // centre line: mirrored half continues smoothly
            if (sum.sqrMagnitude < 1e-14f) sum = _fn[fj, fi];
            return sum;
        }

        /// <summary>
        /// Emits both halves. <paramref name="mat"/>(ringSegment, station) picks each cell's material (null = hole).
        /// <paramref name="inner"/> emits the back side instead (normals flipped), e.g. a headliner.
        /// </summary>
        public void Emit(MeshBuilder mb, Func<int, int, Material> mat, bool inner = false, bool mirror = true)
        {
            Faces();
            for (int j = 0; j < Stations - 1; j++)
            for (int i = 0; i < Ring - 1; i++)
            {
                var m = mat(i, j);
                if (m == null) continue;
                Vector3 a = P(j, i), b = P(j, i + 1), c = P(j + 1, i + 1), d = P(j + 1, i);
                Vector3 na = CornerNormal(j, i, j, i), nb = CornerNormal(j, i, j, i + 1);
                Vector3 nc = CornerNormal(j, i, j + 1, i + 1), nd = CornerNormal(j, i, j + 1, i);
                if (inner) { na = -na; nb = -nb; nc = -nc; nd = -nd; }
                mb.Quad(a, b, c, d, na, nb, nc, nd, m);
                if (!mirror) continue;
                mb.Quad(X(a), X(b), X(c), X(d), X(na), X(nb), X(nc), X(nd), m);
            }
        }

        private static Vector3 X(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>Flat end cap over station j (both halves), facing +z (sign 1) or -z (sign -1).</summary>
        public void Cap(MeshBuilder mb, int j, float sign, Material m)
        {
            var sec = S[j];
            Vector3 c = Vector3.zero;
            foreach (var p in sec) c += new Vector3(0f, p.y, Z[j]);
            c /= sec.Length;
            Vector3 n = new Vector3(0, 0, sign);
            for (int i = 0; i < sec.Length - 1; i++)
            {
                Vector3 a = P(j, i), b = P(j, i + 1);
                mb.Tri(c, a, b, n, n, n, m);
                mb.Tri(c, X(a), X(b), n, n, n, m);
            }
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>Half section interpolated at z.</summary>
        public Vector2[] SectionAt(float z)
        {
            int j = 0;
            while (j < Stations - 2 && z > Z[j + 1]) j++;
            float h = Z[j + 1] - Z[j];
            float t = h < 1e-6f ? 0f : Mathf.Clamp01((z - Z[j]) / h);
            var r = new Vector2[Ring];
            for (int i = 0; i < Ring; i++) r[i] = Vector2.Lerp(S[j][i], S[j + 1][i], t);
            return r;
        }

        /// <summary>Point at arc fraction s (0 = first ring point, 1 = last) of a section.</summary>
        public static Vector2 AtArc(Vector2[] sec, float s)
        {
            float total = 0f;
            for (int i = 0; i < sec.Length - 1; i++) total += (sec[i + 1] - sec[i]).magnitude;
            float target = Mathf.Clamp01(s) * total, acc = 0f;
            for (int i = 0; i < sec.Length - 1; i++)
            {
                float l = (sec[i + 1] - sec[i]).magnitude;
                if (acc + l >= target && l > 1e-7f) return Vector2.Lerp(sec[i], sec[i + 1], (target - acc) / l);
                acc += l;
            }
            return sec[sec.Length - 1];
        }

        /// <summary>Arc fraction of the first point between ring indices [i0, i1] whose y (axis 1) or x (axis 0) equals v.</summary>
        public static float ArcWhere(Vector2[] sec, int axis, float v, int i0, int i1)
        {
            float total = 0f;
            for (int i = 0; i < sec.Length - 1; i++) total += (sec[i + 1] - sec[i]).magnitude;
            float acc = 0f, best = -1f, bestErr = float.MaxValue;
            for (int i = 0; i < sec.Length - 1; i++)
            {
                float l = (sec[i + 1] - sec[i]).magnitude;
                if (i >= i0 && i < i1 && l > 1e-7f)
                {
                    float a = sec[i][axis], b = sec[i + 1][axis];
                    float t = Mathf.Abs(b - a) < 1e-7f ? 0f : Mathf.Clamp01((v - a) / (b - a));
                    float err = Mathf.Abs(Mathf.Lerp(a, b, t) - v);
                    if (err < bestErr - 1e-5f) { bestErr = err; best = (acc + t * l) / Mathf.Max(total, 1e-6f); }
                }
                acc += l;
            }
            return Mathf.Max(0f, best);
        }

        public Vector3 Point(float z, float s, float side = 1f)
        {
            var p = AtArc(SectionAt(z), s);
            return new Vector3(p.x * side, p.y, z);
        }

        public Vector3 Normal(float z, float s, float side = 1f)
        {
            const float e = 0.004f, es = 0.004f;
            Vector3 ds = Point(z, s + es) - Point(z, s - es);
            Vector3 dz = Point(z + e, s) - Point(z - e, s);
            var n = Vector3.Cross(ds, dz);
            if (n.sqrMagnitude < 1e-14f) n = Vector3.up;
            n.Normalize();
            n.x *= side;
            return n;
        }

        /// <summary>
        /// A panel laid on the surface: rows from z0 to z1, each spanning arc fractions range(t) (t = 0..1 along z),
        /// lifted by <paramref name="lift"/> along the normal. Used for lamps, grilles, vents, seams, stripes.
        /// </summary>
        public void Patch(MeshBuilder mb, float side, float z0, float z1, Func<float, Vector2> range, float lift, Material m,
            int nz = 6, int ns = 6, bool flip = false)
        {
            var pts = new Vector3[nz + 1, ns + 1];
            var nrm = new Vector3[nz + 1, ns + 1];
            for (int a = 0; a <= nz; a++)
            {
                float t = a / (float)nz;
                float z = Mathf.Lerp(z0, z1, t);
                Vector2 r = range(t);
                for (int b = 0; b <= ns; b++)
                {
                    float s = Mathf.Lerp(r.x, r.y, b / (float)ns);
                    var n = Normal(z, s, side);
                    nrm[a, b] = flip ? -n : n;
                    pts[a, b] = Point(z, s, side) + n * lift;
                }
            }
            for (int a = 0; a < nz; a++)
            for (int b = 0; b < ns; b++)
                mb.Quad(pts[a, b], pts[a, b + 1], pts[a + 1, b + 1], pts[a + 1, b],
                    nrm[a, b], nrm[a, b + 1], nrm[a + 1, b + 1], nrm[a + 1, b], m);
        }

        /// <summary>Patch whose rows are bounded by heights y0..y1 on the ring part [i0, i1] (sides, fascias).</summary>
        public void PatchY(MeshBuilder mb, float side, float z0, float z1, Func<float, Vector2> yRange, int i0, int i1,
            float lift, Material m, int nz = 6, int ns = 4, bool flip = false) =>
            Patch(mb, side, z0, z1, t =>
            {
                var sec = SectionAt(Mathf.Lerp(z0, z1, t));
                var y = yRange(t);
                return new Vector2(ArcWhere(sec, 1, y.x, i0, i1), ArcWhere(sec, 1, y.y, i0, i1));
            }, lift, m, nz, ns, flip);

        /// <summary>Patch whose rows are bounded by |x| = x0..x1 on the ring part [i0, i1] (hoods, decks, roofs).</summary>
        public void PatchX(MeshBuilder mb, float side, float z0, float z1, Func<float, Vector2> xRange, int i0, int i1,
            float lift, Material m, int nz = 6, int ns = 4) =>
            Patch(mb, side, z0, z1, t =>
            {
                var sec = SectionAt(Mathf.Lerp(z0, z1, t));
                var x = xRange(t);
                return new Vector2(ArcWhere(sec, 0, x.x, i0, i1), ArcWhere(sec, 0, x.y, i0, i1));
            }, lift, m, nz, ns);

        /// <summary>Surface point at height y on ring part [i0, i1] at z, with its outward normal.</summary>
        public (Vector3 p, Vector3 n) OnSideAtY(float z, float y, int i0, int i1, float side = 1f)
        {
            float s = ArcWhere(SectionAt(z), 1, y, i0, i1);
            return (Point(z, s, side), Normal(z, s, side));
        }

        /// <summary>Surface point at |x| on ring part [i0, i1] at z, with its outward normal.</summary>
        public (Vector3 p, Vector3 n) OnTopAtX(float z, float x, int i0, int i1, float side = 1f)
        {
            float s = ArcWhere(SectionAt(z), 0, x, i0, i1);
            return (Point(z, s, side), Normal(z, s, side));
        }
    }
}
