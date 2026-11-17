using UnityEngine;

namespace HideAndSeek.Player
{
    /// <summary>
    /// Default "big concrete space" reverb carried by the player: an <see cref="AudioReverbZone"/> that follows the
    /// listener (affects 3D sounds such as other players' footsteps) and the matching preset for the local player's
    /// 2D footstep filters. A map overrides it simply by placing its own AudioReverbZone(s): on start this component
    /// then disables its zone and adopts the map zone's preset for the local filters.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioReverbZone))]
    public class GarageReverb : MonoBehaviour
    {
        [SerializeField] private AudioReverbPreset preset = AudioReverbPreset.ParkingLot;
        [SerializeField] private bool yieldToSceneZones = true;

        private AudioReverbZone _zone;

        public AudioReverbPreset Preset => preset;

        /// <summary>True when a scene (map) reverb zone replaced this default.</summary>
        public bool Overridden { get; private set; }

        private void Awake()
        {
            _zone = GetComponent<AudioReverbZone>();
            Apply(preset);
        }

        private void Start()
        {
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

        private void SetLocalPreset(AudioReverbPreset p)
        {
            foreach (var fa in transform.root.GetComponentsInChildren<FootstepAudio>(true))
                fa.SetLocalReverbPreset(p);
        }
    }
}
