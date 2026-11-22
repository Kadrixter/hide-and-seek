using System;
using System.Diagnostics;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Jitter estimate and loss concealment of <see cref="VoiceStream"/>.
    /// <para>Jitter: the sender produces a frame every 20 ms, so arrival time minus 20 ms x sequence number shows how late
    /// each frame landed compared to the earliest one of the talk spurt (network + both main threads, which send and
    /// receive the frames). The target delay = the worst lateness of the last 4 s + one audio block, between
    /// <see cref="VoiceStream.MinTargetMs"/> and <see cref="VoiceStream.MaxTargetMs"/>; it rises at once and falls
    /// slowly. (The old rule added 30 ms after every dropout - also after every sentence end - and took 6 s to give back
    /// 10 ms, so the delay crept up to 300-400 ms and every restart waited for all of it.)</para>
    /// <para>Concealment: a missing stretch is filled with the last pitch period (autocorrelation, 80-400 Hz) repeated
    /// with a 60 ms fade, and crossfaded into the audio that follows.</para>
    /// </summary>
    public partial class VoiceStream
    {
        private const int Hist = 640, MinLag = 40, MaxLag = 200, PlcFade = Rate * 60 / 1000;
        private static readonly double SwToSec = 1.0 / Stopwatch.Frequency;

        // ---- jitter
        private const int Win = 256;
        private readonly double[] _lateAt = new double[Win];
        private readonly float[] _late = new float[Win];
        private int _lateHead;
        private long _seqIdx;
        private double _vMin, _vMinAt;
        private int _spurtFrames;
        private float _nextAdapt;

        // ---- concealment (audio thread for dropouts, main thread for lost packets; both under _lock)
        private readonly float[] _hist = new float[Hist];
        private int _histPos;
        private readonly float[] _plc = new float[MaxLag];
        private int _plcLen;
        private float _plcPhase, _plcGain;
        private readonly float[] _tail = new float[Fade];

        /// <summary>
        /// -voiceoldjitter (tests only): the policy before this change - +30 ms after every dropout, -10 ms per steady 6 s,
        /// restart only at the full target, silence instead of concealment. For before / after measurements.
        /// </summary>
        private static readonly bool OldPolicy = Array.IndexOf(Environment.GetCommandLineArgs(), "-voiceoldjitter") >= 0;
        private int _seenUnderruns;
        private float _steadySince;

        private void OldTick(bool speaking)
        {
            int u = _underruns, lo = Rate * 45 / 1000;
            if (u != _seenUnderruns)
            {
                if (speaking) _target = Mathf.Min(Rate * 400 / 1000, _target + Rate * 30 / 1000);
                _seenUnderruns = u;
                _steadySince = Time.unscaledTime;
            }
            else if (Time.unscaledTime - _steadySince > 6f && _target > lo)
            {
                _target = Mathf.Max(lo, _target - Rate * 10 / 1000);
                _steadySince = Time.unscaledTime;
            }
        }

        /// <summary>Worst arrival lateness in the window (ms, tests).</summary>
        public float JitterMs { get; private set; }

        /// <summary>Main thread, every received frame (before it is queued).</summary>
        private void Arrived(ushort seq, bool fresh, int length)
        {
            double t = Stopwatch.GetTimestamp() * SwToSec;
            double frame = length / (double)Rate;
            if (fresh)
            {
                _seqIdx = 0;
                _spurtFrames = 0;
                _vMin = t;
                _vMinAt = t;
                lock (_lock) { _dry = false; _dryFrames = 0; }
            }
            else _seqIdx += Math.Max(1, (int)(short)(seq - _prevSeqForIdx));
            _prevSeqForIdx = seq;
            double v = t - _seqIdx * frame;
            // The reference may creep up 1 ms/s (clock drift, a route change), never jumps.
            _vMin = Math.Min(_vMin + (t - _vMinAt) * 0.001, v);
            _vMinAt = t;
            if (++_spurtFrames < 3) return; // the first frames of a spurt set the reference
            _late[_lateHead] = (float)(v - _vMin);
            _lateAt[_lateHead] = t;
            _lateHead = (_lateHead + 1) % Win;
        }

        private ushort _prevSeqForIdx;

        private void AdaptTarget()
        {
            float now = Time.unscaledTime;
            if (now < _nextAdapt) return;
            _nextAdapt = now + 0.1f;
            double t = Stopwatch.GetTimestamp() * SwToSec;
            float worst = 0f;
            for (int i = 0; i < Win; i++)
                if (t - _lateAt[i] < 4.0 && _late[i] > worst) worst = _late[i];
            JitterMs = worst * 1000f;
            int want = Mathf.Clamp(Mathf.RoundToInt((worst + 0.022f) * Rate), Rate * MinTargetMs / 1000, Rate * MaxTargetMs / 1000);
            lock (_lock)
            {
                if (want > _target) _target = want;                                       // rise at once
                else _target = Mathf.Max(want, _target - Rate * 4 / 1000);                // fall 40 ms/s at most
            }
        }

        // ================================================================ concealment

        /// <summary>Audio thread: a sample left the buffer (history for the pitch estimate).</summary>
        private void Remember(float x)
        {
            _hist[_histPos] = x;
            _histPos = (_histPos + 1) % Hist;
        }

        private float HistAt(int back) => _hist[(_histPos - back + Hist * 2) % Hist];

        /// <summary>Audio thread, buffer ran dry: take the last pitch period of what was played.</summary>
        private void StartConceal()
        {
            int lag = BestLag(HistAt);
            _plcLen = lag;
            for (int i = 0; i < lag; i++) _plc[i] = HistAt(lag - i);
            _plcPhase = 0f;
            _plcGain = 1f;
        }

        /// <summary>One concealment sample (advancing <paramref name="step"/> input samples); fades out over 60 ms.</summary>
        private float PlcSample(float step)
        {
            if (_plcGain <= 0f || _plcLen <= 0 || OldPolicy) return 0f;
            float x = _plc[(int)_plcPhase % _plcLen] * _plcGain;
            _plcPhase += step;
            if (_plcPhase >= _plcLen) _plcPhase -= _plcLen;
            _plcGain -= step / PlcFade;
            return x;
        }

        /// <summary>Main thread (lost packets): <paramref name="n"/> samples of concealment from what was received last.</summary>
        private void ConcealInto(int n)
        {
            int have = Mathf.Min(_count, Hist);
            Func<int, float> back = k => k <= have ? _ring[(_write - k + BufferSize) % BufferSize] : 0f;
            int lag = have >= MaxLag * 2 ? BestLag(back) : 160;
            var period = new float[lag];
            for (int i = 0; i < lag; i++) period[i] = back(lag - i);
            float g = 1f, dg = 1f / PlcFade;
            for (int i = 0; i < n; i++, g = Mathf.Max(0f, g - dg)) Put(period[i % lag] * g);
            for (int i = 0; i < Fade; i++, g = Mathf.Max(0f, g - dg)) _tail[i] = period[(n + i) % lag] * g;
        }

        /// <summary>Main thread: crossfade the first samples of the frame queued at <paramref name="from"/> with the tail.</summary>
        private void Crossfade(int from)
        {
            for (int i = 0; i < Fade && from + i < _count; i++)
            {
                int idx = (_read + from + i) % BufferSize;
                float k = (i + 1) / (float)(Fade + 1);
                _ring[idx] = _ring[idx] * k + _tail[i] * (1f - k);
            }
        }

        /// <summary>Pitch period by normalised autocorrelation of the last 20 ms (falls back to 10 ms for noise).</summary>
        private static int BestLag(Func<int, float> back)
        {
            const int span = 320;
            int best = 160;
            float bestR = 0.3f;
            for (int lag = MinLag; lag <= MaxLag; lag += 2)
            {
                float xy = 0f, xx = 0f, yy = 0f;
                for (int i = 1; i <= span - lag && i + lag <= Hist; i += 2)
                {
                    float a = back(i), b = back(i + lag);
                    xy += a * b; xx += a * a; yy += b * b;
                }
                float r = xy / Mathf.Sqrt(xx * yy + 1e-9f);
                if (r > bestR) { bestR = r; best = lag; }
            }
            return best;
        }
    }
}
