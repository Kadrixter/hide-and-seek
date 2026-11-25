using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Łapki" (patty-cake), a loop of four beats written once in own space: clap the own hands, right hands together
    /// in the middle at chest height, clap, left hands together; the hands snap into each contact (ease-in) and spring
    /// back out of it (ease-out) through a point pulled back towards the chest, knees bounce on every beat. Both
    /// bodies use the same clock and the same mirrored targets, so the palms meet exactly in the middle.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float PattyY = 1.2f;

        /// <summary>Key of a hand at beat <paramref name="k"/> (0 left five, 1 clap, 2 right five, 3 clap).</summary>
        static Vector3 PattyKey(in CharacterPose p, in PairFrame fr, int arm, int k)
        {
            float sx = arm == 0 ? 1f : -1f;
            k &= 3;
            if (k == 1 || k == 3) return p.TorsoPoint(new Vector3(0.115f * sx, 0.44f, 0.34f));
            bool mine = (k == 2 && arm == 0) || (k == 0 && arm == 1);
            if (mine) return fr.Own(MeetPoint(fr, PattyY));
            return p.TorsoPoint(new Vector3(0.3f * sx, 0.46f, 0.16f));
        }

        CharacterPose PattyCakePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            const float beat = EmoteTimeline.PattyBeat;
            // Contacts on the beats; u runs from one contact to the next (the first beat only leads in from the rest).
            float x = t / beat;
            int k = Mathf.FloorToInt(x);
            float u = x - k;
            int kk = ((k % 4) + 4) % 4;
            float hit = 1f - Mathf.Min(Seg(u, 0f, 0.35f), 1f - Seg(u, 0.65f, 1f)); // 1 at a contact
            bool fiveNow = (kk == 0 || kk == 2), fiveNext = (kk == 1 || kk == 3);
            float fiveW = fiveNow ? 1f - Seg(u, 0f, 0.5f) : 0f;
            fiveW = Mathf.Max(fiveW, fiveNext ? Seg(u, 0.5f, 1f) : 0f);
            int fiveSide = (fiveNow ? kk : (kk + 1) & 3) == 2 ? 0 : 1;
            float bounce = 0.02f * (0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * x));

            var p = OwnStance(b, fr, new Vector3(0f, -bounce, 0f), 0f, 1f + 2f * fiveW, (fiveSide == 0 ? -14f : 14f) * fiveW);
            p.Head = Quaternion.Euler(-3f + 6f * hit * fiveW + 3f * Mathf.Sin(Mathf.PI * x), (fiveSide == 0 ? 6f : -6f) * fiveW, 0f);
            OwnFeet(ref p, fr, new Vector3(0.13f, 0f, 0.01f), new Vector3(-0.13f, 0f, -0.01f));
            Alive(ref p, t, 0.3f);

            for (int arm = 0; arm < 2; arm++)
            {
                float sx = arm == 0 ? 1f : -1f;
                Vector3 a = PattyKey(p, fr, arm, kk), c = PattyKey(p, fr, arm, kk + 1);
                // Spring out of the contact and snap into the next one, via a point pulled back to the chest and up.
                Vector3 back = (a + c) * 0.5f + p.TorsoRoot * new Vector3(0.07f * sx, 0.07f, -0.08f);
                Vector3 hand = u < 0.5f
                    ? Vector3.Lerp(a, back, ActionTimeline.EaseOut(u * 2f))
                    : Vector3.Lerp(back, c, ActionTimeline.EaseIn((u - 0.5f) * 2f));
                if (t < beat) hand = Vector3.Lerp(p.TorsoPoint(arm == 0 ? RestR : RestL), hand, Seg(t, 0f, beat * 0.9f));
                p.SolveArm(arm, hand, p.TorsoRoot * new Vector3(sx, -0.75f, -0.45f));
            }
            return p;
        }
    }
}
