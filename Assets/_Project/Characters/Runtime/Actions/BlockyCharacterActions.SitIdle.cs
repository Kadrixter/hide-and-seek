using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated pin-up poses: the small idle motion on top of the pose (<see cref="SeatPoses.Idle"/>: breathing, a
    /// swinging foot) and the pose's head tilt, which the first-person camera needs to sit in the tilted head's eyes
    /// (<see cref="SitHeadRoll"/>). The idle only turns local joints (torso, knees), so it works in any space and on
    /// top of the change from one pose to another.
    /// </summary>
    public partial class BlockyCharacterActions
    {
        float sitIdleClock, sitRollShown;

        /// <summary>Head tilt of the seated pose now (degrees, eased with the sit blend and pose changes).</summary>
        public float SitHeadRoll => sitRollShown * SitWeight;

        /// <summary>Neutral head yaw of the current pose relative to the seat (lying poses look along the sofa).</summary>
        public float SitNeutralYaw => SeatPoses.Limits(sitVariant, sitShape).Center;

        void ApplySitIdle(ref CharacterPose p) => SeatPoses.Idle(sitVariant, sitShape, ref p, sitIdleClock);

        void AdvanceSitIdle(float dt)
        {
            sitIdleClock += dt;
            if (sitIdleClock > SeatPoses.IdlePeriod) sitIdleClock -= SeatPoses.IdlePeriod; // whole cycles: no jump
            float roll = sitW > 0f ? SeatPoses.Limits(sitVariant, sitShape).Roll : 0f;
            sitRollShown = Mathf.MoveTowards(sitRollShown, roll, dt * 40f);
        }
    }
}
