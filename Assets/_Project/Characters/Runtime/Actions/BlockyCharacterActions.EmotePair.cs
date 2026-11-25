using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// High five with a proper build-up: a bouncy "come on" with the hand cocked, a big wind-up and a dip, both jump into
    /// the slap at the top of the jump (hands meet at the middle of the two actual roots), land, shake the stinging hand
    /// and finish with a double fist pump.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        /// <summary>Jump height of the high five's body at <paramref name="t"/> (both bodies the same).</summary>
        static float FiveJump(float t)
        {
            const float up = 1.22f, down = 1.64f, h = 0.13f;
            if (t <= up || t >= down) return 0f;
            float x = (t - up) / (down - up) * 2f - 1f;
            return h * (1f - x * x);
        }

        CharacterPose HighFivePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float hit = EmoteTimeline.FiveHit;
            float jump = FiveJump(t);
            // Knee bounces: two ready bounces, the dip before the jump, landing, the fist pumps.
            float knees = 0.035f * (Hump(t, 0.05f, 0.3f) + Hump(t, 0.32f, 0.57f)) + 0.075f * Hump(t, 0.95f, 1.3f)
                          + 0.06f * Hump(t, 1.6f, 1.9f) + 0.045f * (Hump(t, 2.68f, 2.98f) + Hump(t, 3.03f, 3.33f));
            float cock = Seg(t, 0.05f, 0.5f), wind = Env(t, 0.55f, 1.1f, 1.18f, 1.38f);
            float swing = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 1.18f, hit));
            float sting = Env(t, 1.75f, 1.9f, 2.4f, 2.6f), pump = Env(t, 2.55f, 2.7f, 3.3f, 3.55f);
            float yes = Hump(t, 2.68f, 2.98f) + Hump(t, 3.03f, 3.33f);
            float home = Seg(t, 3.45f, 4.1f);

            // Upright at the slap and a step of the body to the own left: the hands meet in front of the faces, not in them.
            float atHit = Env(t, 1.18f, hit, hit + 0.12f, 1.8f);
            float lean = -3f * cock * (1f - swing) - 6f * wind - 3f * atHit + 7f * yes + 3f * sting;
            var p = PairStance(b, fr, lean);
            p.Torso = p.Torso * Quaternion.Euler(0f, 25f * wind - 22f * atHit + 8f * sting, 4f * atHit);
            p.Head = Quaternion.Euler(-8f * cock * (1f - home) - 12f * Env(t, 0.9f, hit, hit + 0.1f, 1.8f) + 6f * yes, 0f, 6f * sting + 8f * atHit);
            float side = Env(t, 0.9f, 1.3f, 1.75f, 2.1f);
            p.PelvisPos += new Vector3(-0.1f * side, jump - knees, -0.02f * wind);
            // Feet: planted, in the air with the jump (knees tuck a little), back down on the landing.
            float tuck = 0.05f * Hump(t, 1.24f, 1.62f);
            var q = Quaternion.Euler(0f, fr.FaceErr, 0f);
            Feet(ref p, q * FootR + Vector3.up * (jump > 0f ? jump + tuck : 0f), q * FootL + Vector3.up * (jump > 0f ? jump + tuck * 0.8f : 0f));
            Alive(ref p, t, 0.5f);

            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 cocked = p.TorsoPoint(new Vector3(0.44f, 0.92f, 0.02f));
            Vector3 back = p.TorsoPoint(new Vector3(0.42f, 0.98f, -0.2f));
            Vector3 contact = AtContact(fr, new Vector3(0f, 1.55f + FiveJump(hit), 0f));
            Vector3 hand;
            if (t < 1.18f) hand = Vector3.Lerp(Vector3.Lerp(rest, cocked, cock), back, wind);
            else if (t < hit) hand = Vector3.Lerp(Vector3.Lerp(cocked, back, wind), contact, swing);
            else if (t < hit + 0.06f) hand = contact;
            else
            {
                Vector3 bounce = contact - fr.Toward * 0.06f + Vector3.up * 0.1f + fr.OwnDir(Vector3.right) * 0.16f;
                hand = Vector3.Lerp(contact, bounce, Seg(t, hit + 0.06f, hit + 0.25f));
                // Stinging hand: in front of the chest, shaken fast.
                Vector3 shake = p.TorsoPoint(new Vector3(0.24f, 0.5f, 0.27f)) + p.TorsoRoot * new Vector3(0.035f * Mathf.Sin(t * 75f), 0.02f * Mathf.Sin(t * 61f), 0f);
                hand = Vector3.Lerp(hand, shake, Seg(t, 1.7f, 1.95f));
                // Fist pumps: up high, yanked down twice.
                Vector3 pumpUp = p.TorsoPoint(new Vector3(0.38f, 0.98f, 0.2f)), pumpDown = p.TorsoPoint(new Vector3(0.3f, 0.42f, 0.28f));
                Vector3 pumpAt = Vector3.Lerp(pumpUp, pumpDown, Mathf.Clamp01(yes));
                hand = Vector3.Lerp(hand, pumpAt, pump);
                hand = Vector3.Lerp(hand, rest, home);
            }
            p.SolveArm(0, hand, p.TorsoRoot * Vector3.Lerp(new Vector3(1f, -0.6f, -0.2f), new Vector3(1f, -0.8f, -0.4f), pump));
            // Left: a small fist pump while bouncing, palm over the stinging hand, else hanging.
            Vector3 l = p.TorsoPoint(Vector3.Lerp(RestL, new Vector3(-0.3f, 0.4f, 0.26f), Env(t, 0.1f, 0.4f, 0.9f, 1.2f)));
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(0.06f, 0.5f, 0.36f)), sting);
            l = Vector3.Lerp(l, p.TorsoPoint(new Vector3(-0.34f, 0.3f + 0.12f * (1f - Mathf.Clamp01(yes)), 0.22f)), pump);
            p.SolveArm(1, l, p.TorsoRoot * new Vector3(-1f, -0.7f, -0.3f));
            return p;
        }
    }
}
