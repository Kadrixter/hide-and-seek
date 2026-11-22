using System;
using UnityEngine;

namespace HideAndSeek.Audio
{
    /// <summary>
    /// Breathing and heartbeat for "the seeker is close" (synthesised once at start-up, deterministic, no files).
    /// Breath: air noise shaped by mouth formants (exhale ~ "haa" 620/1150/2450 Hz, inhale brighter 850/1650/3000 Hz),
    /// slow turbulence and - when scared - a shaky tremor in the inhale. Heartbeat: two soft low thumps ("lub-dub").
    /// Everything is broadband noise or below 200 Hz: no tones, nothing that could whistle.
    /// </summary>
    public static class StressSounds
    {
        public const int Rate = 44100;
        public const int Variants = 3;

        private static AudioClip[] _inCalm, _inFast, _exCalm, _exFast;
        private static AudioClip _beatSlow, _beatFast;

        /// <summary>Inhale clip: <paramref name="fast"/> = short panicked gasp (~0.42 s), else a deep one (~0.85 s).</summary>
        public static AudioClip Inhale(bool fast, int variant) { Build(); return (fast ? _inFast : _inCalm)[Mod(variant)]; }

        /// <summary>Exhale clip: <paramref name="fast"/> = short (~0.5 s), else long (~1.05 s).</summary>
        public static AudioClip Exhale(bool fast, int variant) { Build(); return (fast ? _exFast : _exCalm)[Mod(variant)]; }

        /// <summary>One "lub-dub" for the given heart rate (the second sound comes sooner when the heart races).</summary>
        public static AudioClip Heartbeat(float bpm) { Build(); return bpm > 112f ? _beatFast : _beatSlow; }

        private static int Mod(int v) => ((v % Variants) + Variants) % Variants;

        private static void Build()
        {
            if (_inCalm != null) return;
            _inCalm = new AudioClip[Variants]; _inFast = new AudioClip[Variants];
            _exCalm = new AudioClip[Variants]; _exFast = new AudioClip[Variants];
            for (int v = 0; v < Variants; v++)
            {
                uint s = (uint)(v * 7919 + 17);
                _inCalm[v] = Clip("breath_in_" + v, Breath(true, 0.86f + 0.05f * v, 0.25f, s + 1));
                _inFast[v] = Clip("breath_in_fast_" + v, Breath(true, 0.40f + 0.03f * v, 1f, s + 2));
                _exCalm[v] = Clip("breath_out_" + v, Breath(false, 1.05f + 0.06f * v, 0.25f, s + 3));
                _exFast[v] = Clip("breath_out_fast_" + v, Breath(false, 0.50f + 0.04f * v, 1f, s + 4));
            }
            _beatSlow = Clip("heartbeat_slow", Beat(0.30f));
            _beatFast = Clip("heartbeat_fast", Beat(0.22f));
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        // ================================================================ breath

        private static float[] Breath(bool inhale, float dur, float effort, uint seed)
        {
            int n = Mathf.RoundToInt(dur * Rate);
            var buf = new float[n];
            var rng = new Rng(seed);
            float j = 1f + rng.Range(-0.06f, 0.06f);
            float f1 = (inhale ? 850f : 620f) * j, f2 = (inhale ? 1650f : 1150f) * j, f3 = (inhale ? 3000f : 2450f) * j;
            float sweep = inhale ? 0.06f : -0.09f; // the mouth opens on an inhale, relaxes on an exhale
            var bp1 = Biquad.BandPass(f1, 2.0f); var bp2 = Biquad.BandPass(f2, 3.0f); var bp3 = Biquad.BandPass(f3, 3.5f);
            var body = Biquad.LowPass(inhale ? 3200f : 2400f, 0.7f);
            var hiss = Biquad.HighPass(inhale ? 4200f : 3500f, 0.7f);
            var hissTop = Biquad.LowPass(8500f, 0.7f);
            var dc = Biquad.HighPass(140f, 0.7f);
            float red = 0f, turb = 1f, turbTarget = 1f;
            float tremPh = rng.Range(0f, 6.28f), tremHz = rng.Range(6.2f, 8.4f);
            int turbStep = Rate / 12;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                if ((i & 63) == 0)
                {
                    float k = 1f + sweep * t;
                    bp1.Retune(Biquad.BandPass(f1 * k, 2.0f));
                    bp2.Retune(Biquad.BandPass(f2 * k, 3.0f));
                }
                if (i % turbStep == 0) turbTarget = rng.Range(0.72f, 1.15f);
                turb += (turbTarget - turb) * 0.0015f;

                float w = rng.Noise();
                red = 0.96f * red + 0.28f * w;
                float x = 0.55f * bp1.Tick(w) + 0.45f * bp2.Tick(w) + 0.22f * bp3.Tick(w)
                          + 0.3f * body.Tick(0.6f * w + 0.4f * red)
                          + (0.05f + 0.1f * effort) * hissTop.Tick(hiss.Tick(w));
                float env = inhale ? Smooth(t / 0.42f) * (1f - Smooth((t - 0.78f) / 0.22f))
                                   : Smooth(t / 0.1f) * Mathf.Pow(1f - t, 1.3f);
                float time = i / (float)Rate;
                float tremDepth = (inhale ? 0.32f : 0.1f) * effort;
                float trem = 1f - tremDepth * (0.5f + 0.5f * Mathf.Sin(6.2831853f * tremHz * time + tremPh));
                buf[i] = dc.Tick(x) * env * turb * trem;
            }
            return Finish(buf, 0.8f, 0.004f);
        }

        // ================================================================ heartbeat

        private static float[] Beat(float gap)
        {
            var buf = new float[Mathf.RoundToInt((gap + 0.34f) * Rate)];
            var rng = new Rng(4242);
            Thump(buf, 0.006f, 1f, 64f, 40f, 0.05f, rng);
            Thump(buf, 0.006f + gap, 0.62f, 76f, 50f, 0.038f, rng);
            // soft and low, but with a little 150-250 Hz body so small speakers still show the beat
            var lp1 = Biquad.LowPass(230f, 0.7f); var lp2 = Biquad.LowPass(260f, 0.6f); var hp = Biquad.HighPass(28f, 0.7f);
            for (int i = 0; i < buf.Length; i++) buf[i] = hp.Tick(lp2.Tick(lp1.Tick(buf[i])));
            return Finish(buf, 0.9f, 0.01f);
        }

        private static void Thump(float[] buf, float start, float amp, float f0, float f1, float decay, Rng rng)
        {
            int s0 = Mathf.RoundToInt(start * Rate);
            int len = Mathf.Min(buf.Length - s0, Mathf.RoundToInt(decay * 9f * Rate));
            var tex = Biquad.LowPass(260f, 0.7f);
            double ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / 0.025f);
                ph += 2.0 * Math.PI * f / Rate;
                float env = (1f - Mathf.Exp(-t / 0.006f)) * Mathf.Exp(-t / decay);
                float flesh = tex.Tick(rng.Noise()) * Mathf.Exp(-t / 0.012f) * 0.9f;
                buf[s0 + i] += amp * (env * ((float)Math.Sin(ph) + 0.3f * (float)Math.Sin(2 * ph)) + flesh);
            }
        }

        // ================================================================ helpers

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float[] Finish(float[] b, float peak, float fade)
        {
            float max = 1e-6f;
            foreach (var s in b) max = Mathf.Max(max, Mathf.Abs(s));
            float g = peak / max;
            int f = Mathf.Min(b.Length / 4, Mathf.RoundToInt(fade * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float k = g;
                if (i < f) k *= i / (float)f;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < f) k *= fromEnd / (float)f;
                b[i] *= k;
            }
            return b;
        }

        private sealed class Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; }
            private uint Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
            public float Value() => (Next() >> 8) / 16777216f;
            public float Range(float a, float b) => a + (b - a) * Value();
            public float Noise() => Value() * 2f - 1f;
        }

        /// <summary>RBJ biquad (transposed direct form II).</summary>
        private struct Biquad
        {
            private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

            public float Tick(float x)
            {
                float y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return y;
            }

            public void Retune(Biquad c) { _b0 = c._b0; _b1 = c._b1; _b2 = c._b2; _a1 = c._a1; _a2 = c._a2; }

            private static Biquad Make(double b0, double b1, double b2, double a0, double a1, double a2) => new Biquad
            {
                _b0 = (float)(b0 / a0), _b1 = (float)(b1 / a0), _b2 = (float)(b2 / a0), _a1 = (float)(a1 / a0), _a2 = (float)(a2 / a0),
            };

            private static void Prep(float f, float q, out double cos, out double alpha)
            {
                double w0 = 2.0 * Math.PI * Mathf.Clamp(f, 10f, Rate * 0.45f) / Rate;
                cos = Math.Cos(w0);
                alpha = Math.Sin(w0) / (2.0 * q);
            }

            public static Biquad LowPass(float f, float q)
            {
                Prep(f, q, out double c, out double a);
                return Make((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
            }

            public static Biquad HighPass(float f, float q)
            {
                Prep(f, q, out double c, out double a);
                return Make((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
            }

            /// <summary>Band-pass with 0 dB peak gain.</summary>
            public static Biquad BandPass(float f, float q)
            {
                Prep(f, q, out double c, out double a);
                return Make(a, 0, -a, 1 + a, -2 * c, 1 - a);
            }
        }
    }
}
