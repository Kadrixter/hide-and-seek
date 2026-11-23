using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Small procedural mesh kit for the lobby furniture: lathe (surface of revolution), profile swept along a straight
    /// run (mouldings, frames), torus, rounded plate. All meshes come back unsaved - <c>MultiplayerBuilder.SaveMesh</c>
    /// stores them under Lobby/Generated/Meshes.
    /// </summary>
    public static class LobbyMesh
    {
        /// <summary>
        /// Lathe around +Y: <paramref name="profile"/> = (radius, height) from bottom to top. Sharp corners where a point
        /// is repeated (split normals). Caps: closes the ends whose radius is &gt; 0.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 32, bool capBottom = true, bool capTop = true)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float total = 0f;
            for (int i = 1; i < profile.Count; i++) total += Vector2.Distance(profile[i - 1], profile[i]);
            float run = 0f;
            for (int i = 0; i < profile.Count - 1; i++)
            {
                Vector2 a = profile[i], b = profile[i + 1];
                float len = Vector2.Distance(a, b);
                if (len < 1e-6f) continue;
                Vector2 d = (b - a) / len;
                Vector2 pn = new Vector2(d.y, -d.x); // outward normal in (r, y)
                int start = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    float c = Mathf.Cos(ang), si = Mathf.Sin(ang);
                    v.Add(new Vector3(a.x * c, a.y, a.x * si));
                    v.Add(new Vector3(b.x * c, b.y, b.x * si));
                    n.Add(new Vector3(pn.x * c, pn.y, pn.x * si).normalized);
                    n.Add(new Vector3(pn.x * c, pn.y, pn.x * si).normalized);
                    uv.Add(new Vector2(s / (float)segments, run / total));
                    uv.Add(new Vector2(s / (float)segments, (run + len) / total));
                }
                for (int s = 0; s < segments; s++)
                {
                    int i0 = start + s * 2;
                    t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2);
                    t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0 + 3);
                }
                run += len;
            }
            void Cap(Vector2 p, bool up)
            {
                if (p.x < 1e-5f) return;
                int c0 = v.Count;
                v.Add(new Vector3(0f, p.y, 0f)); n.Add(up ? Vector3.up : Vector3.down); uv.Add(new Vector2(0.5f, 0.5f));
                for (int s = 0; s <= segments; s++)
                {
                    float ang = s / (float)segments * Mathf.PI * 2f;
                    v.Add(new Vector3(p.x * Mathf.Cos(ang), p.y, p.x * Mathf.Sin(ang)));
                    n.Add(up ? Vector3.up : Vector3.down);
                    uv.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang)));
                }
                for (int s = 0; s < segments; s++)
                {
                    if (up) { t.Add(c0); t.Add(c0 + s + 2); t.Add(c0 + s + 1); }
                    else { t.Add(c0); t.Add(c0 + s + 1); t.Add(c0 + s + 2); }
                }
            }
            if (capBottom) Cap(profile[0], false);
            if (capTop) Cap(profile[^1], true);
            return Build(v, n, uv, t);
        }

        /// <summary>
        /// Moulding: a closed 2D <paramref name="profile"/> (x = out of the wall, y = up) swept along local +X over
        /// <paramref name="length"/>, the wall face at z = 0 and the moulding standing out towards -Z.
        /// Ends are mitred in the X-Y plane by <paramref name="miterStart"/> / <paramref name="miterEnd"/> (45 degree cuts for picture frames: profile y = 0 is the outer edge).
        /// </summary>
        public static Mesh Sweep(IList<Vector2> profile, float length, bool miterStart = false, bool miterEnd = false)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int m = profile.Count;
            for (int i = 0; i < m; i++)
            {
                Vector2 a = profile[i], b = profile[(i + 1) % m];
                Vector2 d = (b - a).normalized;
                Vector3 nn = new Vector3(0f, -d.x, d.y); // profile ccw seen from +X: outward = (dy, -dx) in (z=-x, y)
                nn = new Vector3(0f, nn.y, -nn.z);
                float xa0 = miterStart ? a.y : 0f, xb0 = miterStart ? b.y : 0f;
                float xa1 = length - (miterEnd ? a.y : 0f), xb1 = length - (miterEnd ? b.y : 0f);
                int s = v.Count;
                v.Add(new Vector3(xa0, a.y, -a.x)); v.Add(new Vector3(xb0, b.y, -b.x));
                v.Add(new Vector3(xa1, a.y, -a.x)); v.Add(new Vector3(xb1, b.y, -b.x));
                for (int k = 0; k < 4; k++) n.Add(nn);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 0.1f)); uv.Add(new Vector2(length, 0f)); uv.Add(new Vector2(length, 0.1f));
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                t.Add(s + 1); t.Add(s + 3); t.Add(s + 2);
            }
            // end caps (fan; profiles are convex enough for mouldings)
            void Cap(bool end)
            {
                int c0 = v.Count;
                for (int i = 0; i < m; i++)
                {
                    var p = profile[i];
                    float x = end ? length - (miterEnd ? p.y : 0f) : (miterStart ? p.y : 0f);
                    v.Add(new Vector3(x, p.y, -p.x));
                    Vector3 cn = end ? (miterEnd ? new Vector3(1f, 1f, 0f).normalized : Vector3.right) : (miterStart ? new Vector3(-1f, 1f, 0f).normalized : Vector3.left);
                    n.Add(cn);
                    uv.Add(new Vector2(p.x, p.y));
                }
                for (int i = 1; i < m - 1; i++)
                {
                    if (end) { t.Add(c0); t.Add(c0 + i); t.Add(c0 + i + 1); }
                    else { t.Add(c0); t.Add(c0 + i + 1); t.Add(c0 + i); }
                }
            }
            Cap(false);
            Cap(true);
            return Build(v, n, uv, t);
        }

        /// <summary>Torus in the XZ plane (ring radius <paramref name="r"/>, tube radius <paramref name="tube"/>).</summary>
        public static Mesh Torus(float r, float tube, int seg = 32, int sides = 10)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    Vector3 nn = c * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(c * r + nn * tube);
                    n.Add(nn);
                    uv.Add(new Vector2(i / (float)seg, j / (float)sides));
                }
            }
            int w = sides + 1;
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * w + j;
                t.Add(a); t.Add(a + 1); t.Add(a + w);
                t.Add(a + 1); t.Add(a + w + 1); t.Add(a + w);
            }
            return Build(v, n, uv, t);
        }

        /// <summary>Fan blade / paddle: flat plate in XZ (root at origin, along +Z), rounded tip, slight bevel.</summary>
        public static Mesh Paddle(float length, float rootWidth, float tipWidth, float thick)
        {
            var outline = new List<Vector2>();
            const int tipSeg = 10;
            outline.Add(new Vector2(-rootWidth * 0.5f, 0f));
            outline.Add(new Vector2(rootWidth * 0.5f, 0f));
            float tr = tipWidth * 0.5f, zc = length - tr;
            for (int i = 0; i <= tipSeg; i++)
            {
                float a = i / (float)tipSeg * Mathf.PI;
                outline.Add(new Vector2(Mathf.Cos(a) * tr, zc + Mathf.Sin(a) * tr));
            }
            return Extrude(outline, thick);
        }

        /// <summary>Convex outline (x, z), counter-clockwise from above, extruded up by <paramref name="thick"/> (centred on y 0).</summary>
        public static Mesh Extrude(IList<Vector2> outline, float thick)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int m = outline.Count;
            float h = thick * 0.5f;
            for (int face = 0; face < 2; face++)
            {
                int s = v.Count;
                float y = face == 0 ? h : -h;
                foreach (var p in outline) { v.Add(new Vector3(p.x, y, p.y)); n.Add(face == 0 ? Vector3.up : Vector3.down); uv.Add(p); }
                for (int i = 1; i < m - 1; i++)
                {
                    if (face == 0) { t.Add(s); t.Add(s + i + 1); t.Add(s + i); }
                    else { t.Add(s); t.Add(s + i); t.Add(s + i + 1); }
                }
            }
            for (int i = 0; i < m; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % m];
                Vector2 d = (b - a).normalized;
                Vector3 nn = new Vector3(d.y, 0f, -d.x);
                int s = v.Count;
                v.Add(new Vector3(a.x, h, a.y)); v.Add(new Vector3(b.x, h, b.y)); v.Add(new Vector3(a.x, -h, a.y)); v.Add(new Vector3(b.x, -h, b.y));
                for (int k = 0; k < 4; k++) n.Add(nn);
                uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0));
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
                t.Add(s + 1); t.Add(s + 3); t.Add(s + 2);
            }
            return Build(v, n, uv, t);
        }

        public static Mesh Build(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var mesh = new Mesh();
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Rectangle profile with a rounded / chamfered front edge (for mouldings): depth out of the wall, height.</summary>
        public static List<Vector2> MouldingProfile(float depth, float height, float bead)
        {
            // ccw as seen from +X with x = out of the wall, y = up
            var p = new List<Vector2> { new(0f, 0f), new(depth - bead, 0f) };
            for (int i = 1; i <= 4; i++)
            {
                float a = -Mathf.PI * 0.5f + i / 4f * Mathf.PI * 0.5f;
                p.Add(new Vector2(depth - bead + Mathf.Cos(a) * bead, bead + Mathf.Sin(a) * bead));
            }
            p.Add(new Vector2(depth, height * 0.55f));
            p.Add(new Vector2(depth * 0.55f, height * 0.75f));
            p.Add(new Vector2(depth * 0.45f, height));
            p.Add(new Vector2(0f, height));
            return p;
        }
    }
}
