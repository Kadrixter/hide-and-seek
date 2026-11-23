using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Knocked out by a thrown leg (<see cref="StunTimeline"/>): the head snaps back, a stumble back and to the side,
    /// the knees give, onto the side and the back, out cold (slow breathing, then the head rolls as he comes round),
    /// slowly up over the side, all fours, one knee and the hands on the knees, and a shake of the head at the end.
    /// Driven by server time (every client and late joiners show the same moment). Overrides every other layer.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float stunT = -1f, stunSeed;

        /// <summary><paramref name="t"/> = seconds since the hit (&lt; 0 = not stunned), <paramref name="seed"/> = sway noise seed.</summary>
        public void SetStun(float t, float seed)
        {
            EnsureInit();
            bool on = t >= 0f && t < StunTimeline.Duration;
            if (on != stunT >= 0f && hasLast)
            {
                fadeFrom = lastOutput;
                fadeT = 0f;
            }
            stunT = on ? t : -1f;
            stunSeed = seed;
        }

        public bool StunActive => stunT >= 0f;

        float StNoise(float f, float k) => Drunkenness.Fbm(time * f, stunSeed + k);

        CharacterPose StunPose(in CharacterPose current, float t)
        {
            float S(float a, float b) => StunTimeline.Seg(t, a, b);
            float In(float a, float b) => ActionTimeline.EaseIn(ActionTimeline.Remap01(t, a, b));
            float Out(float a, float b) => ActionTimeline.EaseOut(ActionTimeline.Remap01(t, a, b));

            CharacterPose p;
            if (t < StunTimeline.Flat)
            {
                if (t < StunTimeline.Snap) p = CharacterPose.Lerp(current, StHitPose(), Out(0f, StunTimeline.Snap));
                else if (t < StunTimeline.Reel) p = CharacterPose.Lerp(StHitPose(), StReelPose(), S(StunTimeline.Snap, StunTimeline.Reel));
                else if (t < StunTimeline.Sway) p = CharacterPose.Lerp(StReelPose(), StSwayPose(), S(StunTimeline.Reel, StunTimeline.Sway));
                else if (t < StunTimeline.Knees) p = CharacterPose.Lerp(StSwayPose(), StKneelPose(), In(StunTimeline.Sway, StunTimeline.Knees));
                else if (t < StunTimeline.Topple) p = CharacterPose.Lerp(StKneelPose(), OdSidePose(false), In(StunTimeline.Knees, StunTimeline.Topple));
                else p = CharacterPose.Lerp(OdSidePose(false), StLiePose(0f), S(StunTimeline.Topple, StunTimeline.Flat));
                // Still on the feet: the dazed body wobbles on top of the keys.
                float up = S(StunTimeline.Snap, StunTimeline.Reel) * (1f - S(StunTimeline.Sway, StunTimeline.Knees));
                if (up > 0f)
                {
                    p.Torso = p.Torso * Quaternion.Euler(4f * StNoise(1.4f, 1f) * up, 5f * StNoise(1f, 2f) * up, 6f * StNoise(1.2f, 3f) * up);
                    p.Head = p.Head * Quaternion.Euler(9f * StNoise(1.3f, 4f) * up, 10f * StNoise(0.9f, 5f) * up, 8f * StNoise(1.1f, 6f) * up);
                }
            }
            else if (t < StunTimeline.Stir) p = StLiePose(0f);
            else
            {
                float stir = S(StunTimeline.Stir, StunTimeline.RollSide);
                if (t < StunTimeline.RollSide) p = StLiePose(stir);
                else if (t < StunTimeline.AllFours) p = CharacterPose.Lerp(StLiePose(1f), OdSidePose(true), S(StunTimeline.RollSide, StunTimeline.AllFours));
                else if (t < StunTimeline.HalfKneel) p = CharacterPose.Lerp(OdSidePose(true), OdAllFoursPose(), S(StunTimeline.AllFours, StunTimeline.HalfKneel));
                else if (t < StunTimeline.Bent) p = CharacterPose.Lerp(OdAllFoursPose(), OdHalfKneelPose(), S(StunTimeline.HalfKneel, StunTimeline.Bent));
                else p = CharacterPose.Lerp(OdHalfKneelPose(), CharacterPose.Lerp(OdBentPose(), current, S(StunTimeline.Bent + 0.15f, StunTimeline.Duration)),
                    S(StunTimeline.Bent - 0.05f, StunTimeline.Bent + 0.2f));
                // Groggy on the way up.
                float sway = S(StunTimeline.Stir, StunTimeline.AllFours) * (1f - S(StunTimeline.Bent, StunTimeline.Duration));
                if (sway > 0f)
                {
                    p.Torso = p.Torso * Quaternion.Euler(4f * StNoise(0.9f, 8f) * sway, 3f * StNoise(0.6f, 9f) * sway, 4f * StNoise(0.8f, 10f) * sway);
                    p.Head = p.Head * Quaternion.Euler(7f * StNoise(0.7f, 11f) * sway, 8f * StNoise(0.5f, 12f) * sway, 5f * StNoise(0.9f, 13f) * sway);
                }
            }

            // Shaking it off: a few quick turns of the head, dying away (and the shoulders with it a little).
            float sh = ActionTimeline.Remap01(t, StunTimeline.ShakeStart, StunTimeline.Duration);
            if (sh > 0f && sh < 1f)
            {
                float env = Mathf.Sin(sh * Mathf.PI) * (1f - 0.4f * sh);
                float wave = Mathf.Sin(sh * Mathf.PI * 2f * 2.6f);
                p.Head = p.Head * Quaternion.Euler(-6f * env, 30f * wave * env, 8f * wave * env);
                p.Torso = p.Torso * Quaternion.Euler(0f, 5f * wave * env, 0f);
            }
            p.PropR = current.PropR;
            return p;
        }

        // ================================================================== falling

        /// <summary>Hit in the face: head thrown back, chest back, a catch step back, the hands fly up.</summary>
        static CharacterPose StHitPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, BlockyCharacter.HipHeight - 0.03f, -0.07f);
            p.Pelvis = Quaternion.Euler(-5f, 3f, 0f);
            p.Torso = Quaternion.Euler(-17f, 5f, 4f);
            p.Head = Quaternion.Euler(-36f, -9f, 7f);
            p.SolveLeg(0, new Vector3(0.12f, 0f, 0.06f), new Vector3(0.15f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.12f, 0f, -0.24f), new Vector3(-0.1f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.24f, 0.66f, 0.26f)), TorsoDir(p, 1f, -0.7f, -0.3f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.46f, 0.5f, 0.14f)), TorsoDir(p, -1f, -0.6f, -0.4f));
            return p;
        }

        /// <summary>Reels back a step, the head lolling forward, the arms hanging out.</summary>
        static CharacterPose StReelPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.02f, BlockyCharacter.HipHeight - 0.07f, -0.26f);
            p.Pelvis = Quaternion.Euler(-4f, -5f, 4f);
            p.Torso = Quaternion.Euler(-2f, -7f, -6f);
            p.Head = Quaternion.Euler(18f, 11f, -15f);
            p.SolveLeg(0, new Vector3(0.14f, 0f, -0.12f), new Vector3(0.15f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.17f, 0f, -0.42f), new Vector3(-0.2f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.5f, 0.14f, 0.1f)), TorsoDir(p, 1f, -0.4f, -0.6f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.52f, 0.18f, 0.02f)), TorsoDir(p, -1f, -0.4f, -0.6f));
            return p;
        }

        /// <summary>Sways over to the right on loose legs, the arms dangling, the head rolling.</summary>
        static CharacterPose StSwayPose()
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0.08f, BlockyCharacter.HipHeight - 0.1f, -0.22f);
            p.Pelvis = Quaternion.Euler(-1f, 6f, -8f);
            p.Torso = Quaternion.Euler(10f, 8f, -10f);
            p.Head = Quaternion.Euler(30f, -12f, 22f);
            p.SolveLeg(0, new Vector3(0.25f, 0f, -0.12f), new Vector3(0.3f, 0f, 1f));
            p.SolveLeg(1, new Vector3(-0.09f, 0f, -0.32f), new Vector3(0f, 0f, 1f));
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.36f, -0.02f, 0.12f)), TorsoDir(p, 1f, -0.2f, -0.6f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.34f, 0.02f, 0.1f)), TorsoDir(p, -1f, -0.2f, -0.6f));
            return p;
        }

        /// <summary>Dropped onto the knees, slumped forward and tipping over, the arms hanging, the head on the chest.</summary>
        static CharacterPose StKneelPose()
        {
            const float z = -0.3f;
            var p = KneelPose(1f, z);
            p.Pelvis = Quaternion.Euler(0f, 0f, 10f);
            p.Torso = Quaternion.Euler(40f, -6f, 12f);
            p.Head = Quaternion.Euler(44f, 12f, 20f);
            p.SolveArm(0, p.TorsoPoint(new Vector3(0.3f, -0.12f, 0.22f)), TorsoDir(p, 1f, 0f, -0.5f));
            p.SolveArm(1, p.TorsoPoint(new Vector3(-0.32f, -0.1f, 0.2f)), TorsoDir(p, -1f, 0f, -0.5f));
            return p;
        }

        /// <summary>
        /// Out cold on the back (head near the root, the legs behind it), limbs loose, the head fallen to one side,
        /// slow deep breaths. <paramref name="stir"/> 0..1: coming round - the head rolls up, an arm and a knee move.
        /// </summary>
        CharacterPose StLiePose(float stir)
        {
            float br = Mathf.Sin(breathPhase * 0.8f);
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, ProneY + 0.004f * br, OdLieZ);
            p.Pelvis = Supine * Quaternion.Euler(0f, 8f - 4f * stir, 0f);
            p.Torso = Quaternion.Euler(-2f + 2.5f * br + 8f * stir, 0f, 3f);
            p.Head = Quaternion.Euler(-6f + 1.5f * br + 22f * stir, 48f - 30f * stir + 6f * StNoise(0.6f, 20f) * stir, 4f * stir);
            Vector3 r = p.TorsoPoint(Vector3.Lerp(new Vector3(0.6f, 0.4f, 0f), new Vector3(0.44f, 0.2f, 0.18f), stir));
            Vector3 l = p.TorsoPoint(new Vector3(-0.56f, 0.12f, 0f));
            r.y = 0.11f + 0.12f * Mathf.Sin(stir * Mathf.PI);
            l.y = 0.11f;
            p.SolveArm(0, r, Vector3.down + p.TorsoRoot * Vector3.right);
            p.SolveArm(1, l, Vector3.down + p.TorsoRoot * Vector3.left);
            Vector3 sideR = p.Pelvis * Vector3.right;
            p.SolveLeg(0, p.HipPos(0) + sideR * 0.1f + Vector3.back * Mathf.Lerp(0.66f, 0.5f, stir) + Vector3.up * 0.12f * stir, Vector3.up + sideR * 0.3f);
            p.SolveLeg(1, p.HipPos(1) - sideR * 0.16f + Vector3.back * 0.54f, -sideR + Vector3.up * 0.4f);
            return p;
        }
    }
}
