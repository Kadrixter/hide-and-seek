using HideAndSeek.Audio;
using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Plays one remote player's voice in 3D from their head: adaptive jitter buffer (grows when packets arrive late,
    /// shrinks back when the connection is steady), loudness boost with soft clipping, garage echo that gets
    /// stronger with distance, and muffling behind walls.
    /// </summary>
    public class VoicePlayback : MonoBehaviour
    {
        private const int Rate = VoiceCodec.SampleRate;
        private const int BufferSize = Rate * 3;
        private const int MinTarget = Rate * 100 / 1000;   // 100 ms
        private const int MaxTarget = Rate * 600 / 1000;   // 600 ms
        private const float BaseGain = 2.4f;

        private readonly float[] _ring = new float[BufferSize];
        private int _write, _read, _count;
        private bool _started;
        private int _target = Rate * 160 / 1000;          // start at 160 ms
        private volatile int _underruns;
        private float _lastSample;
        private readonly object _lock = new object();

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private AudioEchoFilter _echo;
        private float _nextOcclusion, _occlusion;
        private int _seenUnderruns;
        private float _steadySince;
        private float _shoutUntil = -10f;
        private float _range;

        /// <summary>True while this player is shouting (heard across the garage).</summary>
        public bool Shouting => Time.time < _shoutUntil;

        /// <summary>Time.time of the last received packet (speaking indicator).</summary>
        public float LastPacketTime { get; private set; } = -10f;
        public bool IsSpeaking => Time.time - LastPacketTime < 0.4f;

        /// <summary>Current buffering delay in ms (debug).</summary>
        public int BufferMs => _target * 1000 / Rate;

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
            _source.clip = AudioClip.Create("voice", Rate, 1, Rate, true, OnRead);
            _source.loop = true;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.1f, 0.95f), new Keyframe(0.4f, 0.55f), new Keyframe(0.75f, 0.2f), new Keyframe(1f, 0f)));
            _source.minDistance = 1f;
            _source.maxDistance = VoiceChat.HearRange;
            _source.dopplerLevel = 0f;
            _source.spread = 30f;
            _source.volume = 1f;
            _source.Play();

            _echo = gameObject.AddComponent<AudioEchoFilter>();
            _echo.delay = 320f;
            _echo.decayRatio = 0.35f;
            _echo.dryMix = 1f;
            _echo.wetMix = 0f;

            _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = 22000f;
        }

        public void Push(byte[] data, int length, float level = 0f)
        {
            LastPacketTime = Time.time;
            if (level >= VoiceChat.ShoutLevel) _shoutUntil = Time.time + 0.8f;
            lock (_lock)
            {
                for (int i = 0; i < length; i++)
                {
                    if (_count >= BufferSize)
                    {
                        _read = (_read + 1) % BufferSize;
                        _count--;
                    }
                    _ring[_write] = VoiceCodec.Decode(data[i]);
                    _write = (_write + 1) % BufferSize;
                    _count++;
                }
                // Too far behind (e.g. after a long stall): drop the oldest audio down to the target delay.
                int limit = _target + Rate / 3;
                if (_count > limit)
                {
                    int drop = _count - _target;
                    _read = (_read + drop) % BufferSize;
                    _count -= drop;
                }
            }
        }

        private void OnRead(float[] data)
        {
            float gain = BaseGain * Mathf.Max(0f, LocalProfile.ChatVolume);
            lock (_lock)
            {
                if (!_started && _count >= _target) _started = true;
                for (int i = 0; i < data.Length; i++)
                {
                    if (_started && _count > 0)
                    {
                        float x = _ring[_read] * gain;
                        _read = (_read + 1) % BufferSize;
                        _count--;
                        // Soft clip so the boost never crackles.
                        x = x / (1f + 0.35f * Mathf.Abs(x));
                        _lastSample = Mathf.Clamp(x, -1f, 1f);
                        data[i] = _lastSample;
                    }
                    else
                    {
                        if (_started) _underruns++; // ran dry mid-speech: buffer more next time
                        _started = false;
                        // Fade the last sample out instead of a hard click.
                        _lastSample *= 0.96f;
                        data[i] = _lastSample;
                    }
                }
            }
        }

        private void Update()
        {
            // Adaptive delay: each underrun while talking adds 60 ms; 8 s without one removes 20 ms.
            int u = _underruns;
            if (u != _seenUnderruns)
            {
                if (IsSpeaking) _target = Mathf.Min(MaxTarget, _target + Rate * 60 / 1000);
                _seenUnderruns = u;
                _steadySince = Time.time;
            }
            else if (Time.time - _steadySince > 8f && _target > MinTarget)
            {
                _target = Mathf.Max(MinTarget, _target - Rate * 20 / 1000);
                _steadySince = Time.time;
            }

            // Shouts carry across the whole garage; the range eases back after the shout.
            bool lobby = VoiceChat.CurrentHearRange > VoiceChat.HearRange;
            float targetRange = Shouting && !lobby ? VoiceChat.ShoutRange : VoiceChat.CurrentHearRange;
            _range = targetRange > _range ? targetRange : Mathf.MoveTowards(_range, targetRange, Time.deltaTime * 120f);
            _source.maxDistance = Mathf.Max(5f, _range);

            var listener = Camera.main;
            float dist = listener != null ? Vector3.Distance(listener.transform.position, transform.position) : 0f;

            // Echo: only in the big concrete garage. Stronger and longer the farther away the speaker is;
            // a shout from the other end of the parking comes back as a clear slap echo.
            bool garage = AudioEnvironment.Current == AudioEnvironmentKind.Garage;
            float far = Mathf.InverseLerp(4f, Shouting ? VoiceChat.ShoutRange * 0.6f : VoiceChat.HearRange, dist);
            _echo.wetMix = garage ? Mathf.Lerp(0.18f, Shouting ? 0.85f : 0.6f, far) : 0f;
            _echo.delay = Mathf.Lerp(220f, Shouting ? 650f : 420f, far);
            _echo.decayRatio = garage ? Mathf.Lerp(0.3f, Shouting ? 0.55f : 0.45f, far) : 0f;
            _echo.enabled = garage;

            if (Time.time >= _nextOcclusion)
            {
                _nextOcclusion = Time.time + 0.15f;
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
            _source.volume = 1f - 0.4f * _occlusion;
            _lowPass.cutoffFrequency = Mathf.Lerp(_lowPass.cutoffFrequency, _occlusion > 0.5f ? 1200f : 22000f, 0.3f);
        }
    }
}
