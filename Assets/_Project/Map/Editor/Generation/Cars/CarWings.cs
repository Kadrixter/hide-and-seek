using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Rear wing parts for the detailed cars: a cambered airfoil blade spanning x, flat plates (end plates, uprights,
    /// stanchions) extruded across x, struts, bolts and lightening holes. Local car space: +z forward, wings face -z.
    /// </summary>
    internal static partial class CarWings
    {
        /// <summary>
        /// Airfoil blade centred at <paramref name="c"/>, span 2 x <paramref name="halfSpan"/> along x, chord along z
        /// (leading edge forward). <paramref name="thick"/> = thickness / chord; <paramref name="camber"/> bows the
        /// blade down (a downforce wing); <paramref name="pitch"/> lifts the trailing edge (degrees).
        /// </summary>
        public static void Airfoil(MeshBuilder mb, Vector3 c, float halfSpan, float chord, float thick, float pitch, Material m,
            float camber = 0.04f, int n = 12)
        {
            var upper = new List<Vector2>();
            var lower = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float t = (1f - Mathf.Cos(Mathf.PI * i / n)) * 0.5f;   // 0 = leading edge, 1 = trailing edge
                float yt = 5f * thick * chord * (0.2969f * Mathf.Sqrt(t) - 0.126f * t - 0.3516f * t * t + 0.2843f * t * t * t - 0.1036f * t * t * t * t);
                float yc = -camber * chord * 4f * t * (1f - t);
                float z = chord * (0.5f - t);
                upper.Add(new Vector2(z, yc + yt));
                lower.Add(new Vector2(z, yc - yt));
            }
            // closed ring: upper surface trailing -> leading edge, lower surface leading -> trailing edge
            var ring = new List<Vector2>();
            for (int i = n; i >= 0; i--) ring.Add(upper[i]);
            for (int i = 1; i < n; i++) ring.Add(lower[i]);
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            Extrude(mb, ring, c, rot, -halfSpan, halfSpan, m, true);
        }

        /// <summary>Flat plate: polygon (z, y in car space) extruded across x, centred on <paramref name="x"/>.</summary>
        public static void Plate(MeshBuilder mb, Vector2[] zy, float x, float thick, Material m) =>
            Extrude(mb, new List<Vector2>(zy), new Vector3(x, 0f, 0f), Quaternion.identity, -thick * 0.5f, thick * 0.5f, m, false);

        /// <summary>Plate strip along a polyline (z, y) with a width measured across the line (bent stanchions, swan necks).</summary>
        public static void Strip(MeshBuilder mb, Vector2[] line, float width, float x, float thick, Material m)
        {
            for (int i = 0; i < line.Length - 1; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                Vector2 d = (b - a).normalized, nn = new Vector2(-d.y, d.x) * (width * 0.5f);
                Vector2 ext = d * (width * 0.3f); // overlap the joints so bends stay closed
                Plate(mb, new[] { a - nn - ext, b - nn + ext, b + nn + ext, a + nn - ext }, x, thick, m);
            }
        }

        /// <summary>Hex-headed bolt on a plate face: axis = outward normal of the face.</summary>
        public static void Bolt(MeshBuilder mb, Vector3 p, Vector3 axis, float r = 0.009f)
        {
            var steel = mb.Lib["Car_ChromeBright"];
            mb.Cylinder(p - axis * 0.002f, p + axis * 0.006f, r, 6, steel);
            mb.Cylinder(p, p + axis * 0.002f, r * 1.4f, 12, steel);
        }

        /// <summary>A lightening hole: dark discs on both faces of a plate of thickness <paramref name="thick"/> at x.</summary>
        public static void Hole(MeshBuilder mb, float x, float z, float y, float r, float thick)
        {
            var dark = mb.Lib["Car_Well"];
            foreach (float s in new[] { -1f, 1f })
                CarWheels.Lathe(mb, new Vector3(x + s * (thick * 0.5f + 0.0008f), y, z), Vector3.right * s,
                    new[] { new Vector2(r, 0f), new Vector2(0f, 0f) }, 16, dark);
        }

        /// <summary>
        /// Prism of a closed (z, y) ring between x0 and x1, rotated by <paramref name="rot"/> about <paramref name="c"/>.
        /// Smooth normals round the ring (or flat per side when not smooth), flat caps at both ends.
        /// </summary>
        private static void Extrude(MeshBuilder mb, List<Vector2> ring, Vector3 c, Quaternion rot, float x0, float x1, Material m, bool smooth)
        {
            int k = ring.Count;
            Vector2 centroid = Vector2.zero;
            foreach (var p in ring) centroid += p;
            centroid /= k;
            Vector3 W(Vector2 p, float x) => c + rot * new Vector3(x, p.y, p.x);
            Vector3 N2(Vector2 d, Vector2 mid)
            {
                var nn = new Vector2(-d.y, d.x).normalized;
                if (Vector2.Dot(nn, mid - centroid) < 0f) nn = -nn;
                return rot * new Vector3(0f, nn.y, nn.x);
            }
            var vn = new Vector3[k];
            for (int i = 0; i < k; i++)
            {
                Vector2 prev = ring[(i + k - 1) % k], next = ring[(i + 1) % k];
                vn[i] = N2(next - prev, ring[i]);
            }
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                Vector3 na = smooth ? vn[i] : N2(ring[j] - ring[i], (ring[i] + ring[j]) * 0.5f);
                Vector3 nb = smooth ? vn[j] : na;
                mb.Quad(W(ring[i], x0), W(ring[j], x0), W(ring[j], x1), W(ring[i], x1), na, nb, nb, na, m);
            }
            Vector3 cap0 = rot * Vector3.left, cap1 = rot * Vector3.right;
            for (int i = 0; i < k; i++)
            {
                int j = (i + 1) % k;
                mb.Tri(W(centroid, x0), W(ring[i], x0), W(ring[j], x0), cap0, cap0, cap0, m);
                mb.Tri(W(centroid, x1), W(ring[i], x1), W(ring[j], x1), cap1, cap1, cap1, m);
            }
        }
    }
}
