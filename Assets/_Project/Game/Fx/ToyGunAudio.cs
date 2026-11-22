using HideAndSeek.Audio;
using HideAndSeek.Core;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// The loud tune the toy slime blaster plays while it is in the hands (fire-fire.mp3, looped): it gives the hider
    /// away. Own AudioSource (not the shared pool, which mutes other floors): parking-lot reverb + echo, a long flat
    /// rolloff so it carries across the whole garage, and instead of silence between floors it is only quieter and
    /// muffled (one floor apart: about 40 %, two floors: about 20 %).
    /// </summary>
    public class ToyGunAudio : MonoBehaviour
    {
        private const float Range = 150f;
        private static AudioClip _clip;
        private static AnimationCurve _rolloff;

        private AudioSource _src;
        private AudioLowPassFilter _lp;
        private NetPlayer _player;
        private float _fade, _floorGain = 1f, _cutoff = 22000f;
        private bool _playing;

        public static int Playing; // number of guns heard right now (tests)

        /// <summary>Current volume / low-pass cut-off (tests).</summary>
        public float DebugVolume => _src != null ? _src.volume : 0f;
        public float DebugCutoff => _cutoff;

        public static ToyGunAudio Attach(NetPlayer p)
        {
            var go = new GameObject("ToyGunTune");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0.15f, 1.25f, 0.4f);
            var a = go.AddComponent<ToyGunAudio>();
            a._player = p;
            a.Build();
            return a;
        }

        private void Build()
        {
            _clip ??= Resources.Load<AudioClip>("Toys/fire-fire");
            _rolloff ??= new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(6f / Range, 0.95f), new Keyframe(25f / Range, 0.62f),
                new Keyframe(60f / Range, 0.36f), new Keyframe(110f / Range, 0.2f), new Keyframe(1f, 0.1f));
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = _clip;
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Custom;
            _src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _rolloff);
            _src.maxDistance = Range;
            _src.minDistance = 1f;
            _src.dopplerLevel = 0f;
            _src.spread = 60f;
            _src.priority = 20;
            _src.volume = 0f;
            _lp = gameObject.AddComponent<AudioLowPassFilter>();
            _lp.cutoffFrequency = 22000f;
            var echo = gameObject.AddComponent<AudioEchoFilter>();
            echo.delay = 280f;
            echo.decayRatio = 0.42f;
            echo.wetMix = 0.42f;
            echo.dryMix = 1f;
            var rev = gameObject.AddComponent<AudioReverbFilter>();
            rev.reverbPreset = AudioReverbPreset.ParkingLot;
            rev.dryLevel = 0f;
        }

        public void SetPlaying(bool on)
        {
            if (on == _playing) return;
            _playing = on;
            Playing += on ? 1 : -1;
            if (on && _src != null && _src.clip != null && !_src.isPlaying)
            {
                _src.time = Random.Range(0f, Mathf.Max(0f, _src.clip.length - 0.1f));
                _src.Play();
            }
        }

        private void Update()
        {
            if (_src == null) return;
            _fade = Mathf.MoveTowards(_fade, _playing ? 1f : 0f, Time.deltaTime / (_playing ? 0.25f : 0.35f));
            if (_fade <= 0f && _src.isPlaying && !_playing) _src.Stop();
            if (!_src.isPlaying && _fade <= 0f) return;

            // Floors: muffled and quieter instead of the usual silence.
            var al = HideAndSeek.Core.ActiveListener.Find();
            var listener = al != null ? al.transform : null;
            float gain = 1f, cut = 22000f;
            if (listener != null && _player != null && !_player.IsOwner)
            {
                int df = Mathf.Abs(FloorAcoustics.ListenerFloor(listener.position.y) - FloorAcoustics.SourceFloor(transform.position.y - 1.2f));
                if (df == 1) { gain = 0.42f; cut = 1500f; }
                else if (df >= 2) { gain = 0.2f; cut = 900f; }
            }
            float k = 1f - Mathf.Exp(-4f * Time.deltaTime);
            _floorGain = Mathf.Lerp(_floorGain, gain, k);
            _cutoff = Mathf.Lerp(_cutoff, cut, k);
            _lp.cutoffFrequency = _cutoff;
            // The holder hears it a bit softer (it is right in the hand), everybody else at full blast.
            float own = _player != null && _player.IsOwner ? 0.55f : 1f;
            _src.volume = Mathf.Clamp01(_fade * _floorGain * own * GameAudio.SfxVolume);
        }

        private void OnDestroy()
        {
            if (_playing) Playing--;
        }
    }
}
