using UnityEngine;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Seated poses (lobby sofas and poufs), see <see cref="SeatPoses"/>: fitted to the seat's real shape (thighs
    /// on the cushion, shins clear of the front, back against the backrest), picked by the game every time somebody
    /// sits down. The body stays still; only the head follows the look (yaw from <see cref="SetSitLook"/>, pitch
    /// from the motion source) within the pose's limits.
    /// <para>
    /// The pose is anchored to the seat (<see cref="SetSitSeat"/>), not to the character root, so the body stays
    /// on the seat while the player root jumps on and off it: sitting down blends from where the player stood
    /// (<see cref="BeginSitFrom"/>) to the seat, standing up from the seat to the new root.
    /// </para>
    /// </summary>
    public partial class BlockyCharacterActions
    {
        public const int SitPoseCount = SeatPoses.Count;
        const float SitOutTime = 0.45f;

        int sitVariant;
        float sitHeadYaw;
        Transform sitAnchor;
        SeatShape sitShape;
        bool sitFromValid;
        Vector3 sitFromPos;
        Quaternion sitFromRot = Quaternion.identity;
        float sitInTime = 0.35f;

        /// <summary>Seated pose variant (0..<see cref="SitPoseCount"/>-1) and head yaw relative to the body (degrees).</summary>
        public void SetSitLook(int variant, float headYaw)
        {
            sitVariant = Mathf.Clamp(variant, 0, SitPoseCount - 1);
            sitHeadYaw = headYaw;
        }

        public int SitVariant => sitVariant;

        /// <summary>Shape of the current / last seat (null = generic 0.45 m seat at the root).</summary>
        public SeatShape SitShape => sitShape;

        /// <summary>The seat transform the pose is anchored to (null = the character root).</summary>
        public Transform SitAnchor => sitAnchor;

        /// <summary>Seat used now (or just left): the seated body is placed relative to it.</summary>
        public void SetSitSeat(Transform anchor, SeatShape shape)
        {
            sitAnchor = anchor;
            sitShape = shape;
        }

        /// <summary>
        /// Sitting down: the standing body starts at this world pose (where the root was before it was moved onto the
        /// seat) and walks into the seat; the blend takes longer the further away it was.
        /// </summary>
        public void BeginSitFrom(Vector3 worldPos, Quaternion worldRot)
        {
            sitFromPos = worldPos;
            sitFromRot = worldRot;
            sitFromValid = true;
            Vector3 seat = sitAnchor != null ? sitAnchor.position : transform.position;
            Vector3 d = worldPos - seat;
            d.y = 0f;
            sitInTime = Mathf.Clamp(0.4f + 0.22f * d.magnitude, 0.4f, 0.9f);
        }

        /// <summary>How long the current sit-down blend takes (set by <see cref="BeginSitFrom"/>).</summary>
        public float SitInTime => sitInTime;

        /// <summary>Visual weight of the seated pose (0 standing .. 1 seated, eased).</summary>
        public float SitWeight => ActionTimeline.SmoothStep01(sitW);

        /// <summary>Seated pose in character-root space (anchored to the seat when known).</summary>
        CharacterPose SitPoseRoot(float lookPitch)
        {
            var p = SeatPoses.Evaluate(sitVariant, sitShape, sitHeadYaw, lookPitch);
            if (sitAnchor == null) return p;
            Quaternion toRoot = Quaternion.Inverse(transform.rotation) * sitAnchor.rotation;
            p.PelvisPos = transform.InverseTransformPoint(sitAnchor.position) + toRoot * p.PelvisPos;
            p.Pelvis = toRoot * p.Pelvis;
            return p;
        }

        /// <summary>Sitting down: moves the (standing) base pose back to where the player stood.</summary>
        void ShiftToSitFrom(ref CharacterPose p)
        {
            if (!sitFromValid || !sitting) return;
            Quaternion q = Quaternion.Inverse(transform.rotation) * sitFromRot;
            q = Quaternion.Euler(0f, q.eulerAngles.y, 0f);
            p.PelvisPos = transform.InverseTransformPoint(sitFromPos) + q * p.PelvisPos;
            p.Pelvis = q * p.Pelvis;
        }

        void AdvanceSit(float dt, bool canSit)
        {
            float time = canSit ? sitInTime : SitOutTime;
            sitW = Mathf.MoveTowards(sitW, canSit ? 1f : 0f, dt / time);
            if (sitW >= 1f || !sitting) sitFromValid = false;
        }
    }
}
