using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>One body part of the blocky rig as an oriented box (outer clothing layer included), in pose-root space.</summary>
    public struct PartBox
    {
        public BodyPart Part;
        public Vector3 Center;
        public Quaternion Rot;
        public Vector3 Half;

        public Vector3 Axis(int i) => Rot * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);

        /// <summary>Point on the box surface / inside for normalised coordinates (-1..1 per axis).</summary>
        public Vector3 Point(float u, float v, float w) => Center + Rot * new Vector3(u * Half.x, v * Half.y, w * Half.z);

        /// <summary>Distance from <paramref name="p"/> to the box (0 inside).</summary>
        public float Distance(Vector3 p)
        {
            Vector3 l = Quaternion.Inverse(Rot) * (p - Center);
            Vector3 d = new Vector3(Mathf.Max(0f, Mathf.Abs(l.x) - Half.x), Mathf.Max(0f, Mathf.Abs(l.y) - Half.y), Mathf.Max(0f, Mathf.Abs(l.z) - Half.z));
            return d.magnitude;
        }

        public PartBox Transformed(Vector3 pos, Quaternion rot) =>
            new PartBox { Part = Part, Center = pos + rot * Center, Rot = rot * Rot, Half = Half };

        public PartBox Inflated(float m) => new PartBox { Part = Part, Center = Center, Rot = Rot, Half = Half + Vector3.one * m };
    }

    public enum BodyPart : byte { Torso, Head, UpperArmR, ForearmR, UpperArmL, ForearmL, ThighR, ShinR, ThighL, ShinL }

    /// <summary>
    /// Forward kinematics of a <see cref="CharacterPose"/> into the rig's box meshes (same sizes as
    /// <see cref="BlockyMeshBuilder"/>: 4 px limbs, 8x12x4 px torso, 8 px head, plus the outer skin layer).
    /// Used by the seated poses to keep the body out of the furniture and by the editor seat check.
    /// </summary>
    public static class BodyBoxes
    {
        public const int Count = 10;
        const float Px = BlockyCharacter.PixelSize;
        const float Layer = 0.25f * Px, Hat = 0.5f * Px;

        static readonly Vector3 LimbHalf = new Vector3(2f * Px + Layer, 3f * Px + 0.5f * Layer, 2f * Px + Layer);

        /// <summary>Fills <paramref name="boxes"/> (length >= <see cref="Count"/>), index = (int)<see cref="BodyPart"/>.</summary>
        public static void Get(in CharacterPose p, PartBox[] boxes)
        {
            Quaternion torso = p.TorsoRoot;
            boxes[(int)BodyPart.Torso] = Box(BodyPart.Torso, p.PelvisPos, torso, new Vector3(0f, 6f * Px, 0f),
                new Vector3(4f * Px + Layer, 6f * Px + Layer, 2f * Px + Layer));
            boxes[(int)BodyPart.Head] = Box(BodyPart.Head, p.TorsoPoint(BlockyCharacter.NeckRest), torso * p.Head, new Vector3(0f, 4f * Px, 0f),
                new Vector3(4f * Px + Hat, 4f * Px + Hat, 4f * Px + Hat));

            for (int arm = 0; arm < 2; arm++)
            {
                Quaternion up = p.UpperArmRoot(arm), fore = p.ForearmRoot(arm);
                // Upper arm: y -4..2 px (+ layer on top), forearm: y -6..0 px (+ layer at the hand end).
                boxes[arm == 0 ? 2 : 4] = Box(arm == 0 ? BodyPart.UpperArmR : BodyPart.UpperArmL, p.ShoulderPos(arm), up,
                    new Vector3(0f, -1f * Px + 0.5f * Layer, 0f), LimbHalf);
                boxes[arm == 0 ? 3 : 5] = Box(arm == 0 ? BodyPart.ForearmR : BodyPart.ForearmL, p.ElbowPos(arm), fore,
                    new Vector3(0f, -3f * Px - 0.5f * Layer, 0f), LimbHalf);
            }

            for (int leg = 0; leg < 2; leg++)
            {
                Quaternion thigh = p.Pelvis * (leg == 0 ? p.HipR : p.HipL);
                Quaternion shin = thigh * (leg == 0 ? p.KneeR : p.KneeL);
                Vector3 hip = p.HipPos(leg);
                Vector3 knee = hip + thigh * BlockyCharacter.KneeRest;
                boxes[leg == 0 ? 6 : 8] = Box(leg == 0 ? BodyPart.ThighR : BodyPart.ThighL, hip, thigh, new Vector3(0f, -3f * Px + 0.5f * Layer, 0f), LimbHalf);
                boxes[leg == 0 ? 7 : 9] = Box(leg == 0 ? BodyPart.ShinR : BodyPart.ShinL, knee, shin, new Vector3(0f, -3f * Px - 0.5f * Layer, 0f), LimbHalf);
            }
        }

        static PartBox Box(BodyPart part, Vector3 joint, Quaternion rot, Vector3 localCenter, Vector3 half) =>
            new PartBox { Part = part, Center = joint + rot * localCenter, Rot = rot, Half = half };

        /// <summary>Knee joint of a leg (0 = right) in pose-root space.</summary>
        public static Vector3 KneePos(in CharacterPose p, int leg) =>
            p.HipPos(leg) + p.Pelvis * (leg == 0 ? p.HipR : p.HipL) * BlockyCharacter.KneeRest;

        /// <summary>Sole (end of the shin) of a leg in pose-root space.</summary>
        public static Vector3 SolePos(in CharacterPose p, int leg)
        {
            Quaternion thigh = p.Pelvis * (leg == 0 ? p.HipR : p.HipL);
            Quaternion shin = thigh * (leg == 0 ? p.KneeR : p.KneeL);
            return KneePos(p, leg) + shin * new Vector3(0f, -RigIK.Shin, 0f);
        }

        // ------------------------------------------------------------------ box vs box (SAT)

        /// <summary>
        /// Penetration depth of two oriented boxes (separating axis test over the 15 axes); 0 or negative = apart
        /// (the value is then minus the gap along the best separating axis).
        /// </summary>
        public static float Penetration(in PartBox a, in PartBox b)
        {
            float best = float.MaxValue;
            Vector3 d = b.Center - a.Center;
            for (int i = 0; i < 3; i++)
            {
                if (!Axis(a, b, a.Axis(i), d, ref best)) return best;
                if (!Axis(a, b, b.Axis(i), d, ref best)) return best;
            }
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                Vector3 ax = Vector3.Cross(a.Axis(i), b.Axis(j));
                if (ax.sqrMagnitude < 1e-6f) continue;
                if (!Axis(a, b, ax.normalized, d, ref best)) return best;
            }
            return best;
        }

        static bool Axis(in PartBox a, in PartBox b, Vector3 ax, Vector3 d, ref float best)
        {
            float ra = Radius(a, ax), rb = Radius(b, ax);
            float overlap = ra + rb - Mathf.Abs(Vector3.Dot(d, ax));
            if (overlap < best) best = overlap;
            return overlap > 0f;
        }

        public static float Radius(in PartBox b, Vector3 ax) =>
            b.Half.x * Mathf.Abs(Vector3.Dot(b.Axis(0), ax)) + b.Half.y * Mathf.Abs(Vector3.Dot(b.Axis(1), ax)) + b.Half.z * Mathf.Abs(Vector3.Dot(b.Axis(2), ax));
    }
}
