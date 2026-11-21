using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    public partial class BlockyCharacterActions
    {
        float limpTarget, limpW;
        float slashT = 99f;
        BlockyCharacter slashVictim;

        public const float SlashDuration = 0.55f;
        /// <summary>Time from PlaySlash at which the blade connects.</summary>
        public const float SlashHitTime = 0.2f;

        /// <summary>Injured after the first hit: limps on the right leg and holds the belly with the left hand.</summary>
        public void SetLimp(float amount)
        {
            EnsureInit();
            limpTarget = Mathf.Clamp01(amount);
            if (animator != null) animator.limp = limpTarget;
        }

        /// <summary>One quick knife slash (first hit that only wounds).</summary>
        public void PlaySlash(BlockyCharacter victim = null)
        {
            EnsureInit();
            slashT = 0f;
            slashVictim = victim;
        }

        void ApplyLimpAndSlash(ref CharacterPose p, float dt, bool standing, HeldItem show)
        {
            // Left hand pressed on the belly wound (only while standing and the left hand is free).
            limpW = Damp(limpW, limpTarget, 4f, dt);
            bool leftFree = show != HeldItem.Glock && show != HeldItem.Chainsaw && action == ActionKind.None;
            if (limpW > 0.001f && standing && leftFree)
            {
                var cp = p;
                float press = 0.012f * Mathf.Sin(time * 2.3f);
                cp.SolveArm(1, cp.TorsoPoint(new Vector3(0.04f, 0.18f + press, 0.17f)), TorsoDir(cp, -1f, -0.5f, -0.4f));
                cp.Torso = p.Torso * Quaternion.Euler(6f, 0f, 0f);
                CharacterPose.LerpArm(ref p, cp, 1, limpW);
                p.Torso = Quaternion.Slerp(p.Torso, cp.Torso, limpW);
            }

            // Quick slash with the knife hand.
            if (slashT < SlashDuration)
            {
                slashT += dt;
                float t = slashT;
                float wind = ActionTimeline.SmoothStep01(t / 0.12f);
                float thrust = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, 0.12f, SlashHitTime));
                float back = ActionTimeline.SmoothStep01(ActionTimeline.Remap01(t, 0.3f, SlashDuration));
                Vector3 cocked = p.TorsoPoint(new Vector3(0.28f, 0.5f, 0.05f));
                Vector3 hit = p.TorsoPoint(new Vector3(0.05f, 0.35f, 0.62f));
                if (slashVictim != null && slashVictim.Torso != null)
                {
                    Vector3 vl = transform.InverseTransformPoint(slashVictim.Torso.position + slashVictim.transform.up * 0.2f);
                    vl.z = Mathf.Clamp(vl.z - 0.12f, 0.35f, 0.75f);
                    vl.x = Mathf.Clamp(vl.x, -0.2f, 0.25f);
                    vl.y = Mathf.Clamp(vl.y, 0.7f, 1.3f);
                    hit = vl;
                }
                Vector3 target = Vector3.Lerp(cocked, hit, thrust);
                var sp = p;
                sp.SolveArm(0, target, TorsoDir(sp, 1f, -0.7f, -0.3f));
                CharacterPose.LerpArm(ref p, sp, 0, wind * (1f - back));
                p.Torso = p.Torso * Quaternion.Euler(8f * thrust * (1f - back), -12f * thrust * (1f - back), 0f);
            }
        }
    }
}
