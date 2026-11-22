using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Small mesh builder for the trash bin parts: lathe-like grids around the Y axis (seamless normals computed from
    /// the neighbours, so the u seam never shows), loose flat-shaded triangles (crumpled paper), vertex colour = baked
    /// occlusion for <c>HideAndSeek/TrashProp</c>.
    /// </summary>
    internal sealed class TrashMesh
    {
        public readonly List<Vector3> V = new();
        public readonly List<Vector3> N = new();
        public readonly List<Vector2> UV = new();
        public readonly List<Color> C = new();
        public readonly List<int> T = new();

        /// <summary>
        /// Grid of <paramref name="cols"/> segments round the axis (periodic) by <paramref name="rows"/> profile steps.
        /// pos(i, j) must accept any i (wraps by itself). Faces outward along the profile direction rotated by
        /// +90 degrees (inner -> axis when the profile goes up) unless <paramref name="flip"/>.
        /// </summary>
        public void Grid(int cols, int rows, Func<int, int, Vector3> pos, Func<int, int, Vector2> uv, Func<int, int, Color> col, bool flip)
        {
            int start = V.Count;
            for (int j = 0; j <= rows; j++)
            for (int i = 0; i <= cols; i++)
            {
                Vector3 p = pos(i, j);
                Vector3 da = pos(i + 1, j) - pos(i - 1, j);
                Vector3 db = pos(i, Mathf.Min(rows, j + 1)) - pos(i, Mathf.Max(0, j - 1));
                Vector3 n = Vector3.Cross(da, db);
                if (flip) n = -n;
                V.Add(p);
                N.Add(n.sqrMagnitude > 1e-14f ? n.normalized : Vector3.up);
                UV.Add(uv(i, j));
                C.Add(col(i, j));
            }
            int w = cols + 1;
            for (int j = 0; j < rows; j++)
            for (int i = 0; i < cols; i++)
            {
                int a = start + j * w + i, b = a + 1, c = a + w, d = c + 1;
                if (!flip) { T.Add(a); T.Add(b); T.Add(c); T.Add(b); T.Add(d); T.Add(c); }
                else { T.Add(a); T.Add(c); T.Add(b); T.Add(b); T.Add(c); T.Add(d); }
            }
        }

        /// <summary>One flat-shaded triangle (front face = clockwise seen from the outside, Unity convention).</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color ca, Color cb, Color cc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int s = V.Count;
            V.Add(a); V.Add(b); V.Add(c);
            N.Add(n); N.Add(n); N.Add(n);
            UV.Add(ua); UV.Add(ub); UV.Add(uc);
            C.Add(ca); C.Add(cb); C.Add(cc);
            T.Add(s); T.Add(s + 1); T.Add(s + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            m.SetColors(C);
            m.SetTriangles(T, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Grey occlusion colour: rgb darkens the direct light a little less than the ambient (a).</summary>
        public static Color Ao(float ao) { float d = Mathf.Lerp(1f, ao, 0.75f); return new Color(d, d, d, ao); }
    }
}
