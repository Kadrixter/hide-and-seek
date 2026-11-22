using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Timings (s) of fitting the rod as a peg leg; shared by third person, first person and sound.</summary>
    public static class RodTimeline
    {
        public const float Duration = 3.6f;
        public const float SitEnd = 0.6f;
        /// <summary>The rod appears in the right hand (pulled from behind the back).</summary>
        public const float Show = 0.5f;
        public const float BringEnd = 1.05f;
        /// <summary>Two shoves of the rod onto the stump (sound marks).</summary>
        public const float Push1 = 1.5f, Push2 = 1.72f, InsertEnd = 1.76f;
        public const float TapeStart = 1.85f, TapeEnd = 2.75f;
        public const float StandStart = 2.85f;

        public static float Seg(float t, float a, float b) => ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, a, b));

        /// <summary>0..1 tape wrapped round the stump.</summary>
        public static float Tape(float t) => ActionTimeline.Remap01(t, TapeStart, TapeEnd);

        /// <summary>How far (m) the top of the rod still is from the stump.</summary>
        public static float Offset(float t) => Mathf.Lerp(Mathf.Lerp(0.24f, 0.13f, Seg(t, 1.38f, Push1)), 0f, Seg(t, 1.6f, Push2));
    }

    /// <summary>
    /// Fitting the rod: sit down on the floor, pull the rod from behind the back, line it up with the stump, shove it
    /// on twice (wincing), wrap tape round the stump and the rod, then push up from the floor and stand on it.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float rodAttachT = -1f;
        int rodAttachLeg;
        Vector3 rodAnimTop, rodAnimAxis = Vector3.down;
        float rodTapeAnim;

        /// <summary>True while the fitting animation plays.</summary>
        public bool RodAttaching => rodAttachT >= 0f;
        /// <summary>Seconds into the fitting animation (-1 when none).</summary>
        public float RodAttachTime => rodAttachT;
        /// <summary>Leg index (0 = right, 1 = left) being fitted.</summary>
        public int RodAttachLegIndex => rodAttachLeg;
        /// <summary>Rod top and axis (top to tip) in character root space during the animation.</summary>
        public Vector3 RodAnimTop => rodAnimTop;
        public Vector3 RodAnimAxis => rodAnimAxis;
        public float RodAnimTape => rodTapeAnim;

        /// <summary>Starts fitting the rod onto the stump of <paramref name="limb"/> (Limb.LegR / LegL).</summary>
        public void PlayRodAttach(int limb)
        {
            EnsureInit();
            EnsureRodRig();
            rodAttachLeg = limb == Limb.LegL ? 1 : 0;
            rodAttachT = 0f;
            rodTapeAnim = 0f;
        }

        /// <summary>Stops the animation without fitting the rod (hit, downed...).</summary>
        public void CancelRodAttach()
        {
            rodAttachT = -1f;
            rodLegsLocal &= rodLegsConfirmed;
        }

        void ApplyRodAttach(ref CharacterPose p, float dt)
        {
            if (VisualLifeState != LifeState.Alive || action != ActionKind.None || pathActive)
            {
                rodAttachT = -1f;
                return;
            }
            rodAttachT += dt;
            float t = rodAttachT;
            if (t >= RodTimeline.Duration)
            {
                rodAttachT = -1f;
                rodLegsLocal |= rodAttachLeg == 0 ? Limb.LegR : Limb.LegL;
                return;
            }
            float w = RodTimeline.Seg(t, 0f, 0.18f) * (1f - RodTimeline.Seg(t, RodTimeline.Duration - 0.3f, RodTimeline.Duration));
            var ap = RodAttachPose(p, t, rodAttachLeg);
            p = CharacterPose.Lerp(p, ap, w);
            p.PropR = ap.PropR;
        }

        CharacterPose RodAttachPose(in CharacterPose current, float t, int li)
        {
            float S(float a, float b) => RodTimeline.Seg(t, a, b);
            float s = li == 0 ? 1f : -1f, g = -s;
            int gi = 1 - li;

            // ---- body: stand -> squat on the good leg -> sit; at the end back up the same way.
            var sit = CharacterPose.Identity;
            sit.PelvisPos = new Vector3(g * 0.02f, 0.16f, -0.42f);
            sit.Torso = Quaternion.Euler(16f, 0f, 0f);
            sit.Head = Quaternion.Euler(34f, 0f, 0f);
            sit.SolveLeg(gi, new Vector3(g * 0.15f, 0f, 0.04f), new Vector3(g * 0.2f, 1f, 0.2f));

            var squat = CharacterPose.Identity;
            squat.PelvisPos = new Vector3(g * 0.04f, 0.4f, -0.16f);
            squat.Torso = Quaternion.Euler(30f, 0f, g * 4f);
            squat.Head = Quaternion.Euler(10f, 0f, 0f);
            squat.SolveLeg(gi, new Vector3(g * 0.13f, 0f, 0.06f), new Vector3(g * 0.15f, 0f, 1f));

            var stand = CharacterPose.Identity;
            stand.PelvisPos = new Vector3(g * 0.025f, BlockyCharacter.HipHeight - 0.01f, 0f);
            stand.Torso = Quaternion.Euler(5f, 0f, 0f);
            stand.SolveLeg(gi, new Vector3(g * 0.13f, 0f, 0f), new Vector3(g * 0.06f, 0f, 1f));

            CharacterPose q;
            float down = S(0f, RodTimeline.SitEnd);
            float up = S(RodTimeline.StandStart + 0.15f, RodTimeline.Duration - 0.2f);
            if (up > 0f) q = up < 0.5f ? CharacterPose.Lerp(sit, squat, ActionTimeline.SmoothStep01(up * 2f)) : CharacterPose.Lerp(squat, stand, ActionTimeline.SmoothStep01(up * 2f - 1f));
            else q = down < 0.5f ? CharacterPose.Lerp(current, squat, ActionTimeline.SmoothStep01(down * 2f)) : CharacterPose.Lerp(squat, sit, ActionTimeline.SmoothStep01(down * 2f - 1f));

            // Wince on each shove: torso jerks back, head thrown back.
            float jolt = Mathf.Exp(-Mathf.Pow((t - RodTimeline.Push1 - 0.03f) / 0.07f, 2f)) + 1.3f * Mathf.Exp(-Mathf.Pow((t - RodTimeline.Push2 - 0.03f) / 0.08f, 2f));
            float taping = t >= RodTimeline.TapeStart && t < RodTimeline.TapeEnd ? 1f : 0f;
            float loops = RodTimeline.Tape(t) * 2.5f;
            float sway = taping * Mathf.Sin(loops * Mathf.PI * 2f);
            q.Torso = q.Torso * Quaternion.Euler(-12f * jolt + 4f * Mathf.Abs(sway), 6f * s * sway, 0f);
            float lookDown = S(0.3f, 0.8f) * (1f - S(RodTimeline.StandStart + 0.3f, RodTimeline.Duration - 0.3f));
            q.Head = q.Head * Quaternion.Euler(-30f * jolt, -8f * s * lookDown, 0f);

            // ---- rod axis: forward along the floor while sitting, swinging down under the hip when standing up.
            Vector3 hip = q.HipPos(li);
            float vy = -Mathf.Clamp01((hip.y - 0.01f) / RodModel.Length);
            Vector3 aSit = new Vector3(s * 0.06f, vy, Mathf.Sqrt(Mathf.Max(0.01f, 1f - vy * vy - 0.0036f))).normalized;
            Vector3 aStand = new Vector3(s * 0.04f, -1f, 0.04f).normalized;
            Vector3 legAxis = up > 0f ? Vector3.Slerp(aSit, RodAxisTo(hip, hip + aStand, 0f), ActionTimeline.SmoothStep01(up)) : aSit;
            legAxis = RodAxisTo(hip, hip + legAxis, 0f);
            SetHipAxis(ref q, li, legAxis);

            // ---- the rod (top point + axis) and the hands on it.
            const float GripR = 0.36f, GripL = 0.12f;
            float off = RodTimeline.Offset(t);
            Vector3 top, axis;
            Vector3 floorR = new Vector3(0.3f, 0.05f, -0.62f), floorL = new Vector3(-0.3f, 0.05f, -0.62f);
            Vector3 r, l;
            if (t < RodTimeline.BringEnd)
            {
                // Right hand reaches behind the back, pulls the rod out and swings it round in front of the stump.
                Vector3 back = q.TorsoPoint(new Vector3(0.2f, 0.32f, -0.2f));
                Vector3 g0 = back;
                Vector3 a0 = new Vector3(0.15f, 0.7f, -1f).normalized;
                float e = S(RodTimeline.Show, RodTimeline.BringEnd);
                axis = Vector3.Slerp(a0, legAxis, e).normalized;
                Vector3 grip = Vector3.Lerp(g0, hip + legAxis * (off + GripR), e) + new Vector3(0f, 0.18f * Mathf.Sin(Mathf.PI * e), 0f);
                top = grip - axis * GripR;
                r = t < RodTimeline.Show ? Vector3.Lerp(floorR, back, S(0.15f, RodTimeline.Show)) : grip;
                l = Vector3.Lerp(floorL, top + axis * GripL, S(0.8f, RodTimeline.BringEnd));
            }
            else if (t < RodTimeline.TapeStart)
            {
                axis = legAxis;
                top = hip + axis * off;
                r = top + axis * GripR;
                l = top + axis * GripL;
            }
            else if (t < RodTimeline.StandStart)
            {
                axis = legAxis;
                top = hip;
                // Left hand steadies the rod, right hand wraps the tape round the stump (2.5 loops).
                l = top + axis * 0.3f;
                Vector3 u = Vector3.Cross(axis, Vector3.forward);
                if (u.sqrMagnitude < 1e-4f) u = Vector3.right;
                u.Normalize();
                Vector3 v = Vector3.Cross(u, axis).normalized;
                float ang = loops * Mathf.PI * 2f;
                Vector3 c = top + axis * 0.07f;
                r = c + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * 0.17f;
                r = Vector3.Lerp(top + axis * GripR, r, S(RodTimeline.TapeStart, RodTimeline.TapeStart + 0.15f));
            }
            else
            {
                axis = legAxis;
                top = hip;
                // Hands to the floor and push up, then free for balance.
                float push = S(RodTimeline.StandStart, RodTimeline.StandStart + 0.25f) * (1f - S(RodTimeline.StandStart + 0.5f, RodTimeline.Duration - 0.2f));
                r = Vector3.Lerp(q.TorsoPoint(new Vector3(0.3f, 0.1f, 0.15f)), new Vector3(0.32f, 0.04f, -0.25f), push);
                l = Vector3.Lerp(q.TorsoPoint(new Vector3(-0.3f, 0.1f, 0.15f)), new Vector3(-0.32f, 0.04f, -0.25f), push);
            }
            q.SolveArm(0, r, TorsoDir(q, 1f, -0.3f, -0.6f));
            q.SolveArm(1, l, TorsoDir(q, -1f, -0.3f, -0.6f));
            q.PropR = current.PropR;

            rodAnimTop = top;
            rodAnimAxis = axis;
            rodTapeAnim = RodTimeline.Tape(t);
            return q;
        }
    }
}
