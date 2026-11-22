using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Camera rig on the CameraPivot: applies eye height, look pitch, distance-driven figure-8 head bob,
    /// landing spring, idle breathing, strafe tilt and run FOV kick. Phase comes from
    /// <see cref="PlayerMotor.StepPhase"/>, so the bob low point coincides with <see cref="PlayerMotor.Footstep"/>.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class HeadBob : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private HeadBobProfile profile;
        [Tooltip("Global multiplier (accessibility option). 0 disables bob but keeps pitch/eye height.")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;

        private const float SpringStep = 1f / 240f;

        private float _bobWeight;
        private float _speedScale;
        private float _breathWeight = 1f;
        private float _breathTime;
        private float _springPos, _springVel, _springAccumulator;
        private float _strafeRoll;
        private float _fov;
        private HeadBobProfile _fallbackProfile;

        public HeadBobProfile Profile { get => profile; set => profile = value; }
        public float Intensity { get => intensity; set => intensity = Mathf.Max(0f, value); }
        public Camera Camera => targetCamera;

        /// <summary>Current bob offset in pivot-parent space (useful for viewmodels).</summary>
        public Vector3 CurrentOffset { get; private set; }

        /// <summary>Scripted camera effects (electrocution shake, lying on the floor...).</summary>
        public Vector3 ExtraEuler { get; set; }
        public Vector3 ExtraPosition { get; set; }
        /// <summary>Gun recoil kick (degrees, added on top of everything else).</summary>
        public Vector3 KickEuler { get; set; }
        /// <summary>Seated: the head turns without the body (degrees of yaw relative to the root).</summary>
        public float YawOffset { get; set; }
        /// <summary>Zoom multiplier on the field of view (aiming down the sights &lt; 1).</summary>
        public float FovScale { get; set; } = 1f;
        /// <summary>0..1 limp on the right leg: uneven bob, sinks and rolls on the injured step.</summary>
        public float Limp { get; set; }
        private float _limpW;
        /// <summary>
        /// 0..1 drunk (vending machine): the view sways and rolls on <see cref="Drunkenness.Sample"/>, staggers more while
        /// walking and trails the mouse a little. Eases in over a few seconds like the drink kicking in.
        /// </summary>
        public float Drunk { get; set; }
        /// <summary>Clock and seed of the shared sway signal (the same ones drive the body other players see).</summary>
        public float DrunkClock { get; set; }
        public float DrunkSeed { get; set; }
        /// <summary>Smoothed drunk amount actually shown (for screen effects).</summary>
        public float DrunkWeight => _drunkW;
        private float _drunkW;
        private float _lagYaw, _lagPitch, _lagYawVel, _lagPitchVel;
        private bool _lagInit;
        public float EyeOverride { get; set; }
        public float EyeOverrideWeight { get; set; }

        private HeadBobProfile P
        {
            get
            {
                if (profile != null) return profile;
                if (_fallbackProfile == null) _fallbackProfile = ScriptableObject.CreateInstance<HeadBobProfile>();
                return _fallbackProfile;
            }
        }

        private void Awake()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            _fov = P.baseFov;
        }

        private void OnEnable()
        {
            if (motor == null) return;
            motor.Landed += OnLanded;
            motor.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            if (motor == null) return;
            motor.Landed -= OnLanded;
            motor.Jumped -= OnJumped;
        }

        private void OnDestroy()
        {
            if (_fallbackProfile != null) Destroy(_fallbackProfile);
        }

        private void OnLanded(float impact)
        {
            var p = P;
            _springVel -= Mathf.Min(impact, p.landingMaxImpact) * p.landingImpulse;
        }

        private void OnJumped() => _springVel -= P.jumpImpulse;

        private void LateUpdate()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            var p = P;

            // ---------------------------------------------------------- gait blend
            float speed = motor.HorizontalSpeed;
            float crouch = motor.CrouchAmount;
            float runT = Mathf.InverseLerp(motor.WalkSpeed, motor.RunSpeed, speed) * (1f - crouch);
            BobGait gait = BobGait.Lerp(BobGait.Lerp(p.walk, p.run, runT), p.crouch, crouch);

            float gaitSpeed = Mathf.Lerp(Mathf.Lerp(motor.WalkSpeed, motor.RunSpeed, runT), motor.CrouchSpeed, crouch);
            float targetScale = Mathf.Pow(Mathf.Clamp01(speed / Mathf.Max(0.1f, gaitSpeed)), p.speedAmplitudeExponent);
            float targetWeight = motor.IsGrounded && speed > 0.1f ? 1f : 0f;

            float k = 1f - Mathf.Exp(-p.blendSharpness * dt);
            _bobWeight = Mathf.Lerp(_bobWeight, targetWeight, k);
            _speedScale = Mathf.Lerp(_speedScale, targetScale, k);

            // ---------------------------------------------------------- figure-8 bob
            // Phase in footsteps: vertical has one cycle per step (low point at footfall = integer phase),
            // lateral one cycle per two steps, shifted for the figure-8 shape.
            float phase = motor.StepPhase * Mathf.Max(0.01f, gait.frequency);
            float vertical = -Mathf.Cos(phase * 2f * Mathf.PI);              // -1 at footfall
            if (p.footfallSharpness > 0f)
            {
                // Bias toward a sharper trough: remap [-1,1] so time near the low point shrinks.
                float t01 = vertical * 0.5f + 0.5f;
                t01 = Mathf.Lerp(t01, Mathf.Sqrt(t01), p.footfallSharpness);
                vertical = t01 * 2f - 1f;
            }
            float lateral = Mathf.Cos(phase * Mathf.PI + p.figureEight * Mathf.PI * 0.25f);

            float w = _bobWeight * _speedScale * intensity;
            Vector3 bobPos = new Vector3(lateral * gait.lateral, vertical * gait.vertical, 0f) * w;
            float bobRoll = -lateral * gait.roll * w;
            float bobPitch = -vertical * gait.pitch * w;

            // ---------------------------------------------------------- limp (injured right leg)
            // Every second step the weight drops onto the bad leg: the head sinks, rolls toward it and nods.
            _limpW = Mathf.MoveTowards(_limpW, Limp, dt * 2f);
            if (_limpW > 0.001f)
            {
                float side = Mathf.Sin(phase * Mathf.PI);
                float bad = Mathf.Max(0f, side);
                bad *= bad;
                float lw = _limpW * _bobWeight * intensity;
                bobPos.y -= 0.05f * bad * lw;
                bobPos.x += 0.02f * side * lw;
                bobRoll += 3.2f * side * lw - 0.8f * _limpW * intensity;
                bobPitch += 2.2f * bad * lw;
            }

            // ---------------------------------------------------------- drunk
            // Slow soft sway (lean, roll, drift), a wider and uneven walk and a view that trails the mouse.
            // Everything rides on low-frequency Perlin noise, so it floats rather than shakes.
            _drunkW = Mathf.Lerp(_drunkW, Mathf.Clamp01(Drunk), 1f - Mathf.Exp(-(Drunk > _drunkW ? 0.6f : 1.4f) * dt));
            float dk = _drunkW * Mathf.Clamp01(intensity);
            Vector3 drunkPos = Vector3.zero;
            float drunkPitch = 0f, drunkYaw = 0f, drunkRoll = 0f, drunkFov = 0f;
            float yawNow = motor.LookYaw, pitchNow = motor.LookPitch;
            if (!_lagInit || dk <= 0.0005f)
            {
                _lagInit = true;
                _lagYaw = yawNow; _lagPitch = pitchNow; _lagYawVel = 0f; _lagPitchVel = 0f;
            }
            else
            {
                float lagTime = 0.04f + 0.16f * dk;
                _lagYaw = Mathf.SmoothDampAngle(_lagYaw, yawNow, ref _lagYawVel, lagTime, Mathf.Infinity, dt);
                _lagPitch = Mathf.SmoothDamp(_lagPitch, pitchNow, ref _lagPitchVel, lagTime, Mathf.Infinity, dt);
                // Never far behind (a teleport or a flick must not leave the view pointing elsewhere).
                _lagYaw = yawNow + Mathf.Clamp(Mathf.DeltaAngle(yawNow, _lagYaw), -12f, 12f);
                _lagPitch = pitchNow + Mathf.Clamp(_lagPitch - pitchNow, -8f, 8f);
                float lagK = Mathf.Clamp01(dk * 3f);
                drunkYaw += Mathf.DeltaAngle(yawNow, _lagYaw) * lagK;
                drunkPitch -= (_lagPitch - pitchNow) * lagK;

                var s = Drunkenness.Sample(DrunkClock, DrunkSeed, dk);
                float walk = _bobWeight * _speedScale;
                // Leaning body: the eye drifts sideways with the lean and rolls into it, knees soften.
                drunkPos = new Vector3(0.06f * s.Lean + 0.012f * s.Wobble, -0.012f - 0.01f * s.Sag, 0.025f * s.Pitch) * dk;
                drunkRoll = -(5.2f * s.Lean + 1.1f * s.Wobble) * dk;
                drunkPitch += (2f * s.Pitch + 0.7f * s.Sag) * dk;
                drunkYaw += (2.6f * s.Yaw + 0.6f * s.Wobble) * dk;
                // Walking: the figure-8 widens and every step lands a little differently.
                float stepSide = Mathf.Sin(phase * Mathf.PI);
                bobPos.x *= 1f + 1.2f * dk;
                bobRoll *= 1f + 1.4f * dk;
                drunkRoll -= 2.2f * stepSide * (0.7f + 0.3f * s.Rhythm) * walk * dk;
                drunkPos.x += 0.025f * stepSide * walk * dk;
                drunkFov = (1.1f + 1.2f * s.Sag) * dk;
            }

            // ---------------------------------------------------------- landing spring (fixed substeps)
            _springAccumulator += dt;
            float omega = 2f * Mathf.PI * p.landingFrequency;
            int guard = 0;
            while (_springAccumulator >= SpringStep && guard++ < 64)
            {
                _springAccumulator -= SpringStep;
                float acc = -omega * omega * _springPos - 2f * p.landingDamping * omega * _springVel;
                _springVel += acc * SpringStep;
                _springPos += _springVel * SpringStep;
                if (_springPos < -p.landingMaxDip) { _springPos = -p.landingMaxDip; if (_springVel < 0f) _springVel = 0f; }
            }
            if (guard >= 64) _springAccumulator = 0f;
            float landY = _springPos * intensity;
            float landPitch = -_springPos * p.landingPitch * intensity;

            // ---------------------------------------------------------- idle breathing
            _breathTime += dt;
            float breathTarget = motor.IsGrounded ? 1f - Mathf.Clamp01(_bobWeight * _speedScale * 1.5f) : 0.3f;
            _breathWeight = Mathf.Lerp(_breathWeight, breathTarget, 1f - Mathf.Exp(-2f * dt));
            float bt = _breathTime * p.breathRate * 2f * Mathf.PI;
            float bw = _breathWeight * intensity;
            Vector3 breathPos = new Vector3(Mathf.Sin(bt * 0.5f + 0.7f) * p.breathLateral, Mathf.Sin(bt) * p.breathVertical, 0f) * bw;
            float breathRoll = Mathf.Sin(bt * 0.37f + 1.3f) * p.breathRoll * bw;
            float breathPitch = Mathf.Sin(bt + 0.4f) * p.breathPitch * bw;

            // ---------------------------------------------------------- strafe tilt
            float strafeTarget = 0f;
            if (p.strafeTilt)
            {
                Vector3 localVel = motor.transform.InverseTransformDirection(motor.Velocity);
                strafeTarget = -Mathf.Clamp(localVel.x / Mathf.Max(0.1f, motor.RunSpeed), -1f, 1f) * p.strafeTiltDegrees * intensity;
            }
            _strafeRoll = Mathf.Lerp(_strafeRoll, strafeTarget, 1f - Mathf.Exp(-p.strafeTiltSharpness * dt));

            // ---------------------------------------------------------- apply
            Vector3 offset = bobPos + breathPos + drunkPos + new Vector3(0f, landY, 0f);
            CurrentOffset = offset;
            float eye = Mathf.Lerp(motor.EyeHeight, EyeOverride, EyeOverrideWeight);
            transform.localPosition = new Vector3(0f, eye, 0f) + offset + ExtraPosition;
            transform.localRotation = Quaternion.Euler(0f, YawOffset + drunkYaw, 0f) * Quaternion.Euler(-motor.LookPitch + bobPitch + landPitch + breathPitch + drunkPitch, 0f,
                bobRoll + breathRoll + _strafeRoll + drunkRoll) * Quaternion.Euler(ExtraEuler + KickEuler);

            // ---------------------------------------------------------- FOV kick
            if (targetCamera != null)
            {
                float runFovT = motor.IsRunning ? Mathf.InverseLerp(motor.WalkSpeed, motor.RunSpeed, speed) : 0f;
                float targetFov = (Mathf.Lerp(p.baseFov, p.runFov, runFovT) + drunkFov) * FovScale;
                _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-p.fovSharpness * dt));
                targetCamera.fieldOfView = _fov;
            }
        }
    }
}
