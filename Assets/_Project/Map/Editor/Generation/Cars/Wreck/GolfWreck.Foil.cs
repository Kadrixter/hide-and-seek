using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Greenish see-through foil thrown over the roof, the left half of the screen and the left front wing, hanging down to
    /// the floor at the left front corner (front photo). Draped over the real geometry: the car built so far is ray-cast
    /// from above (temporary collider far from the scene), the height field is spread into a tent (a sheet cannot cling
    /// to a vertical edge), softened, then wrinkled.
    /// </summary>
    internal sealed partial class GolfWreck
    {
        private const float FX0 = -1.15f, FX1 = 0.5f, FZ0 = -0.6f, FZ1 = 2.05f, FCell = 0.015f;

        private void Foil()
        {
            int nx = Mathf.CeilToInt((FX1 - FX0) / FCell) + 1, nz = Mathf.CeilToInt((FZ1 - FZ0) / FCell) + 1;
            var h = HeightField(nx, nz);
            h = Tent(h, nx, nz, 2.6f, 10);
            h = Blur(Blur(h, nx, nz), nx, nz);
            float H(float x, float z)
            {
                float fx = Mathf.Clamp((x - FX0) / FCell, 0f, nx - 1.001f), fz = Mathf.Clamp((z - FZ0) / FCell, 0f, nz - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(h[iz * nx + ix], h[iz * nx + ix + 1], tx), Mathf.Lerp(h[(iz + 1) * nx + ix], h[(iz + 1) * nx + ix + 1], tx), tz);
            }
            // sheet rows along z; each row spans from the hanging left edge to a ragged right edge
            const int rows = 120, cols = 80;
            var P = new Vector3[rows + 1, cols + 1];
            for (int r = 0; r <= rows; r++)
            {
                float z = Mathf.Lerp(-0.45f, 1.97f + 0.04f * Mathf.Sin(r * 0.7f), r / (float)rows);
                float xr = Mathf.Lerp(0.32f + 0.06f * Mathf.Sin(z * 5f), -0.42f, WreckKit.Smooth(0.85f, 1.2f, z));
                float xl = Mathf.Lerp(-0.7f, -1.1f, WreckKit.Smooth(0.8f, 1.15f, z)) + 0.03f * Mathf.Sin(z * 9f);
                for (int c = 0; c <= cols; c++)
                {
                    float x = Mathf.Lerp(xl, xr, c / (float)cols);
                    var p = new Vector3(x, 0f, z);
                    float w = WreckKit.Noise2(p * 7f) * 0.03f + Mathf.Sin(x * 13f + z * 7f) * 0.008f + WreckKit.Noise2(p * 23f) * 0.006f;
                    p.y = Mathf.Max(H(x, z) + 0.014f + Mathf.Max(w, -0.004f), 0.006f);
                    // the part hanging down the side falls in loose vertical folds and spreads on the floor
                    float hang = WreckKit.Smooth(-0.8f, -1.0f, x) * WreckKit.Smooth(0.9f, 0.25f, p.y);
                    float fold = Mathf.Sin(z * 13f + WreckKit.Noise2(p * 3f) * 4f) * 0.5f + 0.5f;
                    p.x -= hang * (0.04f + 0.12f * fold);
                    p.z += hang * Mathf.Sin(z * 7f + 1.3f) * 0.03f;
                    P[r, c] = p;
                }
            }
            var foil = Lib["Wreck_Foil"];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                Vector3 a = P[r, c], b = P[r, c + 1], cc = P[r + 1, c + 1], d = P[r + 1, c];
                Vector3 N(int i, int k)
                {
                    Vector3 du = P[r + i, Mathf.Min(c + k + 1, cols)] - P[r + i, Mathf.Max(c + k - 1, 0)];
                    Vector3 dv = P[Mathf.Min(r + i + 1, rows), c + k] - P[Mathf.Max(r + i - 1, 0), c + k];
                    var n = Vector3.Cross(dv, du).normalized;
                    return n.y < 0f && Mathf.Abs(n.y) > 0.3f ? -n : n;
                }
                Vector3 na = N(0, 0), nb = N(0, 1), nc = N(1, 1), nd = N(1, 0);
                Mb.Quad(a, b, cc, d, na, nb, nc, nd, foil);
                const float o = 0.0008f;
                Mb.Quad(a - na * o, b - nb * o, cc - nc * o, d - nd * o, -na, -nb, -nc, -nd, foil);
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
