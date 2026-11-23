using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Changing the seated pose without standing up (pose wheel) and the pose-wheel preview copy.
    /// <para>
    /// When the variant changes while the character is (mostly) seated, the body moves from the pose it shows now to
    /// the new one over <see cref="SitPoseBlendTime"/> s, lifting the hips a little half way (shifting the weight), so
    /// every client sees a smooth change. Sitting down from standing still snaps to the new variant (the sit-down blend
    /// hides it).
    /// </para>
    /// </summary>
    public partial class BlockyCharacterActions
    {
        public const float SitPoseBlendTime = 0.7f;
        int sitBlendFromVariant;

        CharacterPose sitLastSeat, sitBlendFrom;
        bool sitLastValid;
        float sitBlendT = 1f;

        /// <summary>True while the body moves from one seated pose to another.</summary>
        public bool SitPoseBlending => sitBlendT < 1f;

        /// <summary>
        /// Preview copy of a player (pose wheel): sits in <paramref name="poseId"/> on <paramref name="shape"/> with the
        /// character root at the seat point (seat space = root space). The first call snaps into the pose, later ones
        /// move to the new pose the same way as in the game.
        /// </summary>
        public void SetSeatedPreview(SeatShape shape, byte poseId) => SetSeatedPreview(shape, poseId, false);

        /// <summary>
        /// As <see cref="SetSeatedPreview(SeatShape, byte)"/>; <paramref name="snap"/> = jump straight into the pose
        /// (pose thumbnails: posed and rendered in the same frame).
        /// </summary>
        public void SetSeatedPreview(SeatShape shape, byte poseId, bool snap)
        {
            EnsureInit();
            bool first = !sitting || snap;
            SetSitSeat(null, shape);
            sitting = true;
            sitFromValid = false;
            if (first)
            {
                sitVariant = Mathf.Clamp(poseId, 0, SitPoseCount - 1);
                sitHeadYaw = SeatPoses.Limits(sitVariant, shape).Center;
                sitW = 1f;
                sitBlendT = 1f;
                sitLastValid = false;
            }
            else SetSitLook(poseId, SeatPoses.Limits(Mathf.Clamp(poseId, 0, SitPoseCount - 1), shape).Center);
        }

        /// <summary>Called by <see cref="SetSitLook"/> before the variant changes.</summary>
        void BeginSitPoseBlend()
        {
            if (!sitting || sitW < 0.05f || !sitLastValid) return;
            sitBlendFrom = sitLastSeat;
            sitBlendFromVariant = sitVariant;
            sitBlendT = 0f;
        }

        /// <summary>Seated pose in seat space (blended from the previous variant after a change).</summary>
        CharacterPose SitPoseSeat(float lookPitch)
        {
            Vector2 wander = SitLookAround(sitHeadYaw, lookPitch); // idle: the head looks round (clamped like the look)
            var p = SeatPoses.Evaluate(sitVariant, sitShape, sitHeadYaw + wander.x, lookPitch + wander.y);
            if (sitBlendT < 1f)
            {
                float t = ActionTimeline.SmoothStep01(sitBlendT);
                p = CharacterPose.Lerp(sitBlendFrom, p, t);
                p.PelvisPos += Vector3.up * (SeatPoses.SwitchLift(sitBlendFromVariant, sitVariant) * Mathf.Sin(Mathf.PI * sitBlendT));
            }
            sitLastSeat = p;
            sitLastValid = true;
            return p;
        }

        void AdvanceSitPoseBlend(float dt)
        {
            if (sitBlendT < 1f) sitBlendT = Mathf.Min(1f, sitBlendT + dt / SeatPoses.SwitchTime(sitBlendFromVariant, sitVariant));
            if (!sitting && sitW <= 0f) sitLastValid = false;
        }
    }
}
