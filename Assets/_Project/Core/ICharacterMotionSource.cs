using UnityEngine;

namespace HideAndSeek.Core
{
    public enum LocomotionState
    {
        Idle,
        Walk,
        Run,
        CrouchIdle,
        CrouchWalk,
        Airborne
    }

    /// <summary>
    /// Contract between whatever moves a character (local player controller, network proxy, AI)
    /// and whatever visualises it (blocky character animator). Implemented on the root GameObject.
    /// </summary>
    public interface ICharacterMotionSource
    {
        /// <summary>World-space velocity in m/s.</summary>
        Vector3 Velocity { get; }

        /// <summary>Horizontal speed in m/s.</summary>
        float HorizontalSpeed { get; }

        bool IsGrounded { get; }
        bool IsCrouching { get; }
        bool IsRunning { get; }

        /// <summary>0 = standing, 1 = fully crouched (smoothly interpolated).</summary>
        float CrouchAmount { get; }

        /// <summary>Pitch of the look direction in degrees (-90 down .. 90 up). Used to tilt the head.</summary>
        float LookPitch { get; }

        LocomotionState State { get; }
    }
}
