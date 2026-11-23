using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Smooth surface lofted through half cross-sections (right side, x &gt;= 0, bottom centre -&gt; top centre) at
    /// ascending z stations, mirrored to the left; the two end stations are closed with flat caps. Smooth vertex
    /// normals except across the listed sharp rings. Can be sampled (z, fractional ring index) to lay lamps, seams and
    /// trim exactly on the panels.
    /// </summary>
    internal sealed class YarisLoft
    {
        public readonly float[] Z;
        public readonly Vector2[][] S;
        public readonly HashSet<int> SharpRing = new HashSet<int>();
        private Vector3[,] _fn;   // face normals (station quad j, band i)
        private Vector3[,] _vn;   // fully smoothed vertex normals (for sampling)

        public int Stations => Z.Length;
        public int Ring => S[0].Length;

        public YarisLoft(List<float> z, List<Vector2[]> s)
        {
            Z = z.ToArray();
            S = s.ToArray();
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
                _fn[j, i] = Vector3.Cross(ds, dz);
            }
            _vn = new Vector3[Stations, Ring];
            for (int j = 0; j < Stations; j++)
            for (int i = 0; i < Ring; i++)
            {
                Vector3 sum = Vector3.zero;
                for (int gj = j - 1; gj <= j; gj++)
                for (int gi = i - 1; gi <= i; gi++)
                    if (gj >= 0 && gi >= 0 && gj < Stations - 1 && gi < Ring - 1) sum += _fn[gj, gi].normalized;
                if (S[j][i].x < 1e-4f) sum.x = 0f;
                _vn[j, i] = sum.sqrMagnitude > 1e-12f ? sum.normalized : Vector3.up;
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
                if (_fn[gj, gi].sqrMagnitude < 1e-14f) continue;
                sum += _fn[gj, gi].normalized;
            }
            if (S[vj][vi].x < 1e-4f) sum.x = 0f; // centre line: the mirrored half continues smoothly
            if (sum.sqrMagnitude < 1e-12f) sum = _fn[fj, fi];
            return sum;
        }

        /// <summary>Emits both halves; <paramref name="slotOf"/>(quad centre z, band i) picks the material slot (-1 = skip).</summary>
        public void Emit(YarisMesh m, Func<float, int, int> slotOf, int capFront, int capRear)
        {
            Faces();
            for (int j = 0; j < Stations - 1; j++)
            for (int i = 0; i < Ring - 1; i++)
            {
                if (_fn[j, i].sqrMagnitude < 1e-14f) continue; // collapsed band (no wheel arch here)
                int slot = slotOf((Z[j] + Z[j + 1]) * 0.5f, i);
                if (slot < 0) continue;
                Vector3 a = P(j, i), b = P(j, i + 1), c = P(j + 1, i + 1), d = P(j + 1, i);
                Vector3 na = CornerNormal(j, i, j, i), nb = CornerNormal(j, i, j, i + 1);
                Vector3 nc = CornerNormal(j, i, j + 1, i + 1), nd = CornerNormal(j, i, j + 1, i);
                m.Quad(a, b, c, d, na, nb, nc, nd, slot);
                m.Quad(X(a), X(d), X(c), X(b), X(na), X(nd), X(nc), X(nb), slot);
            }
            if (capFront >= 0) Cap(m, Stations - 1, 1f, capFront);
            if (capRear >= 0) Cap(m, 0, -1f, capRear);
        }

        private static Vector3 X(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        private void Cap(YarisMesh m, int j, float dir, int slot)
        {
            Faces();
            var s = S[j];
            float cy = (s[0].y + s[s.Length - 1].y) * 0.5f;
            Vector3 c = new Vector3(0f, cy, Z[j]);
            Vector3 n = new Vector3(0f, 0f, dir);
            // Rim normals lean towards the body's so the edge of the cap reads rounded, not creased.
            Vector3 Rim(int i) => (n * 1.6f + _vn[j, i]).normalized;
            for (int i = 0; i < s.Length - 1; i++)
            {
                Vector3 a = P(j, i), b = P(j, i + 1);
                if ((b - a).sqrMagnitude < 1e-12f) continue;
                Vector3 na = Rim(i), nb = Rim(i + 1);
                if (dir > 0f) { m.Tri(c, a, b, n, na, nb, slot); m.Tri(c, X(b), X(a), n, X(nb), X(na), slot); }
                else { m.Tri(c, b, a, n, nb, na, slot); m.Tri(c, X(a), X(b), n, X(na), X(nb), slot); }
            }
        }

        // ================================================================ sampling

        /// <summary>Point and smooth normal at station position <paramref name="z"/> and fractional ring <paramref name="u"/> (right side).</summary>
        public void Sample(float z, float u, out Vector3 p, out Vector3 n)
        {
            Faces();
            int j = Array.BinarySearch(Z, z);
            if (j < 0) j = ~j - 1;
            j = Mathf.Clamp(j, 0, Stations - 2);
            float t = Mathf.Clamp01((z - Z[j]) / Mathf.Max(1e-6f, Z[j + 1] - Z[j]));
            u = Mathf.Clamp(u, 0f, Ring - 1.0001f);
            int i = Mathf.FloorToInt(u);
            float s = u - i;
            Vector3 p0 = Vector3.Lerp(P(j, i), P(j, i + 1), s), p1 = Vector3.Lerp(P(j + 1, i), P(j + 1, i + 1), s);
            p = Vector3.Lerp(p0, p1, t);
            Vector3 n0 = Vector3.Lerp(_vn[j, i], _vn[j, i + 1], s), n1 = Vector3.Lerp(_vn[j + 1, i], _vn[j + 1, i + 1], s);
            n = Vector3.Lerp(n0, n1, t).normalized;
        }

        /// <summary>Fractional ring index on the side (rings <paramref name="from"/>..<paramref name="to"/>) where the section at z has height y.</summary>
        public float USide(float z, float y, int from, int to)
        {
            int j = Array.BinarySearch(Z, z);
            if (j < 0) j = ~j - 1;
            j = Mathf.Clamp(j, 0, Stations - 2);
            float t = Mathf.Clamp01((z - Z[j]) / Mathf.Max(1e-6f, Z[j + 1] - Z[j]));
            for (int i = from; i < to; i++)
            {
                float y0 = Mathf.Lerp(S[j][i].y, S[j + 1][i].y, t), y1 = Mathf.Lerp(S[j][i + 1].y, S[j + 1][i + 1].y, t);
                if ((y >= y0 && y <= y1) || (y <= y0 && y >= y1)) return i + (Mathf.Abs(y1 - y0) < 1e-6f ? 0f : (y - y0) / (y1 - y0));
            }
            float yf = Mathf.Lerp(S[j][from].y, S[j + 1][from].y, t);
            return y < yf ? from : to;
        }

        /// <summary>Fractional ring index on the top (rings <paramref name="from"/>..<paramref name="to"/>, x decreasing) where x is reached.</summary>
        public float UTop(float z, float x, int from, int to)
        {
            int j = Array.BinarySearch(Z, z);
            if (j < 0) j = ~j - 1;
            j = Mathf.Clamp(j, 0, Stations - 2);
            float t = Mathf.Clamp01((z - Z[j]) / Mathf.Max(1e-6f, Z[j + 1] - Z[j]));
            for (int i = from; i < to; i++)
            {
                float x0 = Mathf.Lerp(S[j][i].x, S[j + 1][i].x, t), x1 = Mathf.Lerp(S[j][i + 1].x, S[j + 1][i + 1].x, t);
                if ((x <= x0 && x >= x1) || (x >= x0 && x <= x1)) return i + (Mathf.Abs(x1 - x0) < 1e-6f ? 0f : (x - x0) / (x1 - x0));
            }
            return x > Mathf.Lerp(S[j][from].x, S[j + 1][from].x, t) ? from : to;
        }

        /// <summary>
        /// A thin sheet laid on the surface (both sides unless <paramref name="mirror"/> is false): <paramref name="map"/>(s, t)
        /// gives (z, u) for s, t in 0..1, lifted <paramref name="lift"/> along the normal.
        /// </summary>
        public void Sheet(YarisMesh m, Func<float, float, Vector2> map, int ns, int nt, float lift, int slot, bool mirror = true, bool right = true)
        {
            var pts = new Vector3[ns + 1, nt + 1];
            var nrm = new Vector3[ns + 1, nt + 1];
            for (int a = 0; a <= ns; a++)
            for (int b = 0; b <= nt; b++)
            {
                Vector2 zu = map(a / (float)ns, b / (float)nt);
                Sample(zu.x, zu.y, out var p, out var n);
                pts[a, b] = p + n * lift;
                nrm[a, b] = n;
            }
            for (int a = 0; a < ns; a++)
            for (int b = 0; b < nt; b++)
            {
                Vector3 p0 = pts[a, b], p1 = pts[a + 1, b], p2 = pts[a + 1, b + 1], p3 = pts[a, b + 1];
                Vector3 n0 = nrm[a, b], n1 = nrm[a + 1, b], n2 = nrm[a + 1, b + 1], n3 = nrm[a, b + 1];
                // Orientation: make the quad face along its normal.
                bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p3 - p0), n0 + n2) < 0f;
                if (right)
                {
                    if (!flip) m.Quad(p0, p1, p2, p3, n0, n1, n2, n3, slot);
                    else m.Quad(p0, p3, p2, p1, n0, n3, n2, n1, slot);
                }
                if (mirror || !right)
                {
                    if (!flip) m.Quad(X(p0), X(p3), X(p2), X(p1), X(n0), X(n3), X(n2), X(n1), slot);
                    else m.Quad(X(p0), X(p1), X(p2), X(p3), X(n0), X(n1), X(n2), X(n3), slot);
                }
            }
        }
    }
}
