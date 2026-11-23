using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated poses: the idle motion on top of the pose (<see cref="SeatIdle"/>: breathing, weight shift, the head
    /// looking round while the player leaves the look alone, per-pose habits) and the pose's head tilt, which the first-person camera needs to sit in the tilted head's eyes
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

        float sitLookYawSeen, sitLookPitchSeen, sitLookStill, sitLookFree;

        /// <summary>Per-character seed of the idle motion: characters next to each other move out of step.</summary>
        int SitIdleSeed => sitIdleSeed != 0 ? sitIdleSeed : (sitIdleSeed = ++sitIdleSeeds * 7919 + 13);
        int sitIdleSeed;
        static int sitIdleSeeds;

        /// <summary>The own body seen from its eyes: no idle motion that would move the camera.</summary>
        bool SitIdleFirstPerson => character != null && (character.IsFirstPerson || character.HeadHidden); // seated FPP draws the body with the head hidden

        void ApplySitIdle(ref CharacterPose p) => SeatIdle.Apply(sitVariant, sitShape, ref p, sitIdleClock, SitIdleSeed, SitIdleFirstPerson);

        /// <summary>
        /// Head wandering while the player leaves the look alone (yaw, pitch offsets, degrees): fades in 2.5 s after the
        /// last look change, out at once when the look moves. Never for the own first-person view.
        /// </summary>
        Vector2 SitLookAround(float yaw, float pitch)
        {
            // Moved by more than 1.5 degrees from where it rested: the player is steering (slow smoothed changes count too).
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, sitLookYawSeen)) > 1.5f || Mathf.Abs(pitch - sitLookPitchSeen) > 1.5f)
            {
                sitLookStill = 0f;
                sitLookYawSeen = yaw;
                sitLookPitchSeen = pitch;
            }
            if (SitIdleFirstPerson) return Vector2.zero;
            return SeatIdle.LookAround(sitVariant, sitIdleClock, SitIdleSeed) * sitLookFree;
        }

        void AdvanceSitIdle(float dt)
        {
            sitIdleClock += dt;
            sitLookStill += dt;
            float free = sitLookStill > 2.5f && !SitIdleFirstPerson ? 1f : 0f;
            sitLookFree = Mathf.MoveTowards(sitLookFree, free, dt * (free > sitLookFree ? 0.5f : 4f));
            float roll = sitW > 0f ? SeatPoses.Limits(sitVariant, sitShape).Roll : 0f;
            sitRollShown = Mathf.MoveTowards(sitRollShown, roll, dt * 40f);
        }
    }
}
