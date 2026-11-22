using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Walkie-talkie sound on the audio thread (after <see cref="VoiceStream"/>): band 300-3000 Hz (4th order high- and
    /// low-pass), light overdrive, band-limited hiss and random crackles while the channel is open, a "pyk" click with a
    /// short noise burst when it opens and a longer "kssh" tail when it closes. Closed = silent.
    /// </summary>
    public class RadioFilter : MonoBehaviour
    {
        private struct Biquad
        {
            private float b0, b1, b2, a1, a2, z1, z2;

            public static Biquad LowPass(float f, float fs, float q) => Make(f, fs, q, false);
            public static Biquad HighPass(float f, float fs, float q) => Make(f, fs, q, true);

            private static Biquad Make(float f, float fs, float q, bool high)
            {
                float w = 2f * Mathf.PI * f / fs, cs = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
                var b = new Biquad();
                if (high) { b.b0 = (1f + cs) * 0.5f; b.b1 = -(1f + cs); b.b2 = (1f + cs) * 0.5f; }
                else { b.b0 = (1f - cs) * 0.5f; b.b1 = 1f - cs; b.b2 = (1f - cs) * 0.5f; }
                b.a1 = -2f * cs;
                b.a2 = 1f - alpha;
                b.b0 /= a0; b.b1 /= a0; b.b2 /= a0; b.a1 /= a0; b.a2 /= a0;
                return b;
            }

            public float Run(float x)
            {
                float y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }
        }

        private Biquad _hp1, _hp2, _lp1, _lp2;
        private System.Random _rng = new System.Random(1234);
        private int _fs = 48000;
        private float _gate;
        private volatile bool _open;
        private volatile int _openClicks, _closeClicks;
        private int _burst, _burstLen, _click;
        private float _burstAmp, _crackle;
        private volatile float _level;
        private float _levelAcc;
        private int _levelN;

        /// <summary>Test capture: when set, the output of the first filter that runs is copied in here (mono).</summary>
        public static float[] TestCapture;
        public static volatile int TestCaptureN;
        public static int CaptureRate { get; private set; } = 48000;

        /// <summary>RMS of the last output blocks (tests).</summary>
        public float OutputLevel => _level;
        public bool IsOpen => _open;

        private void Awake()
        {
            _fs = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            CaptureRate = _fs;
            _hp1 = Biquad.HighPass(300f, _fs, 0.54f);
            _hp2 = Biquad.HighPass(300f, _fs, 1.31f);
            _lp1 = Biquad.LowPass(3000f, _fs, 0.54f);
            _lp2 = Biquad.LowPass(3000f, _fs, 1.31f);
        }

        /// <summary>Main thread: a sentence starts (click + short burst, then hiss under the voice).</summary>
        public void Open()
        {
            _open = true;
            _openClicks++;
        }

        /// <summary>Main thread: the sentence ended (click + noise tail, then silence).</summary>
        public void Close()
        {
            _open = false;
            _closeClicks++;
        }

        private float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_openClicks > 0)
            {
                _openClicks = 0;
                _click = _fs / 400;                 // 2.5 ms "pyk"
                _burst = _burstLen = _fs * 40 / 1000;
                _burstAmp = 0.22f;
            }
            if (_closeClicks > 0)
            {
                _closeClicks = 0;
                _click = _fs / 300;
                _burst = _burstLen = _fs * 160 / 1000; // "kssh"
                _burstAmp = 0.3f;
            }
            bool open = _open;
            float gateStep = 1f / (_fs * 0.004f);
            for (int i = 0; i < data.Length; i += channels)
            {
                float voice = data[i];
                _gate = Mathf.MoveTowards(_gate, open ? 1f : 0f, gateStep);
                // Overdrive the voice a little (cheap radio amp), then hiss + crackles under it.
                float v = voice * 1.5f;
                v = v / (1f + Mathf.Abs(v)) * 1.2f;
                float x = v * _gate;
                if (open)
                {
                    x += Noise() * 0.03f;
                    if (_rng.NextDouble() < 7.0 / _fs) _crackle = (float)(_rng.NextDouble() * 0.35 + 0.12) * (_rng.NextDouble() < 0.5 ? -1f : 1f);
                }
                if (_crackle != 0f)
                {
                    x += _crackle;
                    _crackle *= 0.86f;
                    if (Mathf.Abs(_crackle) < 0.002f) _crackle = 0f;
                }
                if (_burst > 0)
                {
                    float env = (float)_burst / _burstLen;
                    x += Noise() * _burstAmp * env;
                    _burst--;
                }
                // Band 300-3000 Hz.
                x = _lp2.Run(_lp1.Run(_hp2.Run(_hp1.Run(x))));
                if (_click > 0) { x += 0.45f * (_click % 2 == 0 ? 1f : -0.6f) * Mathf.Min(1f, _click / 20f); _click--; }
                x /= 1f + 0.5f * Mathf.Abs(x); // soft ceiling, never a hard clip
                for (int c = 0; c < channels; c++) data[i + c] = x;
                var cap = TestCapture;
                if (cap != null && TestCaptureN < cap.Length) cap[TestCaptureN++] = x;
                _levelAcc += x * x;
                _levelN++;
            }
            if (_levelN >= _fs / 10)
            {
                _level = Mathf.Sqrt(_levelAcc / _levelN);
                _levelAcc = 0f;
                _levelN = 0;
            }
        }
    }
}
