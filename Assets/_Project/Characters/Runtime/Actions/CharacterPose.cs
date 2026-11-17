using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Plain-data snapshot of every animated joint of a <see cref="BlockyCharacter"/>.
    /// Pelvis position/rotation are in character-root space, all other rotations are local to their parent joint
    /// (exactly what the transforms hold). Root-space helpers make IK and contact targets pure math, so poses can be
    /// evaluated, blended and only then written to the transforms once per frame.
    /// </summary>
    public struct CharacterPose
    {
        public Vector3 PelvisPos;
        public Quaternion Pelvis, Torso, Head;
        public Quaternion ShoulderR, ElbowR, ShoulderL, ElbowL;
        public Quaternion HipR, KneeR, HipL, KneeL;

        /// <summary>Local rotation of the right-hand prop socket relative to the forearm (not part of the rig itself).</summary>
        public Quaternion PropR;

        const float Px = BlockyCharacter.PixelSize;

        /// <summary>Hand grip point along the forearm (fist centre), metres below the elbow pivot.</summary>
        public const float HandOffset = 5f * Px;

        public static readonly Vector3 HandLocal = new Vector3(0f, -HandOffset, 0f);

        public static CharacterPose Identity => new CharacterPose
        {
            PelvisPos = BlockyCharacter.PelvisRest,
            Pelvis = Quaternion.identity, Torso = Quaternion.identity, Head = Quaternion.identity,
            ShoulderR = Quaternion.identity, ElbowR = Quaternion.identity,
            ShoulderL = Quaternion.identity, ElbowL = Quaternion.identity,
            HipR = Quaternion.identity, KneeR = Quaternion.identity,
            HipL = Quaternion.identity, KneeL = Quaternion.identity,
            PropR = Quaternion.identity
        };

        public static CharacterPose Capture(BlockyCharacter c)
        {
            return new CharacterPose
            {
                PropR = Quaternion.identity,
                PelvisPos = c.Pelvis.localPosition,
                Pelvis = c.Pelvis.localRotation,
                Torso = c.Torso.localRotation,
                Head = c.Head.localRotation,
                ShoulderR = c.ShoulderRight.localRotation,
                ElbowR = c.ElbowRight.localRotation,
                ShoulderL = c.ShoulderLeft.localRotation,
                ElbowL = c.ElbowLeft.localRotation,
                HipR = c.HipRight.localRotation,
                KneeR = c.KneeRight.localRotation,
                HipL = c.HipLeft.localRotation,
                KneeL = c.KneeLeft.localRotation
            };
        }

        public void Apply(BlockyCharacter c)
        {
            c.Pelvis.localPosition = PelvisPos;
            c.Pelvis.localRotation = Pelvis;
            c.Torso.localRotation = Torso;
            c.Head.localRotation = Head;
            c.ShoulderRight.localRotation = ShoulderR;
            c.ElbowRight.localRotation = ElbowR;
            c.ShoulderLeft.localRotation = ShoulderL;
            c.ElbowLeft.localRotation = ElbowL;
            c.HipRight.localRotation = HipR;
            c.KneeRight.localRotation = KneeR;
            c.HipLeft.localRotation = HipL;
            c.KneeLeft.localRotation = KneeL;
        }

        // ------------------------------------------------------------------ blending

        public static CharacterPose Lerp(in CharacterPose a, in CharacterPose b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return new CharacterPose
            {
                PelvisPos = Vector3.LerpUnclamped(a.PelvisPos, b.PelvisPos, t),
                Pelvis = Quaternion.Slerp(a.Pelvis, b.Pelvis, t),
                Torso = Quaternion.Slerp(a.Torso, b.Torso, t),
                Head = Quaternion.Slerp(a.Head, b.Head, t),
                ShoulderR = Quaternion.Slerp(a.ShoulderR, b.ShoulderR, t),
                ElbowR = Quaternion.Slerp(a.ElbowR, b.ElbowR, t),
                ShoulderL = Quaternion.Slerp(a.ShoulderL, b.ShoulderL, t),
                ElbowL = Quaternion.Slerp(a.ElbowL, b.ElbowL, t),
                HipR = Quaternion.Slerp(a.HipR, b.HipR, t),
                KneeR = Quaternion.Slerp(a.KneeR, b.KneeR, t),
                HipL = Quaternion.Slerp(a.HipL, b.HipL, t),
                KneeL = Quaternion.Slerp(a.KneeL, b.KneeL, t),
                PropR = Quaternion.Slerp(a.PropR, b.PropR, t)
            };
        }

        /// <summary>Blends only one arm (0 = right, 1 = left) towards <paramref name="b"/>.</summary>
        public static void LerpArm(ref CharacterPose a, in CharacterPose b, int arm, float t)
        {
            if (t <= 0f) return;
            if (arm == 0)
            {
                a.ShoulderR = Quaternion.Slerp(a.ShoulderR, b.ShoulderR, t);
                a.ElbowR = Quaternion.Slerp(a.ElbowR, b.ElbowR, t);
                a.PropR = Quaternion.Slerp(a.PropR, b.PropR, t);
            }
            else
            {
                a.ShoulderL = Quaternion.Slerp(a.ShoulderL, b.ShoulderL, t);
                a.ElbowL = Quaternion.Slerp(a.ElbowL, b.ElbowL, t);
            }
        }

        // ------------------------------------------------------------------ root-space helpers

        public Quaternion TorsoRoot => Pelvis * Torso;

        /// <summary>A point given in torso-local space (origin at the hips), in character-root space.</summary>
        public Vector3 TorsoPoint(Vector3 local) => PelvisPos + Pelvis * (Torso * local);

        public Vector3 ShoulderPos(int arm) =>
            TorsoPoint(arm == 0 ? BlockyCharacter.ShoulderRightRest : BlockyCharacter.ShoulderLeftRest);

        public Quaternion GetShoulder(int arm) => arm == 0 ? ShoulderR : ShoulderL;
        public Quaternion GetElbow(int arm) => arm == 0 ? ElbowR : ElbowL;

        public void SetArm(int arm, Quaternion shoulder, Quaternion elbow)
        {
            if (arm == 0) { ShoulderR = shoulder; ElbowR = elbow; }
            else { ShoulderL = shoulder; ElbowL = elbow; }
        }

        public Quaternion UpperArmRoot(int arm) => TorsoRoot * GetShoulder(arm);
        public Quaternion ForearmRoot(int arm) => UpperArmRoot(arm) * GetElbow(arm);
        public Vector3 ElbowPos(int arm) => ShoulderPos(arm) + UpperArmRoot(arm) * BlockyCharacter.ElbowRest;
        public Vector3 HandPos(int arm) => ElbowPos(arm) + ForearmRoot(arm) * HandLocal;

        public Vector3 HipPos(int leg) => PelvisPos + Pelvis * (leg == 0 ? BlockyCharacter.HipRightRest : BlockyCharacter.HipLeftRest);

        /// <summary>Two-bone IK for one arm. Target (fist centre) and elbow pole direction in root space.</summary>
        public void SolveArm(int arm, Vector3 target, Vector3 pole)
        {
            RigIK.SolveArm(ShoulderPos(arm), TorsoRoot, target, pole, out var sh, out var el);
            SetArm(arm, sh, el);
        }

        /// <summary>Two-bone IK for one leg. Target = sole (end of the shin), knee pole direction, both in root space.</summary>
        public void SolveLeg(int leg, Vector3 sole, Vector3 pole)
        {
            RigIK.SolveLeg(HipPos(leg), Pelvis, sole, pole, out var hip, out var knee);
            if (leg == 0) { HipR = hip; KneeR = knee; }
            else { HipL = hip; KneeL = knee; }
        }
    }

    /// <summary>Analytic two-bone IK for the blocky rig (limbs hang along local -Y).</summary>
    public static class RigIK
    {
        const float Px = BlockyCharacter.PixelSize;
        public const float UpperArm = 4f * Px;
        public const float Forearm = CharacterPose.HandOffset;
        public const float Thigh = 6f * Px;
        public const float Shin = 6f * Px;

        /// <summary>
        /// Arm: the forearm folds towards the upper arm's local +Z (negative elbow X rotation), so the elbow points
        /// roughly along <paramref name="pole"/>. Outputs local rotations given the parent's rotation.
        /// </summary>
        public static void SolveArm(Vector3 shoulderPos, Quaternion parentRot, Vector3 target, Vector3 pole,
            out Quaternion shoulderLocal, out Quaternion elbowLocal, float upper = UpperArm, float lower = Forearm)
        {
            Solve(shoulderPos, target, pole, upper, lower, out var upperDir, out var bend, out float angle);
            Quaternion world = Quaternion.LookRotation(bend, -upperDir);
            shoulderLocal = Quaternion.Inverse(parentRot) * world;
            elbowLocal = Quaternion.Euler(-angle, 0f, 0f);
        }

        /// <summary>Leg: the shin folds towards local -Z (positive knee X rotation), the knee points along the pole.</summary>
        public static void SolveLeg(Vector3 hipPos, Quaternion parentRot, Vector3 sole, Vector3 pole,
            out Quaternion hipLocal, out Quaternion kneeLocal)
        {
            Solve(hipPos, sole, pole, Thigh, Shin, out var upperDir, out var bend, out float angle);
            Quaternion world = Quaternion.LookRotation(-bend, -upperDir);
            hipLocal = Quaternion.Inverse(parentRot) * world;
            kneeLocal = Quaternion.Euler(angle, 0f, 0f);
        }

        static void Solve(Vector3 root, Vector3 target, Vector3 pole, float a, float b,
            out Vector3 upperDir, out Vector3 bend, out float angle)
        {
            Vector3 toT = target - root;
            float d = toT.magnitude;
            Vector3 u = d > 1e-5f ? toT / d : Vector3.down;
            d = Mathf.Clamp(d, Mathf.Abs(a - b) + 0.01f, (a + b) * 0.9995f);
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));

            Vector3 p = pole - u * Vector3.Dot(pole, u);
            if (p.sqrMagnitude < 1e-8f)
            {
                p = Vector3.Cross(u, Vector3.right);
                if (p.sqrMagnitude < 1e-8f) p = Vector3.Cross(u, Vector3.forward);
            }

            p.Normalize();
            upperDir = u * cosA + p * sinA;
            Vector3 elbow = root + upperDir * a;
            Vector3 lowerDir = (root + u * d - elbow).normalized;
            bend = lowerDir - upperDir * Vector3.Dot(lowerDir, upperDir);
            if (bend.sqrMagnitude < 1e-8f) bend = -(p - upperDir * Vector3.Dot(p, upperDir));
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.Cross(upperDir, Vector3.right);
            bend.Normalize();
            angle = Vector3.Angle(upperDir, lowerDir);
        }
    }
}
