using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Thread-safe, tileable noise for the generated asphalt maps (<see cref="AsphaltMaps"/>): integer hash, periodic
    /// value noise and fractal sums of it (the period is in cells per tile, so every map wraps seamlessly), and a
    /// periodic cellular (Worley) distance pair for the crack network. Plain math only (no UnityEngine.Random,
    /// no Mathf.PerlinNoise): it runs on a worker thread.
    /// </summary>
    public static class AsphaltNoise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static int Wrap(int i, int p) { int r = i % p; return r < 0 ? r + p : r; }

        /// <summary>Value noise 0..1 at (u, v) in tile units (0..1), <paramref name="period"/> cells across, wrapping.</summary>
        public static float Value(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = (int)System.Math.Floor(x), y0 = (int)System.Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period), ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return (a + (b - a) * fx) * (1f - fy) + (c + (d - c) * fx) * fy;
        }

        /// <summary>Fractal sum (<paramref name="octaves"/> doublings from <paramref name="period"/>), about 0..1.</summary>
        public static float Fbm(float u, float v, int period, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u, v, period, seed + o * 131) * amp;
                norm += amp;
                amp *= gain;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Periodic Worley: distances (tile units) to the nearest and second nearest feature point.</summary>
        public static void Cells(float u, float v, int cells, int seed, out float f1, out float f2) => Cells(u, v, cells, seed, out f1, out f2, out _);

        /// <summary>As <see cref="Cells(float, float, int, int, out float, out float)"/>, plus a 0..1 id of the nearest cell.</summary>
        public static void Cells(float u, float v, int cells, int seed, out float f1, out float f2, out float id)
        {
            id = 0f;
            float x = u * cells, y = v * cells;
            int cx = (int)System.Math.Floor(x), cy = (int)System.Math.Floor(y);
            f1 = f2 = 1e9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int gx = cx + i, gy = cy + j;
                int hx = Wrap(gx, cells), hy = Wrap(gy, cells);
                float px = gx + Hash(hx, hy, seed), py = gy + Hash(hx, hy, seed + 7);
                float dx = px - x, dy = py - y;
                float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (d < f1) { f2 = f1; f1 = d; id = Hash(hx, hy, seed + 13); }
                else if (d < f2) f2 = d;
            }
            f1 /= cells;
            f2 /= cells;
        }

        public static float Smooth(float a, float b, float x)
        {
            float t = x <= a ? 0f : x >= b ? 1f : (x - a) / (b - a);
            return t * t * (3f - 2f * t);
        }
    }
}
