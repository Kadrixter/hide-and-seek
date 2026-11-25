using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames.Pool
{
    /// <summary>
    /// Meshes of the wall cue rack: a board top with real round recesses (butt cups, ball cups), a rail with U notches
    /// for the shafts (flat extrusion), front silhouettes (stiles, back boards, pediment) and side brackets. Cached by key.
    /// </summary>
    internal static class CueRackMeshes
    {
        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Rotation that lays a <see cref="BoardMeshes.Extrude"/> outline flat: outline (a, b) = (X, Z), thickness along Y.</summary>
        public static readonly Quaternion Flat = Quaternion.LookRotation(Vector3.right, Vector3.forward);
        /// <summary>Rotation that stands an outline up against the wall: outline (a, b) = (X, Y), thickness along Z.</summary>
        public static readonly Quaternion Upright = Quaternion.LookRotation(Vector3.right, Vector3.up);

        /// <summary>
        /// Top layer of a shelf: the rectangle x0..x1, z0..z1 at y = 0 with round recesses of radius <paramref name="r"/>
        /// and depth <paramref name="depth"/> at (holeX[i], zc), plus the four side faces down to -<paramref name="side"/>.
        /// The recess bottoms are left open (a felt disc goes there).
        /// </summary>
        public static Mesh HoledTop(string key, float x0, float x1, float z0, float z1, float[] holeX, float zc, float r, float depth, float side)
        {
            key = "holed_" + key;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const float uvs = 4f;
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, bool wall)
            {
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                n.Add(na); n.Add(nb); n.Add(nc); n.Add(nd);
                foreach (var p in new[] { a, b, c, d })
                    uv.Add(wall ? new Vector2((p.x + p.z) * uvs, p.y * uvs) : new Vector2(p.x * uvs, p.z * uvs));
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            }
            var xs = new List<float>(holeX);
            xs.Sort();
            float cellStart = x0;
            for (int h = 0; h < xs.Count; h++)
            {
                float cellEnd = h + 1 < xs.Count ? (xs[h] + xs[h + 1]) * 0.5f : x1;
                Vector2 c = new Vector2(xs[h], zc);
                var angles = new List<float>();
                for (int i = 0; i < 40; i++) angles.Add(i / 40f * Mathf.PI * 2f);
                foreach (var corner in new[] { new Vector2(cellEnd, z1), new Vector2(cellStart, z1), new Vector2(cellStart, z0), new Vector2(cellEnd, z0) })
                {
                    float a = Mathf.Atan2(corner.y - c.y, corner.x - c.x);
                    angles.Add(a < 0f ? a + Mathf.PI * 2f : a);
                }
                angles.Sort();
                for (int i = 0; i < angles.Count; i++)
                {
                    float a0 = angles[i], a1 = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.PI * 2f;
                    if (a1 - a0 < 1e-5f) continue;
                    Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                    Vector2 e0 = c + d0 * RectHit(c, d0, cellStart, cellEnd, z0, z1), e1 = c + d1 * RectHit(c, d1, cellStart, cellEnd, z0, z1);
                    Vector2 h0 = c + d0 * r, h1 = c + d1 * r;
                    Quad(new Vector3(h0.x, 0f, h0.y), new Vector3(h1.x, 0f, h1.y), new Vector3(e1.x, 0f, e1.y), new Vector3(e0.x, 0f, e0.y),
                        Vector3.up, Vector3.up, Vector3.up, Vector3.up, false);
                    // Recess wall, facing the hole's axis.
                    Vector3 n0 = new Vector3(-d0.x, 0f, -d0.y), n1 = new Vector3(-d1.x, 0f, -d1.y);
                    Quad(new Vector3(h0.x, -depth, h0.y), new Vector3(h1.x, -depth, h1.y), new Vector3(h1.x, 0f, h1.y), new Vector3(h0.x, 0f, h0.y), n0, n1, n1, n0, true);
                }
                cellStart = cellEnd;
            }
            if (cellStart < x1 - 1e-5f || xs.Count == 0)
                Quad(new Vector3(cellStart, 0f, z0), new Vector3(cellStart, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0), Vector3.up, Vector3.up, Vector3.up, Vector3.up, false);
            // Sides.
            Quad(new Vector3(x0, -side, z1), new Vector3(x1, -side, z1), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward, true);
            Quad(new Vector3(x1, -side, z0), new Vector3(x0, -side, z0), new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), Vector3.back, Vector3.back, Vector3.back, Vector3.back, true);
            Quad(new Vector3(x0, -side, z0), new Vector3(x0, -side, z1), new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0), Vector3.left, Vector3.left, Vector3.left, Vector3.left, true);
            Quad(new Vector3(x1, -side, z1), new Vector3(x1, -side, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.right, Vector3.right, Vector3.right, Vector3.right, true);
            var m = new Mesh { name = key };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            BoardMeshes.FixWinding(m);
            m.RecalculateTangents();
            m.RecalculateBounds();
            Cache[key] = m;
            return m;
        }

        /// <summary>Distance from <paramref name="c"/> along <paramref name="d"/> to the edge of the rectangle.</summary>
        static float RectHit(Vector2 c, Vector2 d, float x0, float x1, float z0, float z1)
        {
            float t = float.MaxValue;
            if (d.x > 1e-6f) t = Mathf.Min(t, (x1 - c.x) / d.x);
            if (d.x < -1e-6f) t = Mathf.Min(t, (x0 - c.x) / d.x);
            if (d.y > 1e-6f) t = Mathf.Min(t, (z1 - c.y) / d.y);
            if (d.y < -1e-6f) t = Mathf.Min(t, (z0 - c.y) / d.y);
            return t;
        }

        /// <summary>
        /// Outline (X, Z) of the shaft rail: x0..x1, z0..z1 with U notches of radius <paramref name="r"/> opening to the
        /// front (+Z), the round end centred at (notchX[i], zc), the mouths slightly flared. Use with <see cref="Flat"/>.
        /// </summary>
        public static List<Vector2> NotchedOutline(float x0, float x1, float z0, float z1, float[] notchX, float zc, float r)
        {
            var o = new List<Vector2> { new(x0, z0), new(x1, z0), new(x1, z1) };
            var xs = new List<float>(notchX);
            xs.Sort();
            const float flare = 0.005f;
            for (int i = xs.Count - 1; i >= 0; i--)
            {
                float x = xs[i];
                o.Add(new Vector2(x + r + flare, z1));
                o.Add(new Vector2(x + r + flare * 0.3f, z1 - flare * 0.7f));
                o.Add(new Vector2(x + r, z1 - flare * 1.6f));
                for (int k = 0; k <= 10; k++)
                {
                    float a = Mathf.Lerp(0f, -180f, k / 10f) * Mathf.Deg2Rad;
                    o.Add(new Vector2(x + Mathf.Cos(a) * r, zc + Mathf.Sin(a) * r));
                }
                o.Add(new Vector2(x - r, z1 - flare * 1.6f));
                o.Add(new Vector2(x - r - flare * 0.3f, z1 - flare * 0.7f));
                o.Add(new Vector2(x - r - flare, z1));
            }
            o.Add(new Vector2(x0, z1));
            return o;
        }

        /// <summary>Stile (upright) of width <paramref name="w"/> from <paramref name="y0"/> to <paramref name="y1"/>: a drop at the bottom, a chamfered flat top.</summary>
        public static List<Vector2> StileOutline(float w, float y0, float y1)
        {
            float hw = w * 0.5f;
            var o = new List<Vector2>();
            // Bottom drop (right half, from the tip up), then the right edge.
            Arc(o, 0f, y0 + 0.012f, 0.012f, -90f, 0f, 5);
            o.Add(new Vector2(0.013f, y0 + 0.026f)); o.Add(new Vector2(hw * 0.7f, y0 + 0.045f)); o.Add(new Vector2(hw, y0 + 0.07f));
            // Chamfered flat top (a turned finial sits on it).
            o.Add(new Vector2(hw, y1 - 0.004f)); o.Add(new Vector2(hw - 0.004f, y1)); o.Add(new Vector2(0f, y1));
            int k = o.Count;
            for (int i = k - 2; i >= 1; i--) o.Add(new Vector2(-o[i].x, o[i].y));
            return o;
        }

        /// <summary>Board between <paramref name="x0"/>..<paramref name="x1"/>, bottom at <paramref name="y0"/>, top at <paramref name="y1"/>
        /// rising by <paramref name="arch"/> in the middle (a shallow pediment) with stepped shoulders.</summary>
        public static List<Vector2> ArchOutline(float x0, float x1, float y0, float y1, float arch)
        {
            var o = new List<Vector2> { new(x0, y0), new(x1, y0), new(x1, y1) };
            float w = x1 - x0;
            for (int i = 1; i < 24; i++)
            {
                float u = 1f - i / 24f;
                float x = x0 + w * u;
                float s = Mathf.Sin(u * Mathf.PI);
                o.Add(new Vector2(x, y1 + arch * Mathf.Pow(s, 0.6f)));
            }
            o.Add(new Vector2(x0, y1));
            return o;
        }

        /// <summary>Side bracket (corbel) under a shelf: outline in (Z, Y), the top at y = 0 from z = z0 to z0 + depth, an ogee down to the wall.</summary>
        public static List<Vector2> CorbelOutline(float z0, float depth, float drop)
        {
            var o = new List<Vector2> { new(z0, 0f), new(z0, -drop) };
            for (int i = 0; i <= 12; i++)
            {
                float u = i / 12f;
                float z = z0 + 0.012f + (depth - 0.016f) * (0.5f - 0.5f * Mathf.Cos(u * Mathf.PI)) ;
                float y = -drop + drop * 0.85f * (u * u * (3f - 2f * u));
                o.Add(new Vector2(z + u * 0.002f, y));
            }
            o.Add(new Vector2(z0 + depth, -drop * 0.12f));
            o.Add(new Vector2(z0 + depth, 0f));
            return o;
        }

        static void Arc(List<Vector2> into, float cx, float cy, float r, float a0, float a1, int n) => BoardMeshes.Arc(into, cx, cy, r, a0, a1, n);
    }
}
