using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Characters
{
    public enum DemoMotionMode
    {
        Idle,
        Walk,
        Run,
        CrouchIdle,
        CrouchWalk,
        Jump,
        CycleAll
    }

    /// <summary>
    /// Fake motion source for the character showcase. Moving modes travel around a small racetrack
    /// (two straights joined by half circles) so the gait can be judged against the ground.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DemoMotionSource : MonoBehaviour, ICharacterMotionSource
    {
        public DemoMotionMode mode = DemoMotionMode.CycleAll;
        public float walkSpeed = 2.0f;
        public float runSpeed = 5.0f;
        public float crouchSpeed = 1.2f;

        [Tooltip("Move along the racetrack. When off, speeds are reported but the object stays in place (treadmill).")]
        public bool moveAlongTrack = true;

        public float trackLength = 4.5f;
        public float trackRadius = 0.8f;

        [Tooltip("Seconds per state in CycleAll mode.")]
        public float cycleStateDuration = 3.5f;

        [Tooltip("Amplitude (deg) of a slow look up/down to show head pitch.")]
        public float lookPitchAmplitude;

        public float jumpVelocity = 6f;
        public float gravity = 18f;

        Vector3 originPos;
        Quaternion originRot;
        bool hasOrigin;
        float trackPos;
        float speed;
        float height, verticalVel;
        bool grounded = true;
        float crouch;
        float stateTime, jumpTimer, lookTime;
        int cycleIndex;
        DemoMotionMode currentState;
        Vector3 velocity;

        static readonly DemoMotionMode[] Cycle =
        {
            DemoMotionMode.Idle, DemoMotionMode.Walk, DemoMotionMode.Run, DemoMotionMode.Walk,
            DemoMotionMode.CrouchIdle, DemoMotionMode.CrouchWalk, DemoMotionMode.Idle, DemoMotionMode.Jump
        };

        public Vector3 Velocity => velocity;
        public float HorizontalSpeed => speed;
        public bool IsGrounded => grounded;
        public bool IsCrouching => crouch > 0.5f;
        public bool IsRunning => currentState == DemoMotionMode.Run;
        public float CrouchAmount => crouch;
        public float LookPitch { get; private set; }

        public LocomotionState State
        {
            get
            {
                if (!grounded) return LocomotionState.Airborne;
                bool moving = speed > 0.1f;
                if (crouch > 0.5f) return moving ? LocomotionState.CrouchWalk : LocomotionState.CrouchIdle;
                if (!moving) return LocomotionState.Idle;
                return IsRunning ? LocomotionState.Run : LocomotionState.Walk;
            }
        }

        void Awake()
        {
            CaptureOrigin();
        }

        void CaptureOrigin()
        {
            originPos = transform.position;
            originRot = transform.rotation;
            hasOrigin = true;
            currentState = mode == DemoMotionMode.CycleAll ? Cycle[0] : mode;
            if (UsesTrack) ApplyTrackPose(0f);
        }

        void Update()
        {
            Step(Time.deltaTime);
        }

        bool UsesTrack => moveAlongTrack && (mode == DemoMotionMode.CycleAll || IsMovingMode(mode));

        static bool IsMovingMode(DemoMotionMode m) =>
            m == DemoMotionMode.Walk || m == DemoMotionMode.Run || m == DemoMotionMode.CrouchWalk;

        /// <summary>Advances the fake motion by dt seconds.</summary>
        public void Step(float dt)
        {
            if (!hasOrigin) CaptureOrigin();
            if (dt <= 0f) return;

            stateTime += dt;
            if (mode == DemoMotionMode.CycleAll)
            {
                if (stateTime >= cycleStateDuration && grounded)
                {
                    stateTime = 0f;
                    cycleIndex = (cycleIndex + 1) % Cycle.Length;
                }

                currentState = Cycle[cycleIndex];
            }
            else
            {
                currentState = mode;
            }

            float targetSpeed = 0f;
            bool wantCrouch = false;
            switch (currentState)
            {
                case DemoMotionMode.Walk: targetSpeed = walkSpeed; break;
                case DemoMotionMode.Run: targetSpeed = runSpeed; break;
                case DemoMotionMode.CrouchIdle: wantCrouch = true; break;
                case DemoMotionMode.CrouchWalk: targetSpeed = crouchSpeed; wantCrouch = true; break;
            }

            // Accelerate like a player controller would (fast but not instant).
            float accel = targetSpeed > speed ? 12f : 16f;
            speed = Mathf.MoveTowards(speed, targetSpeed, accel * dt);
            crouch = Mathf.MoveTowards(crouch, wantCrouch ? 1f : 0f, dt / 0.22f);

            // Jumping.
            if (currentState == DemoMotionMode.Jump)
            {
                jumpTimer += dt;
                if (grounded && jumpTimer > 1.3f)
                {
                    jumpTimer = 0f;
                    grounded = false;
                    verticalVel = jumpVelocity;
                }
            }
            else
            {
                jumpTimer = 0.9f;
            }

            if (!grounded)
            {
                verticalVel -= gravity * dt;
                height += verticalVel * dt;
                if (height <= 0f)
                {
                    height = 0f;
                    grounded = true;
                }
            }

            float vy = grounded ? 0f : verticalVel;

            if (UsesTrack)
            {
                trackPos += speed * dt;
                Vector3 tangent = ApplyTrackPose(trackPos);
                velocity = tangent * speed + Vector3.up * vy;
            }
            else
            {
                transform.position = originPos + Vector3.up * height;
                velocity = transform.forward * speed + Vector3.up * vy;
            }

            lookTime += dt;
            LookPitch = lookPitchAmplitude * Mathf.Sin(lookTime * 0.6f);
        }

        /// <summary>Places the object on the racetrack and returns the world-space tangent.</summary>
        Vector3 ApplyTrackPose(float s)
        {
            float l = trackLength, r = trackRadius;
            float total = 2f * l + 2f * Mathf.PI * r;
            s = Mathf.Repeat(s, total);
            Vector3 p, t;
            if (s < l)
            {
                p = new Vector3(r, 0f, -l * 0.5f + s);
                t = Vector3.forward;
            }
            else if (s < l + Mathf.PI * r)
            {
                float a = (s - l) / r;
                p = new Vector3(0f, 0f, l * 0.5f) + new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a));
                t = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
            else if (s < 2f * l + Mathf.PI * r)
            {
                p = new Vector3(-r, 0f, l * 0.5f - (s - l - Mathf.PI * r));
                t = Vector3.back;
            }
            else
            {
                float a = (s - 2f * l - Mathf.PI * r) / r + Mathf.PI;
                p = new Vector3(0f, 0f, -l * 0.5f) + new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a));
                t = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            }

            Vector3 worldT = originRot * t;
            transform.SetPositionAndRotation(originPos + originRot * p + Vector3.up * height, Quaternion.LookRotation(worldT, Vector3.up));
            return worldT;
        }
    }
}
