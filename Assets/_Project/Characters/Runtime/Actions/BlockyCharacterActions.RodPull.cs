using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared clock of pulling a metal rod out of a rubble heap (5 s; third person, first person, the rod, sounds,
    /// server): bend down and grab it with both hands (0-0.55), brace a foot on the rubble and lean back (0.55-0.9),
    /// five yanks - every one moves the rod out a bit, the rod wobbles and the rubble crumbles (<see cref="Jerks"/>),
    /// the last tug rips it out (<see cref="Rip"/>), one step back to keep the balance, then the rod goes over the
    /// shoulder behind the back (<see cref="StowStart"/>). An interrupted pull keeps the yanks already done
    /// (<see cref="Map"/> skips them on the next try).
    /// </summary>
    public static class RodPullTimeline
    {
        public const float Duration = 5f;
        public const float GripEnd = 0.55f, BraceEnd = 0.9f;
        public static readonly float[] Jerks = { 1.05f, 1.68f, 2.3f, 2.9f, 3.48f };
        /// <summary>Fraction of the buried length that is out after each yank.</summary>
        public static readonly float[] OutAfter = { 0.15f, 0.3f, 0.46f, 0.62f, 0.78f };
        public const float Rip = 4.1f, StowStart = 4.5f;
        /// <summary>Length of the rod still in the rubble before the first yank (m).</summary>
        public const float Buried = 0.24f;
        /// <summary>Feet of the puller this far (m) from where the rod enters the rubble, horizontally.</summary>
        public const float StandDistance = 0.78f;

        public static float S(float t, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));

        /// <summary>Length (s) of a pull that starts with <paramref name="done"/> yanks already done.</summary>
        public static float LengthFrom(int done) => Duration - Skip(done);

        /// <summary>Seconds of the timeline skipped when <paramref name="done"/> yanks are already done.</summary>
        public static float Skip(int done) => done <= 0 ? 0f : (done >= Jerks.Length ? Rip - 0.25f : Jerks[done]) - Jerks[0];

        /// <summary>Timeline time of a pull that started with <paramref name="done"/> yanks done and runs for <paramref name="t"/> s.</summary>
        public static float Map(float t, int done) => t < BraceEnd ? t : t + Skip(done);

        /// <summary>Yanks completed at timeline time <paramref name="tl"/>.</summary>
        public static int JerksDone(float tl)
        {
            int n = 0;
            foreach (float j in Jerks) if (tl >= j + 0.12f) n++;
            return n;
        }

        /// <summary>0..1 of the buried length out of the rubble (1 = free) at timeline time <paramref name="tl"/>.</summary>
        public static float Out(float tl, int startDone)
        {
            float o = startDone > 0 ? OutAfter[Mathf.Min(startDone, OutAfter.Length) - 1] : 0f;
            for (int k = 0; k < Jerks.Length; k++)
            {
                float prev = k > 0 ? OutAfter[k - 1] : 0f;
                float step = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Jerks[k] + 0.02f, Jerks[k] + 0.14f, tl));
                o = Mathf.Max(o, Mathf.Lerp(prev, OutAfter[k], step));
            }
            if (tl >= Rip) o = Mathf.Lerp(OutAfter[OutAfter.Length - 1], 1f, S(tl, Rip, Rip + 0.08f));
            return o;
        }

        /// <summary>Wobble of the rod (degrees) after each yank, decaying.</summary>
        public static float Wobble(float tl)
        {
            float w = 0f;
            foreach (float j in Jerks)
                if (tl > j) w += Mathf.Sin((tl - j) * 31f) * Mathf.Exp(-(tl - j) * 7f) * 7f;
            // trembling while it is held under strain
            if (tl > BraceEnd && tl < Rip) w += Mathf.Sin(tl * 53f) * 0.8f + Mathf.Sin(tl * 37f) * 0.6f;
            return w;
        }

        /// <summary>Yank pulse 0..1 (fast pull, slow give back).</summary>
        public static float Yank(float tl)
        {
            float y = 0f;
            foreach (float j in Jerks)
            {
                float a = tl - j;
                if (a > -0.08f && a < 0.5f) y = Mathf.Max(y, a < 0.06f ? Mathf.InverseLerp(-0.08f, 0.06f, a) : Mathf.Exp(-(a - 0.06f) * 6f));
            }
            return y;
        }
    }

    /// <summary>
    /// Pulling a rod out of rubble (third person): crouch and grab the rod with both hands, right foot braced up on
    /// the heap, the whole body yanks back five times, trembling under strain in between, the head follows the rod;
    /// the rip throws the body back a step, then the rod goes over the right shoulder. The rod points come from
    /// the game every frame (world space).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float rodPullT = -1f, rodPullW;
        Vector3 rodPullGripR, rodPullGripL, rodPullFoot;

        public bool PullingRod => rodPullT >= 0f;

        /// <summary>
        /// Drives the pull. <paramref name="tl"/> = timeline time (see <see cref="RodPullTimeline"/>), &lt; 0 ends it.
        /// Grips are on the rod (right hand first), the foot is the braced spot on the rubble; all world space.
        /// </summary>
        public void SetRodPull(float tl, Vector3 gripRightWorld, Vector3 gripLeftWorld, Vector3 footWorld)
        {
            EnsureInit();
            rodPullT = tl;
            if (tl < 0f) return;
            rodPullGripR = transform.InverseTransformPoint(gripRightWorld);
            rodPullGripL = transform.InverseTransformPoint(gripLeftWorld);
            rodPullFoot = transform.InverseTransformPoint(footWorld);
        }

        CharacterPose RodPullPose(in CharacterPose current, float t)
        {
            float S(float a, float b) => RodPullTimeline.S(t, a, b);
            float yank = RodPullTimeline.Yank(t);
            float strain = S(RodPullTimeline.BraceEnd - 0.2f, RodPullTimeline.BraceEnd) * (1f - S(RodPullTimeline.Rip, RodPullTimeline.Rip + 0.1f));
            float shake = (Mathf.Sin(t * 43f) * 0.6f + Mathf.Sin(t * 29f) * 0.4f) * strain;
            float rip = t > RodPullTimeline.Rip ? Mathf.Exp(-(t - RodPullTimeline.Rip) * 4.5f) * S(RodPullTimeline.Rip, RodPullTimeline.Rip + 0.06f) : 0f;
            float after = S(RodPullTimeline.Rip, RodPullTimeline.Rip + 0.5f);
            float breath = Mathf.Sin(t * 5.2f) * (1f - strain);

            Vector3 grip = 0.5f * (rodPullGripR + rodPullGripL);
            // Lean: forward to reach the rod, back on every yank, thrown back by the rip, upright at the end.
            float reach = S(0f, RodPullTimeline.GripEnd) * (1f - after);
            float pitch = 34f * reach - 9f * strain - 16f * yank - 22f * rip + 6f * after * (1f - S(RodPullTimeline.StowStart, RodPullTimeline.Duration));
            var p = CharacterPose.Identity;
            p.PropR = current.PropR;
            p.Pelvis = Quaternion.Euler(0.3f * pitch, 4f * shake * 0.3f, 0.8f * shake - 4f * rip);
            p.Torso = Quaternion.Euler(0.7f * pitch + 0.8f * shake + 1.5f * breath, -6f * strain, 0.6f * shake + 5f * rip);
            // Pelvis: squat, back on the yanks, a step back after the rip.
            float squat = Mathf.Lerp(BlockyCharacter.HipHeight - 0.02f, 0.5f, reach * 0.7f + strain * 0.3f) - 0.03f * yank + 0.015f * breath;
            float back = -0.04f * reach - 0.1f * strain - 0.1f * yank - 0.2f * rip - 0.06f * after * (1f - S(4.6f, RodPullTimeline.Duration));
            p.PelvisPos = new Vector3(0.02f * shake - 0.03f * rip, squat - 0.04f * rip, back);

            // Head on the rod (on the hands), thrown back a little on the yanks.
            Vector3 neck = p.TorsoPoint(new Vector3(0f, 12f * BlockyCharacter.PixelSize, 0f));
            Vector3 look = grip - (neck + Vector3.up * 0.22f);
            float headDown = -Mathf.Atan2(look.y, Mathf.Max(0.05f, look.z)) * Mathf.Rad2Deg;
            p.Head = Quaternion.Euler(Mathf.Clamp(headDown - pitch - 8f * yank, -50f, 60f), 3f * shake, 2f * rip);

            // Legs: left foot planted behind; right foot up on the rubble while pulling, back down for the stagger.
            float brace = S(RodPullTimeline.GripEnd, RodPullTimeline.BraceEnd) * (1f - S(RodPullTimeline.Rip, RodPullTimeline.Rip + 0.18f));
            Vector3 footFloor = new Vector3(0.14f, 0f, 0.12f);
            Vector3 footStep = new Vector3(0.16f, 0f, -0.18f - 0.06f * after);
            Vector3 lift = Vector3.up * 0.12f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(Mathf.InverseLerp(RodPullTimeline.Rip, RodPullTimeline.Rip + 0.3f, t)));
            Vector3 fr = Vector3.Lerp(footFloor, rodPullFoot, brace);
            if (t > RodPullTimeline.Rip) fr = Vector3.Lerp(fr, footStep, S(RodPullTimeline.Rip + 0.05f, RodPullTimeline.Rip + 0.35f)) + lift;
            p.SolveLeg(0, fr, new Vector3(0.2f, 0.2f, 1f));
            p.SolveLeg(1, new Vector3(-0.15f, 0f, -0.2f + 0.05f * reach), new Vector3(-0.15f, 0f, 1f));

            // Arms: both hands on the rod (from wherever they were), then the right swings it over the shoulder.
            float hands = S(0.08f, RodPullTimeline.GripEnd);
            Vector3 hr = Vector3.Lerp(current.HandPos(0), rodPullGripR, hands);
            Vector3 hl = Vector3.Lerp(current.HandPos(1), rodPullGripL, hands * (1f - S(RodPullTimeline.StowStart + 0.05f, RodPullTimeline.StowStart + 0.35f)));
            float elbowOut = 0.6f + 0.3f * strain;
            p.SolveArm(0, hr, TorsoDir(p, elbowOut, -0.8f, -0.3f));
            p.SolveArm(1, hl, TorsoDir(p, -elbowOut, -0.8f, -0.3f));
            return p;
        }

        void ApplyRodPullLayer(ref CharacterPose p, float dt)
        {
            bool on = rodPullT >= 0f && rodPullT < RodPullTimeline.Duration && VisualLifeState == LifeState.Alive;
            rodPullW = Damp(rodPullW, on ? 1f : 0f, 12f, dt);
            if (rodPullW < 0.001f) return;
            float t = on ? rodPullT : RodPullTimeline.Duration;
            var rp = RodPullPose(p, t);
            float body = RodPullTimeline.S(t, 0f, 0.3f) * (1f - RodPullTimeline.S(t, RodPullTimeline.Duration - 0.3f, RodPullTimeline.Duration));
            var keep = p.PropR;
            p = CharacterPose.Lerp(p, rp, body * rodPullW);
            p.PropR = keep;
        }
    }
}
