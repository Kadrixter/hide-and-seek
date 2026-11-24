using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>Timeline of the lobby fork-in-the-socket easter egg (seconds from the start).</summary>
    public static class ShockTimeline
    {
        public const float Insert = 0.9f;      // fork goes in -> short circuit
        public const float ShockEnd = 2.9f;    // 2 s standing, electrocuted
        public const float FallEnd = 3.35f;    // thrown on the back
        public const float TwitchEnd = 5.35f;  // 2 s twitching on the floor
        public const float Respawn = 11.35f;   // 6 s lying still, then respawn
    }

    public partial class BlockyCharacterActions
    {
        float shockT = -1f;
        Vector3 shockOutletLocal = new Vector3(0f, 0.3f, 0.75f);

        bool arcadeOn;
        float arcadeW;
        Vector3 arcadeStickLocal, arcadeButtonsLocal;
        Vector2 arcadeDir;
        float arcadeTap;

        /// <summary>Fork easter egg clock (t &lt; 0 = off). <paramref name="outletWorld"/> = the socket the fork goes into.</summary>
        public void SetShock(float t, Vector3 outletWorld)
        {
            EnsureInit();
            if (shockT >= 0f && t < 0f && hasLast)
            {
                // Respawn: quick blend from lying back to the normal stance.
                fadeFrom = lastOutput;
                fadeT = 0f;
            }
            shockT = t;
            if (t >= 0f) shockOutletLocal = transform.InverseTransformPoint(outletWorld);
        }

        public bool ShockActive => shockT >= 0f;

        /// <summary>Playing the arcade: left hand on the joystick ball (tilted by <paramref name="dir"/>), right hand on the buttons.</summary>
        public void SetArcade(bool on, Vector3 stickBallWorld, Vector3 buttonsWorld, Vector2 dir)
        {
            EnsureInit();
            arcadeOn = on;
            if (!on) return;
            arcadeStickLocal = transform.InverseTransformPoint(stickBallWorld);
            arcadeButtonsLocal = transform.InverseTransformPoint(buttonsWorld);
            if (dir != arcadeDir) arcadeTap = 1f;
            arcadeDir = dir;
        }

        bool ShockHidesItem => shockT >= ShockTimeline.Insert + 0.05f;

        static float J(float time, float seed) => 0.6f * Mathf.Sin(time * 61f + seed * 3.1f) + 0.4f * Mathf.Sin(time * 97f + seed * 7.7f);

        /// <summary>Arms on the arcade controls (applied on top of the standing pose).</summary>
        void ApplyArcade(ref CharacterPose p, float dt, bool standing)
        {
            arcadeW = Damp(arcadeW, arcadeOn && standing ? 1f : 0f, 8f, dt);
            arcadeTap = Mathf.MoveTowards(arcadeTap, 0f, dt * 6f);
            if (arcadeW < 0.001f) return;
            var ap = p;
            ap.Torso = p.Torso * Quaternion.Euler(10f, 0f, 0f);
            ap.Head = Quaternion.Euler(12f, 0f, 0f);
            // Stick ball moves with the tilt; the hand wraps it from above.
            Vector3 tilt = new Vector3(arcadeDir.x, 0f, arcadeDir.y) * 0.035f;
            Vector3 l = arcadeStickLocal + tilt + new Vector3(0f, 0.03f, -0.02f);
            Vector3 r = arcadeButtonsLocal + new Vector3(0f, 0.035f - 0.025f * Mathf.Sin(arcadeTap * Mathf.PI), -0.02f);
            ap.SolveArm(1, l, TorsoDir(ap, -1f, -0.6f, -0.4f));
            ap.SolveArm(0, r, TorsoDir(ap, 1f, -0.6f, -0.4f));
            CharacterPose.LerpArm(ref p, ap, 0, arcadeW);
            CharacterPose.LerpArm(ref p, ap, 1, arcadeW);
            p.Torso = Quaternion.Slerp(p.Torso, ap.Torso, arcadeW);
            p.Head = Quaternion.Slerp(p.Head, ap.Head, arcadeW);
        }

        /// <summary>Supine, head towards -Z (thrown back from the wall), pelvis pushed back.</summary>
        static CharacterPose LieBackPose()
        {
            var d = DeadPose();
            var rot = Quaternion.Euler(0f, 180f, 0f);
            d.PelvisPos = rot * d.PelvisPos + new Vector3(0f, 0f, -0.55f);
            d.Pelvis = rot * d.Pelvis;
            return d;
        }

        /// <summary>Standing rigid under current, everything shaking (fork shock, RC car zap).</summary>
        CharacterPose ElectrocutedPose()
        {
            var rigid = CharacterPose.Identity;
            rigid.PelvisPos = new Vector3(0.02f * J(time, 1f), BlockyCharacter.HipHeight - 0.05f + 0.015f * J(time, 2f), -0.05f);
            rigid.Pelvis = Quaternion.Euler(4f * J(time, 3f), 3f * J(time, 4f), 3f * J(time, 5f));
            rigid.Torso = Quaternion.Euler(-12f + 6f * J(time, 6f), 5f * J(time, 7f), 5f * J(time, 8f));
            rigid.Head = Quaternion.Euler(-28f + 10f * J(time, 9f), 14f * J(time, 10f), 8f * J(time, 11f));
            rigid.SolveLeg(0, new Vector3(0.16f + 0.02f * J(time, 12f), 0f, 0.02f), new Vector3(0.2f, 0f, 1f));
            rigid.SolveLeg(1, new Vector3(-0.16f + 0.02f * J(time, 13f), 0f, -0.02f), new Vector3(-0.2f, 0f, 1f));
            Vector3 jr = new Vector3(J(time, 14f), J(time, 15f), J(time, 16f)) * 0.07f;
            Vector3 jl = new Vector3(J(time, 17f), J(time, 18f), J(time, 19f)) * 0.07f;
            rigid.SolveArm(0, rigid.TorsoPoint(new Vector3(0.55f, 0.75f, 0.22f)) + jr, TorsoDir(rigid, 1f, -0.4f, -0.6f));
            rigid.SolveArm(1, rigid.TorsoPoint(new Vector3(-0.55f, 0.72f, 0.2f)) + jl, TorsoDir(rigid, -1f, -0.4f, -0.6f));
            return rigid;
        }

        CharacterPose ShockPose(in CharacterPose basePose, float t)
        {
            float S(float a, float b) => HealTimeline.Seg(t, a, b);
            Vector3 outlet = shockOutletLocal;
            Vector3 toOutlet = new Vector3(outlet.x, 0f, outlet.z).normalized;
            if (toOutlet.sqrMagnitude < 0.01f) toOutlet = Vector3.forward;
            float forkLen = BlockyProps.ForkTipLocal.z * BlockyProps.ForkScale;

            // ---- crouch down and push the fork in
            var kneel = KneelPose(0f, -0.05f);
            kneel.Torso = Quaternion.Euler(30f, 0f, 0f);
            kneel.Head = Quaternion.Euler(18f, 0f, 0f);
            Vector3 belly = kneel.TorsoPoint(new Vector3(0.12f, 0.3f, 0.3f));
            float reach = S(0.35f, 0.82f);
            Vector3 hand = Vector3.Lerp(belly, outlet - toOutlet * (forkLen + 0.02f) + Vector3.up * 0.02f, reach);
            hand += toOutlet * 0.03f * S(0.8f, Mathf.Max(0.81f, ShockTimeline.Insert)); // final push
            kneel.SolveArm(0, hand, TorsoDir(kneel, 1f, -0.7f, -0.3f));
            var forkRot = Quaternion.LookRotation(Vector3.Lerp(Vector3.forward + Vector3.down * 0.4f, toOutlet, reach), Vector3.up);
            kneel.PropR = Quaternion.Inverse(kneel.ForearmRoot(0)) * forkRot;
            if (t < ShockTimeline.Insert)
            {
                var r = CharacterPose.Lerp(basePose, kneel, S(0f, 0.4f));
                r.PropR = kneel.PropR;
                return r;
            }

            // ---- electrocuted: jolts up rigid, everything shaking
            var rigid = ElectrocutedPose();
            // Hand still stuck at the socket for the first moment.
            if (t < ShockTimeline.Insert + 0.25f)
            {
                var stuck = kneel;
                stuck.Torso = kneel.Torso * Quaternion.Euler(-10f * S(ShockTimeline.Insert, ShockTimeline.Insert + 0.2f) + 5f * J(time, 20f), 4f * J(time, 21f), 0f);
                var r = CharacterPose.Lerp(stuck, rigid, S(ShockTimeline.Insert + 0.05f, ShockTimeline.Insert + 0.25f));
                return r;
            }
            if (t < ShockTimeline.ShockEnd) return rigid;

            // ---- thrown on the back, twitching, then still
            var lie = LieBackPose();
            float tw = t < ShockTimeline.TwitchEnd ? Mathf.Pow(1f - ActionTimeline.Remap01(t, ShockTimeline.FallEnd, ShockTimeline.TwitchEnd), 0.7f) : 0f;
            if (tw > 0f)
            {
                lie.Torso = lie.Torso * Quaternion.Euler(9f * J(time, 22f) * tw, 4f * J(time, 23f) * tw, 7f * J(time, 24f) * tw);
                lie.Head = lie.Head * Quaternion.Euler(14f * J(time, 25f) * tw, 10f * J(time, 26f) * tw, 0f);
                lie.ShoulderR = lie.ShoulderR * Quaternion.Euler(25f * J(time, 27f) * tw, 0f, 18f * J(time, 28f) * tw);
                lie.ShoulderL = lie.ShoulderL * Quaternion.Euler(25f * J(time, 29f) * tw, 0f, 18f * J(time, 30f) * tw);
                lie.ElbowR = lie.ElbowR * Quaternion.Euler(30f * J(time, 31f) * tw, 0f, 0f);
                lie.ElbowL = lie.ElbowL * Quaternion.Euler(30f * J(time, 32f) * tw, 0f, 0f);
                lie.HipR = lie.HipR * Quaternion.Euler(14f * J(time, 33f) * tw, 0f, 0f);
                lie.HipL = lie.HipL * Quaternion.Euler(14f * J(time, 34f) * tw, 0f, 0f);
                lie.KneeR = lie.KneeR * Quaternion.Euler(18f * Mathf.Abs(J(time, 35f)) * tw, 0f, 0f);
                lie.KneeL = lie.KneeL * Quaternion.Euler(18f * Mathf.Abs(J(time, 36f)) * tw, 0f, 0f);
                lie.PelvisPos += Vector3.up * 0.02f * Mathf.Abs(J(time, 37f)) * tw;
            }
            if (t < ShockTimeline.FallEnd)
            {
                float f = ActionTimeline.Remap01(t, ShockTimeline.ShockEnd, ShockTimeline.FallEnd);
                return CharacterPose.Lerp(rigid, lie, f * f);
            }
            return lie;
        }
    }
}
