using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Core;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Recorded stress breathing of one hider (ElevenLabs, Audio/Resources/Stress): a nervous "calm" loop and a panicked
    /// loop, blended by stress (crossfade 0.4-0.7), louder and a little faster the closer the seeker is. Each loop plays on
    /// two sources that overlap with an equal-power crossfade at the end (no audible seam) and start at a quiet moment
    /// between breaths. Single inhales are played as accents when the stress jumps (the seeker suddenly close).
    /// Local hider: 2D. Other players: 3D up to <see cref="StressBreathing.OthersRange"/> m, muffled by walls and silent
    /// through floors (<see cref="FloorAcoustics"/>), mixed like the other voices (effects x "Krzyki i oddech").
    /// </summary>
    public class BreathLoops : MonoBehaviour
    {
        private const float Crossfade = 0.6f;
        private const float CheckInterval = 0.15f;

        private static AudioClip _calm, _panic, _calmIn, _fastIn;
        private static float[] _calmQuiet, _panicQuiet;
        private static bool _loaded;

        /// <summary>True when the recorded clips are in the build (else the synthesised fallback is used).</summary>
        public static bool Available { get { Load(); return _calm != null && _panic != null; } }

        private class Track
        {
            public AudioClip Clip;
            public float[] Quiet;
            public readonly AudioSource[] Src = new AudioSource[2];
            public readonly AudioLowPassFilter[] Lp = new AudioLowPassFilter[2];
            public int Active;
            public bool Fading;
            public float T; // crossfade progress 0..1
        }

        private readonly Track _calmT = new Track(), _panicT = new Track();
        private AudioSource _accent;
        private AudioLowPassFilter _accentLp;
        private bool _local;
        private float _basePitch = 1f, _silentFor, _duck = 1f, _duckUntil;
        private float _floorGain = 1f, _occGain = 1f, _cutoff = 22000f, _nextCheck;

        public float CalmWeight { get; private set; }
        public float PanicWeight { get; private set; }
        /// <summary>Loudness factor sent to the sources (before distance / floor), for tests.</summary>
        public float Level { get; private set; }
        public float FloorGain => _floorGain;
        public int Seams { get; private set; }
        public int Accents { get; private set; }
        public bool Playing => _calmT.Src[0] != null && (_calmT.Src[0].isPlaying || _calmT.Src[1].isPlaying || _panicT.Src[0].isPlaying || _panicT.Src[1].isPlaying);

        public static BreathLoops Create(Transform parent, bool local)
        {
            var go = new GameObject(local ? "LocalBreath" : "BreathLoops");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BreathLoops>();
            b.Init(local);
            return b;
        }

        private void Init(bool local)
        {
            Load();
            _local = local;
            _basePitch = Random.Range(0.96f, 1.04f);
            _calmT.Clip = _calm; _calmT.Quiet = _calmQuiet;
            _panicT.Clip = _panic; _panicT.Quiet = _panicQuiet;
            foreach (var t in new[] { _calmT, _panicT })
                for (int i = 0; i < 2; i++) (t.Src[i], t.Lp[i]) = MakeSource("loop" + i);
            (_accent, _accentLp) = MakeSource("accent");
        }

        private (AudioSource, AudioLowPassFilter) MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.dopplerLevel = 0f;
            s.priority = _local ? 24 : 110;
            if (_local) { s.spatialBlend = 0f; s.reverbZoneMix = 0f; }
            else
            {
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.15f, 0.75f), new Keyframe(0.45f, 0.3f), new Keyframe(0.8f, 0.07f), new Keyframe(1f, 0f)));
                s.minDistance = 1f;
                s.maxDistance = StressBreathing.OthersRange;
                s.spread = 40f;
                s.reverbZoneMix = 0.5f;
            }
            var lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
            lp.enabled = !_local;
            return (s, lp);
        }

        /// <summary>Every frame: <paramref name="stress"/> 0..1 drives volume, blend and tempo.</summary>
        public void Tick(float stress, float dt)
        {
            float s = Mathf.Clamp01(stress);
            float mix = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.7f, s));
            CalmWeight = Mathf.Cos(mix * Mathf.PI * 0.5f);
            PanicWeight = Mathf.Sin(mix * Mathf.PI * 0.5f);
            float level = s < 0.03f ? 0f : Mathf.Lerp(0.18f, 0.95f, Mathf.SmoothStep(0f, 1f, s));
            if (Time.time > _duckUntil) _duck = Mathf.MoveTowards(_duck, 1f, dt * 1.5f);
            Level = level;
            float pitch = _basePitch * Mathf.Lerp(0.98f, 1.04f, s);

            if (level <= 0.001f) _silentFor += dt; else _silentFor = 0f;
            if (_silentFor > 0.5f) { StopAll(); return; }

            if (!_local) UpdateSpace();
            float mixVol = GameAudio.SfxVolume * GameAudio.VoiceVolume * (_local ? 1f : 0.85f * _floorGain * _occGain);
            UpdateTrack(_calmT, level * CalmWeight * _duck * mixVol, pitch, dt);
            UpdateTrack(_panicT, level * PanicWeight * _duck * mixVol, pitch, dt);
            if (_accent.isPlaying) _accent.volume = _accentLevel * mixVol;
        }

        private float _accentLevel;

        /// <summary>A single inhale on top of the loops (the stress jumped): deep nervous one or a panicked gasp.</summary>
        public void Accent(float stress)
        {
            var clip = stress > 0.6f ? _fastIn : _calmIn;
            if (clip == null) return;
            _accent.clip = clip;
            _accent.pitch = _basePitch * Random.Range(0.97f, 1.03f);
            _accentLevel = Mathf.Lerp(0.5f, 1f, stress);
            _accent.volume = 0f; // set in Tick
            _accent.Play();
            _duck = 0.45f; // the loops step back while the gasp sounds
            _duckUntil = Time.time + clip.length * 0.6f;
            Accents++;
        }

        private void UpdateTrack(Track t, float gain, float pitch, float dt)
        {
            if (t.Clip == null) return;
            var a = t.Src[t.Active];
            if (gain <= 0.0005f && !t.Fading)
            {
                foreach (var src in t.Src) if (src.isPlaying) src.volume = 0f;
                if (!a.isPlaying) return;
            }
            if (!a.isPlaying && !t.Fading)
            {
                StartAt(a, t, pitch);
                a.volume = 0f;
            }
            float remaining = (t.Clip.length - a.time) / Mathf.Max(0.5f, pitch);
            if (!t.Fading && a.isPlaying && remaining <= Crossfade + 0.05f)
            {
                // seam: the other source enters at a quiet moment while this one fades out
                StartAt(t.Src[1 - t.Active], t, pitch, true);
                t.Fading = true;
                t.T = 0f;
                Seams++;
            }
            float wOld = 1f, wNew = 0f;
            if (t.Fading)
            {
                t.T += dt / Crossfade;
                if (t.T >= 1f)
                {
                    a.Stop();
                    t.Active = 1 - t.Active;
                    t.Fading = false;
                    a = t.Src[t.Active];
                }
                else { wOld = Mathf.Cos(t.T * Mathf.PI * 0.5f); wNew = Mathf.Sin(t.T * Mathf.PI * 0.5f); }
            }
            var b = t.Src[1 - t.Active];
            a.pitch = b.pitch = pitch;
            // the fresh source of a track that just started fades in quickly instead of popping in
            a.volume = Mathf.MoveTowards(a.volume, gain * wOld, dt * 4f + (t.Fading ? 1f : 0f));
            if (t.Fading) b.volume = gain * wNew;
            for (int i = 0; i < 2; i++) if (t.Lp[i].enabled) t.Lp[i].cutoffFrequency = _cutoff;
            if (_accentLp.enabled) _accentLp.cutoffFrequency = _cutoff;
        }

        /// <summary>Starts a loop source at a quiet moment: anywhere for a fresh start, early in the clip for a seam
        /// (so the next seam comes as late as possible).</summary>
        private static void StartAt(AudioSource src, Track t, float pitch, bool seam = false)
        {
            src.clip = t.Clip;
            src.pitch = pitch;
            int n = t.Quiet.Length;
            if (seam)
            {
                n = 0;
                while (n < t.Quiet.Length && t.Quiet[n] < t.Clip.length * 0.3f) n++;
                n = Mathf.Max(1, n);
            }
            src.time = t.Quiet[Random.Range(0, n)];
            src.Play();
        }

        private void StopAll()
        {
            foreach (var t in new[] { _calmT, _panicT })
            {
                foreach (var s in t.Src) if (s != null && s.isPlaying) s.Stop();
                t.Fading = false;
            }
        }

        /// <summary>Remote breather: walls muffle it, another floor silences it (except next to a ramp / stairwell).</summary>
        private void UpdateSpace()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;
            var l = GameAudio.Listener;
            if (l == null) return;
            Vector3 lp = l.position, pos = transform.position;
            float occ = AudioOcclusion.Sample(lp, pos, l.root, transform.root, GameAudio.OcclusionMask);
            float floorCut = 22000f;
            _floorGain = FloorAcoustics.DifferentFloor(lp, pos) ? FloorAcoustics.Transfer(lp, pos, occ <= 0f, out floorCut) : 1f;
            float o = GameAudio.OcclusionEnabled ? occ : 0f;
            _occGain = Mathf.Lerp(1f, 0.5f, o);
            _cutoff = Mathf.Min(Mathf.Lerp(22000f, 750f, Mathf.Sqrt(o)), floorCut);
        }

        // ================================================================ clips

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _calm = Resources.Load<AudioClip>("Stress/calm_loop");
            _panic = Resources.Load<AudioClip>("Stress/panic_loop");
            _calmIn = Resources.Load<AudioClip>("Stress/calm_in_1");
            _fastIn = Resources.Load<AudioClip>("Stress/fast_in_1");
            _calmQuiet = QuietPoints(_calm);
            _panicQuiet = QuietPoints(_panic);
            Debug.Log($"[Breath] recorded clips: calm={(_calm != null ? _calm.length : 0f):0.0}s ({_calmQuiet.Length} start points) " +
                      $"panic={(_panic != null ? _panic.length : 0f):0.0}s ({_panicQuiet.Length}) in={_calmIn != null}/{_fastIn != null}");
        }

        /// <summary>Times (s) of the quiet moments between breaths - where a loop may start without cutting a breath.</summary>
        private static float[] QuietPoints(AudioClip c)
        {
            if (c == null) return new[] { 0f };
            var data = new float[c.samples * c.channels];
            if (!c.GetData(data, 0)) return new[] { 0f };
            int win = Mathf.Max(1, c.frequency / 20) * c.channels; // 50 ms
            var rms = new List<float>();
            for (int i = 0; i + win <= data.Length; i += win)
            {
                double sum = 0;
                for (int k = 0; k < win; k++) sum += data[i + k] * data[i + k];
                rms.Add((float)System.Math.Sqrt(sum / win));
            }
            var sorted = new List<float>(rms);
            sorted.Sort();
            float limit = sorted[Mathf.Clamp(sorted.Count * 3 / 10, 0, sorted.Count - 1)];
            var pts = new List<float>();
            float maxStart = c.length - Crossfade - 1.5f;
            for (int i = 0; i < rms.Count; i++)
            {
                float t = i * 0.05f;
                if (rms[i] <= limit && t <= maxStart && (pts.Count == 0 || t - pts[pts.Count - 1] > 0.4f)) pts.Add(t);
            }
            if (pts.Count == 0) pts.Add(0f);
            return pts.ToArray();
        }
    }
}
