using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pair emotes. Both bodies run the same clock (started by the server). Contacts are found from the two actual
    /// roots on this peer (the pair frame: middle point, axis from the one who asked - role 0 - to the other one), so the
    /// hands meet even if the bodies stand a little off their spots; the hug reads the partner's real torso. The body
    /// also turns a little towards the partner when the root does not face it exactly.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float FistHalf = 0.105f;

        struct PairFrame
        {
            public Vector3 Mid, F, R, Toward;
            public float Dist, FaceErr;
            public Vector3 Point(Vector3 c) => Mid + R * c.x + Vector3.up * c.y + F * c.z;
        }

        PairFrame Frame()
        {
            var info = Emotes.Get(emoteId);
            Vector3 other = emotePartner != null ? transform.InverseTransformPoint(emotePartner.transform.position)
                : new Vector3(0f, 0f, info != null ? info.PairDistance : 0.8f);
            Vector3 flat = new Vector3(other.x, 0f, other.z);
            float d = flat.magnitude;
            Vector3 dir = d > 0.01f ? flat / d : Vector3.forward;
            var fr = new PairFrame { Dist = d, Toward = dir, Mid = new Vector3(other.x * 0.5f, other.y * 0.5f, other.z * 0.5f) };
            fr.F = emoteRole == 0 ? dir : -dir;
            fr.R = Vector3.Cross(Vector3.up, fr.F);
            fr.FaceErr = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -30f, 30f);
            return fr;
        }

        /// <summary>Partner torso-space point in this root's space (falls back to the ideal partner spot).</summary>
        Vector3 PartnerTorso(in PairFrame fr, Vector3 local)
        {
            if (emotePartner != null && emotePartner.Torso != null)
                return transform.InverseTransformPoint(emotePartner.Torso.TransformPoint(local));
            Vector3 root = fr.Toward * fr.Dist;
            var rot = Quaternion.LookRotation(-fr.Toward);
            return root + rot * (BlockyCharacter.PelvisRest + local);
        }

        /// <summary>Stance turned to the partner (pelvis yaw makes up for a root that is a bit off).</summary>
        CharacterPose PairStance(in CharacterPose b, in PairFrame fr, float lean)
        {
            var p = Stance(b);
            p.Pelvis = Quaternion.Euler(0f, fr.FaceErr, 0f);
            p.Torso = Quaternion.Euler(lean, 0f, 0f);
            return p;
        }

        void PairFeet(ref CharacterPose p, in PairFrame fr)
        {
            var q = Quaternion.Euler(0f, fr.FaceErr, 0f);
            Feet(ref p, q * FootR, q * FootL);
        }

        /// <summary>Hand target at a contact point: the fist stops <see cref="FistHalf"/> short of it, on this side.</summary>
        static Vector3 AtContact(in PairFrame fr, Vector3 c) => fr.Point(c) - fr.Toward * FistHalf;

        // ------------------------------------------------------------------ high five / fist bump / handshake

        CharacterPose HighFivePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float hit = EmoteTimeline.FiveHit;
            float wind = Seg(t, 0f, 0.45f), swing = ActionTimeline.EaseIn(ActionTimeline.Remap01(t, 0.45f, hit));
            float recoil = Hump(t, hit, hit + 0.35f), down = Seg(t, 1.05f, 1.75f);
            float lean = -4f * wind * (1f - swing) + 9f * swing * (1f - down) + 3f * recoil;
            var p = PairStance(b, fr, lean);
            p.Torso = p.Torso * Quaternion.Euler(0f, -18f * Seg(t, 0.3f, hit) * (1f - down), 0f);
            p.Head = Quaternion.Euler(-12f * Seg(t, 0.3f, hit) * (1f - down) + 4f * recoil, 0f, 0f);
            p.PelvisPos += new Vector3(0f, -0.02f * recoil, 0.02f * swing * (1f - down));
            PairFeet(ref p, fr);
            Alive(ref p, t, 0.6f);
            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 cocked = p.TorsoPoint(new Vector3(0.44f, 0.92f, -0.04f));
            Vector3 contact = AtContact(fr, new Vector3(0f, 1.58f, 0f));
            Vector3 hand;
            if (t < 0.45f) hand = Vector3.Lerp(rest, cocked, wind);
            else if (t < hit) hand = Vector3.Lerp(cocked, contact, swing);
            else
            {
                Vector3 bounce = contact - fr.Toward * 0.12f + Vector3.up * 0.06f;
                hand = Vector3.Lerp(contact, bounce, Seg(t, hit, hit + 0.18f));
                hand += new Vector3(0.012f, 0f, 0f) * Mathf.Sin(t * 70f) * Env(t, hit + 0.15f, hit + 0.25f, 1.25f, 1.45f);
                hand = Vector3.Lerp(hand, rest, down);
            }
            p.SolveArm(0, hand, p.TorsoRoot * new Vector3(1f, -0.6f, -0.2f));
            Hang(ref p, 1, 0.03f * swing);
            return p;
        }

        CharacterPose FistBumpPose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float hit = EmoteTimeline.BumpHit;
            float ready = Seg(t, 0f, 0.55f), approach = Seg(t, 0.55f, hit);
            float boom = Seg(t, hit, hit + 0.32f) * (1f - Seg(t, 1.6f, 2.25f));
            float back = 1f - Seg(t, 1.6f, 2.25f);
            var p = PairStance(b, fr, 4f * ready * (1f - boom) - 5f * boom);
            p.Torso = p.Torso * Quaternion.Euler(0f, -12f * approach * (1f - boom) + 10f * boom, 0f);
            p.Head = Quaternion.Euler(6f * approach * (1f - boom) - 6f * boom, 14f * boom, 0f);
            p.PelvisPos += new Vector3(0f, 0f, -0.02f * boom);
            PairFeet(ref p, fr);
            Alive(ref p, t, 0.7f);
            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 readyAt = p.TorsoPoint(new Vector3(0.24f, 0.5f, 0.3f));
            Vector3 contact = AtContact(fr, new Vector3(0f, 1.14f, 0f));
            Vector3 hand = Vector3.Lerp(Vector3.Lerp(rest, readyAt, ready), contact, approach);
            if (t >= hit)
            {
                // "Boom": the hand flies back and up, wiggling, then drops.
                Vector3 blown = p.TorsoPoint(new Vector3(0.56f, 0.86f, 0.06f)) + Vector3.up * 0.03f * Mathf.Sin(t * 38f) * Env(t, hit + 0.25f, hit + 0.35f, 1.4f, 1.6f);
                hand = Vector3.Lerp(contact, blown, Seg(t, hit, hit + 0.32f));
                hand = Vector3.Lerp(hand, rest, 1f - back);
            }
            p.SolveArm(0, hand, p.TorsoRoot * new Vector3(1f, -0.8f, -0.3f));
            Hang(ref p, 1);
            return p;
        }

        CharacterPose HandshakePose(in CharacterPose b, float t)
        {
            var fr = Frame();
            float grip = EmoteTimeline.ShakeGrip, release = EmoteTimeline.ShakeRelease;
            float reach = Seg(t, 0f, grip), off = Seg(t, release, release + 0.6f);
            float pump = Env(t, grip + 0.08f, grip + 0.2f, 1.85f, 2.05f) * Mathf.Sin((t - grip - 0.08f) * 2f * Mathf.PI * 2.3f);
            float bow = Hump(t, 0.4f, 1.3f);
            var p = PairStance(b, fr, 5f * reach * (1f - off) + 7f * bow);
            p.Torso = p.Torso * Quaternion.Euler(0f, -10f * reach * (1f - off), 0f);
            p.Head = Quaternion.Euler(10f * bow + 2f * pump, 0f, 0f);
            PairFeet(ref p, fr);
            Alive(ref p, t, 0.7f);
            Vector3 rest = p.TorsoPoint(RestR);
            Vector3 contact = AtContact(fr, new Vector3(0f, 0.99f + 0.05f * pump, 0f));
            Vector3 hand = Vector3.Lerp(rest, contact, reach) + Vector3.up * 0.08f * Hump(reach, 0f, 1f);
            hand = Vector3.Lerp(hand, rest, off);
            p.SolveArm(0, hand, p.TorsoRoot * new Vector3(1f, -1f, -0.3f));
            // The one who asked puts the other hand on top for a moment (warm handshake).
            float cover = emoteRole == 0 ? Env(t, 0.95f, 1.25f, 1.75f, 2.05f) : 0f;
            Vector3 coverAt = fr.Point(new Vector3(0f, 1.1f + 0.05f * pump, 0f)) - fr.Toward * 0.03f;
            Vector3 left = Vector3.Lerp(p.TorsoPoint(RestL), coverAt, cover);
            p.SolveArm(1, left, p.TorsoRoot * new Vector3(-1f, -0.8f, -0.3f));
            return p;
        }
    }
}
