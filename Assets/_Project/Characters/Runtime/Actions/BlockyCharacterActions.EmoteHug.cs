using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Hug and the dance in pairs. Hug: the one who asked (role 0) puts the arms over the partner's shoulders, the
    /// other one (role 1) round the waist, so the arms never cross; heads pass cheek by cheek (each to its own left).
    /// Dance: both hands held, step-touch sway, then the partner spins under the raised hands and the hands meet again.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        CharacterPose HugPose(in CharacterPose b, float t)
        {
            var fr = Frame();
            bool high = emoteRole == 0;
            float open = Seg(t, 0f, 0.55f) * (1f - Seg(t, 0.55f, 1.1f));
            float wrap = Seg(t, 0.55f, 1.1f) * (1f - Seg(t, 3.0f, 3.55f));
            float lean = 10f * Seg(t, 0.2f, 1.0f) * (1f - Seg(t, 3.0f, 3.8f)) + 4f * open;
            float sway = Mathf.Sin((t - 1.1f) * 2f * Mathf.PI * 0.55f) * Env(t, 1.1f, 1.5f, 2.7f, 3.1f);
            float side = high ? 1f : -1f; // same world direction for both (role 1 faces the other way)
            var p = PairStance(b, fr, lean);
            p.Pelvis = p.Pelvis * Quaternion.Euler(0f, 0f, 3.5f * sway * side);
            p.PelvisPos += new Vector3(0f, -0.012f * wrap, 0.04f * wrap);
            // Cheek by cheek: head turns and tilts to its own left, the chest follows a little.
            p.Torso = p.Torso * Quaternion.Euler(0f, 0f, 5f * wrap);
            p.Head = Quaternion.Euler(6f * wrap, -32f * wrap, 9f * wrap - 2f * sway * side);
            PairFeet(ref p, fr);
            Alive(ref p, t, 0.5f);

            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 rest = p.TorsoPoint(arm == 0 ? RestR : RestL);
                Vector3 wide = p.TorsoPoint(new Vector3(0.56f * sx, 0.62f, 0.3f));
                // My right hand goes to the partner's left side of the back (partner-space -x), and the other way round.
                Vector3 back = high
                    ? PartnerTorso(fr, new Vector3(-0.19f * sx, 0.58f, -0.15f))
                    : PartnerTorso(fr, new Vector3(-0.17f * sx, 0.25f, -0.15f));
                if (high && arm == 0) back += (PartnerTorso(fr, new Vector3(0f, 0f, 0.4f)) - PartnerTorso(fr, Vector3.zero)) * (0.2f * PatLift(t));
                Vector3 shoulderFront = high
                    ? PartnerTorso(fr, new Vector3(-0.33f * sx, 0.6f, 0.1f))
                    : PartnerTorso(fr, new Vector3(-0.36f * sx, 0.3f, 0.12f));
                Vector3 hand = rest;
                hand = Vector3.Lerp(hand, wide, Seg(t, 0f, 0.55f));
                hand = Vector3.Lerp(hand, back, Seg(t, 0.55f, 1.1f));
                hand = Vector3.Lerp(hand, shoulderFront, Seg(t, 3.0f, 3.4f));
                hand = Vector3.Lerp(hand, rest, Seg(t, 3.75f, 4.3f));
                Vector3 pole = high ? new Vector3(sx, 0.25f, -0.2f) : new Vector3(sx, -0.6f, -0.1f);
                p.SolveArm(arm, hand, p.TorsoRoot * pole);
            }
            return p;
        }

        /// <summary>Back pats: the hand lifts off the back and comes down on each pat.</summary>
        static float PatLift(float t)
        {
            float l = 0f;
            foreach (float pat in EmoteTimeline.HugPats) l = Mathf.Max(l, Hump(t, pat - 0.16f, pat));
            return l;
        }

        CharacterPose PairDancePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float tl = Mathf.Repeat(t, EmoteTimeline.PairDancePeriod);
            bool lead = emoteRole == 0;
            float spin = Seg(tl, EmoteTimeline.SpinStart + 0.05f, EmoteTimeline.SpinEnd - 0.05f);
            float raise = Env(tl, EmoteTimeline.SpinStart - 0.25f, EmoteTimeline.SpinStart + 0.05f, EmoteTimeline.SpinEnd - 0.05f, EmoteTimeline.SpinEnd + 0.25f);
            float freeB = Env(tl, EmoteTimeline.SpinStart - 0.15f, EmoteTimeline.SpinStart + 0.1f, EmoteTimeline.SpinEnd - 0.1f, EmoteTimeline.SpinEnd + 0.05f);
            float swayW = 1f - raise;

            // Step-touch sway, both bodies the same way in the world (role 1 faces the other way).
            float ph = t / 2f * 2f * Mathf.PI;
            float sw = Mathf.Sin(ph) * swayW;
            float bounce = (0.5f + 0.5f * Mathf.Cos(2f * ph)) * swayW;
            float dirSign = lead ? 1f : -1f;
            var p = PairStance(b, fr, 3f);
            p.PelvisPos += new Vector3(0.07f * sw * dirSign, -0.03f * bounce - 0.01f, 0f);
            p.Pelvis = p.Pelvis * Quaternion.Euler(0f, 0f, -4f * sw * dirSign);
            p.Torso = p.Torso * Quaternion.Euler(0f, 0f, 5f * sw * dirSign);
            p.Head = Quaternion.Euler(-4f + 4f * bounce, 0f, -6f * sw * dirSign);

            // The follower spins once round (feet turn with the body, small steps).
            var q = Quaternion.Euler(0f, fr.FaceErr, 0f);
            if (!lead && spin > 0f)
            {
                var spinRot = Quaternion.Euler(0f, 360f * spin, 0f);
                p.Pelvis = spinRot * p.Pelvis;
                q = spinRot * q;
            }
            float stepR = 0.05f * Hump(Mathf.Repeat(t, 2f), 0f, 0.5f), stepL = 0.05f * Hump(Mathf.Repeat(t, 2f), 1f, 1.5f);
            if (!lead) stepR = stepL = 0.05f * Mathf.Abs(Mathf.Sin(spin * Mathf.PI * 3f)) * raise;
            Feet(ref p, q * (FootR + new Vector3(0.04f + 0.08f * Mathf.Max(0f, sw * dirSign), stepR, 0f)),
                q * (FootL + new Vector3(-0.04f + 0.08f * Mathf.Min(0f, sw * dirSign), stepL, 0f)));
            Alive(ref p, t, 0.3f);

            // Hands: A = lead's right + partner's left, B = lead's left + partner's right.
            Vector3 swayOff = new Vector3(0.07f * Mathf.Sin(ph) * swayW, 0f, 0f);
            Vector3 a = AtContact(fr, Vector3.Lerp(new Vector3(0.27f, 1.0f, 0f) + swayOff, new Vector3(0.04f, 1.55f, 0f), raise));
            Vector3 bb = AtContact(fr, new Vector3(-0.27f, 1.0f, 0f) + swayOff);
            int armA = lead ? 0 : 1, armB = lead ? 1 : 0;
            float sa = armA == 0 ? 1f : -1f, sb = armB == 0 ? 1f : -1f;
            p.SolveArm(armA, a, p.TorsoRoot * new Vector3(sa, -0.6f * (1f - raise), -0.4f));
            Vector3 free = lead ? p.TorsoPoint(new Vector3(-0.3f, 0.1f, 0.02f)) : p.TorsoPoint(new Vector3(0.56f, 0.55f, 0.05f));
            p.SolveArm(armB, Vector3.Lerp(bb, free, freeB), p.TorsoRoot * new Vector3(sb, -0.5f, -0.4f));
            return p;
        }
    }
}
