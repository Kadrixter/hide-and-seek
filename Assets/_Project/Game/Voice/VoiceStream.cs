using System;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// One remote talker's jitter buffer, played straight from the audio thread (<see cref="OnAudioFilterRead"/>).
    /// The AudioSource next to it loops a constant 1.0 clip; this filter multiplies it by the voice, so the source's
    /// 3D panning / rolloff and the filters after it (echo, low-pass) still apply. Unlike a streamed AudioClip there is
    /// no read-ahead: a sample leaves the buffer one DSP block before it is heard.
    /// <para>Adaptive delay (<c>VoiceStream.Adapt.cs</c>): the target follows the measured arrival jitter of the last
    /// seconds (frames are 20 ms apart at the sender, so how late each one lands is known); a sentence end is not a
    /// dropout. The voice always plays at its natural speed (Hermite 16 -> 48 kHz): a burst after a long stall is cut
    /// back to the target with a 3 ms fade, a dry buffer fades out and restarts at the full target, a lost packet is a
    /// short faded gap - no time-stretching and no synthetic repeats (they coloured the voice; -voicev1928 brings that
    /// 22.11 behaviour back for comparisons).</para>
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public partial class VoiceStream : MonoBehaviour
    {
        public const int Rate = VoiceCodec.SampleRate;
        public const int StartTargetMs = 60, MinTargetMs = 40, MaxTargetMs = 300;
        private const int BufferSize = Rate * 2;
        private const int Fade = 48; // samples (3 ms) of fade around a concealed gap
        private const int ResumeMs = 30;   // after running dry mid-speech: restart with this much, the target is rebuilt by stretching

        private readonly float[] _ring = new float[BufferSize];
        private readonly object _lock = new object();
        private int _write, _read, _count;
        private bool _started, _dry, _dryCounted;
        private float _prevSample, _tailOut;
        /// <summary>-voicev1928 (tests): the playback of 22.11 (linear, 4-5 % speed steering, pitch repeat, early resume).</summary>
        private static readonly bool Legacy = Array.IndexOf(Environment.GetCommandLineArgs(), "-voicev1928") >= 0;
        private float _frac, _step = 1f / 3f, _fadeIn = 1f, _fadeStep = 1f / 96f;
        private int _target = Rate * StartTargetMs / 1000;
        private volatile int _underruns;
        private volatile float _gain = 1f;

        // packet order (frames carry a sequence number; the transport may drop or reorder them)
        private ushort _lastSeq;
        private bool _haveSeq;
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
            _outRate = outRate;
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
            Arrived(seq, fresh, length);

            lock (_lock)
            {
                // Ran dry and the talker is still talking: that was a real dropout (not the end of a sentence).
                if (_dry && !fresh)
                {
                    if (!_dryCounted) _underruns++;
                    _dryCounted = true;
                    _gapFrames += _dryFrames;
                }
                _dryFrames = 0;
                if (missing > 0)
                {
                    Lost += missing;
                    int hole = missing * length;
                    if (Legacy) ConcealInto(hole); // 22.11: repeat of the last pitch period
                    else
                    {
                        // Short, clean hole: fade out what is queued, silence, fade the next frame in (no synthetic sound).
                        int n = Mathf.Min(Fade, _count);
                        for (int i = 0; i < n; i++)
                        {
                            int idx = (_write - n + i + BufferSize) % BufferSize;
                            _ring[idx] *= 1f - (i + 1) / (float)n;
                        }
                        for (int i = 0; i < hole; i++) Put(0f);
                    }
                }
                int crossStart = _count;
                for (int i = 0; i < length; i++)
                {
                    float x = VoiceCodec.Decode(data[i]);
                    if (missing > 0 && !Legacy && i < Fade) x *= i / (float)Fade;
                    Put(x);
                }
                if (missing > 0 && Legacy) Crossfade(crossStart);
                if (stamp != 0) Stamp(stamp, length);
                // Far behind (a burst after a long stall): drop the oldest audio back to the target delay, with a 3 ms
                // fade-in at the cut. (No speeding up / slowing down: that bends the pitch of the voice.)
                if (_count > _target + Rate * (Legacy ? 150 : 100) / 1000)
                {
                    int drop = _count - _target;
                    _read = (_read + drop) % BufferSize;
                    _readAbs += drop;
                    _count -= drop;
                    for (int i = 0; i < Fade && i < _count; i++) _ring[(_read + i) % BufferSize] *= i / (float)Fade;
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

        /// <summary>Main thread, every frame: adaptive delay from the measured jitter.</summary>
        public void Tick(bool speaking)
        {
            if (OldPolicy) OldTick(speaking);
            else AdaptTarget();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            lock (_lock)
            {
                DiagCallback(t0, data.Length / channels);
                if (!_started && _count >= (_dry && Legacy ? Mathf.Min(_target, Rate * ResumeMs / 1000) : _target))
                {
                    _started = true;
                    _fadeIn = 0f;
                }
                // Always the natural speed. (-voicev1928: the 22.11 steering, up to 5 % faster / 4 % slower.)
                float step = _step;
                if (Legacy)
                {
                    int excess = _count - _target;
                    step = excess > Rate * 120 / 1000 ? _step * 1.05f
                         : excess > Rate * 25 / 1000 ? _step * 1.025f
                         : excess < -Rate * 15 / 1000 ? _step * 0.96f
                         : _step;
                }
                for (int i = 0; i < data.Length; i += channels)
                {
                    float x;
                    if (_started && _count >= 3)
                    {
                        float a = _ring[_read], b = _ring[(_read + 1) % BufferSize];
                        if (Legacy) x = a + (b - a) * _frac; // linear
                        else
                        {
                            // 4-point Hermite (Catmull-Rom): 16 -> 48 kHz without the metallic images of linear.
                            float p0 = _prevSample, p3 = _ring[(_read + 2) % BufferSize], t = _frac;
                            x = a + 0.5f * t * (b - p0 + t * (2f * p0 - 5f * a + 4f * b - p3 + t * (3f * (a - b) + p3 - p0)));
                        }
                        _frac += step;
                        while (_frac >= 1f)
                        {
                            _frac -= 1f;
                            _prevSample = _ring[_read];
                            Remember(_ring[_read]);
                            _read = (_read + 1) % BufferSize;
                            _readAbs++;
                            _count--;
                        }
                        if (_fadeIn < 1f)
                        {
                            // Coming back from a dropout: crossfade from the faded tail into the new audio (2 ms).
                            x = x * _fadeIn + (Legacy ? PlcSample(_step) : _tailOut) * (1f - _fadeIn);
                            _fadeIn += _fadeStep;
                        }
                        _dry = false;
                        _tailOut = x;
                    }
                    else
                    {
                        if (_started) { _started = false; _dry = true; _dryCounted = false; if (Legacy) StartConceal(); }
                        if (_dry) _dryFrames++;
                        // Ran dry: the last sample fades out (as before 22.11) - no synthetic repeat.
                        if (Legacy) x = PlcSample(_step);
                        else { _tailOut *= 0.985f; x = _tailOut; }
                    }
                    x *= gain;
                    x /= 1f + 0.35f * Mathf.Abs(x); // soft clip: the boost never crackles
                    if (_recOwner == this) DiagRecord(x);
                    for (int c = 0; c < channels; c++) data[i + c] *= x;
                }
                if (_stampCount > 0) MeasureStamps();
            }
            DiagCost(t0);
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
                DiagLatency(ms);
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
