using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Small runtime mesh builder for the Yaris easter egg (one sub-mesh per material slot, explicit normals so the
    /// lofted body stays smooth while every quad keeps its own vertices and can take its own material).
    /// </summary>
    internal sealed class YarisMesh
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        private readonly List<int>[] _t;

        public YarisMesh(int slots)
        {
            _t = new List<int>[slots];
            for (int i = 0; i < slots; i++) _t[i] = new List<int>();
        }

        public int Count => V.Count;

        /// <summary>Triangle a-b-c (clockwise seen from the visible side = Unity front face).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, int slot)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(na.normalized); N.Add(nb.normalized); N.Add(nc.normalized);
            var t = _t[slot];
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>Quad a-b-c-d (a, b, c, d around the face, clockwise from the visible side).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int slot)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            N.Add(na.normalized); N.Add(nb.normalized); N.Add(nc.normalized); N.Add(nd.normalized);
            var t = _t[slot];
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void FlatQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int slot)
        {
            Vector3 n = Vector3.Cross(b - a, d - a);
            if (n.sqrMagnitude < 1e-16f) n = Vector3.Cross(c - b, a - b);
            Quad(a, b, c, d, n, n, n, n, slot);
        }

        /// <summary>Flat quad facing roughly <paramref name="towards"/> (winding chosen automatically).</summary>
        public void FlatQuadAuto(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 towards, int slot)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-16f) n = Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(n, towards) < 0f) { n = -n; Quad(a, d, c, b, n, n, n, n, slot); }
            else Quad(a, b, c, d, n, n, n, n, slot);
        }

        /// <summary>Box with rounded-looking (bevel-shaded) edges, oriented by <paramref name="rot"/>.</summary>
        public void Box(Vector3 c, Vector3 size, Quaternion rot, int slot, float soft = 0.35f)
        {
            Vector3 h = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                int axis = f / 2;
                float s = (f % 2 == 0) ? 1f : -1f;
                Vector3 n = Vector3.zero;
                n[axis] = s;
                Vector3 u = Vector3.zero, v = Vector3.zero;
                u[(axis + 1) % 3] = 1f;
                v[(axis + 2) % 3] = 1f;
                if (s > 0f) { var tmp = u; u = v; v = tmp; }
                Vector3 fc = Vector3.Scale(n, h);
                Vector3 hu = Vector3.Scale(u, h), hv = Vector3.Scale(v, h);
                Vector3 p0 = fc - hu - hv, p1 = fc - hu + hv, p2 = fc + hu + hv, p3 = fc + hu - hv;
                // Corner normals lean out a little: soft shading on small boxes reads as a bevel.
                Vector3 N0 = n + soft * (p0 - fc).normalized, N1 = n + soft * (p1 - fc).normalized;
                Vector3 N2 = n + soft * (p2 - fc).normalized, N3 = n + soft * (p3 - fc).normalized;
                Quad(c + rot * p0, c + rot * p1, c + rot * p2, c + rot * p3, rot * N0, rot * N1, rot * N2, rot * N3, slot);
            }
        }

        /// <summary>
        /// Surface of revolution round the local X axis (wheels): <paramref name="prof"/> = (x, radius) points in order;
        /// <paramref name="outward"/> = +1 when the profile runs so its normals face away from the axis.
        /// </summary>
        public void Lathe(IList<Vector2> prof, int seg, int slot, Vector3 at, float side, bool smooth = true)
        {
            for (int k = 0; k < prof.Count - 1; k++)
            {
                Vector2 a = prof[k], b = prof[k + 1];
                Vector2 d = b - a;
                // Normal of the profile segment in (x, r): perpendicular, pointing to +r when the profile goes +x.
                Vector2 pn = new Vector2(-d.y, d.x).normalized;
                Vector2 na2 = pn, nb2 = pn;
                if (smooth)
                {
                    if (k > 0) { var dp = a - prof[k - 1]; var pp = new Vector2(-dp.y, dp.x).normalized; if (Vector2.Dot(pp, pn) > 0.75f) na2 = (pp + pn).normalized; }
                    if (k < prof.Count - 2) { var dn = prof[k + 2] - b; var pq = new Vector2(-dn.y, dn.x).normalized; if (Vector2.Dot(pq, pn) > 0.75f) nb2 = (pq + pn).normalized; }
                }
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 r0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)), r1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1));
                    Vector3 A = at + new Vector3(a.x * side, 0f, 0f) + r0 * a.y;
                    Vector3 B = at + new Vector3(b.x * side, 0f, 0f) + r0 * b.y;
                    Vector3 C = at + new Vector3(b.x * side, 0f, 0f) + r1 * b.y;
                    Vector3 D = at + new Vector3(a.x * side, 0f, 0f) + r1 * a.y;
                    Vector3 nA = new Vector3(na2.x * side, 0f, 0f) + r0 * na2.y, nD = new Vector3(na2.x * side, 0f, 0f) + r1 * na2.y;
                    Vector3 nB = new Vector3(nb2.x * side, 0f, 0f) + r0 * nb2.y, nC = new Vector3(nb2.x * side, 0f, 0f) + r1 * nb2.y;
                    if (side > 0f) Quad(A, D, C, B, nA, nD, nC, nB, slot);
                    else Quad(A, B, C, D, nA, nB, nC, nD, slot);
                }
            }
        }

        /// <summary>Ellipsoid (UV sphere) with radii <paramref name="r"/>; points with local z below
        /// <paramref name="cutZ"/> (radius units) are flattened onto that plane.</summary>
        public void Ellipsoid(Vector3 c, Vector3 r, Quaternion rot, int slot, int seg = 12, int rings = 8, float cutZ = -2f)
        {
            Vector3 P(int i, int j)
            {
                float th = Mathf.PI * j / rings, ph = Mathf.PI * 2f * i / seg;
                var u = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                if (u.z < cutZ) u.z = cutZ;
                return u;
            }
            for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                Vector3 a = P(i, j), b = P(i + 1, j), cc = P(i + 1, j + 1), d = P(i, j + 1);
                Vector3 W(Vector3 u) => c + rot * Vector3.Scale(u, r);
                Vector3 Nn(Vector3 u) => rot * (u.z <= cutZ + 1e-4f ? new Vector3(0f, 0f, -1f) : new Vector3(u.x / r.x, u.y / r.y, u.z / r.z));
                Quad(W(a), W(b), W(cc), W(d), Nn(a), Nn(b), Nn(cc), Nn(d), slot);
            }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.subMeshCount = _t.Length;
            for (int i = 0; i < _t.Length; i++) m.SetTriangles(_t[i], i, false);
            m.RecalculateBounds();
            m.UploadMeshData(false);
            return m;
        }
    }
}
