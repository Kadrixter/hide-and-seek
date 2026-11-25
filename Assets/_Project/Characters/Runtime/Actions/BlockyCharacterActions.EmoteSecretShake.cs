using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Tajny uścisk dłoni": a fast secret handshake, the same code for both (own space, mirrored), so every contact
    /// is at pair-space x = 0 in the middle: a sneaky glance round, high five (right hands), low five, right elbows,
    /// both spin once, turn sideways and bump the right hips, turn back, fist bump that "explodes" (both hands fly
    /// out with wiggling fingers) and both end in the same pose: finger gun at the partner, the other hand on the hip.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Body yaw of the secret handshake (spin, sideways for the hips, back), own space degrees.</summary>
        static float SecretYaw(float t) =>
            -360f * Smoother(t, EmoteTimeline.SecretElbow + 0.22f, EmoteTimeline.SecretSpinEnd)
            - 90f * Seg(t, EmoteTimeline.SecretSpinEnd, 3.2f) + 90f * Seg(t, 3.58f, 3.85f);

        CharacterPose SecretShakePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float half = fr.Half;
            const float hi = EmoteTimeline.SecretHigh, lo = EmoteTimeline.SecretLow, el = EmoteTimeline.SecretElbow;
            const float hipT = EmoteTimeline.SecretHip, fist = EmoteTimeline.SecretFist, pose = EmoteTimeline.SecretPose;
            float yaw = SecretYaw(t);
            float glance = 35f * (Hump(t, 0.05f, 0.4f) - Hump(t, 0.35f, 0.72f));
            float highW = Env(t, 0.8f, hi, hi + 0.05f, lo - 0.05f);
            float elbowW = Env(t, lo + 0.1f, el - 0.08f, el + 0.05f, el + 0.25f);
            float hipIn = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 3.22f, hipT)) * (1f - Seg(t, hipT, hipT + 0.22f));
            float boom = Env(t, fist, fist + 0.18f, 4.5f, 4.75f);
            float endW = Seg(t, 4.55f, pose) * (1f - Seg(t, 6.25f, 6.75f));
            float wink = Hump(t, pose + 0.15f, pose + 0.55f);

            Vector3 shift = new Vector3(-0.1f * highW, -0.02f * Hump(t, lo - 0.25f, lo + 0.2f), 0.11f * elbowW + 0.25f * hipIn);
            float lean = 5f * Hump(t, 0.55f, 0.95f) - 3f * highW + 9f * Hump(t, lo - 0.2f, lo + 0.2f) + 4f * elbowW - 6f * boom - 4f * endW;
            float torsoYaw = -22f * highW - 50f * elbowW + 6f * endW;
            var p = OwnStance(b, fr, shift, yaw, lean, torsoYaw);
            p.Pelvis = p.Pelvis * Quaternion.Euler(0f, 0f, 14f * hipIn);
            float hipsUp = Env(t, EmoteTimeline.SecretSpinEnd, 3.15f, 3.6f, 3.85f);
            p.Torso = p.Torso * Quaternion.Euler(0f, 0f, 4f * highW + 10f * hipIn);
            p.Head = Quaternion.Euler(9f * Hump(t, 0.7f, 0.95f) - 10f * highW + 6f * Hump(t, lo - 0.2f, lo + 0.2f) + 10f * wink - 6f * boom, glance - 8f * endW + 20f * elbowW, 8f * endW + 8f * highW);

            // Feet: planted, stepping with the spin and the quarter turns.
            for (int leg = 0; leg < 2; leg++)
            {
                StepPhase(yaw, 45f, leg, out float a, out float lift);
                Vector3 local = leg == 0 ? new Vector3(0.14f, 0f, 0.01f) : new Vector3(-0.14f, 0f, -0.01f);
                Vector3 foot = Quaternion.Euler(0f, a, 0f) * local + Vector3.up * 0.05f * lift;
                Vector3 fwd = fr.OwnDir(Quaternion.Euler(0f, a, 0f) * Vector3.forward);
                Vector3 sideDir = Vector3.Cross(Vector3.up, fwd);
                p.SolveLeg(leg, fr.Own(foot), fwd + sideDir * (leg == 0 ? 0.12f : -0.12f));
            }
            Alive(ref p, t, 0.3f);

            // ---- right arm
            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 cocked = p.TorsoPoint(new Vector3(0.42f, 0.95f, 0.0f));
            Vector3 high = fr.Own(MeetPoint(fr, 1.55f));
            Vector3 low = fr.Own(MeetPoint(fr, 0.98f));
            Vector3 arc = p.TorsoPoint(new Vector3(0.45f, 0.62f, 0.25f));
            Vector3 tuck = p.TorsoPoint(new Vector3(0.2f, 0.42f, 0.24f));
            Vector3 jut = p.TorsoPoint(new Vector3(0.38f, 0.98f, -0.08f));
            Vector3 fistAt = fr.Own(MeetPoint(fr, 1.15f));
            Vector3 fistBack = p.TorsoPoint(new Vector3(0.3f, 0.52f, 0.16f));
            Vector3 blown = p.TorsoPoint(new Vector3(0.6f + 0.03f * Mathf.Sin(t * 47f), 0.98f + 0.03f * Mathf.Sin(t * 53f), 0.2f));
            Vector3 gun = fr.Own(new Vector3(0.12f, 1.28f, 0.3f));
            Vector3 hand;
            if (t < hi)
            {
                hand = Vector3.Lerp(rest, cocked, Seg(t, 0.55f, 0.95f));
                hand = Vector3.Lerp(hand, high, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 0.95f, hi)));
            }
            else if (t < lo)
            {
                float u = ActionTimeline.Remap01(t, hi, lo);
                hand = u < 0.5f ? Vector3.Lerp(high, arc, Seg(u, 0f, 0.5f)) : Vector3.Lerp(arc, low, ActionTimeline.EaseIn(Seg(u, 0.5f, 1f)));
            }
            else if (t < el + 0.25f)
            {
                hand = low;
                if (t > lo + 0.08f) hand = Vector3.Lerp(low, tuck, Seg(t, lo + 0.08f, lo + 0.3f));
            }
            else if (t < fist)
            {
                hand = Vector3.Lerp(tuck, jut, Env(t, EmoteTimeline.SecretSpinEnd, 3.2f, 3.6f, 3.8f));
                hand = Vector3.Lerp(hand, fistBack, Seg(t, 3.6f, 3.85f));
                hand = Vector3.Lerp(hand, fistAt, ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 3.85f, fist)));
            }
            else
            {
                hand = Vector3.Lerp(fistAt, blown, Seg(t, fist, fist + 0.2f));
                hand = Vector3.Lerp(hand, gun, Seg(t, 4.55f, pose - 0.15f));
                hand += Vector3.up * 0.04f * wink;
                hand = Vector3.Lerp(hand, rest, Seg(t, 6.25f, 6.75f));
            }
            if (elbowW > 0.001f)
            {
                // Elbow bump: elbow at the middle, forearm folded back up towards the own shoulder.
                var e = p;
                ElbowAt(ref e, 0, fr.Own(new Vector3(0f, 1.18f, half - 0.112f)), fr.OwnDir(new Vector3(0.2f, 0.95f, -0.2f)));
                p.SolveArm(0, Vector3.Lerp(hand, e.HandPos(0), elbowW), Vector3.Lerp(p.TorsoRoot * new Vector3(1f, -0.6f, -0.3f), e.ElbowPos(0) - p.ShoulderPos(0), elbowW));
            }
            else p.SolveArm(0, hand, p.TorsoRoot * new Vector3(1f, -0.6f, -0.3f));

            // ---- left arm: little fist on the high five, tucked in the spin, up for the hips, the explosion, on the hip.
            Vector3 l = p.TorsoPoint(RestL);
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.3f, 0.36f, 0.24f)), Env(t, 0.8f, 1.0f, 1.9f, 2.1f));
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.24f, 0.45f, 0.24f)), Env(t, 2.0f, 2.15f, 2.8f, 2.95f));
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.38f, 1.0f, 0.12f)), hipsUp);
            Vector3 lBlown = p.TorsoPoint(new Vector3(-0.6f - 0.03f * Mathf.Sin(t * 43f), 0.98f + 0.03f * Mathf.Sin(t * 57f), 0.2f));
            l = Vector3.Lerp(l, lBlown, boom);
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.3f, 0.1f, 0.01f)), endW);
            p.SolveArm(1, l, p.TorsoRoot * Vector3.Lerp(new Vector3(-1f, -0.6f, -0.3f), new Vector3(-1f, 0f, -0.45f), endW));
            return p;
        }
    }
}
