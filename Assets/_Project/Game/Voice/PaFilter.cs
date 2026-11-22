using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Sound of a car-park horn loudspeaker, applied to the voice on the audio thread at the stream rate (16 kHz, before
    /// spatialisation - <see cref="VoiceStream.Post"/>): band 330-3800 Hz (4th order), presence peak at 1.8 kHz and a
    /// narrow "honk" resonance of the horn at 950 Hz, a short reflection inside the horn (metallic colour), light
    /// overdrive of the driver, a faint amplifier hiss while the channel is open. Opening / closing the channel gives a
    /// short crackle with a soft pop of the cone. Closed = silent. Each speaker gets its own instance (own crackles).
    /// </summary>
    public sealed class PaFilter : IVoicePost
    {
        private struct Biquad
        {
            private float b0, b1, b2, a1, a2, z1, z2;

            public static Biquad LowPass(float f, float fs, float q) => Pass(f, fs, q, false);
            public static Biquad HighPass(float f, float fs, float q) => Pass(f, fs, q, true);

            private static Biquad Pass(float f, float fs, float q, bool high)
            {
                float w = 2f * Mathf.PI * f / fs, cs = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q), a0 = 1f + alpha;
                var b = new Biquad();
                if (high) { b.b0 = (1f + cs) * 0.5f; b.b1 = -(1f + cs); b.b2 = (1f + cs) * 0.5f; }
                else { b.b0 = (1f - cs) * 0.5f; b.b1 = 1f - cs; b.b2 = (1f - cs) * 0.5f; }
                b.a1 = -2f * cs;
                b.a2 = 1f - alpha;
                b.Norm(a0);
                return b;
            }

            /// <summary>Peaking EQ (RBJ cookbook).</summary>
            public static Biquad Peak(float f, float fs, float q, float db)
            {
                float A = Mathf.Pow(10f, db / 40f), w = 2f * Mathf.PI * f / fs, cs = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q);
                var b = new Biquad { b0 = 1f + alpha * A, b1 = -2f * cs, b2 = 1f - alpha * A, a1 = -2f * cs, a2 = 1f - alpha / A };
                b.Norm(1f + alpha / A);
                return b;
            }

            private void Norm(float a0)
            {
                b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            }

            public float Run(float x)
            {
                float y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }
        }

        private const int Fs = VoiceStream.Rate;
        private const int CombMask = 31, CombDelay = 13; // ~0.8 ms reflection in the horn

        private Biquad _hp1, _hp2, _lp1, _lp2, _presence, _honk;
        private readonly float[] _comb = new float[CombMask + 1];
        private int _ci;
        private readonly System.Random _rng;
        private volatile bool _open;
        private volatile int _openEv, _closeEv;
        private float _gate, _crack, _thumpAmp, _burstAmp;
        private int _click, _burst, _burstLen, _thump, _thumpLen;
        private volatile float _level;
        private float _levelAcc;
        private int _levelN;

        /// <summary>Test capture (-pawav): output of the filter marked <see cref="CaptureThis"/> is copied in here (16 kHz mono).</summary>
        public static float[] TestCapture;
        /// <summary>This instance feeds <see cref="TestCapture"/>.</summary>
        public bool CaptureThis;
        public static volatile int TestCaptureN;

        /// <summary>RMS of the last ~0.1 s of output (tests).</summary>
        public float OutputLevel => _level;
        public bool IsOpen => _open;

        public PaFilter(int seed)
        {
            _rng = new System.Random(seed);
            _hp1 = Biquad.HighPass(330f, Fs, 0.54f);
            _hp2 = Biquad.HighPass(330f, Fs, 1.31f);
            _lp1 = Biquad.LowPass(3800f, Fs, 0.54f);
            _lp2 = Biquad.LowPass(3800f, Fs, 1.31f);
            _presence = Biquad.Peak(1800f, Fs, 1.1f, 6f);
            _honk = Biquad.Peak(950f, Fs, 4f, 3.5f);
        }

        /// <summary>Main thread: the talk bar went down (crackle + pop, then the voice).</summary>
        public void Open()
        {
            _open = true;
            _openEv++;
        }

        /// <summary>Main thread: the talk bar was released (pop + crackle tail, then silence).</summary>
        public void Close()
        {
            _open = false;
            _closeEv++;
        }

        private float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

        public void Process(float[] data)
        {
            if (_openEv > 0)
            {
                _openEv = 0;
                _click = 36;
                _burst = _burstLen = Fs * 60 / 1000;
                _burstAmp = 0.15f;
                _thump = _thumpLen = Fs * 45 / 1000;
                _thumpAmp = 0.3f;
            }
            if (_closeEv > 0)
            {
                _closeEv = 0;
                _click = 48;
                _burst = _burstLen = Fs * 130 / 1000;
                _burstAmp = 0.11f;
                _thump = _thumpLen = Fs * 35 / 1000;
                _thumpAmp = 0.18f;
            }
            bool open = _open;
            float gateStep = 1f / (Fs * 0.006f);
            for (int i = 0; i < data.Length; i++)
            {
                _gate = Mathf.MoveTowards(_gate, open ? 1f : 0f, gateStep);
                float v = data[i] * _gate;
                if (open) v += Noise() * 0.0035f; // amplifier hiss, shaped by the horn band below

                v = _hp2.Run(_hp1.Run(v));
                v = _presence.Run(v);
                v = _honk.Run(v);
                v = _lp2.Run(_lp1.Run(v));
                float refl = _comb[(_ci - CombDelay) & CombMask];
                _comb[_ci] = v;
                _ci = (_ci + 1) & CombMask;
                v += 0.2f * refl;
                float dr = v * 2f;
                v = dr / (1f + Mathf.Abs(dr)) * 1.25f;

                if (_burst > 0)
                {
                    float env = _burst / (float)_burstLen;
                    v += Noise() * _burstAmp * env * env;
                    if (_rng.NextDouble() < 0.012) _crack = (float)(_rng.NextDouble() * 0.3 + 0.08) * (_rng.NextDouble() < 0.5 ? -1f : 1f) * env;
                    _burst--;
                }
                if (_crack != 0f)
                {
                    v += _crack;
                    _crack *= -0.62f;
                    if (Mathf.Abs(_crack) < 0.002f) _crack = 0f;
                }
                if (_thump > 0)
                {
                    float t = (_thumpLen - _thump) / (float)Fs;
                    float k = _thump / (float)_thumpLen;
                    v += _thumpAmp * Mathf.Sin(2f * Mathf.PI * 70f * t) * k * k;
                    _thump--;
                }
                if (_click > 0)
                {
                    v += 0.35f * (_click % 2 == 0 ? 1f : -0.7f) * Mathf.Min(1f, _click / 12f);
                    _click--;
                }
                v /= 1f + 0.45f * Mathf.Abs(v);
                data[i] = v;

                var cap = TestCapture;
                if (CaptureThis && cap != null && TestCaptureN < cap.Length) cap[TestCaptureN++] = v;
                _levelAcc += v * v;
                _levelN++;
            }
            if (_levelN >= Fs / 10)
            {
                _level = Mathf.Sqrt(_levelAcc / _levelN);
                _levelAcc = 0f;
                _levelN = 0;
            }
        }
    }
}
