using UnityEngine;

namespace HideAndSeek.Core
{
    /// <summary>
    /// Shared model of being drunk from the lobby vending machine: the level (cans in the blood) holds for
    /// <see cref="AbsorbSeconds"/> after the last can, then wears off linearly; one can alone does nothing, the effect
    /// grows from the second can on, from <see cref="HeartCans"/> the drinker hears the own heart and at
    /// <see cref="CollapseCans"/> the body gives up (<see cref="OverdoseTimeline"/>). <see cref="Sample"/> is the common
    /// sway signal (soft Perlin noise, no sines) read by both the first-person camera and the third-person body, so with
    /// the same clock and seed the owner's view and the body other players see lean the same way at the same moment.
    /// </summary>
    public static class Drunkenness
    {
        /// <summary>One can wears off in this many seconds.</summary>
        public const float SecondsPerCan = 60f;
        /// <summary>
        /// The level holds this long after the last can before it starts wearing off (still being absorbed). One purchase
        /// takes ~9 s, so without it cans drunk in a row could never add up to <see cref="CollapseCans"/>.
        /// </summary>
        public const float AbsorbSeconds = 25f;
        /// <summary>Level cap (cans): room above <see cref="CollapseCans"/>.</summary>
        public const float MaxCans = 10f;
        /// <summary>Below this level nothing shows (a single can).</summary>
        public const float SoberCans = 0.9f;
        /// <summary>Full effect from this level on.</summary>
        public const float FullCans = 4.0f;
        /// <summary>From this level the drinker hears the own heart (only them), louder and faster up to <see cref="CollapseCans"/>.</summary>
        public const float HeartCans = 5f;
        /// <summary>At this level the body collapses (lobby only): falls, 15 s of seizures, gets up slowly.</summary>
        public const float CollapseCans = 8f;
        /// <summary>
        /// Level once back on the feet (counted from the end of the get-up): still very drunk with a fading heartbeat,
        /// but the next collapse takes three more cans (5.5 + 2 = 7.5 stays below <see cref="CollapseCans"/>).
        /// </summary>
        public const float AfterCollapseCans = 5.5f;

        /// <summary>Level <paramref name="since"/> seconds after it was <paramref name="cans"/> (the last can).</summary>
        public static float Level(float cans, double since)
        {
            since -= AbsorbSeconds;
            if (since < 0d) since = 0d;
            return Mathf.Max(0f, cans - (float)since / SecondsPerCan);
        }

        /// <summary>0..1 heartbeat strength for a level: 0 below <see cref="HeartCans"/>, 1 at <see cref="CollapseCans"/>.</summary>
        public static float Heart(float level) => Mathf.Clamp01((level - HeartCans) / (CollapseCans - HeartCans));

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

        /// <summary>
        /// Seizure signal at <paramref name="u"/> seconds into the fit (0..<see cref="OverdoseTimeline.SeizeDuration"/>).
        /// <paramref name="tonic"/>: 0..1 rigid phase at the start (stiff, fine tremor). Returns the clonic jerk 0..1:
        /// rhythmic whole-body jerks (sharp contraction, slower release) that start at ~4.5 per second and slow down
        /// to ~1.7, every jerk with its own strength (some barely there), the rhythm drifting on Perlin noise; they fade
        /// out over the last seconds. <paramref name="beat"/> = index of the current jerk (per-limb variation).
        /// Pure function of time and seed: every client shows the same jerk at the same moment.
        /// </summary>
        public static float Seizure(float u, float seed, out float tonic, out int beat)
        {
            const float d = OverdoseTimeline.SeizeDuration;
            tonic = 1f - Seg(u, 0.9f, 1.7f);
            float env = Seg(u, 0.7f, 1.5f) * (1f - Seg(u, d - 3.5f, d - 0.2f)) * (0.78f + 0.22f * Fbm(u * 0.31f, seed + 5.1f));
            float ph = 4.5f * u - 0.095f * u * u + 1.15f * Fbm(u * 0.33f, seed + 11.3f);
            beat = Mathf.FloorToInt(ph);
            float f = ph - beat;
            float amp = 0.62f + 0.38f * Hash(beat, seed);
            if (Hash(beat, seed + 3.3f) > 0.62f) amp *= 0.35f; // a weak one now and then
            float pulse = f < 0.11f ? Mathf.SmoothStep(0f, 1f, f / 0.11f) : Mathf.Exp(-(f - 0.11f) * 6.5f);
            return Mathf.Max(0f, pulse * amp * env);
        }

        /// <summary>Smooth 0..1 ramp of <paramref name="t"/> between <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static float Seg(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
    }

    /// <summary>
    /// Collapse after <see cref="Drunkenness.CollapseCans"/> (seconds from the server-time start, the same on every
    /// client): staggers, clutches the chest, the knees give, topples onto the side and rolls onto the back, 15 s of
    /// seizures, a moment of lying still, then slowly gets up (onto the side, hands and knees, one knee, hands on the
    /// knees, up) and is left swaying.
    /// </summary>
    public static class OverdoseTimeline
    {
        public const float Lurch = 0.45f;       // lurches to the side, arms out
        public const float StepBack = 0.95f;    // catch step back, the other way
        public const float Clutch = 1.5f;       // hand to the chest, knees going
        public const float Knees = 2.1f;        // down on the knees
        public const float Topple = 2.45f;      // onto the side
        public const float Thud = 2.4f;         // the body hits the floor
        public const float FallEnd = 2.75f;     // on the back
        public const float SeizeDuration = 15f;
        public const float SeizeStart = FallEnd;
        public const float SeizeEnd = SeizeStart + SeizeDuration;
        public const float GetUpStart = SeizeEnd + 0.7f;
        public const float RollSide = GetUpStart + 1.0f;
        public const float AllFours = GetUpStart + 2.3f;
        public const float HalfKneel = GetUpStart + 3.5f;
        public const float HandsOnKnees = GetUpStart + 4.6f;
        public const float End = GetUpStart + 5.8f;
    }
}
