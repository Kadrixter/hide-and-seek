using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// Repairs zero-length / NaN vertex normals of generated meshes. Such a normal makes the Standard shader return NaN:
    /// a black pixel, which the bloom lights up into a white glow and the blurs (Esc menu, drunk look, stun) spread into
    /// black squares. Typical source: Vector3.Cross(a, b).normalized on a face smaller than ~3 mm (Unity's normalize
    /// returns zero below a length of 1e-5). The new normal is the average of the faces around the vertex, computed from
    /// normalised edge directions in double precision, so even 0.1 mm faces get a proper direction.
    /// </summary>
    public static class MeshNormalFix
    {
        /// <summary>True for a usable normal (finite, not about zero).</summary>
        public static bool Usable(Vector3 n) =>
            !float.IsNaN(n.x) && !float.IsNaN(n.y) && !float.IsNaN(n.z) &&
            !float.IsInfinity(n.x) && !float.IsInfinity(n.y) && !float.IsInfinity(n.z) && n.sqrMagnitude > 1e-8f;

        /// <summary>Unit normal of the triangle a-b-c (Unity winding), zero only for a truly degenerate one.</summary>
        public static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            double ux = b.x - a.x, uy = b.y - a.y, uz = b.z - a.z;
            double vx = c.x - a.x, vy = c.y - a.y, vz = c.z - a.z;
            double lu = System.Math.Sqrt(ux * ux + uy * uy + uz * uz), lv = System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
            if (lu < 1e-12 || lv < 1e-12) return Vector3.zero;
            ux /= lu; uy /= lu; uz /= lu;
            vx /= lv; vy /= lv; vz /= lv;
            double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
            double l = System.Math.Sqrt(x * x + y * y + z * z);
            if (l < 1e-9) return Vector3.zero;
            return new Vector3((float)(x / l), (float)(y / l), (float)(z / l));
        }

        /// <summary>
        /// Normal of the plane spanned by <paramref name="a"/> and <paramref name="b"/> (cross(a, b) direction) without the
        /// tiny-vector trap of <c>Vector3.Cross(a, b).normalized</c>; <paramref name="fallback"/> if they are parallel.
        /// </summary>
        public static Vector3 SafeCross(Vector3 a, Vector3 b, Vector3 fallback)
        {
            var n = FaceNormal(Vector3.zero, a, b);
            return n == Vector3.zero ? fallback : n;
        }

        /// <summary>Replaces the unusable normals of <paramref name="mesh"/> (all triangle submeshes). Returns how many.</summary>
        public static int Repair(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return 0;
            var n = mesh.normals;
            if (n == null || n.Length == 0) return 0;
            int bad = 0;
            for (int i = 0; i < n.Length; i++) if (!Usable(n[i])) bad++;
            if (bad == 0) return 0;
            var v = mesh.vertices;
            var acc = new Vector3[n.Length];
            var first = new Vector3[n.Length];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var t = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2];
                    if (Usable(n[a]) && Usable(n[b]) && Usable(n[c])) continue;
                    var f = FaceNormal(v[a], v[b], v[c]);
                    if (f == Vector3.zero) continue;
                    acc[a] += f;
                    acc[b] += f;
                    acc[c] += f;
                    if (first[a] == Vector3.zero) first[a] = f;
                    if (first[b] == Vector3.zero) first[b] = f;
                    if (first[c] == Vector3.zero) first[c] = f;
                }
            }
            for (int i = 0; i < n.Length; i++)
            {
                if (Usable(n[i])) continue;
                // faces of a sliver can point both ways and cancel out: then one of them, never Vector3.normalized
                // (it returns zero below a length of 1e-5 - the very bug repaired here)
                float len = acc[i].magnitude;
                n[i] = len > 1e-3f ? acc[i] / len : first[i] != Vector3.zero ? first[i] : Vector3.up;
            }
            mesh.normals = n;
            return bad;
        }
    }
}
