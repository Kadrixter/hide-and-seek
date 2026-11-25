using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Pair frame of the pair emotes. Both bodies run the same clock (started by the server). Contacts are found from
    /// the two actual roots on this peer: pair space has its origin in the middle between them and +Z from the one who
    /// asked (role 0) to the other one, so the hands meet even if the bodies stand a little off their spots. "Own"
    /// space is the same for both roles: origin at the own ideal spot (half the emote distance from the middle), +Z
    /// towards the partner, +X the own right; a choreography written once in own space is mirrored for the partner.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        const float FistHalf = 0.105f;

        struct PairFrame
        {
            public Vector3 Mid, F, R, Toward;
            public float Dist, FaceErr, Half;
            public bool Lead;

            /// <summary>Pair-space point in root space.</summary>
            public Vector3 Point(Vector3 c) => Mid + R * c.x + Vector3.up * c.y + F * c.z;

            /// <summary>Pair-space rotation in root space.</summary>
            public Quaternion Rot => Quaternion.LookRotation(F, Vector3.up);

            /// <summary>Own-space point (this body) in root space.</summary>
            public Vector3 Own(Vector3 c) => Lead ? Point(new Vector3(c.x, c.y, c.z - Half)) : Point(new Vector3(-c.x, c.y, Half - c.z));

            /// <summary>Partner's own-space point in root space.</summary>
            public Vector3 Other(Vector3 c) => Lead ? Point(new Vector3(-c.x, c.y, Half - c.z)) : Point(new Vector3(c.x, c.y, c.z - Half));

            /// <summary>Own space rotation (facing the partner) in root space.</summary>
            public Quaternion OwnRot => Quaternion.LookRotation(Lead ? F : -F, Vector3.up);

            public Quaternion OtherRot => Quaternion.LookRotation(Lead ? -F : F, Vector3.up);

            /// <summary>Own-space direction in root space.</summary>
            public Vector3 OwnDir(Vector3 d) => OwnRot * d;
        }

        PairFrame Frame()
        {
            var info = Emotes.Get(emoteId);
            float ideal = info != null && info.PairDistance > 0f ? info.PairDistance : 0.8f;
            Vector3 other = emotePartner != null ? transform.InverseTransformPoint(emotePartner.transform.position)
                : new Vector3(0f, 0f, ideal);
            Vector3 flat = new Vector3(other.x, 0f, other.z);
            float d = flat.magnitude;
            Vector3 dir = d > 0.01f ? flat / d : Vector3.forward;
            var fr = new PairFrame { Dist = d, Toward = dir, Mid = new Vector3(other.x * 0.5f, other.y * 0.5f, other.z * 0.5f) };
            fr.F = emoteRole == 0 ? dir : -dir;
            fr.R = Vector3.Cross(Vector3.up, fr.F);
            fr.FaceErr = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -30f, 30f);
            fr.Half = ideal * 0.5f;
            fr.Lead = emoteRole == 0;
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

        /// <summary>Stance turned to the partner (pelvis yaw makes up for a root that is a bit off, the pelvis for the distance).</summary>
        CharacterPose PairStance(in CharacterPose b, in PairFrame fr, float lean)
        {
            var p = Stance(b);
            p.Pelvis = Quaternion.Euler(0f, fr.FaceErr, 0f);
            p.Torso = Quaternion.Euler(lean, 0f, 0f);
            // Roots a few cm off the emote distance (the walk to the spots stops early): the body leans the gap away.
            p.PelvisPos += fr.Toward * Mathf.Clamp((fr.Dist - 2f * fr.Half) * 0.5f, -0.08f, 0.08f);
            return p;
        }

        void PairFeet(ref CharacterPose p, in PairFrame fr)
        {
            var q = Quaternion.Euler(0f, fr.FaceErr, 0f);
            Feet(ref p, q * FootR, q * FootL);
        }

        /// <summary>Hand target at a contact point: the fist stops <see cref="FistHalf"/> short of it, on this side.</summary>
        static Vector3 AtContact(in PairFrame fr, Vector3 c) => fr.Point(c) - fr.Toward * FistHalf;

        /// <summary>
        /// Stance in own space: pelvis at the own spot (shifted by <paramref name="shift"/>, own space), facing the partner
        /// turned by <paramref name="yaw"/>, torso lean / yaw on top. Spacing comes from the middle, not the root.
        /// </summary>
        static CharacterPose OwnStance(in CharacterPose b, in PairFrame fr, Vector3 shift, float yaw, float lean, float torsoYaw = 0f)
        {
            var p = Stance(b);
            p.PelvisPos = fr.Own(new Vector3(0f, BlockyCharacter.HipHeight - 0.005f, 0f) + shift);
            p.Pelvis = fr.OwnRot * Quaternion.Euler(0f, yaw, 0f);
            p.Torso = Quaternion.Euler(lean, torsoYaw, 0f);
            return p;
        }

        /// <summary>Both soles at own-space points (knees forward along the own facing turned by <paramref name="yaw"/>).</summary>
        static void OwnFeet(ref CharacterPose p, in PairFrame fr, Vector3 r, Vector3 l, float yaw = 0f)
        {
            Vector3 fwd = fr.OwnDir(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);
            FeetAt(ref p, fr.Own(r), fr.Own(l), fwd);
        }

        /// <summary>Hand target in own space with the elbow pole in own space.</summary>
        static void OwnArm(ref CharacterPose p, in PairFrame fr, int arm, Vector3 own, Vector3 pole) =>
            p.SolveArm(arm, fr.Own(own), fr.OwnDir(pole));

        /// <summary>Own-space hand target that meets the partner's mirrored one at pair-space x = 0, depth z = half.</summary>
        static Vector3 MeetPoint(in PairFrame fr, float y, float x = 0f) => new Vector3(x, y, fr.Half - FistHalf);
    }
}
