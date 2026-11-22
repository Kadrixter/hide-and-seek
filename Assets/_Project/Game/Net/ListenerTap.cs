using System;
using System.IO;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Test helper on the AudioListener: RMS of the mixed output over a window, optional WAV recording.</summary>
    public class ListenerTap : MonoBehaviour
    {
        private readonly object _lock = new object();
        private double _sum;
        private float _peak;
        private long _n;
        private float[] _rec;
        private int _recPos, _channels = 2;

        public void BeginWindow() { lock (_lock) { _sum = 0; _n = 0; _peak = 0f; } }

        /// <summary>Peak in dBFS since <see cref="BeginWindow"/>.</summary>
        public float WindowPeakDb() { lock (_lock) return 20f * Mathf.Log10(_peak + 1e-10f); }

        /// <summary>RMS in dBFS since <see cref="BeginWindow"/>.</summary>
        public float WindowDb()
        {
            lock (_lock) return _n == 0 ? -200f : 10f * Mathf.Log10((float)(_sum / _n) + 1e-20f);
        }

        public void StartRecording(float seconds)
        {
            lock (_lock) { _rec = new float[(int)(seconds * AudioSettings.outputSampleRate) * 2]; _recPos = 0; }
        }

        public void SaveRecording(string path)
        {
            float[] r; int n, ch;
            lock (_lock) { r = _rec; n = _recPos; ch = _channels; _rec = null; }
            if (r == null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var w = new BinaryWriter(File.Create(path));
            int rate = AudioSettings.outputSampleRate;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + n * 2);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(n * 2);
            for (int i = 0; i < n; i++) w.Write((short)Mathf.Clamp(r[i] * 32767f, -32768f, 32767f));
            Debug.Log($"[AudioTests] saved {path} ({n / ch / (float)rate:0.0} s)");
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_lock)
            {
                _channels = channels;
                for (int i = 0; i < data.Length; i++) { float x = data[i]; _sum += x * x; if (x > _peak) _peak = x; else if (-x > _peak) _peak = -x; }
                _n += data.Length;
                if (_rec != null && channels == 2)
                {
                    int m = Mathf.Min(data.Length, _rec.Length - _recPos);
                    Array.Copy(data, 0, _rec, _recPos, m);
                    _recPos += m;
                }
            }
        }
    }
}
