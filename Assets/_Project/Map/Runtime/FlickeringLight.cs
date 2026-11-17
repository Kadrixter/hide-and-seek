using UnityEngine;

namespace HideAndSeek.Map
{
    /// <summary>
    /// Broken ceiling lamp. Drives a Light's intensity (and optionally the emission of the lamp's
    /// diffuser renderer) with noise, short dropouts and stuttering bursts.
    /// Deterministic per <see cref="seed"/> so every client sees a similar rhythm.
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class FlickeringLight : MonoBehaviour
    {
        public enum Pattern
        {
            /// <summary>Mostly on, unstable hum with short dropouts and stutter bursts.</summary>
            Unstable,
            /// <summary>Mostly off, occasionally sputters to life for a moment.</summary>
            Dying
        }

        [SerializeField] private Pattern pattern = Pattern.Unstable;
        [SerializeField] private float baseIntensity = 0.8f;
        [SerializeField] private Renderer emissiveRenderer;
        [SerializeField] private Color emissionColor = new Color(1f, 0.86f, 0.62f);
        [SerializeField] private float emissionStrength = 1.4f;
        [SerializeField] private int seed = 1;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Light _light;
        private MaterialPropertyBlock _block;
        private uint _rng;
        private float _stateUntil;
        private float _stutterToggleAt;
        private bool _stuttering;
        private bool _stutterOn;
        private bool _off;
        private float _lastFactor = -1f;

        public void Configure(Pattern newPattern, float intensity, Renderer emissive, Color emission, int newSeed)
        {
            pattern = newPattern;
            baseIntensity = intensity;
            emissiveRenderer = emissive;
            emissionColor = emission;
            seed = newSeed;
        }

        private void Awake()
        {
            _light = GetComponent<Light>();
            _block = new MaterialPropertyBlock();
            _rng = (uint)(seed * 747796405 + 2891336453u) | 1u;
        }

        private float Rand()
        {
            // xorshift32
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000;
        }

        private float Range(float a, float b) => a + (b - a) * Rand();

        private void Update()
        {
            float t = Time.time;
            float factor;

            if (t >= _stateUntil)
                PickNextState(t);

            if (_off)
            {
                factor = 0f;
            }
            else if (_stuttering)
            {
                if (t >= _stutterToggleAt)
                {
                    _stutterOn = !_stutterOn;
                    _stutterToggleAt = t + Range(0.02f, 0.09f);
                }
                factor = _stutterOn ? Range(0.6f, 1f) : 0.05f;
            }
            else
            {
                float n = Mathf.PerlinNoise(seed * 3.17f, t * (pattern == Pattern.Unstable ? 7f : 2.5f));
                factor = pattern == Pattern.Unstable ? Mathf.Lerp(0.6f, 1.05f, n) : Mathf.Lerp(0.15f, 0.7f, n);
            }

            Apply(factor);
        }

        private void PickNextState(float t)
        {
            _stuttering = false;
            _off = false;
            float r = Rand();
            if (pattern == Pattern.Unstable)
            {
                if (r < 0.55f) { _stateUntil = t + Range(0.8f, 4f); }
                else if (r < 0.85f) { _stuttering = true; _stateUntil = t + Range(0.2f, 0.9f); }
                else { _off = true; _stateUntil = t + Range(0.08f, 0.6f); }
            }
            else
            {
                if (r < 0.6f) { _off = true; _stateUntil = t + Range(1.5f, 6f); }
                else if (r < 0.85f) { _stuttering = true; _stateUntil = t + Range(0.15f, 0.7f); }
                else { _stateUntil = t + Range(0.3f, 1.5f); }
            }
        }

        private void Apply(float factor)
        {
            if (Mathf.Abs(factor - _lastFactor) < 0.002f) return;
            _lastFactor = factor;
            _light.intensity = baseIntensity * factor;
            _light.enabled = factor > 0.01f;
            if (emissiveRenderer != null)
            {
                emissiveRenderer.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, emissionColor * (emissionStrength * factor));
                emissiveRenderer.SetPropertyBlock(_block);
            }
        }
    }
}
