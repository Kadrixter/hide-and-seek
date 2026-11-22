using System;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Player
{
    public enum MovementNoiseKind
    {
        Footstep,
        Landing,
        Jump,
        Crouch,
    }

    /// <summary>A movement sound that other systems (seeker "noise" UI, AI) may react to.</summary>
    public struct MovementNoise
    {
        public GameObject Source;
        public Vector3 Position;
        public MovementNoiseKind Kind;
        public SurfaceType Surface;
        public FootstepGait Gait;
        /// <summary>0..1 relative loudness (1 = landing from a big drop / sprint on metal).</summary>
        public float Loudness;
        /// <summary>Distance in metres at which the sound becomes inaudible.</summary>
        public float AudibleRange;
        public float Time;
    }

    /// <summary>
    /// Footstep and movement audio driven by an <see cref="ICharacterMotionSource"/> plus a step event.
    /// Local player: 2D with alternating left/right pan and a garage reverb filter.
    /// Remote players: 3D with a logarithmic-style rolloff that fades to silence at the gait's audible range
    /// (run ~25 m, walk ~15 m, crouch ~6 m). When a <see cref="PlayerMotor"/> is present its Footstep/Landed/Jumped
    /// events drive playback (in sync with the head bob); otherwise steps, jumps and landings are derived from motion.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(60)]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private FootstepLibrary library;
        [Tooltip("Component implementing ICharacterMotionSource (defaults to one on this GameObject or its parents).")]
        [SerializeField] private MonoBehaviour motionSource;
        [Tooltip("Local player = 2D stereo with alternating pan; otherwise fully 3D.")]
        [SerializeField] private bool localPlayer = true;
        [Tooltip("Derive steps from motion when no PlayerMotor step events are available (remote proxies).")]
        [SerializeField] private bool generateStepsFromMotion = true;

        [Header("Mix")]
        [SerializeField, Range(0f, 2f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float crouchVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.62f;
        [SerializeField, Range(0f, 1f)] private float runVolume = 1f;
        [Tooltip("Random per-step volume variation (fraction).")]
        [SerializeField, Range(0f, 0.5f)] private float volumeJitter = 0.12f;
        [Tooltip("Random per-step pitch variation (fraction, 0.06 = +-6%).")]
        [SerializeField, Range(0f, 0.2f)] private float pitchJitter = 0.06f;
        [Tooltip("Alternating left/right pan for the local player's feet.")]
        [SerializeField, Range(0f, 0.5f)] private float stereoAlternation = 0.12f;
        [SerializeField, Range(0f, 2f)] private float landingVolume = 0.95f;
        [SerializeField, Range(0f, 2f)] private float jumpVolume = 0.5f;
        [SerializeField, Range(0f, 2f)] private float crouchRustleVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float clothVolume = 0.16f;
        [Tooltip("Volume multiplier per surface (concrete, metal, water).")]
        [SerializeField] private Vector3 surfaceVolume = new Vector3(1f, 1.05f, 0.95f);
        [Tooltip("Volume multiplier (relative to concrete) for wood (x) and carpet (y).")]
        [SerializeField] private Vector2 woodCarpetVolume = new Vector2(1f, 0.6f);

        [Header("Audible range (m) - also used for noise events")]
        [SerializeField] private float crouchRange = 6f;
        [SerializeField] private float walkRange = 15f;
        [SerializeField] private float runRange = 25f;
        [SerializeField] private float landingRangeMax = 30f;
        [Tooltip("Range multiplier per surface (concrete, metal, water).")]
        [SerializeField] private Vector3 surfaceRange = new Vector3(1f, 1.3f, 1.2f);
        [Tooltip("Range multiplier (relative to concrete) for wood (x) and carpet (y).")]
        [SerializeField] private Vector2 woodCarpetRange = new Vector2(1.05f, 0.55f);
        [Tooltip("Full volume inside this fraction of the audible range; 1/d falloff beyond, fading to 0 at the range.")]
        [SerializeField, Range(0.02f, 0.5f)] private float rolloffInnerFraction = 0.1f;

        [Header("Surface detection")]
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private float footSpacing = 0.11f;
        [SerializeField] private float probeUp = 0.5f;
        [SerializeField] private float probeDown = 0.6f;

        [Header("Local reverb")]
        [SerializeField] private bool localReverb = true;
        [SerializeField] private AudioReverbPreset localReverbPreset = AudioReverbPreset.ParkingLot;

        [Header("Pool / derived steps")]
        [SerializeField, Range(2, 12)] private int poolSize = 6;
        [SerializeField] private float strideWalk = 1.35f;
        [SerializeField] private float strideRun = 2.05f;
        [SerializeField] private float strideCrouch = 0.85f;
        [SerializeField] private float walkSpeedRef = 3f;
        [SerializeField] private float runSpeedRef = 5.8f;

        /// <summary>Raised for every movement sound this character makes.</summary>
        public event Action<MovementNoise> NoiseEmitted;

        /// <summary>Raised for every movement sound of any character (global hook for the game / seeker UI).</summary>
        public static event Action<MovementNoise> AnyNoise;

        private ICharacterMotionSource _motion;
        private PlayerMotor _motor;
        private AudioSource[] _pool;
        private int _poolNext;
        private AudioSource _cloth;
        private float _clothVol;
        private bool _leftFoot;
        private readonly int[] _lastIndex = new int[64];
        private System.Random _rng;
        private float _noiseLevel;
        private bool _wasCrouching, _wasGrounded = true;
        private float _airVelY;
        private float _derivedPhase;
        private AnimationCurve _rolloff;
        private bool _initialised;

        public FootstepLibrary Library { get => library; set => library = value; }
        public ICharacterMotionSource MotionSource => _motion;

        /// <summary>Smoothed 0..1 "how loud is this character right now" (decays ~1.5/s). For noise meters.</summary>
        public float NoiseLevel => _noiseLevel;

        /// <summary>The most recent movement noise.</summary>
        public MovementNoise LastNoise { get; private set; }

        /// <summary>Surface under the last step.</summary>
        public SurfaceType CurrentSurface { get; private set; }

        public float MasterVolume { get => masterVolume; set => masterVolume = Mathf.Max(0f, value); }

        public bool LocalPlayer
        {
            get => localPlayer;
            set { localPlayer = value; if (_initialised) ConfigureSources(); }
        }

        /// <summary>Audible range in metres for a gait on a surface.</summary>
        public float GetAudibleRange(FootstepGait gait, SurfaceType surface)
        {
            float r = gait == FootstepGait.Run ? runRange : gait == FootstepGait.Crouch ? crouchRange : walkRange;
            return r * SurfaceComponent(surfaceRange, surface);
        }

        /// <summary>Per-gait base volume (0..1) - exposed for UI/noise tuning.</summary>
        public float GetGaitVolume(FootstepGait gait) =>
            gait == FootstepGait.Run ? runVolume : gait == FootstepGait.Crouch ? crouchVolume : walkVolume;

        /// <summary>Gait from the motion state (crouch, run if flagged or fast, walk otherwise).</summary>
        public static FootstepGait GaitFrom(ICharacterMotionSource m, float walkSpeed = 3f, float runSpeed = 5.8f)
        {
            if (m == null) return FootstepGait.Walk;
            if (m.IsCrouching) return FootstepGait.Crouch;
            if (m.IsRunning || m.HorizontalSpeed > (walkSpeed + runSpeed) * 0.5f) return FootstepGait.Run;
            return FootstepGait.Walk;
        }

        /// <summary>Overrides the reverb used for the local player's 2D sources (e.g. set by a map reverb zone).</summary>
        public void SetLocalReverbPreset(AudioReverbPreset preset)
        {
            localReverbPreset = preset;
            if (_initialised) ConfigureSources();
        }

        // ------------------------------------------------------------------ lifecycle

        private void Awake() => Initialise();

        private void Initialise()
        {
            if (_initialised) return;
            _initialised = true;
            _rng = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < _lastIndex.Length; i++) _lastIndex[i] = -1;

            _motion = motionSource as ICharacterMotionSource;
            if (_motion == null)
            {
                foreach (var mb in GetComponentsInParent<MonoBehaviour>())
                    if (mb is ICharacterMotionSource ms) { _motion = ms; motionSource = mb; break; }
            }
            _motor = motionSource as PlayerMotor;

            _rolloff = BuildRolloffCurve(rolloffInnerFraction);
            var poolRoot = new GameObject("FootstepAudioSources").transform;
            poolRoot.SetParent(transform, false);
            _pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Step" + i);
                go.transform.SetParent(poolRoot, false);
                _pool[i] = go.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
            }
            var clothGo = new GameObject("ClothRustle");
            clothGo.transform.SetParent(poolRoot, false);
            clothGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            _cloth = clothGo.AddComponent<AudioSource>();
            _cloth.playOnAwake = false;
            ConfigureSources();
        }

        private void OnEnable()
        {
            Initialise();
            if (_motor != null)
            {
                _motor.Footstep += PlayFootstep;
                _motor.Landed += PlayLanding;
                _motor.Jumped += PlayJump;
                _motor.CrouchChanged += PlayCrouchChange;
            }
            if (_motion != null)
            {
                _wasCrouching = _motion.IsCrouching;
                _wasGrounded = _motion.IsGrounded;
            }
        }

        private void OnDisable()
        {
            if (_motor != null)
            {
                _motor.Footstep -= PlayFootstep;
                _motor.Landed -= PlayLanding;
                _motor.Jumped -= PlayJump;
                _motor.CrouchChanged -= PlayCrouchChange;
            }
            if (_cloth != null) _cloth.Stop();
        }

        private void ConfigureSources()
        {
            foreach (var s in _pool) ConfigureSource(s, false);
            ConfigureSource(_cloth, true);
            if (library != null && library.clothLoop != null)
            {
                _cloth.clip = library.clothLoop;
                _cloth.loop = true;
                _cloth.volume = 0f;
                if (isActiveAndEnabled && !_cloth.isPlaying)
                    _cloth.Play();
            }
        }

        private void ConfigureSource(AudioSource s, bool cloth)
        {
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            s.spread = 0f;
            s.priority = localPlayer ? 64 : 128;
            if (localPlayer)
            {
                s.spatialBlend = 0f;
            }
            else
            {
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _rolloff);
                s.maxDistance = cloth ? crouchRange : walkRange;
                s.panStereo = 0f;
            }
            var reverb = s.GetComponent<AudioReverbFilter>();
            bool wantReverb = localPlayer && localReverb;
            if (wantReverb)
            {
                if (reverb == null) reverb = s.gameObject.AddComponent<AudioReverbFilter>();
                reverb.reverbPreset = localReverbPreset;
                reverb.enabled = true;
            }
            else if (reverb != null) reverb.enabled = false;
        }

        /// <summary>Normalised-distance rolloff: 1 inside <paramref name="inner"/>, then inner/x, smoothly reaching 0 at 1.</summary>
        public static AnimationCurve BuildRolloffCurve(float inner)
        {
            var keys = new Keyframe[18];
            for (int i = 0; i < keys.Length; i++)
            {
                float x = i == 0 ? 0f : inner * Mathf.Pow(1f / inner, (i - 1) / (float)(keys.Length - 2));
                float g = x <= inner ? 1f : inner / x;
                float t = Mathf.InverseLerp(0.55f, 1f, x);
                g *= 1f - t * t * (3f - 2f * t);
                keys[i] = new Keyframe(x, g);
            }
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.deltaTime;
            _noiseLevel = Mathf.Max(0f, _noiseLevel - 1.5f * dt);
            if (_motion == null) return;

            // crouch / stand rustles (event-driven with a PlayerMotor, polled for other motion sources)
            bool crouching = _motion.IsCrouching;
            if (_motor == null && crouching != _wasCrouching)
            {
                _wasCrouching = crouching;
                PlayCrouchChange(crouching);
            }

            // remote proxies: derive jump / landing / steps from motion
            if (_motor == null)
            {
                bool grounded = _motion.IsGrounded;
                Vector3 v = _motion.Velocity;
                if (!grounded) _airVelY = Mathf.Min(_airVelY, v.y);
                if (grounded && !_wasGrounded && -_airVelY > 1f) PlayLanding(-_airVelY);
                if (!grounded && _wasGrounded && v.y > 1.5f) PlayJump();
                if (grounded) _airVelY = 0f;
                _wasGrounded = grounded;

                if (generateStepsFromMotion && grounded)
                {
                    float speed = _motion.HorizontalSpeed;
                    if (speed > 0.15f)
                    {
                        float stride = speed <= walkSpeedRef ? strideWalk
                            : Mathf.Lerp(strideWalk, strideRun, Mathf.InverseLerp(walkSpeedRef, runSpeedRef, speed));
                        stride = Mathf.Lerp(stride, strideCrouch, _motion.CrouchAmount);
                        float before = _derivedPhase;
                        _derivedPhase += speed * dt / Mathf.Max(0.1f, stride);
                        if (Mathf.Floor(_derivedPhase) > Mathf.Floor(before))
                            PlayFootstep(Mathf.Clamp01(speed / runSpeedRef));
                    }
                }
            }

            UpdateCloth(dt);
        }

        private void UpdateCloth(float dt)
        {
            if (_cloth == null || _cloth.clip == null) return;
            if (!_cloth.isPlaying) _cloth.Play();
            float speed = _motion.HorizontalSpeed;
            float speedT = Mathf.Clamp01(speed / runSpeedRef);
            float phase = _motor != null ? _motor.StepPhase : _derivedPhase;
            // swell with each step so the rustle "breathes" with the gait
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(phase * Mathf.PI));
            float target = clothVolume * masterVolume * Mathf.Pow(speedT, 0.8f) * pulse;
            if (!_motion.IsGrounded) target *= 0.6f;
            if (_motion.IsCrouching) target *= 1.2f;
            _clothVol = Mathf.Lerp(_clothVol, target, 1f - Mathf.Exp(-10f * dt));
            _cloth.volume = _clothVol * FloorGain(transform.position);
            _cloth.pitch = 0.92f + 0.16f * speedT;
            if (!localPlayer) _cloth.maxDistance = Mathf.Lerp(crouchRange, walkRange, speedT);
        }

        // ------------------------------------------------------------------ public playback API

        /// <summary>Plays one footstep. Hook this to a step event (PlayerMotor.Footstep is wired automatically).</summary>
        public void PlayFootstep(float intensity)
        {
            if (library == null) return;
            FootstepGait gait = GaitFrom(_motion, walkSpeedRef, runSpeedRef);
            _leftFoot = !_leftFoot;
            Vector3 foot = FootPoint(_leftFoot);
            SurfaceType surface = SurfaceDetector.Detect(foot, transform, surfaceMask, probeUp, probeDown, out _);
            CurrentSurface = surface;

            var set = library.GetSurface(surface);
            var clips = set?.Get(gait);
            if (clips == null || clips.Length == 0) return;

            // Within-gait loudness follows actual speed a little (slow walk is quieter).
            float speed = _motion != null ? _motion.HorizontalSpeed : walkSpeedRef;
            float gaitRef = gait == FootstepGait.Run ? runSpeedRef : gait == FootstepGait.Crouch ? 1.5f : walkSpeedRef;
            float speedScale = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(speed / gaitRef));
            float vol = GetGaitVolume(gait) * speedScale * SurfaceComponent(surfaceVolume, surface);
            float pan = localPlayer ? (_leftFoot ? -stereoAlternation : stereoAlternation) : 0f;
            float range = GetAudibleRange(gait, surface);

            Play(Pick(clips, (int)surface * 8 + (int)gait), vol, pan, range, foot);
            Emit(MovementNoiseKind.Footstep, surface, gait, Mathf.Clamp01(vol * (range / (runRange * surfaceRange.y))), range, foot);
        }

        /// <summary>Plays a landing; light-to-heavy crossfade by impact speed (m/s), plus a surface step layer.</summary>
        public void PlayLanding(float impactSpeed)
        {
            if (library == null) return;
            float t = Mathf.InverseLerp(2.5f, 9f, impactSpeed);
            float presence = Mathf.InverseLerp(1f, 3f, impactSpeed);
            Vector3 foot = FootPoint(false);
            SurfaceType surface = SurfaceDetector.Detect(transform.position, transform, surfaceMask, probeUp, probeDown, out _);
            CurrentSurface = surface;
            float range = Mathf.Lerp(walkRange, landingRangeMax, t) * SurfaceComponent(surfaceRange, surface);
            float baseVol = landingVolume * presence;

            if (library.landLight.Length > 0) Play(Pick(library.landLight, 40), baseVol * (1f - 0.65f * t), 0f, range, foot);
            if (t > 0.05f && library.landHeavy.Length > 0) Play(Pick(library.landHeavy, 41), baseVol * t, 0f, range, foot);
            var set = library.GetSurface(surface);
            var clips = set?.Get(FootstepGait.Run);
            if (clips != null && clips.Length > 0)
                Play(Pick(clips, (int)surface * 8 + 2), baseVol * Mathf.Lerp(0.55f, 0.95f, t) * SurfaceComponent(surfaceVolume, surface), 0f, range, foot);

            Emit(MovementNoiseKind.Landing, surface, FootstepGait.Run, Mathf.Lerp(0.35f, 1f, t) * presence, range, foot);
        }

        /// <summary>Plays the jump take-off (shoe push + cloth).</summary>
        public void PlayJump()
        {
            if (library == null || library.jump.Length == 0) return;
            Vector3 foot = FootPoint(false);
            SurfaceType surface = SurfaceDetector.Detect(transform.position, transform, surfaceMask, probeUp, probeDown, out _);
            float range = walkRange * SurfaceComponent(surfaceRange, surface);
            Play(Pick(library.jump, 42), jumpVolume, 0f, range, foot);
            if (surface != SurfaceType.Concrete)
            {
                var clips = library.GetSurface(surface)?.Get(FootstepGait.Walk);
                if (clips != null && clips.Length > 0) Play(Pick(clips, (int)surface * 8 + 1), jumpVolume * 0.6f, 0f, range, foot);
            }
            Emit(MovementNoiseKind.Jump, surface, FootstepGait.Walk, 0.4f, range, foot);
        }

        /// <summary>Plays a crouch-down (true) or stand-up (false) clothing rustle.</summary>
        public void PlayCrouchChange(bool down)
        {
            if (library == null) return;
            var clips = down ? library.crouchDown : library.standUp;
            if (clips.Length == 0) return;
            Vector3 p = transform.position + Vector3.up * 0.9f;
            Play(Pick(clips, down ? 43 : 44), crouchRustleVolume, 0f, crouchRange, p);
            Emit(MovementNoiseKind.Crouch, CurrentSurface, FootstepGait.Crouch, 0.15f, crouchRange, p);
        }

        // ------------------------------------------------------------------ internals

        private Vector3 FootPoint(bool left)
        {
            Vector3 right = transform.right;
            return transform.position + right * (left ? -footSpacing : footSpacing);
        }

        private AudioClip Pick(AudioClip[] clips, int slot)
        {
            if (clips.Length == 1) return clips[0];
            slot = Mathf.Clamp(slot, 0, _lastIndex.Length - 1);
            int last = _lastIndex[slot];
            int i = _rng.Next(clips.Length - (last >= 0 && last < clips.Length ? 1 : 0));
            if (last >= 0 && last < clips.Length && i >= last) i++; // never the same twice in a row
            _lastIndex[slot] = i;
            return clips[i];
        }

        // ---- other players' steps are heard on the same floor only (or muffled next to a ramp / stairwell)
        private static AudioListener _listener;
        private static float _nextListenerSearch;

        private static Vector3 ListenerPos(out bool ok)
        {
            if ((_listener == null || !_listener.isActiveAndEnabled) && Time.unscaledTime >= _nextListenerSearch)
            {
                _nextListenerSearch = Time.unscaledTime + 0.5f;
                _listener = FindAnyObjectByType<AudioListener>();
            }
            ok = _listener != null;
            return ok ? _listener.transform.position : Vector3.zero;
        }

        /// <summary>
        /// 0..1: how much of this (remote) player's steps reaches the listener. Another floor is silent, except next to a
        /// ramp shaft / stairwell (<see cref="FloorAcoustics"/>), where it is heavily muffled.
        /// </summary>
        private float FloorGain(Vector3 position)
        {
            if (localPlayer) return 1f;
            Vector3 lp = ListenerPos(out bool ok);
            if (!ok) return 1f;
            // Neighbours on the same ramp: feet at nearly the same height and close by (eye ~1.6 m above the feet).
            Vector2 flat = new Vector2(lp.x - position.x, lp.z - position.z);
            bool neighbours = flat.sqrMagnitude < 36f && Mathf.Abs(lp.y - 1.6f - position.y) < 1.6f;
            return FloorAcoustics.Transfer(lp, position, neighbours, out _);
        }

        private void Play(AudioClip clip, float volume, float pan, float range, Vector3 position)
        {
            if (clip == null || volume <= 0.0001f) return;
            float floorGain = FloorGain(position);
            if (floorGain <= 0.001f) return;
            volume *= floorGain;
            AudioSource s = NextSource();
            float jitterV = 1f + ((float)_rng.NextDouble() * 2f - 1f) * volumeJitter;
            float jitterP = 1f + ((float)_rng.NextDouble() * 2f - 1f) * pitchJitter;
            s.transform.position = position;
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * jitterV * masterVolume);
            s.pitch = jitterP;
            if (localPlayer) s.panStereo = pan;
            else s.maxDistance = Mathf.Max(1f, range);
            s.Play();
        }

        private AudioSource NextSource()
        {
            // Prefer a free source; otherwise steal round-robin (oldest).
            for (int k = 0; k < _pool.Length; k++)
            {
                var s = _pool[(_poolNext + k) % _pool.Length];
                if (!s.isPlaying) { _poolNext = (_poolNext + k + 1) % _pool.Length; return s; }
            }
            var steal = _pool[_poolNext];
            _poolNext = (_poolNext + 1) % _pool.Length;
            return steal;
        }

        private void Emit(MovementNoiseKind kind, SurfaceType surface, FootstepGait gait, float loudness, float range, Vector3 pos)
        {
            var n = new MovementNoise
            {
                Source = gameObject,
                Position = pos,
                Kind = kind,
                Surface = surface,
                Gait = gait,
                Loudness = Mathf.Clamp01(loudness),
                AudibleRange = range,
                Time = Time.time,
            };
            LastNoise = n;
            _noiseLevel = Mathf.Max(_noiseLevel, n.Loudness);
            NoiseEmitted?.Invoke(n);
            AnyNoise?.Invoke(n);
        }

        private float SurfaceComponent(Vector3 v, SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Metal: return v.y;
                case SurfaceType.Water: return v.z;
                case SurfaceType.Wood: return v.x * (v == surfaceRange ? woodCarpetRange.x : woodCarpetVolume.x);
                case SurfaceType.Carpet: return v.x * (v == surfaceRange ? woodCarpetRange.y : woodCarpetVolume.y);
                default: return v.x;
            }
        }
    }
}
