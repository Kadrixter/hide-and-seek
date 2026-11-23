using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Electric fireplace: flame cards behind the glass waver and flicker (UV scroll + scale), embers glow, a warm
    /// light flickers; the toggle (<see cref="LobbyPropKind.Fireplace"/>) fades it all in / out, the hiss follows.
    /// </summary>
    public class LobbyFireplace : MonoBehaviour
    {
        public Renderer[] flames;
        public Renderer embers;
        public Light glow;
        public Transform knob;
        private Material[] _mats;
        private Vector3[] _scale;
        private Material _ember;
        private Color _emberEmit;
        private float _glowBase, _level = -1f;
        private AudioSource _hiss;

        private void Start()
        {
            _mats = new Material[flames.Length];
            _scale = new Vector3[flames.Length];
            for (int i = 0; i < flames.Length; i++) if (flames[i] != null) { _mats[i] = flames[i].material; _scale[i] = flames[i].transform.parent.localScale; }
            if (embers != null)
            {
                _ember = embers.material;
                _emberEmit = _ember.HasProperty("_EmissionColor") ? _ember.GetColor("_EmissionColor") : Color.black;
            }
            if (glow != null) _glowBase = glow.intensity;
            _hiss = LobbyPropSounds.Loop(transform, LobbyPropSounds.Id.FireLoop, 5f);
            _hiss.Play();
        }

        private void Update()
        {
            bool on = LobbyPropDriver.On(LobbyPropKind.Fireplace);
            if (_level < 0f) _level = on ? 1f : 0f;
            _level = Mathf.MoveTowards(_level, on ? 1f : 0f, Time.deltaTime * (on ? 0.8f : 1.5f));
            float t = Time.time;
            for (int i = 0; i < _mats.Length; i++)
            {
                if (_mats[i] == null) continue;
                _mats[i].mainTextureOffset = new Vector2(i * 0.37f + Mathf.Sin(t * 0.7f + i) * 0.05f, -t * (0.35f + i * 0.08f));
                float f = (0.75f + 0.25f * Mathf.PerlinNoise(t * 3.1f, i * 3.7f)) * _level;
                if (_mats[i].HasProperty("_TintColor")) _mats[i].SetColor("_TintColor", new Color(0.5f * f, 0.5f * f, 0.5f * f, 0.5f));
                else _mats[i].color = new Color(1f, 1f, 1f, f);
                var tr = flames[i].transform.parent; // pivot at the flame base
                tr.localScale = new Vector3(_scale[i].x, Mathf.Lerp(0.02f, 1f, _level) * (0.88f + 0.18f * Mathf.PerlinNoise(t * 2.3f, i)) * _scale[i].y, _scale[i].z);
            }
            if (_ember != null) _ember.SetColor("_EmissionColor", _emberEmit * (0.15f + 0.85f * _level) * (0.85f + 0.15f * Mathf.PerlinNoise(t * 1.7f, 9f)));
            if (glow != null) glow.intensity = _glowBase * _level * (0.8f + 0.25f * Mathf.PerlinNoise(t * 5f, 2f));
            if (_hiss != null) _hiss.volume = 0.18f * _level;
            if (knob != null) knob.localRotation = Quaternion.Euler(0f, 0f, on ? -60f : 30f);
        }

    }
}
