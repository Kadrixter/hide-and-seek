using UnityEngine;

namespace HideAndSeek.Game.UI
{
    /// <summary>
    /// Main menu backdrop life: the shadow figure's red eyes blink (and sometimes vanish for a while), the red
    /// lantern flickers, the corridor light pulses, the reflection probe for the wet floor is rendered once
    /// (and again after a few seconds, when everything is lit), and the camera gets a depth texture for the beam.
    /// </summary>
    public class MenuAmbience : MonoBehaviour
    {
        [SerializeField] private Renderer[] eyes;
        [SerializeField] private Light eyeGlow;
        [SerializeField] private Light lantern;
        [SerializeField] private Light corridor;
        [SerializeField] private ReflectionProbe probe;

        private float _lanternBase, _corridorBase, _glowBase;
        private float _nextBlink, _eyesOffUntil;
        private float _probeAgain = 1.5f;

        private void Start()
        {
            if (lantern != null) _lanternBase = lantern.intensity;
            if (corridor != null) _corridorBase = corridor.intensity;
            if (eyeGlow != null) _glowBase = eyeGlow.intensity;
            var cam = Camera.main;
            if (cam != null) cam.depthTextureMode |= DepthTextureMode.Depth;
            if (probe != null) probe.RenderProbe();
            _nextBlink = Time.time + Random.Range(2f, 5f);
        }

        private void Update()
        {
            float t = Time.time;
            if (_probeAgain > 0f && t > _probeAgain)
            {
                _probeAgain = 0f;
                if (probe != null) probe.RenderProbe();
            }

            // Lantern: soft flame flicker with an occasional dip.
            if (lantern != null)
            {
                float f = 0.85f + 0.1f * Mathf.PerlinNoise(t * 3.1f, 0.3f) + 0.05f * Mathf.Sin(t * 17f);
                if (Mathf.PerlinNoise(t * 0.7f, 5f) > 0.82f) f *= 0.6f;
                lantern.intensity = _lanternBase * f;
            }
            if (corridor != null) corridor.intensity = _corridorBase * (0.8f + 0.2f * Mathf.PerlinNoise(t * 0.6f, 9f));

            // Eyes: quick blinks every few seconds; sometimes the figure "disappears" for a moment.
            bool on = t >= _eyesOffUntil;
            if (t >= _nextBlink)
            {
                bool longGone = Random.value < 0.15f;
                _eyesOffUntil = t + (longGone ? Random.Range(2f, 4f) : 0.12f);
                _nextBlink = _eyesOffUntil + Random.Range(2.5f, 6f);
            }
            if (eyes != null)
                foreach (var e in eyes)
                    if (e != null && e.enabled != on) e.enabled = on;
            if (eyeGlow != null) eyeGlow.intensity = on ? _glowBase : 0f;
        }
    }
}
