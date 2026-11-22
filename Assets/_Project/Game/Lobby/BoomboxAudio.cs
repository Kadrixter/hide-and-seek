using System;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Boombox output on the audio thread. The AudioSource loops a constant 1.0 clip (3D panning, rolloff and volume
    /// still apply to it) and this filter multiplies it by the music, like the voice playback does: no read-ahead of a
    /// streamed clip, so a start / pause / skip is heard within one DSP block and every copy plays the same moment.
    /// Chain: bass shelf (90 Hz, up to +14 dB) -> loudness boost -> peak limiter (no clipping) -> bass envelope for the
    /// speaker cones (<see cref="Kick"/>) and a level for the LEDs.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BoomboxAudio : MonoBehaviour
    {
        const int Ch = RadioStream.Ch;
        /// <summary>
        /// Loudness: drive into the limiter (+7.6 dB) and make-up after it (+3.2 dB), the source volume is 1.0 at 100 %
        /// (was 0.9). Measured at the listener 1.6 m away at 100 %: the old chain clipped (peaks +2 dBFS, 4.6 % of the
        /// samples over full scale); this one stays at about -1 dBFS and is ~3.5 dB louder wherever the old one did not clip.
        /// </summary>
        public const float Boost = 2.4f, Post = 1.45f;
        const float Ceiling = 0.78f;

        public volatile RadioStream Stream;
        public volatile float Bass;

        /// <summary>Bass envelope (0..~1.5) normalised to the song's own loudness: what the cones follow.</summary>
        public float Kick => _kick;
        /// <summary>Output level 0..1 (LEDs).</summary>
        public float Level => _level;
        /// <summary>Largest gain reduction of the limiter since the last read (dB, tests).</summary>
        public float TakeLimiterDb() { float r = _grMaxDb; _grMaxDb = 0f; return r; }
        public float TakePeak() { float r = _peak; _peak = 0f; return r; }

        private volatile float _kick, _level, _grMaxDb, _peak;
        private float[] _buf = new float[4096 * Ch];
        private int _outRate = 48000;

        // filters
        private float _gainFor = -1f;
        private float _b0, _b1, _b2, _a1, _a2, _makeup = 1f;
        private readonly float[] _x1 = new float[Ch], _x2 = new float[Ch], _y1 = new float[Ch], _y2 = new float[Ch];
        private float _limGain = 1f;
        private float _lp1, _lp2, _bassEnv, _bassAvg = 0.05f;
        private float _att, _rel, _limRel, _lpk, _avgK;

        private void Awake()
        {
            _outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            var src = GetComponent<AudioSource>();
            var dc = AudioClip.Create("boombox-dc", _outRate, 1, _outRate, false);
            var ones = new float[_outRate];
            for (int i = 0; i < ones.Length; i++) ones[i] = 1f;
            dc.SetData(ones, 0);
            src.clip = dc;
            src.loop = true;
            float r = _outRate;
            _att = 1f - Mathf.Exp(-1f / (0.004f * r));     // bass envelope attack 4 ms
            _rel = 1f - Mathf.Exp(-1f / (0.11f * r));      // release 110 ms: separate kicks
            _limRel = 1f - Mathf.Exp(-1f / (0.12f * r));   // limiter release 120 ms
            _lpk = 1f - Mathf.Exp(-2f * Mathf.PI * 140f / r); // bass band: 2 x one-pole at 140 Hz
            _avgK = 1f - Mathf.Exp(-1f / (2.5f * r));      // slow loudness average (2.5 s)
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var s = Stream;
            int frames = data.Length / channels;
            if (s == null || channels < 1)
            {
                Array.Clear(data, 0, data.Length);
                Decay(frames);
                return;
            }
            if (_buf.Length < frames * Ch) _buf = new float[frames * Ch];
            s.Read(_buf, frames, AudioSettings.dspTime);
            if (!s.Playing && !HasSignal(frames))
            {
                Array.Clear(data, 0, data.Length);
                Decay(frames);
                return;
            }
            UpdateFilter();
            double sum = 0;
            float peak = 0f, grMin = 1f;
            for (int f = 0; f < frames; f++)
            {
                float l = Shelf(_buf[f * Ch], 0) * _makeup * Boost;
                float r = Shelf(_buf[f * Ch + 1], 1) * _makeup * Boost;
                // Peak limiter: instant attack, smooth release - loud, never clipped or crunched.
                float a = Mathf.Max(Mathf.Abs(l), Mathf.Abs(r));
                float need = a > Ceiling ? Ceiling / a : 1f;
                _limGain = need < _limGain ? need : _limGain + (1f - _limGain) * _limRel;
                l *= _limGain * Post;
                r *= _limGain * Post;
                if (_limGain < grMin) grMin = _limGain;
                // Bass envelope (mono, ~<140 Hz).
                float m = (l + r) * (0.5f / Post);
                _lp1 += (m - _lp1) * _lpk;
                _lp2 += (_lp1 - _lp2) * _lpk;
                float b = Mathf.Abs(_lp2);
                _bassEnv += (b - _bassEnv) * (b > _bassEnv ? _att : _rel);
                _bassAvg += (_bassEnv - _bassAvg) * _avgK;
                if (channels == 1) data[f] *= m;
                else
                {
                    data[f * channels] *= l;
                    data[f * channels + 1] *= r;
                    for (int c = 2; c < channels; c++) data[f * channels + c] *= m;
                }
                sum += m * m;
                float pa = Mathf.Max(Mathf.Abs(l), Mathf.Abs(r));
                if (pa > peak) peak = pa;
            }
            // Relative to the song's average bass (2.5 s), so quiet and loud masters both pump.
            // Above the average = a kick: the cone rests near 0 between the beats and jumps out on them.
            _kick = Mathf.Clamp((_bassEnv / Mathf.Max(0.012f, _bassAvg) - 0.7f) * 1.25f, 0f, 1.6f);
            _level = Mathf.Clamp01(Mathf.Sqrt((float)(sum / Mathf.Max(1, frames))) * 2.2f);
            float grDb = -20f * Mathf.Log10(Mathf.Max(1e-4f, grMin));
            if (grDb > _grMaxDb) _grMaxDb = grDb;
            if (peak > _peak) _peak = peak;
        }

        private bool HasSignal(int frames)
        {
            for (int i = 0; i < frames * Ch; i += 16) if (_buf[i] != 0f) return true;
            return false;
        }

        private void Decay(int frames)
        {
            float k = Mathf.Pow(1f - _rel, frames);
            _bassEnv *= k;
            _kick *= k;
            _level *= k;
        }

        private float Shelf(float x, int c)
        {
            float y = _b0 * x + _b1 * _x1[c] + _b2 * _x2[c] - _a1 * _y1[c] - _a2 * _y2[c];
            _x2[c] = _x1[c]; _x1[c] = x; _y2[c] = _y1[c]; _y1[c] = y;
            return y;
        }

        private void UpdateFilter()
        {
            float g = Mathf.Clamp01(Bass) * 14f;
            if (Mathf.Abs(g - _gainFor) < 0.01f) return;
            _gainFor = g;
            // RBJ low shelf, f0 = 90 Hz, S = 1; makeup keeps the boosted bass from just turning everything up.
            double A = Math.Pow(10, g / 40.0), w0 = 2 * Math.PI * 90.0 / _outRate, cs = Math.Cos(w0), sn = Math.Sin(w0);
            double alpha = sn / 2 * Math.Sqrt(2), sA = 2 * Math.Sqrt(A) * alpha;
            double b0 = A * ((A + 1) - (A - 1) * cs + sA), b1 = 2 * A * ((A - 1) - (A + 1) * cs), b2 = A * ((A + 1) - (A - 1) * cs - sA);
            double a0 = (A + 1) + (A - 1) * cs + sA, a1 = -2 * ((A - 1) + (A + 1) * cs), a2 = (A + 1) + (A - 1) * cs - sA;
            _b0 = (float)(b0 / a0); _b1 = (float)(b1 / a0); _b2 = (float)(b2 / a0); _a1 = (float)(a1 / a0); _a2 = (float)(a2 / a0);
            _makeup = Mathf.Pow(10f, -g * 0.3f / 20f);
        }
    }
}
