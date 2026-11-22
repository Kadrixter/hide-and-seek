using System.Diagnostics;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Counters of one talker's jitter buffer for the voice tests (-voicetest, -testvcmusic): dropouts (underruns and how
    /// long they lasted), the interval between audio-thread callbacks and their cost. All cumulative; read by the tests.
    /// </summary>
    public partial class VoiceStream
    {
        private long _gapFrames, _dryFrames, _outFrames, _lastCbTicks;
        private double _cbGapMaxMs, _cbCostMaxMs;
        private static readonly double TickMs = 1000.0 / Stopwatch.Frequency;

        /// <summary>Times the buffer ran dry while the talker was still talking.</summary>
        public int Underruns => _underruns;
        /// <summary>Total output time (ms) spent in those dropouts (concealed or silent).</summary>
        public double GapMs => _gapFrames * 1000.0 / _outRate;
        public double OutputMs => _outFrames * 1000.0 / _outRate;
        private int _outRate = 48000;

        private void DiagCallback(long now, int frames)
        {
            _outFrames += frames;
            if (_lastCbTicks != 0)
            {
                double gap = (now - _lastCbTicks) * TickMs;
                if (gap > _cbGapMaxMs) _cbGapMaxMs = gap;
            }
            _lastCbTicks = now;
        }

        private void DiagCost(long t0)
        {
            double cost = (Stopwatch.GetTimestamp() - t0) * TickMs;
            if (cost > _cbCostMaxMs) _cbCostMaxMs = cost;
        }

        /// <summary>Largest interval between two audio callbacks and largest callback cost since the last call (ms).</summary>
        public (double gapMax, double costMax) TakeCallbackStats()
        {
            var r = (_cbGapMaxMs, _cbCostMaxMs);
            _cbGapMaxMs = 0;
            _cbCostMaxMs = 0;
            return r;
        }

        /// <summary>Cumulative capture-to-mixer latency of stamped (test tone) frames: sum, count, max.</summary>
        public (double sum, int n, double max) LatencyTotals => (_latTotSum, _latTotN, _latTotMax);
        private double _latTotSum, _latTotMax;
        private int _latTotN;

        private void DiagLatency(double ms)
        {
            _latTotSum += ms;
            _latTotN++;
            if (ms > _latTotMax) _latTotMax = ms;
        }
    }
}
