using System;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// One remote talker's jitter buffer, played straight from the audio thread (<see cref="OnAudioFilterRead"/>).
    /// The AudioSource next to it loops a constant 1.0 clip; this filter multiplies it by the voice, so the source's
    /// 3D panning / rolloff and the filters after it (echo, low-pass) still apply. Unlike a streamed AudioClip there is
    /// no read-ahead: a sample leaves the buffer one DSP block before it is heard.
    /// <para>Adaptive delay: starts at <see cref="StartTargetMs"/>, grows when the buffer runs dry mid-speech, slowly
    /// shrinks back while the connection is steady. Lost / late packets are concealed with a short fade (no clicks).</para>
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceStream : MonoBehaviour
    {
        public const int Rate = VoiceCodec.SampleRate;
        public const int StartTargetMs = 70, MinTargetMs = 45, MaxTargetMs = 400;
        private const int BufferSize = Rate * 2;
        private const int Fade = 48; // samples (3 ms) of fade around a concealed gap

        private readonly float[] _ring = new float[BufferSize];
        private readonly object _lock = new object();
        private int _write, _read, _count;
        private bool _started;
        private float _frac, _step = 1f / 3f, _fadeIn = 1f, _fadeStep = 1f / 96f, _last;
        private int _target = Rate * StartTargetMs / 1000;
        private volatile int _underruns;
        private volatile float _gain = 1f;
        private int _seenUnderruns;
        private float _steadySince;

        // packet order (frames carry a sequence number; the transport may drop or reorder them)
        private ushort _lastSeq;
        private bool _haveSeq, _fadeNext;
        private float _lastPushTime = -10f;

        /// <summary>Linear gain applied to the voice (boost x chat volume x per-player volume).</summary>
        public float Gain { set => _gain = value; }
        public int TargetMs => _target * 1000 / Rate;
        public int BufferedMs { get { lock (_lock) return _count * 1000 / Rate; } }
        public int Lost { get; private set; }
        public int Late { get; private set; }

        private void Awake()
        {
            int outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            _step = Rate / (float)outRate;
            _fadeStep = 1000f / (2f * outRate); // 2 ms fade-in after every (re)start
            var src = GetComponent<AudioSource>();
            var dc = AudioClip.Create("voice-dc", outRate, 1, outRate, false);
            var ones = new float[outRate];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            dc.SetData(ones, 0);
            src.clip = dc;
            src.loop = true;
            AudioSettings.GetDSPBufferSize(out int len, out int num);
            _dspLatencyMs = 1000.0 * len * Mathf.Max(2, num) / outRate;
        }

        /// <summary>Main thread: one received frame. Returns false when it was late / duplicated and dropped.</summary>
        public bool Push(ushort seq, byte[] data, int length, long stamp = 0)
        {
            float now = Time.unscaledTime;
            bool fresh = !_haveSeq || now - _lastPushTime > 0.5f; // new talk spurt
            _lastPushTime = now;
            int missing = 0;
            if (!fresh)
            {
                int diff = (short)(seq - _lastSeq);
                if (diff <= 0) { Late++; return false; }
                missing = diff - 1;
                if (missing > 6) missing = 0; // long hole: treat as a new spurt, the buffer refills on its own
            }
            _haveSeq = true;
            _lastSeq = seq;

            lock (_lock)
            {
                if (missing > 0)
                {
                    Lost += missing;
                    // fade out what is still waiting in the buffer, fill the hole with silence, fade the next frame in
                    int n = Mathf.Min(Fade, _count);
                    for (int i = 0; i < n; i++)
                    {
                        int idx = (_write - n + i + BufferSize) % BufferSize;
                        _ring[idx] *= 1f - (i + 1) / (float)n;
                    }
                    for (int i = 0; i < missing * length; i++) Put(0f);
                    _fadeNext = true;
                }
                for (int i = 0; i < length; i++)
                {
                    float x = VoiceCodec.Decode(data[i]);
                    if (_fadeNext && i < Fade) x *= i / (float)Fade;
                    Put(x);
                }
                _fadeNext = false;
                if (stamp != 0) Stamp(stamp, length);
                // far behind (after a stall): drop the oldest audio back to the target delay
                if (_count > _target + Rate * 150 / 1000)
                {
                    int drop = _count - _target;
                    _read = (_read + drop) % BufferSize;
                    _readAbs += drop;
                    _count -= drop;
                }
            }
            return true;
        }

        private void Put(float x)
        {
            if (_count >= BufferSize)
            {
                _read = (_read + 1) % BufferSize;
                _readAbs++;
                _count--;
            }
            _ring[_write] = x;
            _write = (_write + 1) % BufferSize;
            _writeAbs++;
            _count++;
        }

        /// <summary>Main thread, every frame: adaptive delay.</summary>
        public void Tick(bool speaking)
        {
            int u = _underruns;
            if (u != _seenUnderruns)
            {
                if (speaking) _target = Mathf.Min(Rate * MaxTargetMs / 1000, _target + Rate * 30 / 1000);
                _seenUnderruns = u;
                _steadySince = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _steadySince > 6f && _target > Rate * MinTargetMs / 1000)
            {
                _target = Mathf.Max(Rate * MinTargetMs / 1000, _target - Rate * 10 / 1000);
                _steadySince = Time.unscaledTime;
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain;
            lock (_lock)
            {
                if (!_started && _count >= _target) { _started = true; _fadeIn = 0f; }
                // More queued than needed (a burst, or the sender's clock runs a bit fast): play up to 5 % faster until
                // the delay is back at the target - unnoticeable, and the excess never sticks around.
                int excess = _count - _target;
                float step = excess > Rate * 120 / 1000 ? _step * 1.05f : excess > Rate * 25 / 1000 ? _step * 1.025f : _step;
                for (int i = 0; i < data.Length; i += channels)
                {
                    float x;
                    if (_started && _count >= 2)
                    {
                        float a = _ring[_read], b = _ring[(_read + 1) % BufferSize];
                        x = (a + (b - a) * _frac) * gain;
                        _frac += step;
                        while (_frac >= 1f)
                        {
                            _frac -= 1f;
                            _read = (_read + 1) % BufferSize;
                            _readAbs++;
                            _count--;
                        }
                        if (_fadeIn < 1f) { x *= _fadeIn; _fadeIn += _fadeStep; }
                        x /= 1f + 0.35f * Mathf.Abs(x); // soft clip: the boost never crackles
                        _last = x;
                    }
                    else
                    {
                        if (_started) _underruns++; // ran dry mid-speech: buffer more next time
                        _started = false;
                        _last *= 0.985f;            // fade instead of a hard click
                        x = _last;
                    }
                    for (int c = 0; c < channels; c++) data[i + c] *= x;
                }
                if (_stampCount > 0) MeasureStamps();
            }
        }

        // ================================================================ latency measurement (test tone only)

        private long _writeAbs, _readAbs;
        private readonly long[] _stampPos = new long[64], _stampTicks = new long[64];
        private int _stampHead, _stampCount;
        private double _dspLatencyMs, _latSum, _latMin = double.MaxValue, _latMax;
        private int _latN;

        private void Stamp(long ticks, int length)
        {
            if (_stampCount == _stampPos.Length) { _stampHead = (_stampHead + 1) % _stampPos.Length; _stampCount--; }
            int slot = (_stampHead + _stampCount) % _stampPos.Length;
            _stampPos[slot] = _writeAbs - length;
            _stampTicks[slot] = ticks;
            _stampCount++;
        }

        private void MeasureStamps()
        {
            long now = DateTime.UtcNow.Ticks; // wall clock: the same in every process on this PC
            while (_stampCount > 0 && _readAbs >= _stampPos[_stampHead])
            {
                double ms = (now - _stampTicks[_stampHead]) / 10000.0;
                _latSum += ms; _latN++;
                if (ms < _latMin) _latMin = ms;
                if (ms > _latMax) _latMax = ms;
                _stampHead = (_stampHead + 1) % _stampPos.Length;
                _stampCount--;
            }
        }

        /// <summary>Capture-to-mixer latency of test-tone frames (ms): average, min, max, count; plus the output block delay.</summary>
        public string LatencyReport()
        {
            lock (_lock)
            {
                string r = _latN == 0 ? "no stamped frames"
                    : $"capture->mixer avg={_latSum / _latN:0.0}ms min={_latMin:0.0} max={_latMax:0.0} n={_latN}, +output blocks {_dspLatencyMs:0.0}ms";
                _latSum = 0; _latN = 0; _latMin = double.MaxValue; _latMax = 0;
                return r;
            }
        }
    }
}
