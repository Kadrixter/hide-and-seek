using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Plays one remote player's voice in 3D from their head: jitter-buffered stream, muffled behind walls.
    /// </summary>
    public class VoicePlayback : MonoBehaviour
    {
        private const int BufferSize = VoiceCodec.SampleRate * 2;
        private const int StartThreshold = VoiceCodec.SampleRate / 12; // ~80 ms before playback starts

        private readonly float[] _ring = new float[BufferSize];
        private int _write, _read, _count;
        private bool _started;
        private readonly object _lock = new object();

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private float _nextOcclusion;
        private float _occlusion;

        /// <summary>Time.time of the last received packet (speaking indicator).</summary>
        public float LastPacketTime { get; private set; } = -10f;
        public bool IsSpeaking => Time.time - LastPacketTime < 0.35f;

        public static VoicePlayback For(NetPlayer p)
        {
            var vp = p.GetComponentInChildren<VoicePlayback>();
            if (vp != null) return vp;
            var go = new GameObject("Voice");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            return go.AddComponent<VoicePlayback>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = AudioClip.Create("voice", VoiceCodec.SampleRate, 1, VoiceCodec.SampleRate, true, OnRead);
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.1f, 0.9f), new Keyframe(0.4f, 0.4f), new Keyframe(0.75f, 0.12f), new Keyframe(1f, 0f)));
            _source.minDistance = 1f;
            _source.maxDistance = VoiceChat.HearRange;
            _source.dopplerLevel = 0f;
            _source.spread = 30f;
            _source.Play();
            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f;
        }

        public void Push(byte[] data, int length)
        {
            LastPacketTime = Time.time;
            lock (_lock)
            {
                for (int i = 0; i < length; i++)
                {
                    if (_count >= BufferSize) // overflow: drop oldest
                    {
                        _read = (_read + 1) % BufferSize;
                        _count--;
                    }
                    _ring[_write] = VoiceCodec.Decode(data[i]);
                    _write = (_write + 1) % BufferSize;
                    _count++;
                }
                // Keep latency low: if far behind, skip ahead.
                while (_count > VoiceCodec.SampleRate / 2)
                {
                    _read = (_read + 1) % BufferSize;
                    _count--;
                }
            }
        }

        private void OnRead(float[] data)
        {
            lock (_lock)
            {
                if (!_started && _count >= StartThreshold) _started = true;
                for (int i = 0; i < data.Length; i++)
                {
                    if (_started && _count > 0)
                    {
                        data[i] = _ring[_read];
                        _read = (_read + 1) % BufferSize;
                        _count--;
                    }
                    else
                    {
                        data[i] = 0f;
                        _started = false;
                    }
                }
            }
        }

        private void Update()
        {
            _source.volume = Mathf.Clamp01(LocalProfile.ChatVolume) * (1f - 0.45f * _occlusion);
            _source.maxDistance = VoiceChat.CurrentHearRange;

            if (Time.time >= _nextOcclusion)
            {
                _nextOcclusion = Time.time + 0.15f;
                var listener = Camera.main;
                float target = 0f;
                if (listener != null && IsSpeaking)
                {
                    Vector3 from = listener.transform.position, to = transform.position;
                    Vector3 d = to - from;
                    if (Physics.Raycast(from, d.normalized, out var hit, d.magnitude, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)
                        && !hit.transform.IsChildOf(transform.parent) && !hit.transform.IsChildOf(listener.transform.root))
                        target = 1f;
                }
                _occlusion = target;
            }
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, _occlusion > 0.5f ? 1100f : 22000f, 0.3f);
        }
    }
}
