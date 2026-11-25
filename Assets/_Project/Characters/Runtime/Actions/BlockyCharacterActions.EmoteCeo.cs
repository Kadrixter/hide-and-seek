using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// "Uścisk prezesa": a pompous handshake. Both straighten up (tie, jacket button), grip in the middle of the two
    /// actual roots, a long firm shake (the one who asked grabs the partner's forearm and pats it, the other covers
    /// the clasp from above), then both turn to "the cameras" on the same side (pair-space +X) with chins up, a
    /// thumbs-up and slow nods while the clasp stays frozen, turn back, two final pumps, release and brush the lapels.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        CharacterPose HandshakePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            bool lead = fr.Lead;
            float grip = EmoteTimeline.ShakeGrip, release = EmoteTimeline.ShakeRelease;
            float pIn = EmoteTimeline.ShakePhotoIn, pOut = EmoteTimeline.ShakePhotoOut;
            float proud = Env(t, 0.0f, 0.5f, 6.4f, 7.0f);
            float reach = Seg(t, 0.65f, grip), off = Seg(t, release, release + 0.55f);
            float photo = Env(t, pIn, pIn + 0.5f, pOut - 0.1f, pOut + 0.4f);
            // Firm pumps: three slow big ones, two at the end.
            float pump = Env(t, grip + 0.05f, grip + 0.25f, 2.75f, 2.95f) * Mathf.Sin((t - grip - 0.05f) * 2f * Mathf.PI * 1.35f)
                         + Env(t, 5.0f, 5.1f, 5.5f, 5.65f) * Mathf.Sin((t - 5.0f) * 2f * Mathf.PI * 1.8f);
            float nod = Hump(t, 3.45f, 3.95f) + Hump(t, 4.1f, 4.6f);
            float bow = Hump(t, 0.75f, 1.55f);
            float turn = (lead ? 1f : -1f) * 48f * photo; // both towards pair-space +X

            var p = PairStance(b, fr, -4f * proud + 3f * reach * (1f - off) * (1f - photo) + 4f * bow);
            p.Torso = p.Torso * Quaternion.Euler(0f, -10f * reach * (1f - off) * (1f - photo) + (lead ? 0f : turn * 0.3f), 0f);
            p.Head = Quaternion.Euler(-7f * proud + 8f * bow + 9f * nod + 2f * pump, turn * 0.75f, 0f);
            p.PelvisPos += new Vector3(0f, 0.008f * proud, 0f);
            PairFeet(ref p, fr);
            Alive(ref p, t, 0.55f);

            // Right: tie, then the clasp (drifts a little towards the cameras during the photo), release, lapel.

            Vector3 clasp = new Vector3(0f, 0.99f + 0.065f * pump + 0.02f * photo, 0f);
            Vector3 contact = AtContact(fr, clasp);
            Vector3 rest = p.TorsoPoint(RestR);
            float tie = Env(t, 0.05f, 0.3f, 0.55f, 0.75f);
            Vector3 tieAt = HeadPoint(p, new Vector3(0.02f + 0.03f * Mathf.Sin(t * 30f) * Env(t, 0.25f, 0.3f, 0.5f, 0.55f), -0.08f, 0.2f));
            Vector3 hand = Vector3.Lerp(rest, tieAt, tie);
            hand = Vector3.Lerp(hand, contact, reach) + Vector3.up * 0.06f * Hump(reach, 0f, 1f);
            hand = Vector3.Lerp(hand, rest, off);
            float lapel = Env(t, 6.0f, 6.25f, 6.6f, 6.9f);
            Vector3 lapelAt = p.TorsoPoint(new Vector3(-0.06f + 0.06f * Mathf.Sin((t - 6.25f) * 14f), 0.5f, 0.3f));
            hand = Vector3.Lerp(hand, lapelAt, lapel);
            p.SolveArm(0, hand, p.TorsoRoot * new Vector3(1f, -1f, -0.3f));

            // Left: buttons the jacket, then the leader grips the partner's forearm (pats), the other covers the clasp;
            // both a thumbs-up to the cameras.
            Vector3 restL = p.TorsoPoint(RestL);
            float button = Env(t, 0.1f, 0.4f, 0.6f, 0.85f);
            Vector3 l = Vector3.Lerp(restL, p.TorsoPoint(new Vector3(-0.02f, 0.3f, 0.17f)), button);
            float hold = Env(t, 1.45f, 1.75f, pIn - 0.05f, pIn + 0.3f);
            Vector3 holdAt;
            if (lead)
            {
                Vector3 elbow = emotePartner != null ? transform.InverseTransformPoint(emotePartner.ElbowRight.position) : contact + fr.Toward * 0.4f;
                Vector3 fore = Vector3.Lerp(fr.Point(clasp), elbow, 0.42f);
                holdAt = fore + Vector3.up * (0.115f + 0.1f * PatLift(t, EmoteTimeline.ShakePats));
            }
            else holdAt = fr.Point(clasp) + Vector3.up * 0.115f + fr.Toward * 0.02f;
            l = Vector3.Lerp(l, holdAt, hold);
            Vector3 cam = fr.Point(new Vector3(0.62f, 1.18f, 0f)); // towards the cameras, in front of the own chest
            Vector3 thumb = Vector3.Lerp(p.TorsoPoint(new Vector3(-0.3f, 0.5f, 0.3f)), cam, 0.25f);
            float thumbs = Env(t, pIn + 0.25f, pIn + 0.6f, pOut - 0.3f, pOut);
            l = Vector3.Lerp(l, thumb, thumbs);
            p.SolveArm(1, l, p.TorsoRoot * new Vector3(-1f, -0.8f, -0.3f));
            return p;
        }

        /// <summary>Pats: the hand lifts off and comes down on each time in <paramref name="pats"/>.</summary>
        static float PatLift(float t, float[] pats)
        {
            float l = 0f;
            foreach (float pat in pats) l = Mathf.Max(l, Hump(t, pat - 0.16f, pat));
            return l;
        }
    }
}
