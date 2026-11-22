using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated poses (lobby sofas and poufs). Root on the floor in front of the seat, facing away from it; seat top
    /// ~0.45 m, pelvis 0.5 m up. Four variants, picked by the game every time somebody sits down:
    /// 0 upright with the hands on the knees, 1 leaning back on both hands, 2 bent forward with the elbows on the knees,
    /// 3 right leg crossed over the left. The body stays still; only the head follows the look
    /// (yaw from <see cref="SetSitLook"/>, pitch from the motion source), compensating the torso's lean.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        public const int SitPoseCount = 4;

        int sitVariant;
        float sitHeadYaw;

        /// <summary>Seated pose variant (0..<see cref="SitPoseCount"/>-1) and head yaw relative to the body (degrees).</summary>
        public void SetSitLook(int variant, float headYaw)
        {
            sitVariant = Mathf.Clamp(variant, 0, SitPoseCount - 1);
            sitHeadYaw = headYaw;
        }

        public int SitVariant => sitVariant;

        CharacterPose SitPoseVariant(float lookPitch)
        {
            var p = CharacterPose.Identity;
            p.PelvisPos = new Vector3(0f, 0.5f, -0.12f);
            switch (sitVariant)
            {
                default:
                    // Upright, hands resting on the knees, feet flat a little apart.
                    p.Torso = Quaternion.Euler(-3f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.14f, 0.06f, 0.3f), new Vector3(0.1f, 0.4f, 1f));
                    p.SolveLeg(1, new Vector3(-0.14f, 0.06f, 0.3f), new Vector3(-0.1f, 0.4f, 1f));
                    p.SolveArm(0, new Vector3(0.19f, 0.58f, 0.22f), new Vector3(1f, -0.2f, -0.5f));
                    p.SolveArm(1, new Vector3(-0.19f, 0.58f, 0.22f), new Vector3(-1f, -0.2f, -0.5f));
                    break;
                case 1:
                    // Leaning back on straight arms, hands on the cushion behind, legs stretched out (one more bent).
                    p.PelvisPos = new Vector3(0f, 0.49f, -0.08f);
                    p.Pelvis = Quaternion.Euler(-8f, 0f, 0f);
                    p.Torso = Quaternion.Euler(-24f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.15f, 0.05f, 0.47f), new Vector3(0.1f, 0.5f, 1f));
                    p.SolveLeg(1, new Vector3(-0.17f, 0.05f, 0.33f), new Vector3(-0.15f, 0.5f, 1f));
                    p.SolveArm(0, new Vector3(0.25f, 0.46f, -0.42f), new Vector3(0.3f, 0.2f, -1f));
                    p.SolveArm(1, new Vector3(-0.25f, 0.46f, -0.42f), new Vector3(-0.3f, 0.2f, -1f));
                    break;
                case 2:
                    // Bent forward, elbows on the knees, hands loosely together in front, feet wide.
                    p.PelvisPos = new Vector3(0f, 0.5f, -0.1f);
                    p.Pelvis = Quaternion.Euler(8f, 0f, 0f);
                    p.Torso = Quaternion.Euler(26f, 0f, 0f);
                    p.SolveLeg(0, new Vector3(0.21f, 0.05f, 0.28f), new Vector3(0.45f, 0.3f, 1f));
                    p.SolveLeg(1, new Vector3(-0.21f, 0.05f, 0.28f), new Vector3(-0.45f, 0.3f, 1f));
                    p.SolveArm(0, new Vector3(0.05f, 0.6f, 0.37f), new Vector3(0.6f, -1f, 0.1f));
                    p.SolveArm(1, new Vector3(-0.05f, 0.62f, 0.36f), new Vector3(-0.6f, -1f, 0.1f));
                    break;
                case 3:
                    // Right leg crossed over the left knee, leaning back a bit and turned slightly; right hand on the
                    // crossed knee, left hand on the cushion beside the hip.
                    p.Pelvis = Quaternion.Euler(-3f, 4f, 0f);
                    p.Torso = Quaternion.Euler(-7f, 5f, 2f);
                    p.SolveLeg(1, new Vector3(-0.13f, 0.06f, 0.3f), new Vector3(-0.05f, 0.4f, 1f));
                    p.SolveLeg(0, new Vector3(-0.1f, 0.24f, 0.36f), new Vector3(-0.35f, 0.7f, 1f));
                    p.SolveArm(0, new Vector3(0.0f, 0.71f, 0.18f), new Vector3(1f, -0.2f, -0.4f));
                    p.SolveArm(1, new Vector3(-0.3f, 0.48f, -0.16f), new Vector3(-1f, 0f, -0.3f));
                    break;
            }

            // Head: looks where the player looks (yaw relative to the body, pitch from the camera), whatever the lean.
            float pitch = Mathf.Clamp(lookPitch, -55f, 40f);
            Quaternion look = Quaternion.Euler(0f, Mathf.Clamp(sitHeadYaw, -80f, 80f), 0f) * Quaternion.Euler(-pitch, 0f, 0f);
            p.Head = Quaternion.Inverse(p.TorsoRoot) * look;
            return p;
        }
    }
}
