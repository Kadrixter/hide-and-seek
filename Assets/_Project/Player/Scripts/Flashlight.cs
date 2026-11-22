using System;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Hand-held flashlight: lagged follow of the camera (trails mouse movement, reacts to head bob),
    /// spot light with cookie and soft shadows, additive volumetric beam mesh, dust motes lit only inside
    /// the cone, procedural click sound and a short flicker when switched on.
    /// Lives as a child of the player camera.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class Flashlight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light spotLight;
        [SerializeField] private MeshRenderer beamRenderer;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Optional; auto-found in parents. Left mouse toggles the light.")]
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Transform the flashlight follows (defaults to the parent camera).")]
        [SerializeField] private Transform anchor;
        [Tooltip("Player root used to separate head motion from body translation (defaults to anchor.root).")]
        [SerializeField] private Transform body;

        [Header("State")]
        [SerializeField] private bool startOn;
        [Tooltip("When false the flashlight ignores the local input (e.g. remote players).")]
        [SerializeField] private bool listenToInput = true;

        [Header("Light")]
        [SerializeField] private Color color = new Color(1f, 0.93f, 0.82f);
        [SerializeField] private float intensity = 3.8f;
        [SerializeField] private float range = 30f;
        [SerializeField, Range(10f, 120f)] private float spotAngle = 58f;

        [Header("Beam")]
        [SerializeField] private float beamIntensity = 1f;
        [SerializeField] private float dustIntensity = 1f;

        [Header("Follow / sway")]
        [Tooltip("Position of the light relative to the anchor (camera).")]
        [SerializeField] private Vector3 holdOffset = new Vector3(0.17f, -0.2f, 0.28f);
        [Tooltip("The beam converges with the view direction at this distance (m).")]
        [SerializeField] private float aimDistance = 9f;
        [Tooltip("How quickly the light catches up with camera rotation (1/s). Lower = more lag.")]
        [SerializeField] private float rotationSharpness = 14f;
        [SerializeField] private float maxLagAngle = 9f;
        [Tooltip("Metres of positional sway per degree of rotation lag.")]
        [SerializeField] private float lagPositionSway = 0.0035f;
        [Tooltip("How much the light trails head motion (bob / crouch / landing). 0 = rigid.")]
        [SerializeField, Range(0f, 1f)] private float headMotionLag = 0.55f;
        [SerializeField] private float headMotionSharpness = 10f;
        [Tooltip("Extra rotation (degrees) per metre/s of head vertical velocity.")]
        [SerializeField] private float headMotionTilt = 2.2f;

        [Header("Switching")]
        [SerializeField] private float flickerDuration = 0.32f;
        [SerializeField] private float switchOffFade = 0.06f;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.55f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlashPosId = Shader.PropertyToID("_FlashPos");
        private static readonly int FlashDirId = Shader.PropertyToID("_FlashDir");
        private static readonly int FlashConeId = Shader.PropertyToID("_FlashCone");
        private static readonly int DepthFadeId = Shader.PropertyToID("_DepthFadeEnabled");

        private bool _isOn;
        private float _level;           // 0..1 visual output
        private float _switchTime = -10f;
        private int _flickerSeed;
        private Quaternion _laggedRotation;
        private Vector3 _laggedHeadPos;  // anchor position in body space, smoothed
        private Vector3 _prevHeadPos;
        private float _headVelY;
        private bool _followInitialised;
        private MaterialPropertyBlock _mpb;
        private ParticleSystemRenderer _dustRenderer;
        private AudioClip _clickOn, _clickOff;
        private bool _depthFade = true;

        /// <summary>Whether the flashlight is switched on. Setting it plays the click and raises <see cref="Toggled"/>.</summary>
        public bool IsOn
        {
            get => _isOn;
            set => SetOn(value, true);
        }

        /// <summary>Raised when the light is switched on (true) or off (false).</summary>
        public event Action<bool> Toggled;

        /// <summary>Current visual output 0..1 (includes flicker / fade).</summary>
        public float Level => _level;

        public Light SpotLight => spotLight;
        public bool ListenToInput { get => listenToInput; set => listenToInput = value; }
        /// <summary>Light position relative to the camera (e.g. right shoulder).</summary>
        public Vector3 HoldOffset { get => holdOffset; set => holdOffset = value; }
        public float Range => range;
        public float SpotAngle => spotAngle;

        public void Toggle() => SetOn(!_isOn, true);

        /// <summary>Sets the state. <paramref name="feedback"/> = click sound + flicker (false for silent sync).</summary>
        public void SetOn(bool on, bool feedback)
        {
            if (_isOn == on) return;
            _isOn = on;
            _switchTime = feedback ? Time.time : -10f;
            _flickerSeed++;
            if (feedback) PlayClick(on);
            Toggled?.Invoke(on);
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (anchor == null) anchor = transform.parent;
            if (body == null && anchor != null) body = anchor.root;
            if (input == null) input = GetComponentInParent<PlayerInputReader>();
            if (dust != null) _dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            if (beamRenderer != null && beamRenderer.GetComponent<BeamOcclusion>() == null && Application.isPlaying)
                beamRenderer.gameObject.AddComponent<BeamOcclusion>();
            _mpb = new MaterialPropertyBlock();
            _isOn = startOn;
            _level = startOn ? 1f : 0f;

            if (audioSource != null)
            {
                _clickOn = FlashlightProcedural.CreateClickClip(true);
                _clickOff = FlashlightProcedural.CreateClickClip(false);
            }

            var cam = anchor != null ? anchor.GetComponent<Camera>() : null;
            if (cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
            ConfigureLight();
        }

        private void OnEnable()
        {
            if (input != null) input.FlashlightTogglePressed += OnTogglePressed;
            _followInitialised = false;
        }

        private void OnDisable()
        {
            if (input != null) input.FlashlightTogglePressed -= OnTogglePressed;
        }

        private void OnDestroy()
        {
            if (_clickOn != null) Destroy(_clickOn);
            if (_clickOff != null) Destroy(_clickOff);
        }

        private void OnTogglePressed()
        {
            if (listenToInput) Toggle();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            UpdateFollow(dt);
            _level = ComputeLevel();
            ApplyVisuals(_level);
        }

        // ------------------------------------------------------------------ follow

        private void UpdateFollow(float dt)
        {
            if (anchor == null || dt <= 0f) return;

            Vector3 headPos = body != null ? body.InverseTransformPoint(anchor.position) : anchor.position;
            if (!_followInitialised)
            {
                _laggedRotation = TargetRotation(anchor.TransformPoint(holdOffset));
                _laggedHeadPos = headPos;
                _prevHeadPos = headPos;
                _headVelY = 0f;
                _followInitialised = true;
            }

            // Head motion (bob, crouch, landing) in body space -> trailing positional offset.
            float hk = 1f - Mathf.Exp(-headMotionSharpness * dt);
            _laggedHeadPos = Vector3.Lerp(_laggedHeadPos, headPos, hk);
            float velY = (headPos.y - _prevHeadPos.y) / dt;
            _prevHeadPos = headPos;
            _headVelY = Mathf.Lerp(_headVelY, Mathf.Clamp(velY, -3f, 3f), hk);
            Vector3 headLagWorld = body != null ? body.TransformVector(_laggedHeadPos - headPos) : _laggedHeadPos - headPos;
            Vector3 headLagLocal = anchor.InverseTransformVector(headLagWorld) * headMotionLag;

            // Rotation lag in world space (trails mouse look), clamped.
            Vector3 basePos = anchor.TransformPoint(holdOffset);
            Quaternion target = TargetRotation(basePos);
            float rk = 1f - Mathf.Exp(-rotationSharpness * dt);
            _laggedRotation = Quaternion.Slerp(_laggedRotation, target, rk);
            float lagAngle = Quaternion.Angle(_laggedRotation, target);
            if (lagAngle > maxLagAngle)
                _laggedRotation = Quaternion.RotateTowards(target, _laggedRotation, maxLagAngle);

            // Positional sway from the rotation lag, expressed in anchor space.
            Quaternion lagLocal = Quaternion.Inverse(target) * _laggedRotation;
            Vector3 lagEuler = WrapEuler(lagLocal.eulerAngles);
            Vector3 sway = new Vector3(lagEuler.y, -lagEuler.x, 0f) * lagPositionSway;

            Vector3 localPos = holdOffset + sway + headLagLocal;
            transform.position = anchor.TransformPoint(localPos);
            transform.rotation = Quaternion.AngleAxis(_headVelY * headMotionTilt, anchor.right) * _laggedRotation;
        }

        private Quaternion TargetRotation(Vector3 lightPos)
        {
            Vector3 aimPoint = anchor.position + anchor.forward * aimDistance;
            Vector3 dir = aimPoint - lightPos;
            return dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir, anchor.up) : anchor.rotation;
        }

        private static Vector3 WrapEuler(Vector3 e) =>
            new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));

        // ------------------------------------------------------------------ visuals

        private float ComputeLevel()
        {
            float since = Time.time - _switchTime;
            if (!_isOn)
            {
                if (switchOffFade <= 0f || since >= switchOffFade) return 0f;
                return Mathf.Min(_level, 1f - since / switchOffFade);
            }
            if (since >= flickerDuration || flickerDuration <= 0f) return 1f;

            // Flicker: a few quick contact bounces then a short warm-up ramp.
            float t = since / flickerDuration;
            float[] pattern = { 1f, 0.05f, 0.8f, 0.15f, 0.55f, 0.92f, 0.7f, 1f };
            float pos = t * (pattern.Length - 1);
            int i = Mathf.Min((int)pos, pattern.Length - 2);
            float seg = pos - i;
            float noise = Mathf.PerlinNoise(_flickerSeed * 3.17f, since * 60f) * 0.25f;
            float v = (seg < 0.6f ? pattern[i] : pattern[i + 1]) - noise * (1f - t);
            return Mathf.Clamp01(v);
        }

        private void ConfigureLight()
        {
            if (spotLight == null) return;
            spotLight.type = LightType.Spot;
            // always per-pixel (with its shadows): as a vertex light it would shine through the floor slabs
            spotLight.renderMode = LightRenderMode.ForcePixel;
            spotLight.range = range;
            spotLight.spotAngle = spotAngle;
            spotLight.innerSpotAngle = spotAngle * 0.4f;
            spotLight.color = color;
        }

        /// <summary>Pushes the given visual level (0..1) to light, beam and dust. Also usable in edit mode.</summary>
        public void ApplyVisuals(float level)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            bool visible = level > 0.001f;

            if (spotLight != null)
            {
                spotLight.enabled = visible;
                spotLight.intensity = intensity * level;
                spotLight.color = color;
            }

            Vector3 pos = transform.position;
            Vector3 dir = transform.forward;
            float half = spotAngle * 0.5f * Mathf.Deg2Rad;
            var cone = new Vector4(Mathf.Cos(half * 0.95f), Mathf.Cos(half * 0.35f), range, 0f);

            if (beamRenderer != null)
            {
                beamRenderer.enabled = visible;
                if (visible)
                {
                    beamRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(IntensityId, beamIntensity * level);
                    _mpb.SetColor(ColorId, color);
                    _mpb.SetFloat(DepthFadeId, _depthFade ? 1f : 0f);
                    beamRenderer.SetPropertyBlock(_mpb);
                }
            }

            if (_dustRenderer == null && dust != null) _dustRenderer = dust.GetComponent<ParticleSystemRenderer>();
            if (_dustRenderer != null)
            {
                _dustRenderer.enabled = visible;
                if (visible)
                {
                    _dustRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(IntensityId, dustIntensity * level);
                    _mpb.SetColor(ColorId, color);
                    _mpb.SetVector(FlashPosId, pos);
                    _mpb.SetVector(FlashDirId, dir);
                    _mpb.SetVector(FlashConeId, cone);
                    _dustRenderer.SetPropertyBlock(_mpb);
                }
            }
        }

        private float _baseRange = -1f, _baseAngle, _baseIntensity;
        private Vector3 _baseBeamScale;

        /// <summary>
        /// Seeker beam focus: 0 = default (wide, normal range), 1 = narrow long throw.
        /// Range and angle trade off; intensity rises a little as the cone narrows.
        /// </summary>
        public void SetFocus(float focus)
        {
            if (_baseRange < 0f)
            {
                _baseRange = range;
                _baseAngle = spotAngle;
                _baseIntensity = intensity;
                if (beamRenderer != null) _baseBeamScale = beamRenderer.transform.localScale;
            }
            focus = Mathf.Clamp01(focus);
            range = _baseRange * Mathf.Lerp(1f, 2.1f, focus);
            spotAngle = _baseAngle * Mathf.Lerp(1f, 0.38f, focus);
            intensity = _baseIntensity * Mathf.Lerp(1f, 1.9f, focus);
            ConfigureLight();
            if (beamRenderer != null)
            {
                float w = Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(_baseAngle * 0.5f * Mathf.Deg2Rad);
                float l = range / _baseRange;
                beamRenderer.transform.localScale = new Vector3(_baseBeamScale.x * w * l, _baseBeamScale.y * w * l, _baseBeamScale.z * l);
            }
        }

        /// <summary>Enables/disables the soft depth fade of the beam (requires a camera depth texture).</summary>
        public void SetBeamDepthFade(bool enabled) => _depthFade = enabled;

        private void PlayClick(bool on)
        {
            if (audioSource == null) return;
            var clip = on ? _clickOn : _clickOff;
            if (clip == null) return;
            audioSource.pitch = 1f + ((_flickerSeed * 37) % 7 - 3) * 0.012f;
            audioSource.PlayOneShot(clip, clickVolume);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (spotLight != null && !Application.isPlaying) ConfigureLight();
        }
#endif
    }
}
