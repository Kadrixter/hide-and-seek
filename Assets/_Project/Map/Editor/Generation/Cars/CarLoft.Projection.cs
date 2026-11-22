using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Front / rear projection onto the body: a point (x, y) seen from straight ahead (dir = +1) or from behind
    /// (dir = -1) lands on the first panel it meets, end cap included. Lamps, grilles and bumper strips drawn as
    /// outlines in the x/y plane then wrap around rounded noses and tails exactly.
    /// </summary>
    internal sealed partial class CarLoft
    {
        /// <summary>Surface z hit by the line (x, y) coming from the front (dir 1) or the back (dir -1).</summary>
        public bool ProjectZ(float x, float y, float dir, out float z, int i0 = CarShapes.BLip, int i1 = CarShapes.BCentre)
        {
            x = Mathf.Abs(x);
            int n = Stations;
            int first = dir > 0 ? n - 1 : 0, step = dir > 0 ? -1 : 1;
            for (int j = first; j >= 0 && j < n; j += step)
            {
                if (!Inside(S[j], x, y)) continue;
                if (j == first) { z = Z[j]; return true; }   // end cap
                // bisect between the last outside station and this one on the interpolated (meshed) surface
                float zIn = Z[j], zOut = Z[j - step];
                for (int it = 0; it < 14; it++)
                {
                    float zm = (zIn + zOut) * 0.5f;
                    if (Inside(SectionAt(zm), x, y)) zIn = zm; else zOut = zm;
                }
                z = (zIn + zOut) * 0.5f;
                return true;
            }
            z = 0f;
            return false;
        }

        /// <summary>Is (x >= 0, y) inside the closed section (half ring + the centre line)?</summary>
        private static bool Inside(Vector2[] s, float x, float y)
        {
            bool inside = false;
            int n = s.Length;
            for (int i = 0, k = n - 1; i < n; k = i++)
            {
                Vector2 a = s[i], b = s[k];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
        /// <summary>Projected point and outward normal for (x, y) (x signed: side follows its sign).</summary>
        public bool Project(float x, float y, float dir, out Vector3 p, out Vector3 nrm)
        {
            p = nrm = Vector3.zero;
            if (!ProjectZ(x, y, dir, out float z)) return false;
            p = new Vector3(x, y, z);
            const float e = 0.006f;
            float z0 = z;
            float Zs(float xx, float yy) => ProjectZ(xx, yy, dir, out float zz) && Mathf.Abs(zz - z0) < 0.1f ? zz : z0;
            float zx1 = Zs(x + e, y), zx0 = Zs(x - e, y), zy1 = Zs(x, y + e), zy0 = Zs(x, y - e);
            Vector3 dx = new Vector3(2 * e, 0, zx1 - zx0);
            Vector3 dy = new Vector3(0, 2 * e, zy1 - zy0);
            nrm = Vector3.Cross(dy, dx).normalized;
            if (nrm.z * dir < 0f) nrm = -nrm;
            return true;
        }

        /// <summary>
        /// Panel whose outline (x/y, right side; mirrored when side = -1) is projected onto the nose (dir 1) or tail
        /// (dir -1), lifted along the normal. Rings from the centroid outwards give a smooth curved lamp face.
        /// </summary>
        /// <summary>Scales the tessellation of projected patches (1 = hero cars; the parked cars use a coarser one).</summary>
        public static float PatchCoarse = 1f;

        public void ProjectedPatch(MeshBuilder mb, Vector2[] outline, float dir, float side, float lift, Material m, int rings = 4,
            float minNz = 0.3f)
        {
            outline = Densify(outline, 0.03f * PatchCoarse); // long chords would cut under curved panels
            Vector2 c = Vector2.zero;
            foreach (var o in outline) c += o;
            c /= outline.Length;
            int n = outline.Length;
            float reach = 0f;
            foreach (var o in outline) reach = Mathf.Max(reach, (o - c).magnitude);
            rings = Mathf.Max(PatchCoarse > 1f ? 2 : rings, Mathf.CeilToInt(reach / (0.035f * PatchCoarse)));
            var P = new Vector3[rings + 1, n];
            var N = new Vector3[rings + 1, n];
            bool ok = true;
            for (int k = 0; k <= rings; k++)
            for (int i = 0; i < n; i++)
            {
                Vector2 q = Vector2.Lerp(c, outline[i], k / (float)rings);
                Vector3 p = Vector3.zero, nn = Vector3.up;
                bool hit = false;
                for (int t = 0; t < 40 && !hit; t++)               // off the body or round the corner: pull it inwards
                    hit = Project(q.x * side * (1f - t * 0.01f), q.y, dir, out p, out nn) && Mathf.Abs(nn.z) >= minNz;
                if (!hit) { ok = false; if (k > 0) { P[k, i] = P[k - 1, i]; N[k, i] = N[k - 1, i]; } continue; }

                P[k, i] = p + nn * lift;
                N[k, i] = nn;
            }
            if (!ok) Debug.LogWarning($"[CarLoft] projected patch partly off the body at {c}");
            for (int k = 0; k < rings; k++)
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                if (k == 0) mb.Tri(P[0, 0], P[1, i], P[1, i2], N[0, 0], N[1, i], N[1, i2], m);
                else mb.Quad(P[k, i], P[k + 1, i], P[k + 1, i2], P[k, i2], N[k, i], N[k + 1, i], N[k + 1, i2], N[k, i2], m);
            }
        }

        /// <summary>Projected lamp: dark shut-line rim, then the lens slightly proud.</summary>
        public void ProjectedLamp(MeshBuilder mb, Vector2[] outline, float dir, float side, Material lens, Material rim,
            float gap = 0.008f, float lift = 0.004f, float minNz = 0.3f)
        {
            if (rim != null) ProjectedPatch(mb, Grow(outline, gap), dir, side, lift * 0.4f, rim, 4, minNz);
            ProjectedPatch(mb, outline, dir, side, lift, lens, 4, minNz);
        }

        /// <summary>Outline with extra points so that no edge is longer than <paramref name="step"/>.</summary>
        public static Vector2[] Densify(Vector2[] o, float step)
        {
            var r = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Length];
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / step));
                for (int k = 0; k < n; k++) r.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return r.ToArray();
        }

        /// <summary>Outline offset outwards from its centroid by d metres.</summary>
        public static Vector2[] Grow(Vector2[] o, float d)
        {
            Vector2 c = Vector2.zero;
            foreach (var p in o) c += p;
            c /= o.Length;
            var r = new Vector2[o.Length];
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 v = o[i] - c;
                r[i] = c + v + v.normalized * d;
            }
            return r;
        }

        /// <summary>Outline helper: a rectangle (rounded corners) centred at (x, y) in the x/y plane.</summary>
        public static Vector2[] RectAt(float x, float y, float w, float h, float r = 0.015f)
        {
            var o = CarKit.Rect(w, h, r);
            for (int i = 0; i < o.Length; i++) o[i] += new Vector2(x, y);
            return o;
        }

        public static Vector2[] PolyAt(float x, float y, params Vector2[] pts)
        {
            var o = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = pts[i] + new Vector2(x, y);
            return o;
        }
    }
}
