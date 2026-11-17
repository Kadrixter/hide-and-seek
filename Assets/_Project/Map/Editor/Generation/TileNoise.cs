using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Seeded, tileable value noise. Coordinates are in texture space (0..1) multiplied by an integer
    /// period, and the lattice wraps at that period so generated textures repeat seamlessly.
    /// </summary>
    internal sealed class TileNoise
    {
        private readonly uint _seed;

        public TileNoise(int seed)
        {
            _seed = (uint)seed * 2654435761u + 0x9E3779B9u;
        }

        public float Hash01(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + _seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Value noise on a lattice that wraps every (px, py) cells.</summary>
        public float Value(float x, float y, int px, int py)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            int x0 = Mod(xi, px), x1 = Mod(xi + 1, px);
            int y0 = Mod(yi, py), y1 = Mod(yi + 1, py);
            float a = Hash01(x0, y0), b = Hash01(x1, y0), c = Hash01(x0, y1), d = Hash01(x1, y1);
            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Fractal sum in 0..1. u,v are texture coordinates (0..1); periods are cell counts across the texture.</summary>
        public float Fbm(float u, float v, int periodX, int periodY, int octaves, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int px = periodX, py = periodY;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Value(u * px + i * 17.13f, v * py + i * 7.71f, px, py);
                norm += amp;
                amp *= gain;
                px *= 2;
                py *= 2;
            }
            return sum / norm;
        }

        public float Fbm(float u, float v, int period, int octaves, float gain = 0.5f) => Fbm(u, v, period, period, octaves, gain);
    }
}
