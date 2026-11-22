namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Test hooks of <see cref="VoiceStream"/> used by the boombox / voice tests. The pre-22.11 playback has no
    /// per-frame latency stamps or callback timing, so those report zero; -vcwav records the played voice (16 kHz mono).
    /// </summary>
    public partial class VoiceStream
    {
        public double GapMs => 0;
        public double OutputMs => 0;
        public float JitterMs => 0f;
        public (double sum, int n, double max) LatencyTotals => (0, 0, 0);
        public (double gapMax, double costMax) TakeCallbackStats() => (0, 0);

        private static VoiceStream _recOwner;
        private static float[] _rec;
        private static int _recPos;

        /// <summary>Records the next <paramref name="seconds"/> of this talker's voice as played (before 3D / effects).</summary>
        public void StartRecording(float seconds)
        {
            _rec = new float[(int)(seconds * Rate)];
            _recPos = 0;
            _recOwner = this;
        }

        private void DiagRecord(float x)
        {
            if (_recOwner != this) return;
            var r = _rec;
            if (r != null && _recPos < r.Length) r[_recPos++] = x;
        }

        /// <summary>Writes the recording (16-bit mono WAV, 16 kHz) once it is full.</summary>
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
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            foreach (float x in r) w.Write((short)UnityEngine.Mathf.Clamp(x * 32767f, -32768f, 32767f));
            _rec = null;
            return true;
        }
    }
}
