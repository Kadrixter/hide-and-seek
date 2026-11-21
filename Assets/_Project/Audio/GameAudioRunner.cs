using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>
    /// Pooled AudioSources behind <see cref="GameAudio"/>. Per voice: custom rolloff, distance-dependent reverb send
    /// (far sounds are wetter), echo for loud sounds taken from <see cref="AudioEnvironment"/>, and cheap occlusion
    /// (a few raycasts per second between listener and source; walls muffle with a low-pass and lower the volume).
    /// </summary>
    [AddComponentMenu("")]
    public class GameAudioRunner : MonoBehaviour
    {
        private const int MaxVoices = 64;
        private const float OcclusionInterval = 0.12f;

        private class Voice
        {
            public AudioSource Source;
            public AudioEchoFilter Echo;
            public AudioLowPassFilter LowPass;
            public Transform Follow;
            public bool HadFollow; // loop / sound attached to an object: stops when the object is destroyed
            public SfxEntry Entry;
            public bool Spatial;
            public float BaseVolume;
            public float EchoWet;
            public float Occlusion, OcclusionTarget, NextOcclusionCheck;
            public float OtherFloor; // 0..1, eased: source on another floor than the listener (and the slab is in the way)
            public bool FloorBlocked;
            public float NextFloorCheck;
            public float StartTime;
            public float FadeOut = -1f;
            public float FadeFrom = 1f;
            public float FadeTime;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private AudioSource _bedA, _bedB;
        private bool _bedAActive;
        private float _bedFade = 1f, _bedFadeTime = 1f, _bedTargetVol;
        private AudioListener _listener;
        private float _nextListenerSearch;

        // ------------------------------------------------------------------ pool

        private Voice CreateVoice()
        {
            var go = new GameObject("sfx");
            go.transform.SetParent(transform, false);
            var voice = new Voice { Source = go.AddComponent<AudioSource>() };
            voice.Source.playOnAwake = false;
            voice.LowPass = go.AddComponent<AudioLowPassFilter>();
            voice.LowPass.cutoffFrequency = 22000f;
            voice.LowPass.lowpassResonanceQ = 1f;
            voice.LowPass.enabled = false;
            voice.Echo = go.AddComponent<AudioEchoFilter>();
            voice.Echo.enabled = false;
            _voices.Add(voice);
            return voice;
        }

        private Voice GetFree()
        {
            foreach (var v in _voices)
                if (v.Source != null && !v.Source.isPlaying && v.FadeOut < 0f) return v;
            if (_voices.Count < MaxVoices) return CreateVoice();

            // steal: the non-looping voice that started first
            Voice best = null;
            foreach (var v in _voices)
            {
                if (v.Source == null || v.Source.loop) continue;
                if (best == null || v.StartTime < best.StartTime) best = v;
            }
            if (best == null) return CreateVoice();
            best.Source.Stop();
            best.FadeOut = -1f;
            return best;
        }

        // ------------------------------------------------------------------ play / stop

        public AudioSource Play(SfxEntry e, Vector3 pos, Transform follow, float volume, bool spatial, bool loop)
        {
            if (e == null || e.clips == null || e.clips.Length == 0) return null;
            var clip = e.clips[Random.Range(0, e.clips.Length)];
            if (clip == null) return null;

            var v = GetFree();
            var s = v.Source;
            v.Follow = follow;
            v.HadFollow = follow != null;
            v.Entry = e;
            v.Spatial = spatial;
            v.FadeOut = -1f;
            v.StartTime = Time.unscaledTime;
            v.BaseVolume = Mathf.Clamp01(e.volume * volume * GameAudio.SfxVolume * GameAudio.CategoryVolume(e.id));
            s.transform.position = pos;
            s.clip = clip;
            s.loop = loop;
            s.volume = v.BaseVolume;
            s.pitch = 1f + Random.Range(-e.pitchJitter, e.pitchJitter);
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Custom;
            s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, e.id == GameSfx.GlockShotDistant ? DistantRolloff : Rolloff);
            s.maxDistance = Mathf.Max(1f, e.range);
            s.minDistance = 1f;
            s.dopplerLevel = 0f;
            s.spread = spatial ? 40f : 0f;
            s.panStereo = 0f;
            s.priority = e.echo ? 32 : (loop ? 96 : 128);
            s.reverbZoneMix = spatial ? Mathf.Clamp(e.reverbMix, 0f, 1.1f) : 0f;

            // occlusion starts from a fresh sample so a sound behind a wall never pops in bright
            v.Occlusion = v.OcclusionTarget = spatial ? SampleOcclusion(pos, follow) : 0f;
            v.NextOcclusionCheck = Time.unscaledTime + OcclusionInterval;
            ApplyOcclusion(v);

            // echo from the current acoustic space
            bool echo = e.echo && spatial;
            var env = AudioEnvironment.Echo;
            if (AudioEnvironment.Current == AudioEnvironmentKind.None)
                env = new EchoSettings { enabled = true, delayMs = 180f, decay = 0.35f, wet = 0.45f };
            echo &= env.enabled;
            v.Echo.enabled = echo;
            if (echo)
            {
                v.Echo.delay = env.delayMs;
                v.Echo.decayRatio = env.decay;
                v.Echo.dryMix = 1f;
                v.EchoWet = env.wet;
                v.Echo.wetMix = env.wet;
            }
            UpdateDistanceMix(v);
            s.Play();
            return s;
        }

        private static AnimationCurve _rolloff, _distantRolloff;

        /// <summary>Default rolloff: near-full up close, ~1/d through the middle, silent at the entry range.</summary>
        private static AnimationCurve Rolloff => _rolloff ??= new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.04f, 0.92f), new Keyframe(0.12f, 0.6f), new Keyframe(0.3f, 0.3f),
            new Keyframe(0.55f, 0.13f), new Keyframe(0.8f, 0.04f), new Keyframe(1f, 0f));

        /// <summary>Far-tail layer: quiet close by (the close shot dominates), strongest at mid/far distances.</summary>
        private static AnimationCurve DistantRolloff => _distantRolloff ??= new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(0.12f, 0.55f), new Keyframe(0.3f, 0.6f), new Keyframe(0.65f, 0.3f),
            new Keyframe(1f, 0f));

        public void Stop(AudioSource src, float fade)
        {
            foreach (var v in _voices)
            {
                if (v.Source != src) continue;
                if (fade <= 0f) { src.Stop(); v.Follow = null; v.FadeOut = -1f; return; }
                v.FadeOut = 0f;
                v.FadeTime = fade;
                v.FadeFrom = 1f;
                return;
            }
        }

        public void StopAll(float fade)
        {
            foreach (var v in _voices)
                if (v.Source != null && v.Source.isPlaying) Stop(v.Source, fade);
        }

        public void SetBed(AmbientEntry e, float fade)
        {
            if (_bedA == null)
            {
                _bedA = gameObject.AddComponent<AudioSource>();
                _bedB = gameObject.AddComponent<AudioSource>();
                foreach (var b in new[] { _bedA, _bedB })
                {
                    b.loop = true; b.spatialBlend = 0f; b.playOnAwake = false; b.volume = 0f;
                    b.reverbZoneMix = 0f; b.priority = 16; b.ignoreListenerPause = true;
                }
            }
            var next = _bedAActive ? _bedB : _bedA;
            if ((_bedAActive ? _bedA : _bedB).clip == e?.clip && e?.clip != null) return;
            _bedAActive = !_bedAActive;
            next.clip = e?.clip;
            next.volume = 0f;
            if (next.clip != null)
            {
                // start loops at a random point so a re-entered space does not always sound identical
                next.time = Random.Range(0f, next.clip.length * 0.9f);
                next.Play();
            }
            _bedTargetVol = e != null ? e.volume : 0f;
            _bedFade = 0f;
            _bedFadeTime = Mathf.Max(0.01f, fade);
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            RefreshListener(now);

            foreach (var v in _voices)
            {
                var s = v.Source;
                if (s == null || !s.isPlaying) continue;
                if (v.Follow != null) s.transform.position = v.Follow.position;
                else if (v.HadFollow && s.loop && v.FadeOut < 0f)
                {
                    // Owner destroyed (scene change, player left) without StopLoop: never leave a loop playing forever.
                    v.HadFollow = false;
                    Debug.Log("[GameAudio] orphan loop stopped: " + (s.clip != null ? s.clip.name : "?"));
                    v.FadeFrom = 1f;
                    v.FadeTime = 0.3f;
                    v.FadeOut = 0f;
                }

                if (v.Spatial)
                {
                    if (now >= v.NextOcclusionCheck)
                    {
                        v.NextOcclusionCheck = now + OcclusionInterval;
                        v.OcclusionTarget = SampleOcclusion(s.transform.position, v.Follow);
                    }
                    v.Occlusion = Mathf.MoveTowards(v.Occlusion, v.OcclusionTarget, dt * 5f);
                    bool other = false;
                    if (_listener != null && AudioFloors.DifferentFloor(_listener.transform.position, s.transform.position))
                    {
                        // Concrete slab between the floors: nothing gets through, unless there is a clear line
                        // (looking down a ramp at the sound source).
                        if (now >= v.NextFloorCheck)
                        {
                            v.NextFloorCheck = now + OcclusionInterval;
                            v.FloorBlocked = AudioOcclusion.Sample(_listener.transform.position, s.transform.position,
                                _listener.transform.root, v.Follow != null ? v.Follow.root : null, GameAudio.OcclusionMask) > 0f;
                        }
                        other = v.FloorBlocked;
                    }
                    v.OtherFloor = Mathf.MoveTowards(v.OtherFloor, other ? 1f : 0f, dt * 5f);
                    ApplyOcclusion(v);
                    UpdateDistanceMix(v);
                }

                float fadeMul = 1f;
                if (v.FadeOut >= 0f)
                {
                    v.FadeOut += dt;
                    fadeMul = Mathf.Max(0f, 1f - v.FadeOut / Mathf.Max(0.001f, v.FadeTime));
                    if (fadeMul <= 0f) { s.Stop(); v.FadeOut = -1f; v.Follow = null; continue; }
                }
                s.volume = v.BaseVolume * fadeMul * OcclusionGain(v.Occlusion) * (1f - v.OtherFloor);
            }

            if (_bedA != null)
            {
                var cur = _bedAActive ? _bedA : _bedB;
                var old = _bedAActive ? _bedB : _bedA;
                if (_bedFade < 1f)
                {
                    _bedFade = Mathf.Min(1f, _bedFade + dt / _bedFadeTime);
                    old.volume = Mathf.Min(old.volume, (1f - _bedFade) * GameAudio.MusicVolume);
                    if (_bedFade >= 1f && old.isPlaying) old.Stop();
                }
                cur.volume = _bedTargetVol * GameAudio.MusicVolume * _bedFade;
            }
        }

        private void RefreshListener(float now)
        {
            if (_listener != null && _listener.isActiveAndEnabled) return;
            if (now < _nextListenerSearch) return;
            _nextListenerSearch = now + 0.5f;
            _listener = FindAnyObjectByType<AudioListener>();
        }

        /// <summary>World position of the active AudioListener (or null when none).</summary>
        public Transform Listener
        {
            get
            {
                RefreshListener(Time.unscaledTime);
                return _listener != null ? _listener.transform : null;
            }
        }

        private void UpdateDistanceMix(Voice v)
        {
            if (!v.Spatial || _listener == null) return;
            float d = Vector3.Distance(_listener.transform.position, v.Source.transform.position);
            float t = Mathf.Clamp01(d / Mathf.Max(1f, v.Source.maxDistance));
            // distant sounds are carried mostly by the room: more reverb send, more echo
            float baseMix = v.Entry != null ? v.Entry.reverbMix : 1f;
            v.Source.reverbZoneMix = Mathf.Clamp(baseMix * Mathf.Lerp(0.85f, 1.1f, t) + v.Occlusion * 0.1f, 0f, 1.1f);
            if (v.Echo.enabled) v.Echo.wetMix = Mathf.Clamp01(v.EchoWet * Mathf.Lerp(0.55f, 1.25f, t) * Mathf.Lerp(1f, 0.25f, v.OtherFloor));
            v.Source.reverbZoneMix *= Mathf.Lerp(1f, 0.5f, v.OtherFloor);
        }

        private static float OcclusionGain(float occ) => Mathf.Lerp(1f, 0.5f, occ);

        private static void ApplyOcclusion(Voice v)
        {
            // Walls (occlusion) and other floors (through concrete slabs: heavy low-pass, little echo).
            bool on = v.Spatial && (v.Occlusion > 0.01f || v.OtherFloor > 0.01f);
            v.LowPass.enabled = on;
            if (on) v.LowPass.cutoffFrequency = Mathf.Min(Mathf.Lerp(22000f, 750f, Mathf.Sqrt(v.Occlusion)), Mathf.Lerp(22000f, 520f, v.OtherFloor));
        }

        private float SampleOcclusion(Vector3 sourcePos, Transform follow)
        {
            if (!GameAudio.OcclusionEnabled) return 0f;
            RefreshListener(Time.unscaledTime);
            if (_listener == null) return 0f;
            return AudioOcclusion.Sample(_listener.transform.position, sourcePos, _listener.transform.root,
                follow != null ? follow.root : null, GameAudio.OcclusionMask);
        }
    }

    /// <summary>Parking floors (slab tops at y 0 / -3.4 / -6.8): sounds do not travel through the concrete between them.</summary>
    public static class AudioFloors
    {
        /// <summary>Floor index (0, -1, -2) of a point at body height.</summary>
        public static int FloorOf(float y) => y >= -0.25f ? 0 : y >= -3.65f ? -1 : -2;

        /// <summary>
        /// True when <paramref name="listener"/> (camera) and <paramref name="source"/> are on different floors and
        /// far enough apart vertically (players next to each other on a ramp still hear each other).
        /// </summary>
        public static bool DifferentFloor(Vector3 listener, Vector3 source)
        {
            if (Mathf.Abs(listener.y - source.y) < 2.0f) return false;
            return FloorOf(listener.y - 0.4f) != FloorOf(source.y - 0.1f);
        }
    }

    /// <summary>Cheap listener-to-source occlusion: counts solid colliders crossed by one ray.</summary>
    public static class AudioOcclusion
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>0 = clear line, ~0.65 = one wall, 1 = two or more walls.</summary>
        public static float Sample(Vector3 listener, Vector3 source, Transform ignoreA, Transform ignoreB, LayerMask mask)
        {
            Vector3 dir = source - listener;
            float dist = dir.magnitude;
            if (dist < 0.75f) return 0f;
            dir /= dist;
            // lift a little off the floor so steps on the ground are not "behind" the floor itself
            Vector3 from = listener;
            float len = dist - 0.35f;
            int n = Physics.RaycastNonAlloc(from, dir, Hits, len, mask, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (c == null) continue;
                var t = c.transform;
                if ((ignoreA != null && t.IsChildOf(ignoreA)) || (ignoreB != null && t.IsChildOf(ignoreB))) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // props / players
                walls++;
            }
            return walls == 0 ? 0f : walls == 1 ? 0.65f : 1f;
        }
    }
}
