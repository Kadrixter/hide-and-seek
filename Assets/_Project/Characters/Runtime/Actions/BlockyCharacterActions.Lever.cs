using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared clock of the big double start lever in the lobby (seconds since the pull began), used by the lever
    /// model, the third-person body, the first-person hands and camera, and the sounds:
    /// reach up and grab the red bar (0-0.5), hang the body weight on it while it sticks (0.5-0.95), drag it down with
    /// the whole body into a squat (0.95-1.8), the blades slam into the lower jaws (1.8), hold, let go (2.45) and
    /// stand up (3.2). The server starts the round at <see cref="StartMatch"/>.
    /// </summary>
    public static class LeverTimeline
    {
        public const float Grip = 0.5f, Unstick = 0.95f, Latch = 1.8f, Release = 2.45f, End = 3.2f;
        public const float StartMatch = 3.5f;
        /// <summary>Feet of the puller, metres in front of the wall plate.</summary>
        public const float StandDistance = 1.1f;

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>How far the lever is thrown: 0 = up (waiting), 1 = down (start). Heavy start, slams at the end.</summary>
        public static float Throw(float t)
        {
            if (t <= 0f) return 0f;
            if (t < Unstick) return 0.035f * S(t, Grip + 0.08f, Unstick - 0.05f);
            if (t < Latch)
            {
                float k = (t - Unstick) / (Latch - Unstick);
                return Mathf.Lerp(0.035f, 1f, k * k * (2.2f - 1.2f * k));
            }
            // Rebounds a little off the stop, then settles in the jaws.
            float a = t - Latch;
            return 1f - 0.045f * Mathf.Exp(-a * 9f) * Mathf.Abs(Mathf.Sin(a * 26f));
        }

        /// <summary>Body involvement 0..1 (blends the whole-body pose in and out).</summary>
        public static float Body(float t) => S(t, 0f, 0.35f) * (1f - S(t, Release + 0.1f, End));

        /// <summary>Hands on the bar 0..1.</summary>
        public static float Hands(float t) => S(t, 0.05f, Grip) * (1f - S(t, Release, Release + 0.35f));
    }

    public partial class BlockyCharacterActions
    {
        float leverT = -1f, leverW;
        Vector3 leverGripR, leverGripL;

        /// <summary>
        /// Drives the lever pull. <paramref name="t"/> &lt; 0 ends it. Grips are the two hand points on the red bar
        /// (world space, right hand first), taken from the actual lever every frame.
        /// </summary>
        public void SetLever(float t, Vector3 gripRightWorld, Vector3 gripLeftWorld)
        {
            EnsureInit();
            leverT = t;
            if (t < 0f) return;
            leverGripR = transform.InverseTransformPoint(gripRightWorld);
            leverGripL = transform.InverseTransformPoint(gripLeftWorld);
        }

        public bool PullingLever => leverT >= 0f;

        static Vector3 LeverBez(Vector3 a, Vector3 mid, Vector3 b, float u)
        {
            Vector3 c = 2f * mid - 0.5f * (a + b);
            return (1f - u) * (1f - u) * a + 2f * u * (1f - u) * c + u * u * b;
        }

        static float LeverBez(float a, float mid, float b, float u)
        {
            float c = 2f * mid - 0.5f * (a + b);
            return (1f - u) * (1f - u) * a + 2f * u * (1f - u) * c + u * u * b;
        }

        CharacterPose LeverPose(in CharacterPose current, float t)
        {
            float th = Mathf.Clamp01(LeverTimeline.Throw(t));
            float hands = LeverTimeline.Hands(t);
            Vector3 bar = 0.5f * (leverGripR + leverGripL);

            // Weight goes on before the bar gives (sinks and trembles), a jolt when it unsticks.
            float strain = LeverTimeline.S(t, 0.55f, 0.9f) * (1f - LeverTimeline.S(t, LeverTimeline.Unstick, 1.2f));
            float shake = (Mathf.Sin(t * 41f) * 0.6f + Mathf.Sin(t * 27f) * 0.4f) * strain;
            float latchHit = t > LeverTimeline.Latch ? Mathf.Exp(-(t - LeverTimeline.Latch) * 7f) : 0f;

            // Shoulders relative to the bar: below-behind it at the top (arms up), behind it in the middle (leaning
            // back, arms forward), above-behind it at the bottom (deep squat, arms down).
            Vector3 shOff = LeverBez(new Vector3(0f, -0.45f, -0.2f), new Vector3(0f, 0.0f, -0.42f), new Vector3(0f, 0.42f, -0.2f), th);
            float pitch = LeverBez(22f, -10f, 25f, th) + 6f * strain + 5f * latchHit;
            Vector3 shoulders = bar + shOff + new Vector3(0f, -0.05f * strain, 0f);

            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.Pelvis = Quaternion.Euler(0.35f * pitch, 0f, 0.8f * shake);
            p.Torso = Quaternion.Euler(0.65f * pitch + 0.8f * shake, 0f, 0.6f * shake);
            Vector3 pelvis = shoulders - p.TorsoRoot * new Vector3(0f, 10f * BlockyCharacter.PixelSize, 0f);
            pelvis.y = Mathf.Clamp(pelvis.y, 0.36f, 0.69f) - 0.03f * latchHit;
            p.PelvisPos = pelvis;

            // Head follows the bar.
            Vector3 neck = p.TorsoPoint(new Vector3(0f, 12f * BlockyCharacter.PixelSize, 0f));
            Vector3 look = bar - (neck + Vector3.up * 0.22f);
            float headDown = -Mathf.Atan2(look.y, Mathf.Max(0.05f, look.z)) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Clamp(headDown - pitch, -55f, 55f), 2f * shake, 0f);

            // Staggered stance, planted.
            p.SolveLeg(0, new Vector3(0.14f, 0f, 0.2f), new Vector3(0.15f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.15f, 0f, -0.14f), new Vector3(-0.15f, 0f, 1f));

            // Arms: from wherever they were up to the bar; elbows out and down.
            Vector3 hr = Vector3.Lerp(current.HandPos(0), leverGripR, hands);
            Vector3 hl = Vector3.Lerp(current.HandPos(1), leverGripL, hands);
            p.SolveArm(0, hr, TorsoDir(p, 1f, -0.7f, -0.4f));
            p.SolveArm(1, hl, TorsoDir(p, -1f, -0.7f, -0.4f));
            return p;
        }

        void ApplyLeverLayer(ref CharacterPose p, float dt)
        {
            bool on = leverT >= 0f && leverT < LeverTimeline.End;
            leverW = Damp(leverW, on ? 1f : 0f, 14f, dt);
            if (!on || leverW < 0.001f) return;
            var lp = LeverPose(p, leverT);
            var keep = p.PropR;
            p = CharacterPose.Lerp(p, lp, LeverTimeline.Body(leverT) * leverW);
            p.PropR = keep;
        }
    }
}
