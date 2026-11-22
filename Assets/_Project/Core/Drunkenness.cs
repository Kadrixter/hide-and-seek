using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// Shared model of being drunk from the lobby vending machine: the level (cans in the blood) wears off linearly,
    /// one can alone does nothing, the effect grows from the second can on. <see cref="Sample"/> is the common sway
    /// signal (soft Perlin noise, no sines) read by both the first-person camera and the third-person body, so with the
    /// same clock and seed the owner's view and the body other players see lean the same way at the same moment.
    /// </summary>
    public static class Drunkenness
    {
        /// <summary>One can wears off in this many seconds.</summary>
        public const float SecondsPerCan = 60f;
        /// <summary>Level cap (cans) - a bit over three minutes of sobering up at most.</summary>
        public const float MaxCans = 4.2f;
        /// <summary>Below this level nothing shows (a single can).</summary>
        public const float SoberCans = 0.9f;
        /// <summary>Full effect from this level on.</summary>
        public const float FullCans = 4.0f;

        /// <summary>Level <paramref name="since"/> seconds after it was <paramref name="cans"/>.</summary>
        public static float Level(float cans, double since)
        {
            if (since < 0d) since = 0d;
            return Mathf.Max(0f, cans - (float)since / SecondsPerCan);
        }

        /// <summary>0..1 strength of the effect for a level (0 for one can, ~0.35 for two drunk in a row, ~0.6 for three, 1 for four or five).</summary>
        public static float Intensity(float level) => Mathf.Pow(Mathf.Clamp01((level - SoberCans) / (FullCans - SoberCans)), 0.85f);

        /// <summary>One sample of the sway signal; every channel is -1..1 and changes smoothly.</summary>
        public struct Sway
        {
            /// <summary>Whole-body lean to the side (+ = right).</summary>
            public float Lean;
            /// <summary>Lean forward (+) / back (-).</summary>
            public float Pitch;
            /// <summary>Slow drift of the head / view to the right (+).</summary>
            public float Yaw;
            /// <summary>Knees giving way (+) / straightening (-).</summary>
            public float Sag;
            /// <summary>Uneven walking rhythm.</summary>
            public float Rhythm;
            /// <summary>Quicker, small wobble on top of the slow lean.</summary>
            public float Wobble;
        }

        /// <summary>
        /// Sway at time <paramref name="t"/> (seconds). Heavier drunk = slower, wider main swing.
        /// <paramref name="seed"/> separates players.
        /// </summary>
        public static Sway Sample(float t, float seed, float intensity)
        {
            float slow = Mathf.Lerp(1f, 0.78f, Mathf.Clamp01(intensity));
            Sway s;
            s.Lean = Fbm(t * 0.21f * slow, seed + 1.7f);
            s.Pitch = Fbm(t * 0.16f * slow, seed + 13.1f);
            s.Yaw = Fbm(t * 0.12f * slow, seed + 27.9f);
            s.Sag = Fbm(t * 0.27f, seed + 41.3f);
            s.Rhythm = Fbm(t * 0.85f, seed + 55.5f);
            s.Wobble = Fbm(t * 0.62f, seed + 71.2f);
            return s;
        }

        /// <summary>Two octaves of Perlin noise, softly saturated to -1..1.</summary>
        public static float Fbm(float x, float y)
        {
            float a = Mathf.PerlinNoise(x, y) - 0.5f;
            float b = Mathf.PerlinNoise(x * 2.07f + 5.3f, y * 1.31f + 3.7f) - 0.5f;
            float v = (a * 2f + b * 0.9f) * 1.9f;
            return v / Mathf.Sqrt(1f + v * v);
        }

        /// <summary>Stable pseudo-random value -1..1 for an integer (e.g. a step number).</summary>
        public static float Hash(int n, float seed)
        {
            unchecked
            {
                uint h = (uint)n * 747796405u + (uint)(seed * 1000f) * 2891336453u + 0x9E3779B9u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x800000 - 1f;
            }
        }

        /// <summary>Seed for a player id (keeps the noise coordinates small).</summary>
        public static float SeedFor(ulong id) => (id % 89ul) * 7.37f + 3.1f;

        /// <summary>Wrapped clock for the sway (server time keeps the owner and every viewer in step).</summary>
        public static float Clock(double serverTime) => (float)(serverTime % 7200d);
    }
}
