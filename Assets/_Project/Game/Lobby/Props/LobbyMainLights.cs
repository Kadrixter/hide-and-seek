using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Ceiling lights on the light switch (<see cref="LobbyPropKind.LightSwitch"/>): fade off / on in 0.25 s with a
    /// short flicker when switching on; lamps, fireplace and neon stay (cosy mode). The switch rocker flips.
    /// </summary>
    public class LobbyMainLights : MonoBehaviour
    {
        public Light[] lights;
        public Renderer[] glows;
        public Transform rocker;
        private float[] _base;
        private Color[] _emit;
        private Material[] _mats;
        private float _level = -1f, _flickerUntil;
        private bool _was;

        private void Start()
        {
            _base = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) _base[i] = lights[i] != null ? lights[i].intensity : 0f;
            _mats = new Material[glows.Length];
            _emit = new Color[glows.Length];
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null) continue;
                _mats[i] = glows[i].material;
                _emit[i] = _mats[i].HasProperty("_EmissionColor") ? _mats[i].GetColor("_EmissionColor") : Color.black;
            }
        }

        private void Update()
        {
            bool on = LobbyPropDriver.On(LobbyPropKind.LightSwitch);
            if (_level < 0f) { _level = on ? 1f : 0f; _was = on; }
            if (on && !_was) _flickerUntil = Time.time + 0.35f;
            if (on != _was && rocker != null) LobbyPropSounds.Play(LobbyPropSounds.Id.Click, rocker.position, 0.5f);
            _was = on;
            _level = Mathf.MoveTowards(_level, on ? 1f : 0f, Time.deltaTime * 4f);
            float l = _level;
            if (Time.time < _flickerUntil) l *= Mathf.PerlinNoise(Time.time * 40f, 0.3f) > 0.45f ? 1f : 0.15f;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) { lights[i].intensity = _base[i] * l; lights[i].enabled = l > 0.01f; }
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i] != null) _mats[i].SetColor("_EmissionColor", _emit[i] * Mathf.Lerp(0.05f, 1f, l));
            if (rocker != null) rocker.localRotation = Quaternion.Euler(on ? -12f : 12f, 0f, 0f);
        }
    }
}
