using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Shared building blocks of the emote poses: the upright stance, planted feet, breathing, torso / head space
    /// targets, envelopes, stepping feet for turning or walking bodies and placing a whole body (pelvis + rotation).
    /// </summary>
    public partial class BlockyCharacterActions
    {
        static readonly Vector3 FootR = new Vector3(0.12f, 0f, 0.01f), FootL = new Vector3(-0.12f, 0f, -0.01f);

        /// <summary>Upright stance to build on: pelvis from the base, both soles planted (re-solved after any pelvis move).</summary>
        static CharacterPose Stance(in CharacterPose b)
        {
            var p = b;
            p.Pelvis = Quaternion.identity;
            p.Torso = Quaternion.identity;
            p.Head = Quaternion.identity;
            p.PelvisPos = new Vector3(0f, BlockyCharacter.HipHeight - 0.005f, 0f);
            return p;
        }

        static void Feet(ref CharacterPose p, Vector3 r, Vector3 l)
        {
            p.SolveLeg(0, r, new Vector3(0.12f, 0f, 1f));
            p.SolveLeg(1, l, new Vector3(-0.12f, 0f, 1f));
        }

        static void Feet(ref CharacterPose p) => Feet(ref p, FootR, FootL);

        /// <summary>Both soles at root-space points, knees towards <paramref name="fwd"/> (the body's facing on the floor).</summary>
        static void FeetAt(ref CharacterPose p, Vector3 r, Vector3 l, Vector3 fwd)
        {
            Vector3 side = Vector3.Cross(Vector3.up, fwd).normalized;
            p.SolveLeg(0, r, fwd + side * 0.12f);
            p.SolveLeg(1, l, fwd - side * 0.12f);
        }

        /// <summary>Breathing and slow weight shifts on top of any standing emote (never a frozen statue).</summary>
        void Alive(ref CharacterPose p, float t, float amount = 1f)
        {
            float b = Mathf.Sin(t * 2f * Mathf.PI * 0.27f + emoteSeed);
            float n1 = Mathf.PerlinNoise(t * 0.35f, emoteSeed) - 0.5f, n2 = Mathf.PerlinNoise(emoteSeed, t * 0.3f) - 0.5f;
            p.Torso = p.Torso * Quaternion.Euler(-1.3f * b * amount, 3f * n1 * amount, 1.5f * n2 * amount);
            p.Head = p.Head * Quaternion.Euler(1.1f * b * amount + 3f * n2 * amount, 5f * n1 * amount, 0f);
            p.PelvisPos += new Vector3(0.012f * n1, 0.003f * b, 0f) * amount;
        }

        /// <summary>A point in the head's space (origin at the neck pivot, +Z = the face) in root space.</summary>
        static Vector3 HeadPoint(in CharacterPose p, Vector3 local) =>
            p.TorsoPoint(BlockyCharacter.NeckRest) + p.TorsoRoot * (p.Head * local);

        /// <summary>Hand target in torso space with the elbow towards a torso-space direction.</summary>
        static void Arm(ref CharacterPose p, int arm, Vector3 torsoLocal, Vector3 poleLocal) =>
            p.SolveArm(arm, p.TorsoPoint(torsoLocal), p.TorsoRoot * poleLocal);

        /// <summary>Relaxed hanging arm (slightly away from the body).</summary>
        static void Hang(ref CharacterPose p, int arm, float swing = 0f)
        {
            float s = arm == 0 ? 1f : -1f;
            Arm(ref p, arm, new Vector3(0.37f * s, 0.06f, 0.03f + swing), new Vector3(0.3f * s, 0f, -1f));
        }

        /// <summary>0 -> 1 -> 0 between <paramref name="a"/> and <paramref name="b"/> (smooth both ways).</summary>
        static float Hump(float t, float a, float b) => Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - a) / (b - a)));

        static float Env(float t, float a, float b, float c, float d) => Seg(t, a, b) * (1f - Seg(t, c, d));

        /// <summary>Smoother step (zero velocity and acceleration at both ends): long turns start and stop softly.</summary>
        static float Smoother(float t, float a, float b)
        {
            float x = Mathf.Clamp01((t - a) / (b - a));
            return x * x * x * (x * (6f * x - 15f) + 10f);
        }

        /// <summary>
        /// Stepping feet for a body moving along a path parameter <paramref name="s"/> (turn angle or walked distance):
        /// a foot stays planted at the body's placement of an earlier <paramref name="s"/> and moves (lifted) only in its
        /// half of each step, so the soles never slide; at rest (s a whole number of steps) both feet are planted.
        /// </summary>
        static void StepPhase(float s, float step, int foot, out float sFoot, out float lift)
        {
            float k = s / step;
            float i = Mathf.Floor(k), f = k - i;
            float a = foot == 0 ? Seg(f, 0f, 0.5f) : Seg(f, 0.5f, 1f);
            sFoot = (i + a) * step;
            // The foot leaves the floor quickly and lands late (square root): it never drags while it moves.
            lift = Mathf.Sqrt(Mathf.Max(0f, foot == 0 ? Hump(f, 0f, 0.5f) : Hump(f, 0.5f, 1f)));
        }

        /// <summary>Puts the whole body at a root-space pelvis position / rotation (torso, head and limbs ride along).</summary>
        static void PlaceBody(ref CharacterPose p, Vector3 pelvis, Quaternion rot)
        {
            p.PelvisPos = pelvis;
            p.Pelvis = rot;
        }

        /// <summary>Hand target with the elbow at a chosen point (for elbow bumps): the forearm leaves it along <paramref name="foreDir"/>.</summary>
        static void ElbowAt(ref CharacterPose p, int arm, Vector3 elbow, Vector3 foreDir)
        {
            Vector3 sh = p.ShoulderPos(arm);
            Vector3 e = sh + (elbow - sh).normalized * RigIK.UpperArm;
            Vector3 hand = e + foreDir.normalized * RigIK.Forearm;
            Vector3 pole = e - (sh + hand) * 0.5f;
            p.SolveArm(arm, hand, pole);
        }
    }
}
