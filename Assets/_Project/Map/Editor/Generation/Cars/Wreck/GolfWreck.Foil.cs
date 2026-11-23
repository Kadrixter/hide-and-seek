using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Greenish tarpaulin thrown over the roof, the left half of the screen and the left front wing (the right of the
    /// picture seen from the front), its edges hanging down, at the front corner to the floor (front photo). Draped over
    /// the real geometry: the car built so far is ray-cast from above (temporary collider far from the scene), the height
    /// field is spread into a tent (cloth stands off edges), softened; then every row of the sheet is laid along that
    /// profile by arc length, so the hem ends wherever the cloth runs out, with soft folds and wrinkles. Opaque, dusty.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private const float FX0 = -1.6f, FX1 = 0.5f, FZ0 = -0.6f, FZ1 = 2.1f, FCell = 0.015f;

        private void Foil()
        {
            int nx = Mathf.CeilToInt((FX1 - FX0) / FCell) + 1, nz = Mathf.CeilToInt((FZ1 - FZ0) / FCell) + 1;
            var h = HeightField(nx, nz);
            h = Tent(h, nx, nz, 2.2f, 12);
            h = Blur(Blur(h, nx, nz), nx, nz);
            float H(float x, float z)
            {
                float fx = Mathf.Clamp((x - FX0) / FCell, 0f, nx - 1.001f), fz = Mathf.Clamp((z - FZ0) / FCell, 0f, nz - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(h[iz * nx + ix], h[iz * nx + ix + 1], tx), Mathf.Lerp(h[(iz + 1) * nx + ix], h[(iz + 1) * nx + ix + 1], tx), tz);
            }
            const int rows = 130, cols = 90;
            var P = new Vector3[rows + 1, cols + 1];
            var prof = new System.Collections.Generic.List<Vector3>();
            var arc = new System.Collections.Generic.List<float>();
            for (int r = 0; r <= rows; r++)
            {
                float z = Mathf.Lerp(-0.45f, 2.0f + 0.05f * Mathf.Sin(r * 0.7f), r / (float)rows);
                float front = WreckKit.Smooth(0.82f, 1.15f, z);
                float xr = Mathf.Lerp(0.32f + 0.06f * Mathf.Sin(z * 5f), -0.42f + 0.05f * Mathf.Sin(z * 11f), front);
                // profile across the car (right edge -> left, down the side, out over the floor)
                prof.Clear(); arc.Clear();
                float len = 0f;
                for (float x = xr; x > FX0 + 0.02f; x -= 0.01f)
                {
                    var q = new Vector3(x, Mathf.Max(H(x, z) + 0.016f, 0.008f), z);
                    if (prof.Count > 0) len += (q - prof[prof.Count - 1]).magnitude;
                    prof.Add(q); arc.Add(len);
                }
                // cloth width of this row: to the roof's edge plus a short drop at the cabin, down to (or near) the floor in front
                float edge = 0f;
                for (int k = 0; k < prof.Count; k++) if (prof[k].x >= -0.68f) edge = arc[k];
                float extra = Mathf.Lerp(0.06f, 0.95f, front) + 0.1f * Mathf.Sin(z * 6.3f) + 0.06f * Mathf.Sin(z * 17f + 1f);
                float width = Mathf.Min(edge + extra, len);
                int seg = 0;
                for (int c = 0; c <= cols; c++)
                {
                    float s = width * c / cols;
                    while (seg < arc.Count - 2 && arc[seg + 1] < s) seg++;
                    float t = Mathf.InverseLerp(arc[seg], arc[seg + 1], s);
                    var p = Vector3.Lerp(prof[seg], prof[seg + 1], t);
                    // soft folds and wrinkles; on the hanging part loose vertical folds swinging out
                    float wr = WreckKit.Noise2(p * 6f) * 0.035f + Mathf.Sin(p.x * 11f + z * 6f) * 0.01f + WreckKit.Noise2(p * 21f) * 0.007f;
                    float hang = WreckKit.Smooth(-0.78f, -0.98f, p.x) * WreckKit.Smooth(0.95f, 0.2f, p.y) * WreckKit.Smooth(0.008f, 0.06f, p.y);
                    float fold = Mathf.Sin(z * 12f + WreckKit.Noise2(p * 3f) * 4f) * 0.5f + 0.5f;
                    p.y += Mathf.Max(wr, -0.006f) * (1f - hang);
                    p.x -= hang * (0.03f + 0.12f * fold);
                    p.z += hang * Mathf.Sin(z * 7f + 1.3f) * 0.035f;
                    // the hem curls a little
                    if (c == cols) p += new Vector3(-0.01f, 0.012f, 0f);
                    P[r, c] = p;
                }
            }
            var tarp = Lib["Wreck_Tarp"];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Vector3 a = P[r, c], b = P[r, c + 1], cc = P[r + 1, c + 1], d = P[r + 1, c];
                Vector3 N(int i, int k)
                {
                    Vector3 du = P[r + i, Mathf.Min(c + k + 1, cols)] - P[r + i, Mathf.Max(c + k - 1, 0)];
                    Vector3 dv = P[Mathf.Min(r + i + 1, rows), c + k] - P[Mathf.Max(r + i - 1, 0), c + k];
                    var n = Vector3.Cross(dv, du).normalized;
                    // outside of the sheet: up on top, away from the car on the sides
                    return n.y < -0.2f || (Mathf.Abs(n.y) <= 0.2f && n.x > 0f) ? -n : n;
                }
                Vector3 na = N(0, 0), nb = N(0, 1), nc = N(1, 1), nd = N(1, 0);
                Mb.Quad(a, b, cc, d, na, nb, nc, nd, tarp);
                const float o = 0.0015f;
                Mb.Quad(a - na * o, b - nb * o, cc - nc * o, d - nd * o, -na, -nb, -nc, -nd, tarp);
            }
        }

        /// <summary>Highest surface of the car built so far (plus the floor at 0) on a grid seen from above.</summary>
        private float[] HeightField(int nx, int nz)
        {
            var h = new float[nx * nz];
            var mesh = Mb.ToMesh("WreckFoilProbe");
            var go = new GameObject("WreckFoilProbe");
            var offset = new Vector3(0f, -5000f, 0f);
            go.transform.position = offset;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            Physics.SyncTransforms();
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                var o = new Vector3(FX0 + ix * FCell, 3f, FZ0 + iz * FCell) + offset;
                h[iz * nx + ix] = Physics.Raycast(o, Vector3.down, out var hit, 3f) && hit.collider == mc ? hit.point.y - offset.y : 0f;
            }
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(mesh);
            return h;
        }

        /// <summary>Spreads every height into a cone (slope k): the sheet stands off edges instead of following them down.</summary>
        private static float[] Tent(float[] h, int nx, int nz, float k, int radius)
        {
            var o = (float[])h.Clone();
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float best = h[iz * nx + ix];
                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = ix + dx, z = iz + dz;
                    if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                    float v = h[z * nx + x] - k * FCell * Mathf.Sqrt(dx * dx + dz * dz);
                    if (v > best) best = v;
                }
                o[iz * nx + ix] = best;
            }
            return o;
        }

        private static float[] Blur(float[] h, int nx, int nz)
        {
            var o = new float[h.Length];
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float s = 0f, w = 0f;
                for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = Mathf.Clamp(ix + dx, 0, nx - 1), z = Mathf.Clamp(iz + dz, 0, nz - 1);
                    float k = 3f - Mathf.Abs(dx) * 0.5f - Mathf.Abs(dz) * 0.5f;
                    s += h[z * nx + x] * k; w += k;
                }
                o[iz * nx + ix] = Mathf.Max(s / w, h[iz * nx + ix]);
            }
            return o;
        }
    }
}
