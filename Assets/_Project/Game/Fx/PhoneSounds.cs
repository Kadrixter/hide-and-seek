using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Own sounds of the toy phone, synthesised once at runtime (fixed seeds, so every copy sounds the same) and played
    /// through <see cref="GameAudio"/> (3D, echo, walls, floors). Crush: dull thump of the sole, the thin plastic shell
    /// cracking in a few snaps, the little screen cracking (glassy hiss + tinkles), small bits scattering on concrete
    /// and the tiny speaker dying with an electronic squeal falling away into a crackle. Drop: plastic clatter.
    /// </summary>
    public static class PhoneSounds
    {
        private const int Rate = 44100;
        private static AudioClip _crush, _drop;

        public static void PlayCrush(Vector3 at) => GameAudio.PlayClipAt(Crush, at, 1f, 30f);
        public static void PlayDrop(Vector3 at) => GameAudio.PlayClipAt(Drop, at, 0.85f, 18f);

        public static AudioClip Crush => _crush != null ? _crush : _crush = MakeCrush();
        public static AudioClip Drop => _drop != null ? _drop : _drop = MakeDrop();

        private static AudioClip MakeCrush()
        {
            var rng = new System.Random(5150);
            int n = (int)(Rate * 1.0f);
            var d = new float[n];
            // Sole thump.
            Tone(d, 0f, 95f, 0.09f, 0.55f, 0.03f);
            Noise(d, rng, 0f, 0.05f, 0.25f, 120f, 900f, 0.015f);
            // Shell cracking: sharp mid-high snaps.
            float[] snaps = { 0.004f, 0.016f, 0.031f, 0.052f, 0.083f };
            for (int i = 0; i < snaps.Length; i++)
                Noise(d, rng, snaps[i], 0.035f, 0.7f - i * 0.08f, 1800f, 6500f, 0.006f + 0.002f * i);
            // Screen cracking: glassy high hiss + tinkles.
            Noise(d, rng, 0.012f, 0.12f, 0.32f, 5000f, 11000f, 0.03f);
            for (int i = 0; i < 6; i++)
                Tone(d, 0.015f + (float)rng.NextDouble() * 0.12f, 3800f + (float)rng.NextDouble() * 4200f, 0.09f, 0.11f, 0.025f);
            // Bits scattering on the concrete.
            float t = 0.09f;
            for (int i = 0; i < 16; i++)
            {
                t += 0.012f + (float)(rng.NextDouble() * rng.NextDouble()) * 0.07f;
                float a = 0.3f * Mathf.Exp(-(t - 0.09f) * 3.2f);
                Tone(d, t, 2200f + (float)rng.NextDouble() * 3300f, 0.02f, a, 0.004f);
                Noise(d, rng, t, 0.012f, a * 0.8f, 2500f, 8000f, 0.003f);
            }
            // The speaker dies: a squeal falling from ~2.6 kHz to 260 Hz, square-ish and crackling, then a pop.
            double ph = 0;
            int s0 = (int)(0.035f * Rate), s1 = (int)(0.52f * Rate);
            for (int i = s0; i < s1 && i < n; i++)
            {
                float k = (i - s0) / (float)(s1 - s0);
                float f = Mathf.Lerp(2600f, 260f, Mathf.Pow(k, 0.6f)) * (1f + 0.04f * Mathf.Sin(i * 0.004f));
                ph += f / Rate;
                float sq = Mathf.Sign(Mathf.Sin((float)(ph * 2.0 * Mathf.PI))) * 0.6f + Mathf.Sin((float)(ph * 2.0 * Mathf.PI)) * 0.4f;
                float env = Mathf.Min(1f, (i - s0) / (0.01f * Rate)) * (1f - k) * (1f - k);
                bool drop = rng.NextDouble() < 0.08 * k; // breaking up
                d[i] += drop ? 0f : sq * 0.16f * env;
            }
            Noise(d, rng, 0.52f, 0.02f, 0.35f, 300f, 3000f, 0.004f);
            for (int i = 0; i < 10; i++) Noise(d, rng, 0.53f + (float)rng.NextDouble() * 0.25f, 0.006f, 0.08f, 1000f, 5000f, 0.002f);
            return Finish("phone-crush", d);
        }

        private static AudioClip MakeDrop()
        {
            var rng = new System.Random(77);
            var d = new float[(int)(Rate * 0.35f)];
            Tone(d, 0f, 180f, 0.05f, 0.4f, 0.012f);
            Noise(d, rng, 0f, 0.03f, 0.55f, 1200f, 5000f, 0.008f);
            Noise(d, rng, 0.07f, 0.025f, 0.3f, 1400f, 5000f, 0.006f);
            Tone(d, 0.07f, 2900f, 0.03f, 0.1f, 0.008f);
            Noise(d, rng, 0.13f, 0.02f, 0.15f, 1600f, 5000f, 0.005f);
            return Finish("phone-drop", d);
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
