using System;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>One frame of movement intent.</summary>
    public struct MotorCommand
    {
        /// <summary>x = strafe, y = forward, clamped to magnitude 1.</summary>
        public Vector2 Move;
        /// <summary>Degrees this frame (x = yaw, y = pitch up).</summary>
        public Vector2 Look;
        public bool Run;
        public bool Crouch;
        /// <summary>Lie down and crawl (toggle state from the input reader).</summary>
        public bool Prone;
        public bool Jump;
    }

    /// <summary>
    /// First-person CharacterController motor: walk / run / crouch / jump with acceleration,
    /// air control, coyote time, ground snapping for stairs and ramps, and smooth crouch.
    /// Owns yaw (root rotation) and pitch (<see cref="LookPitch"/>, applied by <see cref="HeadBob"/>).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour, ICharacterMotionSource
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader input;

        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float runSpeed = 5.8f;
        [SerializeField] private float crouchSpeed = 1.5f;

        [Header("Acceleration")]
        [SerializeField] private float groundAcceleration = 18f;
        [SerializeField] private float groundDeceleration = 22f;
        [Tooltip("Acceleration available while airborne (air control).")]
        [SerializeField] private float airAcceleration = 5f;
        [Tooltip("Run is only allowed when the forward input exceeds this value.")]
        [SerializeField, Range(0f, 1f)] private float runForwardThreshold = 0.1f;

        [Header("Jump & Gravity")]
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float jumpHeight = 1.05f;
        [SerializeField] private float maxFallSpeed = 40f;
        [Tooltip("Grace period after leaving a ledge in which a jump is still allowed.")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("Downward velocity kept while grounded so the controller hugs the floor.")]
        [SerializeField] private float groundStickVelocity = 2f;
        [Tooltip("Max distance the controller snaps down to stay on stairs/ramps when walking down.")]
        [SerializeField] private float groundSnapDistance = 0.45f;

        [Header("Capsule")]
        [SerializeField] private float standHeight = 1.8f;
        [SerializeField] private float crouchHeight = 1.1f;
        [SerializeField] private float standEyeHeight = 1.62f;
        [SerializeField] private float crouchEyeHeight = 0.96f;
        [Tooltip("Seconds for a full stand <-> crouch transition.")]
        [SerializeField] private float crouchTransitionTime = 0.22f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Look")]
        [SerializeField] private float minPitch = -88f;
        [SerializeField] private float maxPitch = 88f;

        [Header("Footsteps (stride length per step, metres)")]
        [Tooltip("Distance per step while walking. Bob frequency = speed / step length.")]
        [SerializeField] private float walkStepLength = 1.35f;
        [SerializeField] private float runStepLength = 2.05f;
        [SerializeField] private float crouchStepLength = 0.85f;

        private CharacterController _cc;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private Vector3 _velocity;
        private bool _grounded;
        private float _lastGroundedTime = float.NegativeInfinity;
        private bool _jumpedSinceGrounded;
        private Vector3 _groundNormal = Vector3.up;
        private float _crouchLinear;     // 0..1 linear progress
        private float _crouchAmount;     // eased
        private bool _wantsCrouch;
        private bool _running;
        private float _pitch;
        private float _stepPhase;
        private LocomotionState _state;
        private float _clock;
        private readonly Collider[] _overlapBuffer = new Collider[16];
        private readonly RaycastHit[] _castBuffer = new RaycastHit[16];
        private float _baseStepOffset = -1f;

        // ---------------------------------------------------------------- events

        /// <summary>Fired when a foot hits the ground (in sync with the head bob low point). Arg: intensity 0..1.</summary>
        public event Action<float> Footstep;

        /// <summary>Fired when landing after being airborne. Arg: downward impact speed in m/s.</summary>
        public event Action<float> Landed;

        /// <summary>Fired when a jump starts.</summary>
        public event Action Jumped;

        /// <summary>Fired when <see cref="IsCrouching"/> changes (true = crouched down).</summary>
        public event Action<bool> CrouchChanged;

        private bool _wasCrouchingEvent;

        // ---------------------------------------------------------------- ICharacterMotionSource

        public Vector3 Velocity => _velocity;
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;
        public bool IsGrounded => _grounded;
        public bool IsCrouching => _wantsCrouch || _crouchAmount > 0.5f;
        public bool IsRunning => _running;
        public float CrouchAmount => _crouchAmount;
        public float LookPitch => _pitch;
        public LocomotionState State => _state;

        // ---------------------------------------------------------------- extra API

        public PlayerInputReader Input => input;
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float CrouchSpeed => crouchSpeed;
        public float LookYaw => transform.eulerAngles.y;

        /// <summary>Camera height above the feet (interpolated with crouch).</summary>
        public float EyeHeight => Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(standEyeHeight, crouchEyeHeight, _crouchAmount), proneEyeHeight, _proneAmount), seatedEyeHeight, _seatBlend), Mathf.Lerp(downedEyeHeight, 0.82f, _sitUpBlend), _downedBlend);

        /// <summary>Kneel / crouch regardless of input (e.g. while injecting someone on the floor).</summary>
        public bool ForceCrouch { get; set; }

        /// <summary>Movement speed multiplier (injured hiders limp slower).</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>Downed but sitting up (bandaging the own wounds): camera rises to a sitting height.</summary>
        public bool DownedSitting { get; set; }
        private float _sitUpBlend;

        /// <summary>Voluntary crawl: lying on the belly (true while lying or getting down).</summary>
        public bool IsProne => _wantsProne || _proneAmount > 0.5f;

        /// <summary>0 = upright (crouch / stand), 1 = flat on the floor.</summary>
        public float ProneAmount => _proneAmount;

        /// <summary>Fired when <see cref="IsProne"/> changes.</summary>
        public event Action<bool> ProneChanged;

        [Header("Prone (crawl)")]
        [SerializeField] private float proneHeight = 0.62f;
        [SerializeField] private float proneEyeHeight = 0.42f;
        [SerializeField] private float proneSpeed = 0.95f;
        [SerializeField] private float proneTransitionTime = 0.55f;
        private float _proneLinear, _proneAmount;
        private bool _wantsProne, _wasProneEvent;
        private bool _remoteProne;
        private float _remoteProneAmount;
        private float _seatBlend;

        /// <summary>Sitting on a seat: camera lowered (movement is locked by the caller).</summary>
        public bool Seated { get; set; }
        [SerializeField] private float seatedEyeHeight = 1.12f;
        private float _downedBlend;

        /// <summary>Continuous step counter; increases by 1 per footstep. Footfall happens at integer values.</summary>
        public float StepPhase => _stepPhase;

        /// <summary>
        /// Optional command source (bots, replays, tests). When set, <see cref="PlayerInputReader"/> is ignored.
        /// Jump = "jump requested" (the provider handles its own buffering).
        /// </summary>
        public Func<MotorCommand> CommandProvider { get; set; }

        /// <summary>When false the motor ignores input (gravity still applies).</summary>
        public bool MovementEnabled { get; set; } = true;

        /// <summary>When false mouse look is ignored.</summary>
        public bool LookEnabled { get; set; } = true;

        /// <summary>Instantly moves the player (e.g. spawn). Resets velocity.</summary>
        public void Teleport(Vector3 position, float yaw, float pitch = 0f)
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            bool was = _cc.enabled;
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            _cc.enabled = was;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            _velocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        public void SetLook(float yaw, float pitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // ---------------------------------------------------------------- lifecycle

        private void Reset()
        {
            input = GetComponent<PlayerInputReader>();
            var cc = GetComponent<CharacterController>();
            cc.height = standHeight;
            cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
            cc.radius = 0.3f;
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 46f;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (_baseStepOffset < 0f) _baseStepOffset = _cc.stepOffset;
            ApplyCapsuleHeight();
        }

        private void Update()
        {
            if (IsRemoteProxy) TickRemote(Time.deltaTime);
            else Tick(Time.deltaTime);
        }

        // ---------------------------------------------------------------- downed / network proxy

        /// <summary>Downed: forced prone crawl (slow, no run/jump, camera near the floor).</summary>
        public bool Downed { get; set; }
        [SerializeField] private float crawlSpeed = 0.7f;
        [SerializeField] private float downedEyeHeight = 0.38f;

        /// <summary>True for other players' copies: no simulation, state comes from the network.</summary>
        public bool IsRemoteProxy { get; private set; }

        private Vector3 _remoteLastPos;
        private bool _remoteHasPos;
        private bool _remoteGrounded = true, _remoteCrouch, _remoteRun;
        private float _remoteCrouchAmount, _remotePitch;

        public void SetRemoteProxy(bool on)
        {
            IsRemoteProxy = on;
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (on && _cc != null) _cc.enabled = false;
            _remoteHasPos = false;
        }

        /// <summary>Network state for a remote proxy (position itself comes from the NetworkTransform).</summary>
        public void ApplyRemoteState(bool grounded, bool crouching, float crouchAmount, bool running, float pitch, bool downed, bool prone = false, float proneAmount = 0f)
        {
            _remoteProne = prone;
            _remoteProneAmount = proneAmount;
            _remoteGrounded = grounded;
            _remoteCrouch = crouching;
            _remoteCrouchAmount = crouchAmount;
            _remoteRun = running;
            _remotePitch = pitch;
            Downed = downed;
        }

        private void TickRemote(float dt)
        {
            if (dt <= 0f) return;
            Vector3 pos = transform.position;
            if (!_remoteHasPos) { _remoteLastPos = pos; _remoteHasPos = true; }
            Vector3 raw = (pos - _remoteLastPos) / dt;
            _remoteLastPos = pos;
            if (raw.sqrMagnitude > 30f * 30f) raw = Vector3.zero; // teleport
            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-dt * 12f));

            bool wasGrounded = _grounded;
            float vy = _velocity.y;
            _grounded = _remoteGrounded;
            if (_grounded && !wasGrounded && -vy > 1f) Landed?.Invoke(-vy);
            if (!_grounded && wasGrounded && vy > 1.5f) Jumped?.Invoke();

            _crouchAmount = Downed ? 1f : _remoteCrouchAmount;
            _wantsCrouch = _remoteCrouch || Downed;
            _wantsProne = _remoteProne && !Downed;
            _proneAmount = Downed ? 0f : Mathf.MoveTowards(_proneAmount, _remoteProneAmount, dt * 4f);
            FireStanceEvents();
            _pitch = Mathf.LerpAngle(_pitch, _remotePitch, 1f - Mathf.Exp(-dt * 15f));
            _running = _remoteRun;
            float hSpeed = HorizontalSpeed;
            UpdateSteps(hSpeed, dt);
            UpdateState(hSpeed);
        }

        private MotorCommand GatherCommand()
        {
            if (CommandProvider != null) return CommandProvider();
            if (input == null) return default;
            return new MotorCommand
            {
                Move = input.Move,
                Look = input.LookDelta,
                Run = input.RunHeld,
                Crouch = input.CrouchActive,
                Prone = input.ProneActive,
                Jump = input.JumpBuffered,
            };
        }

        /// <summary>Advances the simulation by <paramref name="dt"/> seconds (called from Update; public for tests/bots).</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_cc == null) Awake();
            _clock += dt;

            MotorCommand cmd = GatherCommand();
            bool hasInput = MovementEnabled;
            Vector2 move = hasInput ? Vector2.ClampMagnitude(cmd.Move, 1f) : Vector2.zero;

            // --- look
            if (LookEnabled)
            {
                transform.Rotate(0f, cmd.Look.x, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch + cmd.Look.y, minPitch, maxPitch);
            }

            // --- crouch
            // Stance commands are not gated by MovementEnabled: a toggled crouch / crawl survives menus and cutscenes.
            bool proneCmd = cmd.Prone && !Downed && !Seated;
            // Lying down needs a grounded player; getting up needs room for the crouched capsule.
            if (proneCmd && !_wantsProne && !_grounded) proneCmd = false;
            if (!proneCmd && _wantsProne && _proneLinear > 0.01f && !CanFitAt(transform.position, crouchHeight)) proneCmd = true;
            _wantsProne = proneCmd;
            _wantsCrouch = (cmd.Crouch && !Seated) || Downed || _wantsProne || ForceCrouch;
            _sitUpBlend = Mathf.MoveTowards(_sitUpBlend, Downed && DownedSitting ? 1f : 0f, dt * 1.6f);
            _downedBlend = Mathf.MoveTowards(_downedBlend, Downed ? 1f : 0f, dt * 1.5f);
            _seatBlend = Mathf.MoveTowards(_seatBlend, Seated ? 1f : 0f, dt * 3f);
            if (!_cc.enabled)
            {
                // Seated / dead: controller off, no physics movement (look still works).
                _velocity = Vector3.zero;
                _horizontalVelocity = Vector3.zero;
                _verticalVelocity = 0f;
                UpdateCrouch(dt);
                UpdateState(0f);
                return;
            }
            UpdateCrouch(dt);
            UpdateStepOffset();
            FireStanceEvents();

            // --- target velocity
            bool crouching = IsCrouching;
            bool wantsRun = hasInput && !Downed && cmd.Run && !crouching && move.y > runForwardThreshold;
            float targetSpeed = Downed ? crawlSpeed : crouching ? Mathf.Lerp(walkSpeed, crouchSpeed, Mathf.Clamp01(_crouchAmount * 2f))
                              : wantsRun ? runSpeed : walkSpeed;
            targetSpeed *= SpeedScale;
            if (!Downed && _proneAmount > 0f)
            {
                // Lying down / getting up: barely moving; flat: slow crawl.
                float pa = _proneAmount;
                targetSpeed = pa > 0.95f ? proneSpeed : Mathf.Lerp(targetSpeed, 0.25f, Mathf.Clamp01(pa * 1.5f));
            }
            Vector3 wishDir = transform.right * move.x + transform.forward * move.y;
            Vector3 targetVelocity = wishDir * targetSpeed;

            if (_grounded)
            {
                float rate = targetVelocity.sqrMagnitude > 0.0001f &&
                             Vector3.Dot(targetVelocity, _horizontalVelocity) >= 0f
                    ? groundAcceleration : groundDeceleration;
                // Faster deceleration when slowing down to a lower target (e.g. releasing run).
                if (targetVelocity.sqrMagnitude < _horizontalVelocity.sqrMagnitude) rate = Mathf.Max(rate, groundDeceleration);
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * dt);
            }
            else if (wishDir.sqrMagnitude > 0.0001f)
            {
                // Air control: steer toward the wish velocity but never gain speed beyond max(target, current).
                float maxSpeed = Mathf.Max(_horizontalVelocity.magnitude, targetSpeed);
                _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, airAcceleration * dt);
                _horizontalVelocity = Vector3.ClampMagnitude(_horizontalVelocity, maxSpeed);
            }

            // --- jump (coyote + buffer)
            bool canJump = !Downed && !_wantsProne && _proneAmount < 0.05f && !_jumpedSinceGrounded && _clock - _lastGroundedTime <= coyoteTime && !crouching;
            if (hasInput && cmd.Jump && canJump)
            {
                if (CommandProvider == null && input != null) input.ConsumeJump();
                _verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                _jumpedSinceGrounded = true;
                _grounded = false;
                Jumped?.Invoke();
            }

            // --- gravity
            if (_grounded && _verticalVelocity <= 0f) _verticalVelocity = -groundStickVelocity;
            else _verticalVelocity = Mathf.Max(_verticalVelocity - gravity * dt, -maxFallSpeed);

            // --- slide down too-steep slopes
            Vector3 horizontal = _horizontalVelocity;
            if (_grounded && Vector3.Angle(_groundNormal, Vector3.up) > _cc.slopeLimit + 1f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized * 4f;
                horizontal += new Vector3(slide.x, 0f, slide.z);
            }

            // --- move
            Vector3 startPos = transform.position;
            float preMoveVertical = _verticalVelocity;
            bool wasGrounded = _grounded;
            _groundNormal = Vector3.up;
            Vector3 displacement = (horizontal + Vector3.up * _verticalVelocity) * dt;
            CollisionFlags flags = _cc.Move(displacement);

            if ((flags & CollisionFlags.Above) != 0 && _verticalVelocity > 0f) _verticalVelocity = 0f;

            bool groundedNow = _cc.isGrounded;
            if (!groundedNow && wasGrounded && _verticalVelocity <= 0f) groundedNow = TrySnapToGround();
            _grounded = groundedNow;

            if (_grounded)
            {
                if (!wasGrounded)
                {
                    float impact = -preMoveVertical;
                    if (impact > 1.0f) Landed?.Invoke(impact);
                }
                _lastGroundedTime = _clock;
                _jumpedSinceGrounded = false;
                if (_verticalVelocity < 0f) _verticalVelocity = -groundStickVelocity;
            }

            // Kill horizontal velocity lost to walls so we don't keep pushing.
            Vector3 actual = (transform.position - startPos) / dt;
            Vector3 actualHorizontal = new Vector3(actual.x, 0f, actual.z);
            if (actualHorizontal.sqrMagnitude < _horizontalVelocity.sqrMagnitude && (flags & CollisionFlags.Sides) != 0)
                _horizontalVelocity = Vector3.ClampMagnitude(_horizontalVelocity, actualHorizontal.magnitude + 0.05f);
            _velocity = actual;

            // --- running flag and steps
            float hSpeed = actualHorizontal.magnitude;
            _running = wantsRun && hSpeed > walkSpeed * 0.5f;
            UpdateSteps(hSpeed, dt);
            UpdateState(hSpeed);
        }

        // ---------------------------------------------------------------- internals

        private void FireStanceEvents()
        {
            if (IsCrouching != _wasCrouchingEvent)
            {
                _wasCrouchingEvent = IsCrouching;
                CrouchChanged?.Invoke(_wasCrouchingEvent);
            }
            if (IsProne != _wasProneEvent)
            {
                _wasProneEvent = IsProne;
                ProneChanged?.Invoke(_wasProneEvent);
            }
        }

        private void UpdateCrouch(float dt)
        {
            float pTarget = _wantsProne ? 1f : 0f;
            _proneLinear = Mathf.MoveTowards(_proneLinear, pTarget, dt / Mathf.Max(0.01f, proneTransitionTime));
            float pe = Mathf.SmoothStep(0f, 1f, _proneLinear);
            if (!Mathf.Approximately(pe, _proneAmount))
            {
                _proneAmount = pe;
                ApplyCapsuleHeight();
            }

            float target = _wantsCrouch ? 1f : 0f;
            if (target < _crouchLinear && !CanStandAt(transform.position)) target = _crouchLinear;
            _crouchLinear = Mathf.MoveTowards(_crouchLinear, target, dt / Mathf.Max(0.01f, crouchTransitionTime));
            float eased = Mathf.SmoothStep(0f, 1f, _crouchLinear);
            if (!Mathf.Approximately(eased, _crouchAmount))
            {
                _crouchAmount = eased;
                ApplyCapsuleHeight();
            }
        }

        private void ApplyCapsuleHeight()
        {
            if (_cc == null) return;
            float h = Mathf.Lerp(Mathf.Lerp(standHeight, crouchHeight, _crouchAmount), proneHeight, _proneAmount);
            _cc.height = h;
            _cc.center = new Vector3(0f, h * 0.5f, 0f);
        }

        /// <summary>
        /// The CharacterController lifts by stepOffset before moving sideways, which would make it snag on low
        /// ceilings (e.g. a 1.3 m crawl tunnel while crouched). Limit the step to the free headroom.
        /// </summary>
        private void UpdateStepOffset()
        {
            // Slightly fatter than the capsule and nudged toward the movement direction so ceilings we are about
            // to walk under are detected before the controller's step-up pass hits their edge.
            float r = _cc.radius + 0.08f;
            Vector3 lead = _horizontalVelocity.sqrMagnitude > 0.0001f ? _horizontalVelocity.normalized * 0.12f : Vector3.zero;
            Vector3 origin = transform.position + lead + Vector3.up * (_cc.height - _cc.radius);
            float maxDist = _baseStepOffset + _cc.skinWidth + 0.05f;
            int n = Physics.SphereCastNonAlloc(origin, r, Vector3.up, _castBuffer, maxDist, obstacleMask, QueryTriggerInteraction.Ignore);
            float headroom = maxDist;
            for (int i = 0; i < n; i++)
            {
                var h = _castBuffer[i];
                if (h.collider == _cc || h.collider.transform.IsChildOf(transform) || h.distance <= 0f) continue;
                headroom = Mathf.Min(headroom, h.distance);
            }
            _cc.stepOffset = Mathf.Clamp(headroom - _cc.skinWidth - 0.03f, 0.01f, _baseStepOffset);
        }

        /// <summary>Capsule overlap test for the full standing capsule at <paramref name="feet"/>.</summary>
        private bool CanStandAt(Vector3 feet) => CanFitAt(feet, standHeight);

        /// <summary>Capsule overlap test for a capsule of <paramref name="height"/> at <paramref name="feet"/>.</summary>
        private bool CanFitAt(Vector3 feet, float height)
        {
            if (_cc == null) return true;
            float r = _cc.radius - 0.02f;
            Vector3 bottom = feet + Vector3.up * (r + _cc.skinWidth + 0.05f);
            Vector3 top = feet + Vector3.up * (height - r);
            int n = Physics.OverlapCapsuleNonAlloc(bottom, top, r, _overlapBuffer, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = _overlapBuffer[i];
                if (c == _cc || c.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Keeps the controller glued to stairs / ramp crests when walking down: sweeps the capsule down by up to
        /// <see cref="groundSnapDistance"/>; if no walkable ground is found the move is reverted (real ledge).
        /// </summary>
        private bool TrySnapToGround()
        {
            Vector3 before = transform.position;
            _groundNormal = Vector3.up;
            _cc.Move(Vector3.down * groundSnapDistance);
            if (_cc.isGrounded && Vector3.Angle(_groundNormal, Vector3.up) <= _cc.slopeLimit + 1f)
                return true;

            // Nothing walkable within reach: undo the probe.
            _cc.enabled = false;
            transform.position = before;
            _cc.enabled = true;
            _groundNormal = Vector3.up;
            return false;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Track the most upward-facing surface beneath us.
            if (hit.normal.y > 0.1f && hit.point.y < transform.position.y + _cc.radius)
            {
                if (_groundNormal == Vector3.up || hit.normal.y > _groundNormal.y) _groundNormal = hit.normal;
            }
        }

        private void UpdateSteps(float hSpeed, float dt)
        {
            if (!_grounded || hSpeed < 0.05f) return;

            // Stride length blends with speed between crouch, walk and run.
            float stride;
            if (hSpeed <= walkSpeed) stride = walkStepLength;
            else stride = Mathf.Lerp(walkStepLength, runStepLength, Mathf.InverseLerp(walkSpeed, runSpeed, hSpeed));
            stride = Mathf.Lerp(stride, crouchStepLength, _crouchAmount);

            float before = _stepPhase;
            _stepPhase += hSpeed * dt / Mathf.Max(0.1f, stride);
            if (Mathf.Floor(_stepPhase) > Mathf.Floor(before))
            {
                float intensity = Mathf.Clamp01(hSpeed / runSpeed);
                intensity *= Mathf.Lerp(1f, 0.35f, _crouchAmount);
                Footstep?.Invoke(intensity);
            }
            // Keep the phase small to preserve float precision (period of 2 steps keeps bob continuous).
            if (_stepPhase > 10000f) _stepPhase -= 10000f;
        }

        private void UpdateState(float hSpeed)
        {
            bool moving = hSpeed > 0.15f;
            if (!_grounded) _state = LocomotionState.Airborne;
            else if (IsCrouching) _state = moving ? LocomotionState.CrouchWalk : LocomotionState.CrouchIdle;
            else if (!moving) _state = LocomotionState.Idle;
            else _state = _running ? LocomotionState.Run : LocomotionState.Walk;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            crouchHeight = Mathf.Clamp(crouchHeight, 0.6f, standHeight);
            crouchEyeHeight = Mathf.Min(crouchEyeHeight, crouchHeight - 0.05f);
            standEyeHeight = Mathf.Min(standEyeHeight, standHeight - 0.05f);
        }
#endif
    }
}
