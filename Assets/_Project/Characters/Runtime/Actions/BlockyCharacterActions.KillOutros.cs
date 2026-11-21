using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Kill outros (aftermath once the victim lies on the floor, see <see cref="KillOutro"/>). The victim lies with the
    /// head towards the seeker; every outro ends exactly in the downed / dead pose.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        // ================================================================== downed: reaches for the boot, stomped

        CharacterPose SeekerStompHand(in CharacterPose from, float t)
        {
            // Steps back off the body, looks down; the victim crawls after the boot and grabs it.
            float back = Seg(t, 0.0f, 0.45f) * (1f - Seg(t, 2.3f, 2.7f));
            float look = Seg(t, 0.2f, 0.7f) * (1f - Seg(t, 2.3f, 2.7f));
            float shake = Seg(t, 0.95f, 1.05f) * (1f - Seg(t, 1.25f, 1.35f));
            float lift = Seg(t, 1.22f, 1.42f) * (1f - Seg(t, 1.42f, 1.5f));
            float down = Seg(t, 1.42f, 1.5f) * (1f - Seg(t, 1.9f, 2.2f));
            float wipe = Seg(t, 1.75f, 1.95f) * (1f - Seg(t, 2.3f, 2.55f));
            float stroke = Seg(t, 1.95f, 2.25f);
            const float Back = 0.3f;

            var p = SeekerBody(from, 1f, 4f + 8f * look + 4f * down - 4f * wipe, -6f * down, 0.02f * down, 0.05f, Back * back);
            p.Head = p.Head * Quaternion.Euler(30f * look + 8f * down, 0f, 0f);

            // Feet: right steps back first, then the left; the left boot is shaken free; the right one stomps.
            float rb = Seg(t, 0.0f, 0.28f) * (1f - Seg(t, 2.42f, 2.7f)), lb = Seg(t, 0.15f, 0.45f) * (1f - Seg(t, 2.3f, 2.55f));
            Vector3 rStance = new Vector3(0.15f, 0.07f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.28f)), -0.15f - Back * rb);
            Vector3 lStance = new Vector3(-0.13f, 0.07f * Mathf.Sin(Mathf.PI * ActionTimeline.Remap01(t, 0.15f, 0.45f)), 0.17f - Back * lb);
            lStance += new Vector3(0f, 0.05f * Mathf.Abs(Mathf.Sin(t * 26f)) * shake, -0.06f * shake);
            Vector3 hand = VictimHand(0);
            Vector3 rUp = new Vector3(hand.x * 0.6f + 0.06f, 0.34f, hand.z - 0.08f);
            Vector3 rDown = new Vector3(hand.x, Mathf.Max(0.04f, hand.y + 0.05f), hand.z);
            Vector3 rs = Vector3.Lerp(Vector3.Lerp(rStance, rUp, lift), rDown, down);
            p.SolveLeg(1, lStance, new Vector3(-0.1f, 0f, 1f));
            p.SolveLeg(0, rs, new Vector3(0.1f, 0.3f, 1f));

            // Wipe the blade along the left forearm, then back to ready.
            Vector3 ready = p.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 lArm = p.TorsoPoint(new Vector3(-0.04f, 0.46f, 0.36f));
            Vector3 kFrom = p.TorsoPoint(new Vector3(-0.18f, 0.52f, 0.34f));
            Vector3 kTo = p.TorsoPoint(new Vector3(0.14f, 0.5f, 0.42f));
            Vector3 knife = Vector3.Lerp(ready, Vector3.Lerp(kFrom, kTo, stroke), wipe);
            Vector3 bladeDir = Vector3.Slerp(KnifeDir, p.TorsoRoot * new Vector3(-1f, 0.05f, 0.15f), wipe);
            KnifeHand(ref p, from, 1f, knife, bladeDir, TorsoDir(p, 1f, -0.6f, -0.6f));
            Vector3 lRest = p.TorsoPoint(new Vector3(-0.24f, 0.36f, 0.26f));
            p.SolveArm(1, Vector3.Lerp(lRest, lArm, wipe), TorsoDir(p, -1f, -0.4f, -0.6f));
            return p;
        }

        CharacterPose VictimStompHand(float t)
        {
            var p = DownedPose();
            float reach = Seg(t, 0.15f, 0.85f) * (1f - Seg(t, 1.0f, 1.3f));
            float hitch = Seg(t, 0.1f, 0.7f) * (1f - Seg(t, 1.9f, 2.6f));
            float floorHand = Seg(t, 1.0f, 1.3f) * (1f - Seg(t, 1.95f, 2.55f));
            float stomp = Pulse(t, 1.5f, 1.5f);
            float pinned = Seg(t, 1.48f, 1.55f) * (1f - Seg(t, 1.9f, 2.05f));

            p.PelvisPos += new Vector3(0f, 0f, 0.12f * hitch);
            p.Torso = p.Torso * Quaternion.Euler(-10f * reach + 14f * stomp - 3f * pinned, 0f, 0f);
            p.Head = p.Head * Quaternion.Euler(-22f * reach + 30f * stomp + 6f * pinned * Mathf.Sin(t * 13f), 0f, 0f);

            Vector3 idle = p.HandPos(0);
            Vector3 boot = KillerFoot(1) + new Vector3(0f, 0.07f, -0.02f);
            Vector3 sh = p.ShoulderPos(0);
            Vector3 toBoot = boot - sh;
            if (toBoot.magnitude > 0.47f) boot = sh + toBoot.normalized * 0.47f;
            Vector3 floor = new Vector3(0.2f, 0.09f, 0.32f + 0.12f * hitch);
            Vector3 target = Vector3.Lerp(idle, boot, reach);
            target = Vector3.Lerp(target, floor, floorHand);
            target += new Vector3(0f, 0.02f * Mathf.Sin(t * 31f), 0f) * pinned;
            p.SolveArm(0, target, new Vector3(1f, 1.2f, -0.4f));
            // The left hand claws at the floor while the right one is pinned.
            Vector3 l = p.HandPos(1) + new Vector3(-0.05f, 0f, 0.12f) * pinned;
            p.SolveArm(1, l, new Vector3(-1f, 0.6f, -0.2f));
            return p;
        }

        // ================================================================== dead: last spasms

        CharacterPose SeekerThroes(in CharacterPose from, float t)
        {
            var p = from;
            if (sawKill)
            {
                // Bar pointed down at the body, two throttle blips, then back to the hold and a step back.
                float aimDown = Seg(t, 0.1f, 0.5f) * (1f - Seg(t, 1.6f, 2.3f));
                float blip = Mathf.Max(Seg(t, 0.5f, 0.6f) * (1f - Seg(t, 0.7f, 0.9f)), Seg(t, 1.05f, 1.15f) * (1f - Seg(t, 1.25f, 1.5f)));
                float back = Seg(t, 1.7f, 2.4f);
                var b = SeekerBody(from, 1f, 10f * aimDown - 6f * blip, 0f, 0.03f * aimDown, 0.05f, 0.18f * back);
                b.Head = b.Head * Quaternion.Euler(26f * aimDown, 0f, 0f);
                Vector3 body = VictimTorso(new Vector3(0f, 0.3f, 0f)) + Vector3.up * 0.35f;
                Vector3 hold = b.TorsoPoint(new Vector3(0.17f, 0.22f, 0.24f));
                Vector3 fist = Vector3.Lerp(hold, b.TorsoPoint(new Vector3(0.18f, 0.32f + 0.08f * blip, 0.3f)), aimDown);
                Vector3 aim = Vector3.Lerp(fist + b.TorsoRoot * new Vector3(0f, 0.1f, 1f), body + Vector3.up * 0.2f * blip, aimDown);
                fist += SawJitter(t, 0.3f + 0.9f * blip);
                SolveSawHands(ref b, fist, AimRot(fist, aim));
                return CharacterPose.Lerp(from, b, Seg(t, 0f, 0.3f));
            }

            // Knife: flick the blood off the blade, step back.
            float flick = Seg(t, 0.4f, 0.55f) * (1f - Seg(t, 0.6f, 0.9f));
            float stepBack = Seg(t, 1.2f, 2.0f);
            var k = SeekerBody(from, 1f, 8f - 4f * stepBack, 0f, 0f, 0.05f, 0.2f * stepBack);
            Vector3 ready = k.TorsoPoint(new Vector3(0.24f, 0.32f, 0.3f));
            Vector3 flickPos = k.TorsoPoint(new Vector3(0.42f, 0.1f, 0.2f));
            KnifeHand(ref k, from, 1f, Vector3.Lerp(ready, flickPos, flick), Vector3.Slerp(KnifeDir, Vector3.down, flick), TorsoDir(k, 1f, -1f, -0.6f));
            return CharacterPose.Lerp(from, k, Seg(t, 0f, 0.3f));
        }

        CharacterPose VictimThroes(float t)
        {
            var p = DeadPose();
            int arm = character != null && (character.SeveredMask & Limb.ArmR) != 0 ? 1 : 0;
            bool noArm = character != null && (character.SeveredMask & Limb.Arms) == Limb.Arms;
            float side = arm == 0 ? 1f : -1f;
            float lift = Seg(t, 0.1f, 0.85f) * (1f - Seg(t, 1.0f, 1.22f));
            float heave = 1f - Seg(t, 1.4f, 2.4f);
            float sp1 = Pulse(t, 1.4f, 1f), sp2 = Pulse(t, 1.85f, 0.55f);

            p.Torso = p.Torso * Quaternion.Euler(3.5f * Mathf.Sin(t * 9f) * heave - 9f * sp1 - 5f * sp2, 0f, 0f);
            p.Head = p.Head * Quaternion.Euler(16f * lift - 10f * sp1, -30f * lift, 0f);
            p.HipL = p.HipL * Quaternion.Euler(-22f * sp1, 0f, 0f);
            p.KneeL = p.KneeL * Quaternion.Euler(26f * sp1, 0f, 0f);
            p.HipR = p.HipR * Quaternion.Euler(-14f * sp2, 0f, 0f);

            if (!noArm && lift > 0.001f)
            {
                // A trembling hand reaches up towards the seeker, then drops.
                Vector3 up = p.TorsoPoint(new Vector3(0.22f * side, 0.62f, 0.42f)) + Tremble(t, 0.02f);
                var a = p;
                a.SolveArm(arm, up, p.TorsoRoot * new Vector3(side, 0f, -0.6f));
                CharacterPose.LerpArm(ref p, a, arm, lift);
            }
            return p;
        }
    }
}
