using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared timings (s) of the fire extinguisher animations: taking it off the wall / the floor, pulling the safety
    /// pin, and the seeker wiping the foam off his face. Used by the third-person body, the first-person arms and the
    /// game (when the hook copy disappears, when the loose pin is thrown, when the weapon may come back).
    /// </summary>
    public static class ExtTimeline
    {
        /// <summary>Take: the hand reaches the handle at <see cref="Grab"/>, the extinguisher is in the hold pose at <see cref="Take"/>.</summary>
        public const float Grab = 0.4f, Take = 0.95f;
        /// <summary>Pin: the left hand is on the ring at <see cref="PinReach"/>, the pin leaves the valve at <see cref="PinOut"/>,
        /// it is flicked away at <see cref="PinThrow"/>, the hand is back on the nozzle at <see cref="PinDone"/>.</summary>
        public const float PinReach = 0.32f, PinOut = 0.58f, PinThrow = 0.74f, PinDone = 1.05f;
        /// <summary>Spraying is accepted this long after the unlock request (the pin animation runs first).</summary>
        public const float SprayAfterUnlock = PinDone - 0.1f;
        /// <summary>How far the pin slides out of the valve before it comes free (model space, along +X).</summary>
        public const float PinTravel = 0.07f;

        /// <summary>Foamed seeker: the hands reach the face after <see cref="WipeIn"/>, leave it in the last <see cref="WipeOut"/> s.</summary>
        public const float WipeIn = 0.42f, WipeOut = 0.55f;
        /// <summary>One wipe stroke (one hand); the hands alternate.</summary>
        public const float WipeStroke = 0.62f;
        /// <summary>Coughing fits (seconds into the effect) and their length.</summary>
        public static readonly float[] Coughs = { 1.55f, 3.05f, 4.25f };
        public const float CoughLength = 0.42f;
        /// <summary>Head shakes (seconds into the effect) and their length.</summary>
        public static readonly float[] Shakes = { 0.5f, 2.35f };
        public const float ShakeLength = 0.55f;

        public static float S(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0..1 bump that rises over <paramref name="rise"/> after <paramref name="start"/> and falls back by <paramref name="start"/> + <paramref name="len"/>.</summary>
        public static float Bump(float t, float start, float len, float rise = 0.12f) =>
            S(t, start, start + rise) * (1f - S(t, start + len - rise * 1.4f, start + len));

        /// <summary>Strength of the coughing fit at time <paramref name="e"/> into the effect (0..1), and its jerks.</summary>
        public static float Cough(float e, float duration, out float jerk)
        {
            jerk = 0f;
            float w = 0f;
            foreach (float c in Coughs)
            {
                if (c > duration - WipeOut - 0.2f) continue;
                float b = Bump(e, c, CoughLength, 0.08f);
                if (b <= w) continue;
                w = b;
                float k = Mathf.Clamp01((e - c) / CoughLength);
                jerk = Mathf.Max(0f, Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k * 0.4f);
            }
            return w;
        }

        /// <summary>Head shake (yaw degrees) at time <paramref name="e"/> into the effect (also at its end).</summary>
        public static float Shake(float e, float left)
        {
            float a = 0f;
            foreach (float s in Shakes)
                a += Bump(e, s, ShakeLength, 0.1f) * Mathf.Sin((e - s) * 26f) * 20f;
            a += Bump(-left, -0.95f, 0.6f, 0.1f) * Mathf.Sin(left * 28f) * 16f;
            return a;
        }
    }
}
