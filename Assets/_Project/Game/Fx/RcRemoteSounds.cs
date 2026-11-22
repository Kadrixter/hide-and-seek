using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Sound of the RC transmitter smashed on concrete, synthesised once at runtime (fixed seed, every copy sounds the
    /// same) and played through <see cref="GameAudio"/> (3D, echo, walls, floors). Different from the stomped car and
    /// the toy phone: a hard hollow clack of the thick case, the case splitting in sharp cracks, the gimbal springs
    /// twanging, the telescopic antenna pinging off, four AA cells clinking and rolling away, small bits ticking.
    /// </summary>
    public static class RcRemoteSounds
    {
        private const int Rate = 44100;
        private static AudioClip _crash;

        public static void PlayCrash(Vector3 at) => GameAudio.PlayClipAt(Crash, at, 1f, 28f);

        public static AudioClip Crash => _crash != null ? _crash : _crash = MakeCrash();

        private static AudioClip MakeCrash()
        {
            var rng = new System.Random(4207);
            int n = (int)(Rate * 1.15f);
            var d = new float[n];
            float Rnd() => (float)rng.NextDouble();

            // Impact: hollow clack of the case on concrete (low knock + case resonance + bright transient).
            Tone(d, 0f, 150f, 0.12f, 0.6f, 0.025f);
            Tone(d, 0f, 690f, 0.1f, 0.38f, 0.018f);
            Tone(d, 0.002f, 1130f, 0.08f, 0.2f, 0.012f);
            Noise(d, rng, 0f, 0.03f, 0.75f, 1500f, 9000f, 0.004f);
            Noise(d, rng, 0f, 0.06f, 0.35f, 200f, 1400f, 0.014f);

            // The case splits: a few sharp cracks.
            float[] cracks = { 0.007f, 0.019f, 0.034f, 0.058f };
            for (int i = 0; i < cracks.Length; i++)
                Noise(d, rng, cracks[i], 0.03f, 0.62f - i * 0.1f, 2200f, 8000f, 0.004f + 0.0015f * i);

            // Gimbal springs: metallic "boing" with a wobble.
            Twang(d, 0.028f, 1160f, 0.32f, 0.12f);
            Twang(d, 0.061f, 1490f, 0.26f, 0.09f);

            // Antenna segments pinging off, the red tip bouncing twice.
            Tone(d, 0.045f, 3350f, 0.12f, 0.12f, 0.03f);
            Tone(d, 0.045f, 5120f, 0.08f, 0.06f, 0.02f);
            Tone(d, 0.12f, 4480f, 0.1f, 0.09f, 0.025f);
            Tone(d, 0.21f, 2900f, 0.05f, 0.07f, 0.012f);
            Tone(d, 0.29f, 2900f, 0.04f, 0.04f, 0.01f);

            // Four AA cells: clink on the floor, bounce, then roll away (low rumble with a wobble).
            for (int b = 0; b < 4; b++)
            {
                float t0 = 0.05f + b * 0.035f + Rnd() * 0.02f;
                for (int k = 0; k < 3; k++)
                {
                    float at = t0 + k * (0.09f - k * 0.02f) + Rnd() * 0.01f;
                    float a = (0.22f - 0.04f * b) * Mathf.Pow(0.55f, k);
                    Tone(d, at, 2450f + b * 330f + Rnd() * 120f, 0.05f, a, 0.009f);
                    Tone(d, at, 420f + b * 40f, 0.04f, a * 0.8f, 0.01f);
                }
                Roll(d, rng, t0 + 0.2f, 0.45f + Rnd() * 0.35f, 0.07f - 0.01f * b, 9f + b * 2.5f);
            }

            // Small plastic bits ticking around.
            float t = 0.08f;
            for (int i = 0; i < 14; i++)
            {
                t += 0.01f + Rnd() * Rnd() * 0.06f;
                float a = 0.22f * Mathf.Exp(-(t - 0.08f) * 4f);
                Noise(d, rng, t, 0.01f, a, 2000f, 7000f, 0.0025f);
                Tone(d, t, 1700f + Rnd() * 2600f, 0.015f, a * 0.5f, 0.003f);
            }
            return Finish("rc-remote-crash", d);
        }

        /// <summary>Spring twang: a sine whose pitch wobbles and settles, with a buzzy overtone.</summary>
        private static void Twang(float[] d, float at, float freq, float len, float amp)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(len * Rate));
            double ph = 0;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate;
                float f = freq * (1f + 0.06f * Mathf.Exp(-t * 18f) * Mathf.Sin(t * 2f * Mathf.PI * 23f));
                ph += f / Rate;
                float x = Mathf.Sin((float)(ph * 2.0 * Mathf.PI)) + 0.3f * Mathf.Sin((float)(ph * 2.0 * Mathf.PI * 2.76));
                d[i] += amp * x * Mathf.Exp(-t / (len * 0.3f)) * Mathf.Min(1f, t / 0.002f);
            }
        }

        /// <summary>Something cylindrical rolling on concrete: band-passed noise, wobbling with the turns, fading out.</summary>
        private static void Roll(float[] d, System.Random rng, float at, float len, float amp, float turns)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(len * Rate));
            float aH = Mathf.Exp(-2f * Mathf.PI * 250f / Rate), aL = 1f - Mathf.Exp(-2f * Mathf.PI * 1300f / Rate);
            float lp = 0f, hpIn = 0f, hpOut = 0f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate, k = t / len;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += aL * (x - lp);
                hpOut = aH * (hpOut + lp - hpIn);
                hpIn = lp;
                float wob = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * turns * (1f - 0.5f * k));
                float env = Mathf.Min(1f, t / 0.03f) * (1f - k) * (1f - k);
                d[i] += amp * 2.2f * hpOut * wob * env;
            }
        }

        /// <summary>Decaying sine at <paramref name="at"/> s.</summary>
        private static void Tone(float[] d, float at, float freq, float len, float amp, float decay)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(len * Rate));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate;
                d[i] += amp * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * freq * t);
            }
        }

        /// <summary>Band-limited noise burst (one-pole high- and low-pass) with an exponential decay.</summary>
        private static void Noise(float[] d, System.Random rng, float at, float len, float amp, float lo, float hi, float decay)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(len * Rate));
            float aH = Mathf.Exp(-2f * Mathf.PI * lo / Rate), aL = 1f - Mathf.Exp(-2f * Mathf.PI * hi / Rate);
            float lp = 0f, hpIn = 0f, hpOut = 0f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += aL * (x - lp);
                hpOut = aH * (hpOut + lp - hpIn);
                hpIn = lp;
                d[i] += amp * 1.8f * hpOut * Mathf.Exp(-t / decay);
            }
        }

        private static AudioClip Finish(string name, float[] d)
        {
            float peak = 0f;
            foreach (float x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            float g = peak > 0.95f ? 0.95f / peak : 1f;
            for (int i = 0; i < d.Length; i++) d[i] = (d[i] * g) / (1f + 0.15f * Mathf.Abs(d[i] * g));
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
