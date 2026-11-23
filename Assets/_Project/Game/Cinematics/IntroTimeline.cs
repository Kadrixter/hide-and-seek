using UnityEngine;

namespace HideAndSeek.Game.Cinematics
{
    /// <summary>
    /// Shared clock of the round intro (seconds from the server start time <c>MatchManager.IntroAt</c>):
    /// A wide shot of the open gate and the killer by the electrical box, B close-up of the hand on the small lever
    /// (reach, grip, yank, spark), C the laugh from below, D the shutter slamming down in one second (shake, dust,
    /// sparks, lamps flicker), E the closed gate with a title, then the view goes back to the player.
    /// </summary>
    public static class IntroTimeline
    {
        public const float ShotB = 2.0f, ShotC = 3.55f, ShotD = 5.75f, ShotE = 7.15f;
        public const float TurnToBox = 1.35f;
        public const float Reach = 2.2f, Grip = 2.62f, PullStart = 2.78f, PullEnd = 3.02f, Release = 3.3f;
        public const float TurnToCamera = 3.45f;
        public const float LaughStart = 3.7f, LaughLength = 2.05f;
        public const float DropStart = 5.95f, DropTime = 1.0f;
        public const float Impact = DropStart + DropTime;
        public const float TitleIn = 7.25f, FadeOut = 7.85f, Length = 8.3f;
        /// <summary>The server starts the intro this long after the players were placed (teleports settle).</summary>
        public const float Lead = 2.0f; // the first frames after the map load hitch (security rooms, cameras)

        static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>Lever throw 0 (up) .. 1 (down): sticks, then snaps over the dead point with a small rebound.</summary>
        public static float Lever(float t)
        {
            if (t < PullStart) return 0f;
            if (t < PullEnd)
            {
                float k = (t - PullStart) / (PullEnd - PullStart);
                return k * k * (1.6f - 0.6f * k);
            }
            float a = t - PullEnd;
            return 1f - 0.06f * Mathf.Exp(-a * 14f) * Mathf.Abs(Mathf.Sin(a * 40f));
        }

        /// <summary>Shutter opening 1 (up) .. 0 (shut): a short release jerk, free fall accelerating, a bounce.</summary>
        public static float Shutter(float t)
        {
            if (t < DropStart) return 1f;
            float a = t - DropStart;
            if (a < DropTime)
            {
                float k = a / DropTime;
                // Stutters on the brake release, then falls faster and faster (weight).
                float stutter = 0.012f * Mathf.Sin(k * 30f) * (1f - S(k, 0f, 0.25f));
                return Mathf.Clamp01(1f - (0.18f * k + 0.82f * k * k * k) + stutter);
            }
            float b = a - DropTime;
            return 0.035f * Mathf.Exp(-b * 7f) * Mathf.Abs(Mathf.Sin(b * 22f));
        }

        /// <summary>Camera shake strength at <paramref name="t"/>.</summary>
        public static float Shake(float t)
        {
            float s = 0f;
            if (t > Impact) s += 1.0f * Mathf.Exp(-(t - Impact) * 3.2f);
            if (t > DropStart && t < Impact) s += 0.12f * S(t, DropStart, Impact);
            if (t > PullEnd && t < PullEnd + 0.4f) s += 0.12f * Mathf.Exp(-(t - PullEnd) * 12f);
            return s;
        }
    }
}
