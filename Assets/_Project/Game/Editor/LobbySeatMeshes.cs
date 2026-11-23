using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Procedural mesh pieces for the lobby seats (LobbySeatModels): parametric surfaces with smooth normals taken from
    /// the surface function itself (no seams in the shading), tubes along paths (piping, seams, legs), rounded / puffed
    /// boxes, buttons. Everything is built in the furniture's local space.
    /// </summary>
    internal static class LobbySeatMeshes
    {
        /// <summary>
        /// Surface over (u, v) in [0,1]^2: <paramref name="f"/> gives the point, the grid is nu x nv quads. Normals come
        /// from finite differences of f (so a closed u seam or a pole shades smoothly); <paramref name="flip"/> turns
        /// the winding round when the surface faces the wrong way. UV = (u * uScale, v * vScale).
        /// </summary>
        public static Mesh Param(int nu, int nv, Func<float, float, Vector3> f, float uScale = 1f, float vScale = 1f, bool flip = false)
        {
            var verts = new List<Vector3>((nu + 1) * (nv + 1));
            var norms = new List<Vector3>(verts.Capacity);
            var uvs = new List<Vector2>(verts.Capacity);
            const float e = 1e-3f;
            for (int j = 0; j <= nv; j++)
            for (int i = 0; i <= nu; i++)
            {
                float u = i / (float)nu, v = j / (float)nv;
                Vector3 p = f(u, v);
                // Derivatives away from the edges of the domain (poles / borders shade like their neighbours).
                float vc = Mathf.Clamp(v, 2f * e, 1f - 2f * e);
                Vector3 du = f(u + e, vc) - f(u - e, vc);
                Vector3 dv = f(u, vc + e) - f(u, vc - e);
                // (Vector3.Normalize zeroes anything shorter than 1e-5: normalise the tiny differences first.)
                Vector3 n = Vector3.Cross(dv.normalized, du.normalized);
                if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
                n.Normalize();
                if (flip) n = -n;
                verts.Add(p);
                norms.Add(n);
                uvs.Add(new Vector2(u * uScale, v * vScale));
            }
            var tris = new List<int>(nu * nv * 6);
            int w = nu + 1;
            for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                if (!flip) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
            }
            var m = new Mesh { indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Surface of revolution round the Y axis: <paramref name="profile"/>(s) = (radius, height) from the bottom centre
        /// (s = 0) out, up and back in to the top centre (s = 1); <paramref name="push"/>(angle, s, point) moves the
        /// surface out (+) / in (-) along its profile normal (tufting, channels, wrinkles).
        /// </summary>
        public static Mesh Revolve(Func<float, Vector2> profile, Func<float, float, Vector3, float> push, int nu, int nv, float uScale = 1f, float vScale = 1f) =>
            Param(nu, nv, (u, v) => RevolvePoint(profile, push, u * Mathf.PI * 2f, v), uScale, vScale);

        /// <summary>One point of <see cref="Revolve"/> (angle in radians, x = cos, z = sin).</summary>
        public static Vector3 RevolvePoint(Func<float, Vector2> profile, Func<float, float, Vector3, float> push, float a, float s)
        {
            s = Mathf.Clamp01(s);
            const float e = 1e-3f;
            Vector2 p = profile(s);
            Vector2 d = profile(Mathf.Min(1f, s + e)) - profile(Mathf.Max(0f, s - e));
            Vector2 n2 = d.sqrMagnitude > 1e-12f ? new Vector2(d.y, -d.x).normalized : Vector2.up;
            Vector3 basePt = new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x);
            float k = push != null ? push(a, s, basePt) : 0f;
            Vector2 q = p + n2 * k;
            return new Vector3(Mathf.Cos(a) * q.x, q.y, Mathf.Sin(a) * q.x);
        }

        /// <summary>
        /// Profile helper: a polyline of (radius, height) points with every inner corner rounded by its radius
        /// (third component), sampled by arc length. Returns a function s in [0,1] -> point.
        /// </summary>
        public static Func<float, Vector2> RoundedProfile(params Vector3[] pts)
        {
            // Build a dense polyline: straight runs plus arcs at the corners.
            var line = new List<Vector2> { new Vector2(pts[0].x, pts[0].y) };
            for (int i = 1; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i], c = pts[i + 1];
                float r = pts[i].z;
                Vector2 da = (a - b).normalized, dc = (c - b).normalized;
                float half = Vector2.Angle(da, dc) * 0.5f * Mathf.Deg2Rad;
                float cut = r <= 0f ? 0f : Mathf.Min(r / Mathf.Tan(half), Mathf.Min((a - b).magnitude, (c - b).magnitude) * 0.49f);
                Vector2 p0 = b + da * cut, p1 = b + dc * cut;
                if (cut <= 1e-5f) { line.Add(b); continue; }
                // Quadratic Bezier through the corner (close to an arc for these angles).
                for (int k = 0; k <= 10; k++)
                {
                    float t = k / 10f;
                    line.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * b + t * t * p1);
                }
            }
            line.Add(new Vector2(pts[pts.Length - 1].x, pts[pts.Length - 1].y));
            var acc = new float[line.Count];
            for (int i = 1; i < line.Count; i++) acc[i] = acc[i - 1] + (line[i] - line[i - 1]).magnitude;
            float total = acc[acc.Length - 1];
            return s =>
            {
                float target = Mathf.Clamp01(s) * total;
                int lo = 0, hi = acc.Length - 1;
                while (hi - lo > 1) { int mid = (lo + hi) / 2; if (acc[mid] < target) lo = mid; else hi = mid; }
                float seg = acc[hi] - acc[lo];
                float t = seg > 1e-7f ? (target - acc[lo]) / seg : 0f;
                return Vector2.Lerp(line[lo], line[hi], t);
            };
        }

        /// <summary>
        /// Point on a rounded rectangle (half sizes hx, hz, corner radius r) by arc length t in [0,1), counter-clockwise
        /// seen from above, starting in the middle of the +X side.
        /// </summary>
        public static Vector3 RoundRect(float t, float hx, float hz, float r, out Vector3 normal)
        {
            float sx = 2f * (hx - r), sz = 2f * (hz - r), arc = Mathf.PI * 0.5f * r;
            float total = 2f * sx + 2f * sz + 4f * arc;
            float d = Mathf.Repeat(t, 1f) * total;
            // Segments: +X side (from z=0 up), corner, +Z side, corner, -X side, corner, -Z side, corner, +X side (to z=0).
            float[] len = { sz * 0.5f, arc, sx, arc, sz, arc, sx, arc, sz * 0.5f };
            int i = 0;
            while (i < len.Length - 1 && d > len[i]) { d -= len[i]; i++; }
            float ix = hx - r, iz = hz - r;
            switch (i)
            {
                case 0: normal = Vector3.right; return new Vector3(hx, 0f, d);
                case 2: normal = Vector3.forward; return new Vector3(ix - d, 0f, hz);
                case 4: normal = Vector3.left; return new Vector3(-hx, 0f, iz - d);
                case 6: normal = Vector3.back; return new Vector3(-ix + d, 0f, -hz);
                case 8: normal = Vector3.right; return new Vector3(hx, 0f, -iz + d);
                default:
                {
                    int corner = (i - 1) / 2; // 0: +x+z, 1: -x+z, 2: -x-z, 3: +x-z
                    float a = corner * Mathf.PI * 0.5f + d / Mathf.Max(r, 1e-5f);
                    normal = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 c = new Vector3(corner == 0 || corner == 3 ? ix : -ix, 0f, corner < 2 ? iz : -iz);
                    return c + normal * r;
                }
            }
        }

        /// <summary>Round tube of radius <paramref name="r"/> along a path (t in [0,1]); closed paths wrap round.</summary>
        public static Mesh Tube(Func<float, Vector3> path, Func<float, float> r, int seg, int sides, bool closed, bool caps = false)
        {
            Vector3 Tangent(float t)
            {
                const float e = 1e-3f;
                float a = closed ? t - e : Mathf.Max(0f, t - e), b = closed ? t + e : Mathf.Min(1f, t + e);
                return (path(Wrap(b, closed)) - path(Wrap(a, closed))).normalized;
            }
            // Parallel transported frame (no twisting).
            var frames = new Vector3[seg + 1];
            Vector3 t0 = Tangent(0f);
            Vector3 n0 = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            frames[0] = n0;
            for (int i = 1; i <= seg; i++)
            {
                Vector3 t = Tangent(i / (float)seg);
                Vector3 n = Vector3.ProjectOnPlane(frames[i - 1], t);
                frames[i] = n.sqrMagnitude > 1e-10f ? n.normalized : frames[i - 1];
            }
            return Param(sides, seg, (u, v) =>
            {
                float tt = Wrap(v, closed);
                int k = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * seg), 0, seg);
                Vector3 tan = Tangent(tt);
                Vector3 n = Vector3.ProjectOnPlane(frames[k], tan).normalized;
                Vector3 b = Vector3.Cross(tan, n);
                float a = u * Mathf.PI * 2f;
                return path(tt) + (n * Mathf.Cos(a) + b * Mathf.Sin(a)) * r(tt);
            }, 1f, 4f, true);
        }

        static float Wrap(float t, bool closed) => closed ? Mathf.Repeat(t, 1f) : Mathf.Clamp01(t);

        /// <summary>Squashed sphere (button, foot cap): radius r across, h tall, centred at the origin.</summary>
        public static Mesh Ellipsoid(float r, float h, int seg = 12) =>
            Param(seg, seg / 2 + 2, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = (v - 0.5f) * Mathf.PI;
                return new Vector3(Mathf.Cos(a) * Mathf.Cos(b) * r, Mathf.Sin(b) * h, Mathf.Sin(a) * Mathf.Cos(b) * r);
            });

        /// <summary>
        /// Rounded box (size, edge radius) whose faces bulge out by <paramref name="puff"/> at their centres; optional
        /// <paramref name="dent"/>(local point) pushes the surface in (sitting dip, tufting).
        /// </summary>
        public static Mesh SoftBox(Vector3 size, float radius, float puff, Func<Vector3, Vector3> dent = null, int res = 14)
        {
            Vector3 h = size * 0.5f;
            radius = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
            // Superellipsoid-like mapping of a sphere: round edges, flat-ish faces, then the puff.
            return Param(res * 4, res * 2, (u, v) =>
            {
                float a = u * Mathf.PI * 2f, b = (v - 0.5f) * Mathf.PI;
                Vector3 d = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                Vector3 inner = h - Vector3.one * radius;
                // Point on the rounded box along direction d: scale to the box, then round the corners.
                Vector3 q = d;
                float s = Mathf.Max(Mathf.Abs(q.x) / Mathf.Max(h.x, 1e-4f), Mathf.Max(Mathf.Abs(q.y) / Mathf.Max(h.y, 1e-4f), Mathf.Abs(q.z) / Mathf.Max(h.z, 1e-4f)));
                Vector3 onBox = q / s;
                Vector3 core = new Vector3(Mathf.Clamp(onBox.x, -inner.x, inner.x), Mathf.Clamp(onBox.y, -inner.y, inner.y), Mathf.Clamp(onBox.z, -inner.z, inner.z));
                Vector3 off = onBox - core;
                Vector3 p = core + (off.sqrMagnitude > 1e-12f ? off.normalized * radius : Vector3.zero);
                // Puff: every face bulges at its centre.
                float fx = p.x / h.x, fy = p.y / h.y, fz = p.z / h.z;
                p.x += Mathf.Sign(p.x) * puff * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(fx)), 8f) * (1f - fy * fy) * (1f - fz * fz);
                p.y += Mathf.Sign(p.y) * puff * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(fy)), 8f) * (1f - fx * fx) * (1f - fz * fz);
                p.z += Mathf.Sign(p.z) * puff * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(fz)), 8f) * (1f - fx * fx) * (1f - fy * fy);
                if (dent != null) p += dent(p);
                return p;
            }, 2f, 1f);
        }

        /// <summary>Gaussian dimple: how far a point at distance <paramref name="d"/> from a tuft is pulled in.</summary>
        public static float Dimple(float d, float depth, float width) => depth * Mathf.Exp(-(d * d) / (width * width));

        /// <summary>Joins meshes (already in the same space) into one.</summary>
        public static Mesh Combine(IList<Mesh> parts)
        {
            var ci = new CombineInstance[parts.Count];
            int verts = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                ci[i] = new CombineInstance { mesh = parts[i], transform = Matrix4x4.identity };
                verts += parts[i].vertexCount;
            }
            var m = new Mesh { indexFormat = verts > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.CombineMeshes(ci, true, true);
            m.RecalculateBounds();
            foreach (var p in parts) UnityEngine.Object.DestroyImmediate(p);
            return m;
        }

        /// <summary>Copy of <paramref name="m"/> moved / turned / scaled.</summary>
        public static Mesh Moved(Mesh m, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var mat = Matrix4x4.TRS(pos, rot, scale);
            var v = m.vertices;
            var n = m.normals;
            var nm = mat.inverse.transpose;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = mat.MultiplyPoint3x4(v[i]);
                if (i < n.Length) n[i] = nm.MultiplyVector(n[i]).normalized;
            }
            var r = UnityEngine.Object.Instantiate(m);
            r.vertices = v;
            r.normals = n;
            r.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(m);
            return r;
        }
    }
}
