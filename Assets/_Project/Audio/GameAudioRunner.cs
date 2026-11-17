using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>Pooled AudioSources behind <see cref="GameAudio"/>.</summary>
    [AddComponentMenu("")]
    public class GameAudioRunner : MonoBehaviour
    {
        private class Voice
        {
            public AudioSource Source;
            public AudioEchoFilter Echo;
            public Transform Follow;
            public float FadeOut = -1f;
            public float FadeFrom;
            public float FadeTime;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private AudioSource _bedA, _bedB;
        private bool _bedAActive;
        private float _bedFade = 1f, _bedFadeTime = 1f, _bedTargetVol;

        private Voice GetFree()
        {
            foreach (var v in _voices)
                if (v.Source != null && !v.Source.isPlaying && v.FadeOut < 0f) return v;
            var go = new GameObject("sfx");
            go.transform.SetParent(transform, false);
            var voice = new Voice { Source = go.AddComponent<AudioSource>() };
            voice.Source.playOnAwake = false;
            voice.Echo = go.AddComponent<AudioEchoFilter>();
            voice.Echo.enabled = false;
            _voices.Add(voice);
            return voice;
        }

        public AudioSource Play(SfxEntry e, Vector3 pos, Transform follow, float volume, bool spatial, bool loop)
        {
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            var clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) return null;

            var v = GetFree();
            var s = v.Source;
            v.Follow = follow;
            v.FadeOut = -1f;
            s.transform.position = pos;
            s.clip = clip;
            s.loop = loop;
            s.volume = Mathf.Clamp01(e.volume * volume * GameAudio.SfxVolume);
            s.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff);
            s.maxDistance = Mathf.Max(1f, e.range);
            s.minDistance = 1f;
            s.dopplerLevel = 0f;
            s.spread = 40f;
            v.Echo.enabled = e.echo;
            if (e.echo)
            {
                v.Echo.delay = 180f;
                v.Echo.decayRatio = 0.35f;
                v.Echo.wetMix = 0.45f;
                v.Echo.dryMix = 1f;
            }
            s.Play();
            return s;
        }

        private static AnimationCurve _rolloff;
        private static AnimationCurve Rolloff => _rolloff ??= new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.08f, 0.85f), new Keyframe(0.3f, 0.35f),
            new Keyframe(0.65f, 0.1f), new Keyframe(1f, 0f));

        public void Stop(AudioSource src, float fade)
        {
            foreach (var v in _voices)
            {
                if (v.Source != src) continue;
                if (fade <= 0f) { src.Stop(); v.Follow = null; return; }
                v.FadeOut = 0f;
                v.FadeTime = fade;
                v.FadeFrom = src.volume;
                return;
            }
        }

        public void SetBed(AmbientEntry e, float fade)
        {
            if (_bedA == null)
            {
                _bedA = gameObject.AddComponent<AudioSource>();
                _bedB = gameObject.AddComponent<AudioSource>();
                foreach (var b in new[] { _bedA, _bedB }) { b.loop = true; b.spatialBlend = 0f; b.playOnAwake = false; b.volume = 0f; }
            }
            var next = _bedAActive ? _bedB : _bedA;
            if ((_bedAActive ? _bedA : _bedB).clip == e?.clip && e?.clip != null) return;
            _bedAActive = !_bedAActive;
            next.clip = e?.clip;
            next.volume = 0f;
            if (next.clip != null) next.Play();
            _bedTargetVol = e != null ? e.volume : 0f;
            _bedFade = 0f;
            _bedFadeTime = Mathf.Max(0.01f, fade);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var v in _voices)
            {
                if (v.Source == null) continue;
                if (v.Follow != null && v.Source.isPlaying) v.Source.transform.position = v.Follow.position;
                if (v.FadeOut >= 0f)
                {
                    v.FadeOut += dt;
                    float k = 1f - v.FadeOut / v.FadeTime;
                    v.Source.volume = v.FadeFrom * Mathf.Max(0f, k);
                    if (k <= 0f) { v.Source.Stop(); v.FadeOut = -1f; v.Follow = null; }
                }
            }

            if (_bedA != null && _bedFade < 1f)
            {
                _bedFade = Mathf.Min(1f, _bedFade + dt / _bedFadeTime);
                var cur = _bedAActive ? _bedA : _bedB;
                var old = _bedAActive ? _bedB : _bedA;
                cur.volume = _bedTargetVol * GameAudio.MusicVolume * _bedFade;
                old.volume = Mathf.Min(old.volume, (1f - _bedFade) * GameAudio.MusicVolume);
                if (_bedFade >= 1f && old.isPlaying) old.Stop();
            }
        }
    }
}
