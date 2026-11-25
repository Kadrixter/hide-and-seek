using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Hug: the one who asked (role 0) puts the arms over the partner's shoulders, the other one (role 1) round the
    /// waist, so the arms never cross; heads pass cheek by cheek (each to its own left).
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
    }
}
