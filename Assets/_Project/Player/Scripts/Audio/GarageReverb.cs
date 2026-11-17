using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Default "big concrete space" reverb carried by the player: an <see cref="AudioReverbZone"/> that follows the
    /// listener (affects 3D sounds such as other players' footsteps) and the matching reverb for the local player's
    /// 2D footstep filters.
    /// <para>When the game calls <see cref="AudioEnvironment.Apply(AudioEnvironmentKind)"/> the global environment wins:
    /// this zone is disabled, the local footstep filters copy the environment's reverb and, in the garage, get a
    /// subtle slap echo off the far walls. Without an environment a map can still override it by placing its own
    /// AudioReverbZone(s): on start this component then disables its zone and adopts the map zone's preset.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioReverbZone))]
    public class GarageReverb : MonoBehaviour
    {
        [SerializeField] private AudioReverbPreset preset = AudioReverbPreset.ParkingLot;
        [SerializeField] private bool yieldToSceneZones = true;
        [Tooltip("Slap echo on the local player's footsteps in the garage (wet level).")]
        [SerializeField, Range(0f, 0.5f)] private float footstepEchoWet = 0.16f;

        private AudioReverbZone _zone;

        public AudioReverbPreset Preset => preset;

        /// <summary>True when a scene (map) reverb zone or the global AudioEnvironment replaced this default.</summary>
        public bool Overridden { get; private set; }

        private void Awake()
        {
            _zone = GetComponent<AudioReverbZone>();
            Apply(preset);
        }

        private void OnEnable()
        {
            AudioEnvironment.Changed += OnEnvironmentChanged;
            if (AudioEnvironment.Current != AudioEnvironmentKind.None) OnEnvironmentChanged(AudioEnvironment.Current);
        }

        private void OnDisable() => AudioEnvironment.Changed -= OnEnvironmentChanged;

        private void Start()
        {
            if (AudioEnvironment.Current != AudioEnvironmentKind.None)
            {
                OnEnvironmentChanged(AudioEnvironment.Current); // footstep sources exist now
                return;
            }
            if (!yieldToSceneZones) return;
            Transform root = transform.root;
            foreach (var z in FindObjectsByType<AudioReverbZone>())
            {
                if (z == _zone || z.transform.IsChildOf(root) || !z.isActiveAndEnabled) continue;
                Overridden = true;
                _zone.enabled = false;
                if (z.reverbPreset != AudioReverbPreset.User) SetLocalPreset(z.reverbPreset);
                return;
            }
        }

        /// <summary>Switches the default reverb preset (zone + local footstep filters).</summary>
        public void Apply(AudioReverbPreset newPreset)
        {
            preset = newPreset;
            if (_zone == null) _zone = GetComponent<AudioReverbZone>();
            _zone.reverbPreset = newPreset;
            _zone.minDistance = 500f;
            _zone.maxDistance = 520f;
            SetLocalPreset(newPreset);
        }

        private void OnEnvironmentChanged(AudioEnvironmentKind kind)
        {
            if (_zone == null) _zone = GetComponent<AudioReverbZone>();
            if (kind == AudioEnvironmentKind.None)
            {
                Overridden = false;
                _zone.enabled = true;
                Apply(preset);
                SetLocalEcho(false);
                return;
            }

            Overridden = true;
            _zone.enabled = false; // the global AudioEnvironment zone handles 3D sounds
            bool hasReverb = AudioEnvironment.HasReverb;
            var settings = AudioEnvironment.Reverb;
            foreach (var fa in transform.root.GetComponentsInChildren<FootstepAudio>(true))
            {
                fa.SetLocalReverbPreset(hasReverb ? AudioReverbPreset.User : AudioReverbPreset.Off);
                if (!fa.LocalPlayer) continue;
                foreach (var f in fa.GetComponentsInChildren<AudioReverbFilter>(true))
                {
                    if (hasReverb) settings.ApplyTo(f);
                    // local 2D sources already carry their own filter: keep them out of the global zone
                    var src = f.GetComponent<AudioSource>();
                    if (src != null) src.reverbZoneMix = 0f;
                }
            }
            SetLocalEcho(kind == AudioEnvironmentKind.Garage);
        }

        private void SetLocalEcho(bool on)
        {
            var echo = AudioEnvironment.GarageEcho;
            foreach (var fa in transform.root.GetComponentsInChildren<FootstepAudio>(true))
            {
                if (!fa.LocalPlayer) continue;
                foreach (var f in fa.GetComponentsInChildren<AudioReverbFilter>(true))
                {
                    var e = f.GetComponent<AudioEchoFilter>();
                    if (!on) { if (e != null) e.enabled = false; continue; }
                    if (e == null) e = f.gameObject.AddComponent<AudioEchoFilter>();
                    e.enabled = true;
                    e.delay = echo.delayMs * 0.75f;
                    e.decayRatio = 0.22f;
                    e.wetMix = footstepEchoWet;
                    e.dryMix = 1f;
                }
            }
        }

        private void SetLocalPreset(AudioReverbPreset p)
        {
            foreach (var fa in transform.root.GetComponentsInChildren<FootstepAudio>(true))
                fa.SetLocalReverbPreset(p);
        }
    }
}
