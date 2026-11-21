using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Monotone cubic interpolation (Fritsch-Carlson) through key values: smooth like Catmull-Rom but never
    /// overshoots between keys, so a car profile drawn with a few keys stays exactly inside them.
    /// </summary>
    internal static class SmoothCurve
    {
        /// <summary>Value at <paramref name="x"/> of the curve through (xs[i], ys[i]); xs ascending.</summary>
        public static float Eval(float[] xs, float[] ys, float x)
        {
            int n = xs.Length;
            if (n == 1 || x <= xs[0]) return ys[0];
            if (x >= xs[n - 1]) return ys[n - 1];
            int k = 0;
            while (k < n - 2 && x > xs[k + 1]) k++;
            float h = xs[k + 1] - xs[k];
            if (h < 1e-6f) return ys[k + 1];
            float t = (x - xs[k]) / h;
            float m0 = Tangent(xs, ys, k), m1 = Tangent(xs, ys, k + 1);
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * ys[k] + (t3 - 2 * t2 + t) * h * m0 + (-2 * t3 + 3 * t2) * ys[k + 1] + (t3 - t2) * h * m1;
        }

        private static float Slope(float[] xs, float[] ys, int i)
        {
            float h = xs[i + 1] - xs[i];
            return h < 1e-6f ? 0f : (ys[i + 1] - ys[i]) / h;
        }

        private static float Tangent(float[] xs, float[] ys, int i)
        {
            int n = xs.Length;
            if (i == 0) return Slope(xs, ys, 0) * 0.5f;
            if (i == n - 1) return Slope(xs, ys, n - 2) * 0.5f;
            float d0 = Slope(xs, ys, i - 1), d1 = Slope(xs, ys, i);
            if (d0 * d1 <= 0f) return 0f;
            // harmonic mean keeps the curve monotone between keys
            float h0 = xs[i] - xs[i - 1], h1 = xs[i + 1] - xs[i];
            float w1 = 2 * h1 + h0, w2 = h1 + 2 * h0;
            return (w1 + w2) / (w1 / d0 + w2 / d1);
        }
    }
}
