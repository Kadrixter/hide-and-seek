using System;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// One remote talker's voice, played exactly the way it was before 22.11 (commit 2ffd4af, VoicePlayback):
    /// a 16 kHz mono streamed AudioClip whose PCMReaderCallback reads a ring buffer, Unity resamples it to the output.
    /// Jitter buffer: start at 160 ms, +60 ms after every dropout while the talker is speaking, -20 ms after 8 steady
    /// seconds (100-600 ms); far behind (target + 333 ms) drops the oldest audio. Gain + soft clip x / (1 + 0.35|x|),
    /// a dry buffer fades its last sample out. Packets carry a sequence number: duplicates / late ones are dropped,
    /// a missing one is simply skipped (no concealment, no time-stretching).
    /// The AudioSource is created by <see cref="VoicePlayback"/> / <see cref="PhoneVoice"/>; their filters (echo,
    /// low-pass, walkie-talkie) act on what this clip plays.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class VoiceStream : MonoBehaviour
    {
        public const int Rate = VoiceCodec.SampleRate;
        private const int BufferSize = Rate * 3;
        private const int MinTarget = Rate * 100 / 1000;   // 100 ms
        private const int MaxTarget = Rate * 600 / 1000;   // 600 ms

        private readonly float[] _ring = new float[BufferSize];
        private readonly object _lock = new object();
        private int _write, _read, _count;
        private bool _started;
        private int _target = Rate * 160 / 1000;          // start at 160 ms
        private volatile int _underruns;
        private float _lastSample;
        private int _seenUnderruns;
        private float _steadySince;
        private volatile float _gain = 2.4f;

        private ushort _lastSeq;
        private bool _haveSeq;
        private float _lastPushTime = -10f;

        /// <summary>Linear gain applied to the voice (boost x chat volume x per-player volume).</summary>
        public float Gain { set => _gain = value; }
        public int TargetMs => _target * 1000 / Rate;
        public int BufferedMs { get { lock (_lock) return _count * 1000 / Rate; } }
        public int Lost { get; private set; }
        public int Late { get; private set; }
        public int Underruns => _underruns;

        private void Awake()
        {
            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("voice", Rate, 1, Rate, true, OnRead);
            src.loop = true;
        }

        /// <summary>Main thread: one received frame. Returns false when it was late / duplicated and dropped.</summary>
        public bool Push(ushort seq, byte[] data, int length, long stamp = 0)
        {
            float now = Time.unscaledTime;
            bool fresh = !_haveSeq || now - _lastPushTime > 0.5f;
            _lastPushTime = now;
            if (!fresh)
            {
                int diff = (short)(seq - _lastSeq);
                if (diff <= 0) { Late++; return false; }
                if (diff > 1 && diff <= 7) Lost += diff - 1;
            }
            _haveSeq = true;
            _lastSeq = seq;
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
            return true;
        }

        private void OnRead(float[] data)
        {
            float gain = _gain;
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
                    DiagRecord(data[i]);
                }
            }
            Post?.Process(data); // optional colouring (public-address horn); null for the normal voice
        }

        /// <summary>Main thread, every frame: each dropout while talking adds 60 ms; 8 s without one removes 20 ms.</summary>
        public void Tick(bool speaking)
        {
            int u = _underruns;
            if (u != _seenUnderruns)
            {
                if (speaking) _target = Mathf.Min(MaxTarget, _target + Rate * 60 / 1000);
                _seenUnderruns = u;
                _steadySince = Time.time;
            }
            else if (Time.time - _steadySince > 8f && _target > MinTarget)
            {
                _target = Mathf.Max(MinTarget, _target - Rate * 20 / 1000);
                _steadySince = Time.time;
            }
        }

        /// <summary>Kept for the stats log (the old playback has no per-frame latency stamps).</summary>
        public string LatencyReport() => "";
    }
}
