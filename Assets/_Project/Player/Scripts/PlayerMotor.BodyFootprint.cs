using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Footprint of the body outside the upright capsule. Lying on the belly (crawl or downed) the hips are 0.6 m and
    /// the boots 1.3 m behind the root, crouched the hips and the back foot stick out a little. The owner keeps that part
    /// out of walls: a wall behind pushes the player forward (backing up stops at the wall, turning near a wall slides
    /// the head away from it), a turn that cannot be resolved that way is refused, and lying down needs room for the
    /// body. The synced position is therefore valid for every peer; the animation folds the legs as a last resort.
    /// </summary>
    public partial class PlayerMotor
    {
        /// <summary>How far the lying body reaches behind the root (boots of the straight legs plus a margin).</summary>
        private const float LyingReach = 1.36f;

        /// <summary>How far the crouched body reaches behind the root (hips sat back, the rear foot of a stride).</summary>
        private const float CrouchReach = 0.4f;

        /// <summary>Shortest room behind the root for lying down (hips plus folded legs, see the leg fold of the rig).</summary>
        private const float MinLyingRoom = 1.0f;

        /// <summary>Current reach of the body behind the root, from the stance blends.</summary>
        private float BodyReachBehind()
        {
            float r = _cc.radius;
            float lying = Mathf.Max(Mathf.Clamp01(_proneAmount * 1.6f), _downedBlend);
            float reach = Mathf.Lerp(r, LyingReach, lying);
            return Mathf.Max(reach, Mathf.Lerp(r, CrouchReach, _crouchAmount));
        }

        /// <summary>How much of <paramref name="reach"/> behind the root (with the body turned to <paramref name="rot"/>) is inside a wall.</summary>
        private float BodyDeficit(Quaternion rot, float reach) =>
            reach - BodyRoom.Free(transform.position, rot * Vector3.back, reach + 0.05f, transform);

        /// <summary>
        /// Called after the controller moved: pushes the player forward until the body behind fits, cancels the
        /// velocity into the wall and undoes this frame's turn when it would still leave the legs inside a wall.
        /// </summary>
        private void KeepBodyOutOfWalls(Quaternion rotBeforeLook)
        {
            if (!BodyRoom.Enabled || !BodyRoom.MotorPush || _cc == null || !_cc.enabled) return;
            float reach = BodyReachBehind();
            if (reach <= _cc.radius + 0.02f) return;
            float deficit = BodyDeficit(transform.rotation, reach);
            if (deficit <= 0.005f) return;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) return;
            fwd.Normalize();
            _cc.Move(fwd * Mathf.Min(deficit, 0.6f) + Vector3.down * 0.002f);
            float into = Vector3.Dot(_horizontalVelocity, -fwd);
            if (into > 0f) _horizontalVelocity += fwd * into;

            if (Quaternion.Angle(rotBeforeLook, transform.rotation) < 0.01f) return;
            float left = BodyDeficit(transform.rotation, reach);
            if (left > 0.02f && BodyDeficit(rotBeforeLook, reach) < left) transform.rotation = rotBeforeLook;
        }

        /// <summary>
        /// Lying down needs <see cref="MinLyingRoom"/> behind the root, counting the room in front the motor can push
        /// the player into. Starting a crawl in a cupboard would otherwise put the hips through its back wall.
        /// </summary>
        private bool RoomToLieDown()
        {
            if (!BodyRoom.Enabled || !BodyRoom.MotorPush || _cc == null) return true;
            float back = BodyRoom.Free(transform.position, -transform.forward, LyingReach, transform);
            if (back >= MinLyingRoom) return true;
            float ahead = BodyRoom.Free(transform.position, transform.forward, 1.5f, transform) - _cc.radius - _cc.skinWidth;
            return back + Mathf.Max(0f, ahead) >= MinLyingRoom;
        }
    }
}
