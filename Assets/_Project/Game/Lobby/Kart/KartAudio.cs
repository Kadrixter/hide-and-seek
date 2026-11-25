using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Go-kart sound on every client: the engine loop (<see cref="KartSounds.Engine"/>) runs while somebody drives,
    /// its pitch following the revs (idle ~2900 rpm, limiter ~9500) and its volume the throttle; the tyre squeal loop
    /// fades in with the slide strength (pitch a little higher the faster it slides); bumps play a thud. 3D sources on
    /// the kart, scaled by the game's effects volume.
    /// </summary>
    public sealed class KartAudio : MonoBehaviour
    {
        private LobbyKart _kart;
        private AudioSource _engine, _screech;
        private float _run;

        public void Init(LobbyKart kart)
        {
            _kart = kart;
            _engine = Source("Engine", KartSounds.Engine, 2.2f, 32f);
            _screech = Source("Screech", KartSounds.Screech, 2f, 30f);
        }

        private AudioSource Source(string name, AudioClip clip, float min, float max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.2f, 0.3f, -0.45f);
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = min;
            s.maxDistance = max;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>A bump of strength <paramref name="k"/> (0..1).</summary>
        public void Bump(float k)
        {
            if (k < 0.08f) return;
            GameAudio.PlayClipAt(KartSounds.Thud, transform.position + Vector3.up * 0.3f, 0.35f + 0.65f * k, 25f);
        }

        private void Update()
        {
            if (_kart == null) return;
            float dt = Time.deltaTime;
            bool on = _kart.View != LobbyKart.Mode.Parked;
            _run = Mathf.MoveTowards(_run, on ? 1f : 0f, dt / (on ? 0.25f : 0.6f));
            float sfx = GameAudio.SfxVolume;

            if (_run > 0f && !_engine.isPlaying) { _engine.time = Random.Range(0f, 0.9f); _engine.Play(); }
            if (_run <= 0f && _engine.isPlaying) _engine.Stop();
            float rpm = _kart.Rpm01, thr = Mathf.Abs(_kart.Throttle);
            _engine.pitch = Mathf.Lerp(_engine.pitch, 0.8f + 1.85f * rpm, 1f - Mathf.Exp(-12f * dt));
            _engine.volume = sfx * _run * (0.22f + 0.28f * thr + 0.25f * rpm);

            float slip = _kart.Slip01;
            float want = on ? Mathf.Clamp01((slip - 0.12f) / 0.6f) : 0f;
            if (want > 0.01f && !_screech.isPlaying) { _screech.time = Random.Range(0f, 1.8f); _screech.Play(); }
            float vol = Mathf.MoveTowards(_screech.volume / Mathf.Max(0.0001f, sfx), want * 0.55f, dt * 3f);
            _screech.volume = sfx * vol;
            _screech.pitch = 0.9f + 0.25f * Mathf.Clamp01(Mathf.Abs(_kart.ShownSpeed) / KartPhysics.MaxSpeed) + 0.05f * Mathf.Sin(Time.time * 2.3f);
            if (vol <= 0.001f && _screech.isPlaying) _screech.Stop();
        }
    }
}
