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
            Vector3 offset = bobPos + breathPos + new Vector3(0f, landY, 0f);
            CurrentOffset = offset;
            transform.localPosition = new Vector3(0f, motor.EyeHeight, 0f) + offset;
            transform.localRotation = Quaternion.Euler(-motor.LookPitch + bobPitch + landPitch + breathPitch, 0f,
                bobRoll + breathRoll + _strafeRoll);

            // ---------------------------------------------------------- FOV kick
            if (targetCamera != null)
            {
                float runFovT = motor.IsRunning ? Mathf.InverseLerp(motor.WalkSpeed, motor.RunSpeed, speed) : 0f;
                float targetFov = Mathf.Lerp(p.baseFov, p.runFov, runFovT);
                _fov = Mathf.Lerp(_fov, targetFov, 1f - Mathf.Exp(-p.fovSharpness * dt));
                targetCamera.fieldOfView = _fov;
            }
        }
    }
}
