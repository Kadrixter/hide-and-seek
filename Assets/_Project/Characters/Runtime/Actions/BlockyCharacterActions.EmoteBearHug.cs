using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Niedźwiadek" (bear hug with a spin). Both open the arms and hug (heads cheek by cheek); the one who asked
    /// (role 0) holds the partner low round the waist, the partner (role 1) holds the shoulders. Role 0 dips, lifts the
    /// partner against the chest and turns three times (the partner's legs fly out with the speed, an arm goes up
    /// "wooo"), sets the partner down; the partner wobbles dizzy while role 0 laughs and points. Every contact is
    /// computed from <see cref="BearState"/> of the other body (exact on both peers), placement from the pair frame.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        CharacterPose BearHugPose(in CharacterPose b, float t)
        {
            var fr = Frame();
            bool lifter = fr.Lead;
            var me = BearState(lifter, t);
            var other = BearState(!lifter, t);
            var p = Stance(b);
            p.PelvisPos = fr.Own(me.Pos);
            p.Pelvis = fr.OwnRot * me.Rot;
            p.Torso = me.Torso;

            float hold = BearHold(t), lift = BearLiftW(t), speed = BearSpeed(t);
            float open = Env(t, 0f, 0.5f, 0.9f, 1.4f);
            float after = Env(t, EmoteTimeline.BearLet, EmoteTimeline.BearLet + 0.35f, 11.6f, 12.2f);
            float ang = BearAngle(t);
            Vector3 fwd = fr.OwnDir(Quaternion.Euler(0f, ang, 0f) * Vector3.forward);

            if (lifter)
            {
                // Laughing after the set-down: folded forward, shaking.
                float hh = Mathf.Sin(t * 2f * Mathf.PI * 6.3f) * after;
                p.Torso = p.Torso * Quaternion.Euler(-10f * after + 2.5f * hh, 0f, 0f);
                p.PelvisPos += Vector3.up * (-0.03f * after);
                p.Head = Quaternion.Euler(6f * hold - 6f * lift - 14f * after + 4f * hh, -10f * hold * (1f - after), 22f * hold * (1f - after));
                FeetAt(ref p, fr.Own(BearLifterFoot(0, t)), fr.Own(BearLifterFoot(1, t)), fwd);
                Alive(ref p, t, 0.4f * (1f - lift));
                float low = Seg(t, EmoteTimeline.BearDip, EmoteTimeline.BearLift);
                for (int arm = 0; arm < 2; arm++)
                {
                    float sx = arm == 0 ? 1f : -1f;
                    Vector3 hand = p.TorsoPoint(arm == 0 ? RestR : RestL);
                    hand = Vector3.Lerp(hand, p.TorsoPoint(new Vector3(0.56f * sx, 0.62f, 0.3f)), Seg(t, 0f, 0.5f));
                    // Round the partner's waist, lower (under the bottom) for the lift.
                    // Hands on the partner's flanks, round towards the back (each body stands a little to its left, cheek by cheek).
                    Vector3 waist = Vector3.Lerp(new Vector3(-0.33f * sx, 0.2f, -0.1f), new Vector3(-0.3f * sx, 0.12f, -0.16f), low);
                    Vector3 back = fr.Other(other.Point(waist));
                    if (arm == 0) back += (fr.Other(other.Point(new Vector3(0f, 0f, -0.3f))) - fr.Other(other.Point(Vector3.zero))) * (0.4f * PatLift(t, Pats2));
                    hand = Vector3.Lerp(hand, back, Seg(t, 0.85f, 1.4f) * (1f - Seg(t, EmoteTimeline.BearLet, EmoteTimeline.BearLet + 0.4f)));
                    // Hands on the belly while laughing; the right one points at the dizzy partner for a moment.
                    Vector3 belly = p.TorsoPoint(new Vector3(0.1f * sx, 0.28f, 0.22f));
                    if (arm == 0) belly = Vector3.Lerp(belly, p.TorsoPoint(new Vector3(0.16f, 0.62f, 0.5f)), Env(t, 10.5f, 10.75f, 11.25f, 11.5f));
                    hand = Vector3.Lerp(hand, belly, after);
                    Vector3 pole = Vector3.Lerp(new Vector3(sx, -0.6f, -0.1f), new Vector3(sx, -0.6f, -0.4f), after);
                    p.SolveArm(arm, hand, p.TorsoRoot * pole);
                }
                return p;
            }

            // ---- the lifted one
            float dizzy = after;
            float ph = (t - EmoteTimeline.BearLet) * 2f * Mathf.PI * 0.75f;
            p.PelvisPos += fr.OwnDir(new Vector3(0.045f * Mathf.Cos(ph), 0f, 0.045f * Mathf.Sin(ph))) * dizzy;
            p.Torso = p.Torso * Quaternion.Euler(4f * Mathf.Sin(ph + 0.7f) * dizzy, 0f, 7f * Mathf.Sin(ph) * dizzy);
            float woo = Env(t, 5.2f, 5.6f, 7.0f, 7.5f);
            p.Head = Quaternion.Euler(6f * hold - 14f * woo + 8f * Mathf.Sin(ph * 1.3f) * dizzy, -10f * hold * (1f - dizzy) * (1f - lift) + 12f * Mathf.Sin(ph * 0.7f) * dizzy,
                22f * hold * (1f - dizzy) * (1f - lift) + 16f * Mathf.Sin(ph + 1.2f) * dizzy);

            // Legs: planted, dangling once up, flying out behind with the speed (alternate kicks), a stagger step later.
            Vector3 stepR = new Vector3(0.09f * Seg(t, 10.55f, 10.85f), 0.06f * Hump(t, 10.55f, 10.85f), 0f);
            Vector3 stepL = new Vector3(-0.07f * Seg(t, 11.05f, 11.35f), 0.05f * Hump(t, 11.05f, 11.35f), 0f);
            float up = Seg(lift, 0.15f, 0.7f);
            float fly = 16f + 60f * speed * speed;
            for (int leg = 0; leg < 2; leg++)
            {
                Vector3 planted = fr.Own((leg == 0 ? FootR + stepR : FootL + stepL));
                Vector3 hipOwn = me.Pos + me.Rot * (leg == 0 ? BlockyCharacter.HipRightRest : BlockyCharacter.HipLeftRest);
                float kick = 12f * Mathf.Sin(t * 8f + leg * Mathf.PI) * Mathf.Clamp01(speed * 1.5f);
                float splay = (leg == 0 ? 1f : -1f) * (6f + 8f * speed);
                Vector3 dir = me.Rot * (Quaternion.Euler(fly + kick, 0f, splay) * Vector3.down);
                Vector3 dangle = fr.Own(hipOwn + dir * 0.63f);
                Vector3 sole = Vector3.Lerp(planted, dangle, up);
                p.SolveLeg(leg, sole, fr.OwnDir(me.Rot * Vector3.forward));
            }
            Alive(ref p, t, 0.4f * (1f - lift));

            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 hand = p.TorsoPoint(arm == 0 ? RestR : RestL);
                hand = Vector3.Lerp(hand, p.TorsoPoint(new Vector3(0.56f * sx, 0.66f, 0.3f)), Seg(t, 0f, 0.5f));
                // Over the shoulders, onto the upper back; on the shoulders when lifted.
                Vector3 back = fr.Other(other.Point(new Vector3(-0.33f * sx, 0.45f, -0.1f)));
                // Lifted: hands on the front of the shoulders (the arms stay clear of the head).
                Vector3 front = fr.Other(other.Point(new Vector3(-0.3f * sx, 0.58f, 0.24f)));
                back = Vector3.Lerp(back, front, up);
                hand = Vector3.Lerp(hand, back, Seg(t, 0.85f, 1.4f) * (1f - Seg(t, EmoteTimeline.BearLet - 0.1f, EmoteTimeline.BearLet + 0.3f)));
                {
                    Vector3 wave = p.TorsoPoint(new Vector3((0.4f + 0.06f * Mathf.Sin(t * 13f + sx)) * sx, 1.02f, 0.1f + 0.05f * Mathf.Cos(t * 13f)));
                    hand = Vector3.Lerp(hand, wave, woo);
                }
                // Dizzy: arms out for balance, circling.
                Vector3 balance = p.TorsoPoint(new Vector3(0.6f * sx, 0.5f + 0.08f * Mathf.Sin(ph * 2f + sx), 0.12f + 0.08f * Mathf.Cos(ph * 2f)));
                hand = Vector3.Lerp(hand, balance, dizzy);
                Vector3 pole = Vector3.Lerp(new Vector3(sx, 0.25f, -0.2f), new Vector3(sx, -0.4f, -0.5f), Mathf.Max(woo, dizzy));
                p.SolveArm(arm, hand, p.TorsoRoot * pole);
            }
            return p;
        }

        static readonly float[] Pats2 = { 1.95f, 2.3f };
    }
}
