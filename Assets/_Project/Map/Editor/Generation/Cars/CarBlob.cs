using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Rounded solids (superellipsoids): exponent 1 = ellipsoid, small exponents = box with soft edges.
    /// Used for mirror housings, seat cushions, head rests, spokes and other small moulded parts.
    /// </summary>
    internal static class CarBlob
    {
        private static float C(float w, float e) { float c = Mathf.Cos(w); return Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e); }
        private static float S(float w, float e) { float s = Mathf.Sin(w); return Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e); }

        /// <summary>
        /// Superellipsoid centred at c with half sizes h (local x, y, z), rotated by r. <paramref name="e"/> sets
        /// how square the cross-sections are, <paramref name="ez"/> the profile along local z (defaults to e).
        /// <paramref name="taper"/> scales the -z end (0.6 = 60 % of the +z end) for teardrop shapes.
        /// </summary>
        public static void Blob(MeshBuilder mb, Vector3 c, Quaternion r, Vector3 h, float e, Material m, float ez = -1f,
            float taper = 1f, int nu = 16, int nv = 10)
        {
            if (ez < 0f) ez = e;
            var P = new Vector3[nu + 1, nv + 1];
            var Nn = new Vector3[nu + 1, nv + 1];
            for (int i = 0; i <= nu; i++)
            {
                float u = -Mathf.PI + i * 2f * Mathf.PI / nu;
                for (int j = 0; j <= nv; j++)
                {
                    float v = -Mathf.PI * 0.5f + j * Mathf.PI / nv;
                    float sc = Mathf.Lerp(taper, 1f, (S(v, ez) + 1f) * 0.5f);
                    P[i, j] = new Vector3(h.x * C(v, ez) * C(u, e) * sc, h.y * C(v, ez) * S(u, e) * sc, h.z * S(v, ez));
                    // analytic superellipsoid normal (exact without taper, close enough with it)
                    Nn[i, j] = new Vector3(C(v, 2f - ez) * C(u, 2f - e) / Mathf.Max(h.x * sc, 1e-4f),
                        C(v, 2f - ez) * S(u, 2f - e) / Mathf.Max(h.y * sc, 1e-4f), S(v, 2f - ez) / Mathf.Max(h.z, 1e-4f));
                }
            }
            for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
                mb.Quad(c + r * P[i, j], c + r * P[i + 1, j], c + r * P[i + 1, j + 1], c + r * P[i, j + 1],
                    r * Nn[i, j], r * Nn[i + 1, j], r * Nn[i + 1, j + 1], r * Nn[i, j + 1], m);
        }

        /// <summary>Rounded bar between two points (spokes, struts, stalks).</summary>
        public static void Bar(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 up, float width, float thick, Material m, float e = 0.5f,
            float taper = 1f)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var r = Quaternion.LookRotation(d, up);
            Blob(mb, (a + b) * 0.5f, r, new Vector3(width * 0.5f, thick * 0.5f, d.magnitude * 0.5f), e, m, 0.25f, taper, 10, 6);
        }
    }
}
