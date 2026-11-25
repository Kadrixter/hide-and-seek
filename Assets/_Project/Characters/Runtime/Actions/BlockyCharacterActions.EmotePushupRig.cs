using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Key bodies of the push-ups as plain parameters (pelvis, rotations, soles, hands, elbow poles in root space),
    /// blended parameter by parameter so the soles and hands travel where they are told (lifted when they move, never
    /// through the floor), then solved into a pose. The plank is a straight line from the toes: its angle sets the
    /// shoulder height, the hands stay planted on the floor and the elbows bend.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        struct PushBody
        {
            public Vector3 Pelvis, FootR, FootL, HandR, HandL, PoleR, PoleL;
            public Quaternion Rot, Torso, Head;

            public static PushBody Lerp(in PushBody a, in PushBody b, float t) => new PushBody
            {
                Pelvis = Vector3.Lerp(a.Pelvis, b.Pelvis, t),
                FootR = Vector3.Lerp(a.FootR, b.FootR, t), FootL = Vector3.Lerp(a.FootL, b.FootL, t),
                HandR = Vector3.Lerp(a.HandR, b.HandR, t), HandL = Vector3.Lerp(a.HandL, b.HandL, t),
                PoleR = Vector3.Lerp(a.PoleR, b.PoleR, t), PoleL = Vector3.Lerp(a.PoleL, b.PoleL, t),
                Rot = Quaternion.Slerp(a.Rot, b.Rot, t), Torso = Quaternion.Slerp(a.Torso, b.Torso, t),
                Head = Quaternion.Slerp(a.Head, b.Head, t),
            };
        }

        const float PushToeZ = -0.7f, PushToeY = 0.1f, PushHandY = 0.11f;

        static CharacterPose PushSolve(in CharacterPose b, in PushBody k)
        {
            var p = Stance(b);
            p.PelvisPos = k.Pelvis;
            p.Pelvis = k.Rot;
            p.Torso = k.Torso;
            p.Head = k.Head;
            Vector3 knee = k.Rot * Vector3.forward;
            p.SolveLeg(0, k.FootR, knee + k.Rot * Vector3.right * 0.12f);
            p.SolveLeg(1, k.FootL, knee - k.Rot * Vector3.right * 0.12f);
            p.SolveArm(0, k.HandR, k.PoleR);
            p.SolveArm(1, k.HandL, k.PoleL);
            return p;
        }

        static PushBody PushStand()
        {
            return new PushBody
            {
                Pelvis = new Vector3(0f, BlockyCharacter.HipHeight - 0.005f, 0f), Rot = Quaternion.identity, Torso = Quaternion.identity,
                Head = Quaternion.identity, FootR = FootR, FootL = FootL,
                HandR = new Vector3(0.37f, 0.73f, 0.04f), HandL = new Vector3(-0.37f, 0.73f, 0.04f),
                PoleR = new Vector3(0.3f, 0f, -1f), PoleL = new Vector3(-0.3f, 0f, -1f),
            };
        }

        static PushBody PushSquat(float handsDown)
        {
            var k = PushStand();
            k.Pelvis = new Vector3(0f, 0.36f, -0.06f);
            k.Torso = Quaternion.Euler(48f, 0f, 0f);
            k.Head = Quaternion.Euler(-30f, 0f, 0f);
            k.HandR = Vector3.Lerp(new Vector3(0.3f, 0.55f, 0.35f), new Vector3(0.24f, PushHandY, 0.42f), handsDown);
            k.HandL = Vector3.Lerp(new Vector3(-0.3f, 0.55f, 0.35f), new Vector3(-0.24f, PushHandY, 0.42f), handsDown);
            k.PoleR = new Vector3(1f, 0f, -0.6f);
            k.PoleL = new Vector3(-1f, 0f, -0.6f);
            return k;
        }

        /// <summary>Plank at body angle <paramref name="deg"/> (elevation from the toes), feet <paramref name="feetX"/> apart each side.</summary>
        static PushBody PushPlank(float deg, float feetX, float roll, bool oneArm, float handX)
        {
            Quaternion rot = Quaternion.Euler(90f - deg, 0f, 0f) * Quaternion.Euler(0f, roll, 0f);
            Vector3 toe = new Vector3(0f, PushToeY, PushToeZ);
            Vector3 dir = rot * Vector3.up;
            Vector3 pelvis = toe + dir * (2f * RigIK.Thigh * 0.985f);
            // Hands under the shoulders of the top position (27 degrees).
            float zTop = PushToeZ + Mathf.Cos(27f * Mathf.Deg2Rad) * (2f * RigIK.Thigh * 0.985f + BlockyCharacter.ShoulderRightRest.y);
            return new PushBody
            {
                Pelvis = pelvis, Rot = rot, Torso = Quaternion.identity, Head = Quaternion.Euler(-48f, 0f, 0f),
                FootR = new Vector3(feetX, PushToeY, PushToeZ), FootL = new Vector3(-feetX, PushToeY, PushToeZ),
                HandR = new Vector3(handX, PushHandY, zTop),
                HandL = oneArm ? pelvis + rot * new Vector3(-0.12f, 0.12f, -0.31f) : new Vector3(-0.3f, PushHandY, zTop),
                PoleR = rot * new Vector3(0.8f, 0f, -0.6f) + Vector3.back * 0.6f,
                PoleL = oneArm ? rot * new Vector3(-1f, 0f, -0.5f) : rot * new Vector3(-0.8f, 0f, -0.6f) + Vector3.back * 0.6f,
            };
        }

        /// <summary>Double biceps flex (standing), <paramref name="pump"/> squeezes, <paramref name="kiss"/> turns to the right biceps.</summary>
        static PushBody PushFlex(float pump, float kiss)
        {
            var k = PushStand();
            k.Torso = Quaternion.Euler(-4f - 3f * pump, -12f * kiss, 0f);
            k.Head = Quaternion.Euler(-6f + 16f * kiss, 50f * kiss, 8f * kiss);
            k.Pelvis += new Vector3(0f, -0.02f * pump, 0f);
            Quaternion tr = k.Rot * k.Torso;
            Vector3 Tp(Vector3 l) => k.Pelvis + tr * l;
            k.HandR = Tp(new Vector3(0.5f - 0.05f * pump - 0.08f * kiss, 0.84f - 0.04f * pump, 0.05f + 0.1f * kiss));
            k.HandL = Tp(new Vector3(-0.5f + 0.05f * pump, 0.84f - 0.04f * pump, 0.05f));
            k.PoleR = tr * new Vector3(1f, -0.6f, -0.1f);
            k.PoleL = tr * new Vector3(-1f, -0.6f, -0.1f);
            return k;
        }
    }
}
