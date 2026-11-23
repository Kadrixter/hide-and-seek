using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Small runtime mesh builder for props (security room furniture, food): lathe (revolved profile), torus,
    /// rounded box, tube along a path, plain box. Every shape takes a placement matrix; UVs are in metres (so a
    /// material tile of 1 = one texture repeat per metre), normals smooth inside a shape.
    /// </summary>
    public sealed class ProcMesh
    {
        private readonly List<Vector3> _v = new();
        private readonly List<Vector3> _n = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<int> _t = new();

        private Matrix4x4 _m = Matrix4x4.identity, _nm = Matrix4x4.identity;

        /// <summary>Placement of the next shapes.</summary>
        public Matrix4x4 M { get => _m; set { _m = value; _nm = value.inverse.transpose; } }

        public ProcMesh At(Vector3 pos, Quaternion rot) { M = Matrix4x4.TRS(pos, rot, Vector3.one); return this; }
        public ProcMesh At(Vector3 pos) => At(pos, Quaternion.identity);
        public ProcMesh At(Vector3 pos, Quaternion rot, Vector3 scale) { M = Matrix4x4.TRS(pos, rot, scale); return this; }

        private int V(Vector3 p, Vector3 n, Vector2 uv)
        {
            _v.Add(_m.MultiplyPoint3x4(p));
            _n.Add(_nm.MultiplyVector(n).normalized);
            _uv.Add(uv);
            return _v.Count - 1;
        }

        private void Q(int a, int b, int c, int d) { _t.Add(a); _t.Add(b); _t.Add(c); _t.Add(a); _t.Add(c); _t.Add(d); }

        /// <summary>Profile (x = radius, y = height) revolved round the local Y axis; a sharp corner where the profile turns more than <paramref name="smoothDeg"/>.</summary>
        public ProcMesh Lathe(Vector2[] prof, int sides, float smoothDeg = 40f, float a0 = 0f, float a1 = 360f)
        {
            int n = prof.Length;
            var segN = new Vector2[n - 1];
            var segLen = new float[n - 1];
            for (int k = 0; k < n - 1; k++)
            {
                Vector2 d = prof[k + 1] - prof[k];
                segLen[k] = d.magnitude;
                segN[k] = segLen[k] > 1e-6f ? new Vector2(d.y, -d.x).normalized : Vector2.right;
            }
            float cos = Mathf.Cos(smoothDeg * Mathf.Deg2Rad);
            float vAcc = 0f;
            for (int k = 0; k < n - 1; k++)
            {
                Vector2 nA = segN[k], nB = segN[k];
                if (k > 0 && Vector2.Dot(segN[k - 1], segN[k]) > cos) nA = (segN[k - 1] + segN[k]).normalized;
                if (k < n - 2 && Vector2.Dot(segN[k + 1], segN[k]) > cos) nB = (segN[k + 1] + segN[k]).normalized;
                int baseIdx = _v.Count;
                for (int s = 0; s <= sides; s++)
                {
                    float ang = Mathf.Lerp(a0, a1, s / (float)sides) * Mathf.Deg2Rad;
                    float cx = Mathf.Cos(ang), sz = Mathf.Sin(ang);
                    Vector2 p0 = prof[k], p1 = prof[k + 1];
                    float u = ang * Mathf.Max(0.02f, Mathf.Max(p0.x, p1.x));
                    V(new Vector3(p0.x * cx, p0.y, p0.x * sz), new Vector3(nA.x * cx, nA.y, nA.x * sz), new Vector2(u, vAcc));
                    V(new Vector3(p1.x * cx, p1.y, p1.x * sz), new Vector3(nB.x * cx, nB.y, nB.x * sz), new Vector2(u, vAcc + segLen[k]));
                }
                for (int s = 0; s < sides; s++)
                {
                    int a = baseIdx + s * 2;
                    // Profile going up with radius > 0: outward faces.
                    Q(a, a + 1, a + 3, a + 2);
                }
                vAcc += segLen[k];
            }
            return this;
        }

        /// <summary>Ring torus in the local XZ plane (major radius R, tube radius r).</summary>
        public ProcMesh Torus(float R, float r, int segU = 20, int segV = 10, float a0 = 0f, float a1 = 360f)
        {
            int b = _v.Count;
            for (int i = 0; i <= segU; i++)
            {
                float u = Mathf.Lerp(a0, a1, i / (float)segU) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j <= segV; j++)
                {
                    float v = j / (float)segV * Mathf.PI * 2f;
                    Vector3 n = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    V(dir * R + n * r, n, new Vector2(u * R, v * r));
                }
            }
            for (int i = 0; i < segU; i++)
            for (int j = 0; j < segV; j++)
            {
                int a = b + i * (segV + 1) + j, c = a + segV + 1;
                Q(a, a + 1, c + 1, c);
            }
            return this;
        }

        /// <summary>Box with rounded edges and corners (radius r, n subdivisions per face side).</summary>
        public ProcMesh RoundBox(Vector3 size, float r, int n = 6)
        {
            Vector3 h = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 1e-4f);
            Vector3 inner = h - Vector3.one * r;
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in fn)
            {
                Vector3 a = new Vector3(f.y, f.z, f.x), c = Vector3.Cross(f, a);
                int b = _v.Count;
                for (int i = 0; i <= n; i++)
                for (int j = 0; j <= n; j++)
                {
                    Vector3 p = f + a * (i / (float)n * 2f - 1f) + c * (j / (float)n * 2f - 1f);
                    p = Vector3.Scale(p, h);
                    Vector3 q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - q;
                    Vector3 nn = d.sqrMagnitude > 1e-12f ? d.normalized : f;
                    Vector3 pos = q + nn * r;
                    V(pos, nn, new Vector2(Vector3.Dot(pos, a), Vector3.Dot(pos, c)));
                }
                for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    int k = b + i * (n + 1) + j;
                    Q(k, k + n + 1, k + n + 2, k + 1);
                }
            }
            return this;
        }

        /// <summary>Round tube through the points (smooth bends, flat caps).</summary>
        public ProcMesh Tube(IList<Vector3> pts, float radius, int sides = 8, bool caps = true)
        {
            int count = pts.Count;
            if (count < 2) return this;
            Vector3 prevUp = Vector3.up;
            int first = _v.Count;
            float len = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 t = i == 0 ? pts[1] - pts[0] : i == count - 1 ? pts[i] - pts[i - 1] : (pts[i + 1] - pts[i]).normalized + (pts[i] - pts[i - 1]).normalized;
                t.Normalize();
                Vector3 side = Vector3.Cross(t, Mathf.Abs(Vector3.Dot(t, prevUp)) > 0.95f ? Vector3.right : prevUp).normalized;
                Vector3 up = Vector3.Cross(side, t);
                prevUp = up;
                if (i > 0) len += (pts[i] - pts[i - 1]).magnitude;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 nn = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    V(pts[i] + nn * radius, nn, new Vector2(a * radius, len));
                }
            }
            for (int i = 0; i < count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = first + i * (sides + 1) + s, c = a + sides + 1;
                Q(a, c, c + 1, a + 1);
            }
            if (caps)
            {
                Cap(first, sides, -(pts[1] - pts[0]).normalized);
                Cap(first + (count - 1) * (sides + 1), sides, (pts[count - 1] - pts[count - 2]).normalized);
            }
            return this;
        }

        private void Cap(int ring, int sides, Vector3 nLocal)
        {
            var w = M.inverse;
            Vector3 c = Vector3.zero;
            for (int s = 0; s < sides; s++) c += w.MultiplyPoint3x4(_v[ring + s]);
            c /= sides;
            int ci = V(c, nLocal, Vector2.zero);
            int b = _v.Count;
            for (int s = 0; s <= sides; s++) V(w.MultiplyPoint3x4(_v[ring + s]), nLocal, Vector2.zero);
            bool flip = Vector3.Dot(Vector3.Cross(w.MultiplyPoint3x4(_v[b]) - c, w.MultiplyPoint3x4(_v[b + 1]) - c), nLocal) < 0f;
            for (int s = 0; s < sides; s++)
            {
                if (flip) { _t.Add(ci); _t.Add(b + s + 1); _t.Add(b + s); }
                else { _t.Add(ci); _t.Add(b + s); _t.Add(b + s + 1); }
            }
        }

        /// <summary>Plain box (hard edges).</summary>
        public ProcMesh Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] fn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in fn)
            {
                Vector3 a = new Vector3(f.y, f.z, f.x), c = Vector3.Cross(f, a);
                Vector3 o = center + Vector3.Scale(f, h);
                Vector3 ah = Vector3.Scale(a, h), ch = Vector3.Scale(c, h);
                int b = _v.Count;
                Vector3[] p = { o - ah - ch, o + ah - ch, o + ah + ch, o - ah + ch };
                foreach (var q in p) V(q, f, new Vector2(Vector3.Dot(q, a), Vector3.Dot(q, c)));
                Q(b, b + 1, b + 2, b + 3);
            }
            return this;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            m.SetUVs(0, _uv);
            m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
