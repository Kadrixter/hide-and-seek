using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Small procedural mesh kit for the parking public-address props (hand microphone, wall station, horn speakers):
    /// flat boxes, smooth surfaces of revolution, smooth tubes and "pebbles" (a closed 2D outline swept through a
    /// list of rings: scale / inset / depth) whose normals are averaged over the grid, so rounded plastic shells
    /// shade smoothly. Everything goes into one mesh with one sub-mesh per material slot.
    /// Triangle rule (same as the CCTV model): (a, b, c) faces along Cross(b - a, c - a).
    /// </summary>
    public sealed class PaMesh
    {
        public readonly List<Vector3> V = new();
        public readonly List<Vector3> N = new();
        public readonly List<int>[] T;

        public PaMesh(int subMeshes)
        {
            T = new List<int>[subMeshes];
            for (int i = 0; i < subMeshes; i++) T[i] = new List<int>();
        }

        private void Tri(int sub, int a, int b, int c, Vector3 want)
        {
            Vector3 fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            var t = T[sub];
            if (Vector3.Dot(fn, want) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        /// <summary>Flat quad facing <paramref name="n"/>.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, int sub)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            N.Add(n); N.Add(n); N.Add(n); N.Add(n);
            Tri(sub, i, i + 1, i + 2, n);
            Tri(sub, i, i + 2, i + 3, n);
        }

        /// <summary>Box with flat faces (centre, full size, rotation).</summary>
        public void Box(Vector3 c, Vector3 size, Quaternion r, int sub)
        {
            Vector3 h = size * 0.5f;
            Vector3[] ax = { r * Vector3.right, r * Vector3.up, r * Vector3.forward };
            float[] e = { h.x, h.y, h.z };
            for (int a = 0; a < 3; a++)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 n = ax[a] * s;
                Vector3 u = ax[(a + 1) % 3] * e[(a + 1) % 3], v = ax[(a + 2) % 3] * e[(a + 2) % 3];
                Vector3 f = c + n * e[a];
                Quad(f - u - v, f + u - v, f + u + v, f - u + v, n, sub);
            }
        }

        /// <summary>
        /// Smooth surface of revolution around <paramref name="axis"/> through <paramref name="c"/>; profile x = radius,
        /// y = offset along the axis. Normals point away from the axis side the profile faces (outer surface when the
        /// profile goes along +axis with growing / constant radius; pass <paramref name="inward"/> for the inside of a horn).
        /// </summary>
        public void Lathe(Vector3 c, Vector3 axis, IList<Vector2> prof, int sides, int sub, bool inward = false, float arc0 = 0f, float arc1 = 360f)
        {
            axis.Normalize();
            Vector3 e1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(axis, e1);
            bool full = Mathf.Abs(arc1 - arc0) >= 359.9f;
            int cols = full ? sides : sides + 1;
            int start = V.Count;
            int rows = prof.Count;
            for (int k = 0; k < rows; k++)
            {
                // Profile normal (2D): perpendicular to the averaged tangent, towards +radius.
                Vector2 tPrev = k > 0 ? prof[k] - prof[k - 1] : prof[k + 1] - prof[k];
                Vector2 tNext = k < rows - 1 ? prof[k + 1] - prof[k] : prof[k] - prof[k - 1];
                Vector2 t = (tPrev.normalized + tNext.normalized);
                if (t.sqrMagnitude < 1e-8f) t = tNext;
                Vector2 n2 = new Vector2(t.y, -t.x).normalized;
                if (inward) n2 = -n2;
                for (int s = 0; s < cols; s++)
                {
                    float a = Mathf.Lerp(arc0, arc1, s / (float)sides) * Mathf.Deg2Rad;
                    Vector3 d = Mathf.Cos(a) * e1 + Mathf.Sin(a) * e2;
                    V.Add(c + d * prof[k].x + axis * prof[k].y);
                    N.Add((d * n2.x + axis * n2.y).normalized);
                }
            }
            for (int k = 0; k < rows - 1; k++)
            for (int s = 0; s < sides; s++)
            {
                int s1 = full ? (s + 1) % sides : s + 1;
                int a = start + k * cols + s, b = start + k * cols + s1, cc = start + (k + 1) * cols + s1, d = start + (k + 1) * cols + s;
                Vector3 want = N[a] + N[b] + N[cc] + N[d];
                if (want.sqrMagnitude < 1e-8f) continue;
                if ((V[a] - V[b]).sqrMagnitude < 1e-12f && (V[d] - V[cc]).sqrMagnitude < 1e-12f) continue;
                Tri(sub, a, b, cc, want);
                Tri(sub, a, cc, d, want);
            }
        }

        /// <summary>Smooth tube through <paramref name="pts"/> (parallel-transport frames), optional end caps.</summary>
        public void Tube(IList<Vector3> pts, float r, int sides, int sub, bool caps = false)
        {
            if (pts.Count < 2) return;
            int start = V.Count;
            Vector3 tan = (pts[1] - pts[0]).normalized;
            Vector3 nrm = Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 tNew = i < pts.Count - 1 ? (pts[i + 1] - pts[i]) : (pts[i] - pts[i - 1]);
                if (i > 0 && i < pts.Count - 1) tNew = (pts[i + 1] - pts[i - 1]);
                if (tNew.sqrMagnitude < 1e-12f) tNew = tan;
                tNew.Normalize();
                // transport the normal
                Vector3 axis = Vector3.Cross(tan, tNew);
                if (axis.sqrMagnitude > 1e-10f)
                {
                    float ang = Mathf.Asin(Mathf.Clamp(axis.magnitude, -1f, 1f)) * Mathf.Rad2Deg;
                    if (Vector3.Dot(tan, tNew) < 0f) ang = 180f - ang;
                    nrm = Quaternion.AngleAxis(ang, axis.normalized) * nrm;
                }
                tan = tNew;
                Vector3 bin = Vector3.Cross(tan, nrm).normalized;
                nrm = Vector3.Cross(bin, tan).normalized;
                for (int s = 0; s < sides; s++)
                {
                    float a = s * Mathf.PI * 2f / sides;
                    Vector3 d = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                    V.Add(pts[i] + d * r);
                    N.Add(d);
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                int a = start + i * sides + s, b = start + i * sides + s1, c = start + (i + 1) * sides + s1, d = start + (i + 1) * sides + s;
                Vector3 want = N[a] + N[b] + N[c] + N[d];
                Tri(sub, a, b, c, want);
                Tri(sub, a, c, d, want);
            }
            if (!caps) return;
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : pts.Count - 1;
                Vector3 dir = e == 0 ? (pts[0] - pts[1]).normalized : (pts[ring] - pts[ring - 1]).normalized;
                int ci = V.Count;
                V.Add(pts[ring]);
                N.Add(dir);
                for (int s = 0; s < sides; s++)
                {
                    V.Add(V[start + ring * sides + s]);
                    N.Add(dir);
                }
                for (int s = 0; s < sides; s++) Tri(sub, ci, ci + 1 + s, ci + 1 + (s + 1) % sides, dir);
            }
        }

        /// <summary>One ring of a <see cref="Pebble"/>: outline scaled by <see cref="Scale"/> after moving it inwards by
        /// <see cref="Inset"/> (along the outline normal), placed at depth <see cref="Z"/>.</summary>
        public struct Ring
        {
            public float Scale, Inset, Z;
            public Ring(float scale, float inset, float z) { Scale = scale; Inset = inset; Z = z; }
        }

        /// <summary>
        /// Rounded shell: the closed convex 2D outline (counter-clockwise, in the plane of <paramref name="right"/> /
        /// <paramref name="up"/>, centred on the origin) swept through <paramref name="rings"/> along <paramref name="fwd"/>.
        /// Rings with scale 0 close the shape at a point (caps). Normals are averaged over the grid (smooth shell).
        /// </summary>
        public void Pebble(IList<Vector2> outline, IList<Ring> rings, Vector3 origin, Vector3 right, Vector3 up, Vector3 fwd, int sub)
        {
            int n = outline.Count;
            var nrm2 = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 t = outline[(i + 1) % n] - outline[(i - 1 + n) % n];
                nrm2[i] = new Vector2(t.y, -t.x).normalized; // outward for a CCW outline
            }
            int start = V.Count;
            int rows = rings.Count;
            for (int k = 0; k < rows; k++)
            for (int i = 0; i < n; i++)
            {
                Vector2 p = (outline[i] - nrm2[i] * rings[k].Inset) * rings[k].Scale;
                V.Add(origin + right * p.x + up * p.y + fwd * rings[k].Z);
                N.Add(Vector3.zero);
            }
            // Accumulate face normals: side faces point away from the centre line, cap faces away from the middle depth.
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var r in rings) { zMin = Mathf.Min(zMin, r.Z); zMax = Mathf.Max(zMax, r.Z); }
            float midZ = (zMin + zMax) * 0.5f;
            var tris = new List<(int a, int b, int c)>();
            for (int k = 0; k < rows - 1; k++)
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                int a = start + k * n + i, b = start + k * n + i1, c = start + (k + 1) * n + i1, d = start + (k + 1) * n + i;
                AddFace(a, b, c, origin, fwd, midZ, tris);
                AddFace(a, c, d, origin, fwd, midZ, tris);
            }
            for (int i = start; i < V.Count; i++)
            {
                Vector3 nn = N[i];
                N[i] = nn.sqrMagnitude > 1e-12f ? nn.normalized : fwd;
            }
            foreach (var (a, b, c) in tris) { T[sub].Add(a); T[sub].Add(b); T[sub].Add(c); }
        }

        private void AddFace(int a, int b, int c, Vector3 origin, Vector3 fwd, float midZ, List<(int, int, int)> tris)
        {
            Vector3 fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (fn.sqrMagnitude < 1e-14f) return;
            Vector3 rel = (V[a] + V[b] + V[c]) / 3f - origin;
            float z = Vector3.Dot(rel, fwd);
            Vector3 radial = rel - fwd * z;
            float axial = Vector3.Dot(fn.normalized, fwd);
            bool flip = Mathf.Abs(axial) > 0.6f ? axial * (z - midZ) < 0f : Vector3.Dot(fn, radial) < 0f;
            if (flip) { (b, c) = (c, b); fn = -fn; }
            N[a] += fn; N[b] += fn; N[c] += fn;
            tris.Add((a, b, c));
        }

        /// <summary>Rings of a rounded slab: flat back at <paramref name="z0"/>, flat front at <paramref name="z1"/>, edges
        /// rounded with radius <paramref name="r"/> (both caps closed).</summary>
        public static List<Ring> SlabRings(float z0, float z1, float r, int arc = 4, float dome = 0f)
        {
            var list = new List<Ring> { new Ring(0f, r, z0), new Ring(0.55f, r, z0), new Ring(0.9f, r, z0) };
            for (int i = 0; i <= arc; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / arc;
                list.Add(new Ring(1f, r * (1f - Mathf.Cos(a)), z0 + r + r * Mathf.Sin(a)));
            }
            for (int i = 0; i <= arc; i++)
            {
                float a = Mathf.PI * 0.5f * i / arc;
                list.Add(new Ring(1f, r * (1f - Mathf.Cos(a)), z1 - r + r * Mathf.Sin(a)));
            }
            list.Add(new Ring(0.9f, r, z1 + dome * 0.19f));
            list.Add(new Ring(0.55f, r, z1 + dome * 0.7f));
            list.Add(new Ring(0f, r, z1 + dome));
            return list;
        }

        /// <summary>Counter-clockwise rounded rectangle (half sizes, corner radius).</summary>
        public static List<Vector2> RoundRect(float hw, float hh, float cr, int perCorner = 4)
        {
            cr = Mathf.Min(cr, Mathf.Min(hw, hh) * 0.999f);
            var pts = new List<Vector2>();
            Vector2[] centres = { new(hw - cr, hh - cr), new(-hw + cr, hh - cr), new(-hw + cr, -hh + cr), new(hw - cr, -hh + cr) };
            for (int c = 0; c < 4; c++)
            for (int i = 0; i <= perCorner; i++)
            {
                float a = (c * 90f + 90f * i / perCorner) * Mathf.Deg2Rad;
                pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cr);
            }
            // drop duplicates where straight edges have zero length
            for (int i = pts.Count - 1; i > 0; i--)
                if ((pts[i] - pts[i - 1]).sqrMagnitude < 1e-12f) pts.RemoveAt(i);
            return pts;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.subMeshCount = T.Length;
            for (int s = 0; s < T.Length; s++) m.SetTriangles(T[s], s);
            m.RecalculateBounds();
            HideAndSeek.Core.MeshNormalFix.Repair(m); // zero normals of tiny faces (gauge needle, strap, lever) = NaN pixels
            return m;
        }

        /// <summary>Standard material helper (colour, smoothness, metallic, optional emission keyword).</summary>
        public static Material Mat(string name, Color c, float smooth, float metal = 0f, bool emissive = false)
        {
            var m = new Material(Shader.Find("Standard")) { name = name, color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", Color.black);
            }
            return m;
        }
    }
}
