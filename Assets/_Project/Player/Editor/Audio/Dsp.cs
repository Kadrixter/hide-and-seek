using System;
using UnityEngine;

namespace HideAndSeek.Player.Editor.Audio
{
    /// <summary>Deterministic xorshift RNG (identical output on every machine / Unity version).</summary>
    internal sealed class Rng
    {
        private ulong _s;

        public Rng(ulong seed)
        {
            _s = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            if (_s == 0) _s = 1;
            for (int i = 0; i < 4; i++) Next();
        }

        public ulong Next()
        {
            _s ^= _s << 13;
            _s ^= _s >> 7;
            _s ^= _s << 17;
            return _s;
        }

        /// <summary>[0,1)</summary>
        public float Value() => (Next() >> 40) / 16777216f;
        public float Range(float a, float b) => a + (b - a) * Value();
        public int Range(int a, int bExclusive) => a + (int)(Value() * (bExclusive - a));
        /// <summary>Uniform white noise in [-1,1).</summary>
        public float Noise() => Value() * 2f - 1f;
        /// <summary>x * (1 +- amount)</summary>
        public float Jitter(float x, float amount) => x * (1f + Range(-amount, amount));
        /// <summary>Exponentially distributed value with the given mean.</summary>
        public float Exp(float mean) => -mean * Mathf.Log(1f - Value() * 0.9999f);
    }

    /// <summary>RBJ cookbook biquad (transposed direct form II).</summary>
    internal struct Biquad
    {
        private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public float Tick(float x)
        {
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }

        public void Reset() { _z1 = 0f; _z2 = 0f; }

        /// <summary>Takes the coefficients of <paramref name="c"/> but keeps this filter's state (for sweeps).</summary>
        public void Retune(Biquad c)
        {
            _b0 = c._b0; _b1 = c._b1; _b2 = c._b2; _a1 = c._a1; _a2 = c._a2;
        }

        private static Biquad Norm(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            return new Biquad
            {
                _b0 = (float)(b0 / a0), _b1 = (float)(b1 / a0), _b2 = (float)(b2 / a0),
                _a1 = (float)(a1 / a0), _a2 = (float)(a2 / a0),
            };
        }

        private static void Prep(float fs, float f, float q, out double cos, out double alpha)
        {
            f = Mathf.Clamp(f, 10f, fs * 0.49f);
            double w0 = 2.0 * Math.PI * f / fs;
            cos = Math.Cos(w0);
            alpha = Math.Sin(w0) / (2.0 * Math.Max(0.05, q));
        }

        public static Biquad LowPass(float fs, float f, float q = 0.7071f)
        {
            Prep(fs, f, q, out var c, out var a);
            return Norm((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public static Biquad HighPass(float fs, float f, float q = 0.7071f)
        {
            Prep(fs, f, q, out var c, out var a);
            return Norm((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
        }

        /// <summary>Band-pass with 0 dB peak gain.</summary>
        public static Biquad BandPass(float fs, float f, float q)
        {
            Prep(fs, f, q, out var c, out var a);
            return Norm(a, 0, -a, 1 + a, -2 * c, 1 - a);
        }

        public static Biquad Peak(float fs, float f, float q, float gainDb)
        {
            Prep(fs, f, q, out var c, out var a);
            double A = Math.Pow(10, gainDb / 40.0);
            return Norm(1 + a * A, -2 * c, 1 - a * A, 1 + a / A, -2 * c, 1 - a / A);
        }

        public static Biquad HighShelf(float fs, float f, float gainDb)
        {
            Prep(fs, f, 0.7071f, out var c, out _);
            double A = Math.Pow(10, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * Mathf.Clamp(f, 10f, fs * 0.49f) / fs;
            double alpha = Math.Sin(w0) / 2.0 * Math.Sqrt(2.0);
            double sq = 2 * Math.Sqrt(A) * alpha;
            return Norm(A * ((A + 1) + (A - 1) * c + sq), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - sq),
                (A + 1) - (A - 1) * c + sq, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - sq);
        }
    }

    /// <summary>Common signal helpers operating on mono float buffers.</summary>
    internal static class Dsp
    {
        public const int SampleRate = 44100;
        public const float Fs = SampleRate;

        public static int Samples(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * Fs));

        /// <summary>Linear attack then exponential decay.</summary>
        public static float AttackDecay(float t, float attack, float tau)
        {
            if (t < 0f) return 0f;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / tau);
        }

        /// <summary>Smooth hump over [0,dur]: sin(pi * u^skew)^power.</summary>
        public static float Hump(float t, float dur, float skew = 0.7f, float power = 1.5f)
        {
            if (t <= 0f || t >= dur) return 0f;
            float u = Mathf.Pow(t / dur, skew);
            return Mathf.Pow(Mathf.Sin(Mathf.PI * u), power);
        }

        /// <summary>Adds filtered noise shaped by <paramref name="env"/> (env(t) with t relative to start).</summary>
        public static void AddNoise(float[] buf, float start, float maxDur, Func<float, float> env, float gain, Rng rng, params Biquad[] filters)
        {
            int s0 = Samples(start), n = Samples(maxDur);
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= buf.Length) break;
                float x = rng.Noise();
                for (int f = 0; f < filters.Length; f++) x = filters[f].Tick(x);
                if (idx < 0) continue;
                buf[idx] += x * env(i / Fs) * gain;
            }
        }

        /// <summary>Sine with exponential pitch glide f0 -> f1 and attack/decay envelope.</summary>
        public static void AddThump(float[] buf, float start, float f0, float f1, float pitchTau, float attack, float ampTau, float gain)
        {
            int s0 = Samples(start);
            int n = Samples(attack + ampTau * 7f);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / Fs;
                float f = f1 + (f0 - f1) * Mathf.Exp(-t / pitchTau);
                phase += 2.0 * Math.PI * f / Fs;
                if (s0 + i < 0) continue;
                buf[s0 + i] += (float)Math.Sin(phase) * AttackDecay(t, attack, ampTau) * gain;
            }
        }

        /// <summary>Decaying sinusoidal mode (modal synthesis).</summary>
        public static void AddMode(float[] buf, float start, float freq, float tau, float gain, float phase0, float attack = 0.0003f)
        {
            if (freq >= Fs * 0.45f) return;
            int s0 = Samples(start);
            int n = Samples(attack + tau * 7f);
            double w = 2.0 * Math.PI * freq / Fs;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                if (s0 + i < 0) continue;
                float t = i / Fs;
                buf[s0 + i] += (float)Math.Sin(phase0 + w * i) * AttackDecay(t, attack, tau) * gain;
            }
        }

        /// <summary>Rising-pitch bubble / droplet "blip" (Minnaert-style chirp).</summary>
        public static void AddBubble(float[] buf, float start, float f0, float risePer10ms, float tau, float gain)
        {
            int s0 = Samples(start);
            int n = Samples(tau * 6f + 0.0005f);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / Fs;
                float f = f0 * (1f + risePer10ms * t / 0.01f);
                phase += 2.0 * Math.PI * Math.Min(f, Fs * 0.45f) / Fs;
                if (s0 + i < 0) continue;
                buf[s0 + i] += (float)Math.Sin(phase) * AttackDecay(t, 0.0004f, tau) * gain;
            }
        }

        /// <summary>
        /// Sparse micro-impacts (grit, sand, crackle): Poisson events of tiny decaying noise blips,
        /// high/band-passed, scaled by <paramref name="env"/>.
        /// </summary>
        public static void AddGrit(float[] buf, float start, float dur, float rate, Func<float, float> env, float gain, Rng rng, float hp, float bp, float bpQ = 0.8f)
        {
            var tmp = new float[Samples(dur + 0.01f)];
            float t = rng.Exp(1f / rate);
            while (t < dur)
            {
                float amp = rng.Range(0.25f, 1f) * env(t);
                float len = rng.Range(0.0002f, 0.0012f);
                int s0 = Samples(t), n = Samples(len * 5f);
                for (int i = 0; i < n && s0 + i < tmp.Length; i++)
                    tmp[s0 + i] += rng.Noise() * Mathf.Exp(-i / (len * Fs)) * amp;
                t += rng.Exp(1f / rate);
            }
            var f1 = Biquad.HighPass(Fs, hp);
            var f2 = Biquad.BandPass(Fs, bp, bpQ);
            int o = Samples(start);
            for (int i = 0; i < tmp.Length && o + i < buf.Length; i++)
            {
                float x = f2.Tick(f1.Tick(tmp[i])) * 2.2f;
                if (o + i >= 0) buf[o + i] += x * gain;
            }
        }

        /// <summary>Random-amplitude modulation made of overlapping raised-cosine bumps (fabric crinkle).</summary>
        public static float[] Crinkle(int length, float rate, float floor, Rng rng)
        {
            var m = new float[length];
            for (int i = 0; i < length; i++) m[i] = floor;
            float t = rng.Exp(1f / rate);
            float total = length / Fs;
            while (t < total)
            {
                float dur = rng.Range(0.003f, 0.014f);
                float amp = rng.Range(0.2f, 1f);
                int s0 = Samples(t), n = Samples(dur);
                for (int i = 0; i < n && s0 + i < length; i++)
                    m[s0 + i] += amp * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / n));
                t += rng.Exp(1f / rate);
            }
            return m;
        }

        /// <summary>Fabric rustle: band-limited noise with crinkle modulation and an envelope.</summary>
        public static void AddRustle(float[] buf, float start, float dur, float center, float gain, Func<float, float> env, Rng rng, float crinkleRate = 90f)
        {
            int n = Samples(dur);
            var mod = Crinkle(n, crinkleRate, 0.22f, rng);
            var hp = Biquad.HighPass(Fs, 320f);
            var bp1 = Biquad.BandPass(Fs, center, 0.55f);
            var bp2 = Biquad.BandPass(Fs, center * 2.3f, 0.9f);
            var lp = Biquad.LowPass(Fs, 4800f);
            int s0 = Samples(start);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float x = hp.Tick(rng.Noise());
                float y = bp1.Tick(x) + 0.3f * bp2.Tick(x);
                y = lp.Tick(y);
                if (s0 + i >= 0) buf[s0 + i] += y * Mathf.Min(mod[i], 1.6f) * env(i / Fs) * gain * 1.6f;
            }
        }

        /// <summary>Applies a chain of filters in place.</summary>
        public static void Filter(float[] buf, params Biquad[] filters)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i];
                for (int f = 0; f < filters.Length; f++) x = filters[f].Tick(x);
                buf[i] = x;
            }
        }

        /// <summary>Adds low-passed delayed copies (early reflections of a concrete room).</summary>
        public static void EarlyReflections(float[] buf, float[] delays, float[] gains, float lowpass)
        {
            var dry = (float[])buf.Clone();
            for (int k = 0; k < delays.Length; k++)
            {
                var lp = Biquad.LowPass(Fs, lowpass);
                var hp = Biquad.HighPass(Fs, 150f);
                int d = Samples(delays[k]);
                for (int i = 0; i + d < buf.Length; i++)
                    buf[i + d] += hp.Tick(lp.Tick(dry[i])) * gains[k];
            }
        }

        public static float Peak(float[] buf)
        {
            float p = 0f;
            for (int i = 0; i < buf.Length; i++) p = Mathf.Max(p, Mathf.Abs(buf[i]));
            return p;
        }

        /// <summary>DC/rumble removal, peak normalisation, trimming of the silent tail and a short fade-out.</summary>
        public static float[] Finish(float[] buf, float peakTarget, float tailDb = -58f, float hpHz = 35f)
        {
            Filter(buf, Biquad.HighPass(Fs, hpHz, 0.6f));
            float p = Peak(buf);
            if (p > 1e-6f) for (int i = 0; i < buf.Length; i++) buf[i] *= peakTarget / p;

            float thresh = peakTarget * Mathf.Pow(10f, tailDb / 20f);
            int last = buf.Length - 1;
            while (last > 0 && Mathf.Abs(buf[last]) < thresh) last--;
            int len = Mathf.Min(buf.Length, last + Samples(0.01f));
            var outBuf = new float[len];
            Array.Copy(buf, outBuf, len);
            int fade = Mathf.Min(len, Samples(0.012f));
            for (int i = 0; i < fade; i++) outBuf[len - 1 - i] *= (float)i / fade;
            return outBuf;
        }
    }
}
