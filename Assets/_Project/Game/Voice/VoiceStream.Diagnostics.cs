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

        // ---- recording of one talker's played voice (tests: -vcwav)
        private static VoiceStream _recOwner;
        private static float[] _rec;
        private static int _recPos;

        /// <summary>Records the next <paramref name="seconds"/> of this talker's voice as played (before 3D / effects).</summary>
        public void StartRecording(float seconds)
        {
            _rec = new float[(int)(seconds * _outRate)];
            _recPos = 0;
            _recOwner = this;
        }

        private void DiagRecord(float x)
        {
            var r = _rec;
            if (r != null && _recPos < r.Length) r[_recPos++] = x;
        }

        /// <summary>Writes the recording (16-bit mono WAV at the output rate) once it is full.</summary>
        public bool SaveRecording(string path)
        {
            var r = _rec;
            if (r == null || _recPos < r.Length) return false;
            _recOwner = null;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using var w = new System.IO.BinaryWriter(System.IO.File.Create(path));
            int n = r.Length;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(_outRate); w.Write(_outRate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            foreach (float x in r) w.Write((short)UnityEngine.Mathf.Clamp(x * 32767f, -32768f, 32767f));
            _rec = null;
            return true;
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
