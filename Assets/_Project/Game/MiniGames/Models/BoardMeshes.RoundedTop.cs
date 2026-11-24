using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Table top seen from above as a rounded rectangle (big corner radius) with a fully rounded (bullnose) edge:
    /// an edge profile swept along the rounded outline, plus flat top and bottom caps. Normals are analytic (the
    /// profile normal turned to the outline's outward direction), so there are no zero normals on the thin edge.
    /// </summary>
    public static partial class BoardMeshes
    {
        /// <param name="sx">Size along X.</param>
        /// <param name="sz">Size along Z.</param>
        /// <param name="corner">Corner radius in plan.</param>
        /// <param name="thick">Thickness; the top surface is at y = 0, the bottom at y = -thick.</param>
        /// <param name="edge">Radius of the rounded edge (at most thick / 2: a half-round nose).</param>
        public static Mesh RoundedTop(float sx, float sz, float corner, float thick, float edge, int cornerSegs = 12, int edgeSegs = 8)
        {
            string key = $"rtop{sx:0.000}x{sz:0.000}c{corner:0.000}t{thick:0.000}e{edge:0.000}";
            if (TryGet(key, out var cached) && cached != null) return cached;
            corner = Mathf.Min(corner, Mathf.Min(sx, sz) * 0.5f - 0.001f);
            edge = Mathf.Min(edge, thick * 0.5f);

            // Outline (counter-clockwise seen from above): corner centres, outward normal per point.
            float hx = sx * 0.5f - corner, hz = sz * 0.5f - corner;
            var centres = new[] { new Vector2(hx, hz), new Vector2(-hx, hz), new Vector2(-hx, -hz), new Vector2(hx, -hz) };
            var pathPos = new List<Vector3>();
            var pathOut = new List<Vector3>();
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= cornerSegs; i++)
                {
                    float a = (c * 90f + 90f * i / cornerSegs) * Mathf.Deg2Rad;
                    var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    pathOut.Add(o);
                    pathPos.Add(new Vector3(centres[c].x, 0f, centres[c].y) + o * corner);
                }
            pathPos.Add(pathPos[0]);
            pathOut.Add(pathOut[0]);

            // Edge profile (d = outward offset from the outline, y), top to bottom, with normals in (d, y).
            var prof = new List<(Vector2 p, Vector2 n)>();
            float flat = thick - 2f * edge; // vertical band between the two quarter rounds
            for (int i = 0; i <= edgeSegs; i++)
            {
                float a = Mathf.Lerp(90f, 0f, i / (float)edgeSegs) * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                prof.Add((new Vector2(-edge, -edge) + n * edge, n));
            }
            for (int i = flat > 0.0005f ? 0 : 1; i <= edgeSegs; i++)
            {
                float a = Mathf.Lerp(0f, -90f, i / (float)edgeSegs) * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                prof.Add((new Vector2(-edge, -thick + edge) + n * edge, n));
            }

            var v = new List<Vector3>();
            var nn = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            int rows = prof.Count, cols = pathPos.Count;
            float along = 0f;
            for (int j = 0; j < cols; j++)
            {
                if (j > 0) along += Vector3.Distance(pathPos[j], pathPos[j - 1]);
                float down = 0f;
                for (int i = 0; i < rows; i++)
                {
                    if (i > 0) down += Vector2.Distance(prof[i].p, prof[i - 1].p);
                    var (p, n) = prof[i];
                    v.Add(pathPos[j] + pathOut[j] * p.x + Vector3.up * p.y);
                    nn.Add((pathOut[j] * n.x + Vector3.up * n.y).normalized);
                    uv.Add(new Vector2(along, down));
                }
            }
            for (int j = 0; j < cols - 1; j++)
                for (int i = 0; i < rows - 1; i++)
                {
                    int a = j * rows + i, b = a + rows;
                    t.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 }); // clockwise seen from outside
                }

            // Caps: fans from the centre to the innermost ring of the top / bottom.
            Cap(v, nn, uv, t, pathPos, pathOut, -edge, 0f, true);
            Cap(v, nn, uv, t, pathPos, pathOut, -edge, -thick, false);

            var m = new Mesh { name = key };
            m.SetVertices(v);
            m.SetNormals(nn);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateTangents();
            return Store(key, m);
        }

        static void Cap(List<Vector3> v, List<Vector3> nn, List<Vector2> uv, List<int> t, List<Vector3> pathPos, List<Vector3> pathOut, float d, float y, bool up)
        {
            var n = up ? Vector3.up : Vector3.down;
            int centre = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            nn.Add(n);
            uv.Add(Vector2.zero);
            for (int j = 0; j < pathPos.Count; j++)
            {
                var p = pathPos[j] + pathOut[j] * d + Vector3.up * y;
                v.Add(p);
                nn.Add(n);
                uv.Add(new Vector2(p.x, p.z));
            }
            for (int j = 0; j < pathPos.Count - 1; j++)
            {
                int a = centre + 1 + j, b = a + 1;
                if (up) t.AddRange(new[] { centre, b, a });
                else t.AddRange(new[] { centre, a, b });
            }
        }
    }
}
