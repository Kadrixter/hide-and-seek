using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Sounds of the energised RC car, synthesised once at runtime (fixed seeds, every copy sounds the same):
    /// <see cref="Buzz"/> = the whole charge (a harsh 100 Hz mains buzz with rattling harmonics, a high sizzle and
    /// snapping crackles, swelling in and dying out), <see cref="Zap"/> = the crack of a bolt into a player
    /// (a sharp snap, a burst of buzz, a few crackles). Played through GameAudio (3D, walls, floors).
    /// </summary>
    public static class RcChargeSounds
    {
        private const int Rate = 44100;
        private static AudioClip _buzz, _zap;

        public static AudioClip Buzz => _buzz != null ? _buzz : _buzz = MakeBuzz(HideAndSeek.Game.Net.RcPower.ChargeTime + 0.15f);
        public static AudioClip Zap => _zap != null ? _zap : _zap = MakeZap();

        private static AudioClip MakeBuzz(float seconds)
        {
            var rng = new System.Random(5150);
            int n = (int)(Rate * seconds);
            var d = new float[n];
            double ph = 0;
            float lp = 0f, hpIn = 0f, hpOut = 0f;
            float aH = Mathf.Exp(-2f * Mathf.PI * 3200f / Rate), aL = 1f - Mathf.Exp(-2f * Mathf.PI * 9000f / Rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Mains buzz: a slightly wavering 100 Hz with odd harmonics, clipped hard (the transformer rattle).
                float f = 100f * (1f + 0.004f * Mathf.Sin(t * 2f * Mathf.PI * 0.7f));
                ph += f / Rate;
                float w = (float)(ph * 2.0 * Mathf.PI);
                float hum = Mathf.Sin(w) + 0.55f * Mathf.Sin(3f * w) + 0.35f * Mathf.Sin(5f * w) + 0.22f * Mathf.Sin(7f * w) + 0.15f * Mathf.Sin(11f * w);
                hum = Mathf.Clamp(hum * 1.6f, -1f, 1f);
                float surge = 0.75f + 0.25f * Mathf.PerlinNoise(t * 2.3f, 0.5f);
                // Sizzle: high band noise, gated by the buzz (arcs strike on the peaks).
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += aL * (x - lp);
                hpOut = aH * (hpOut + lp - hpIn);
                hpIn = lp;
                float gate = Mathf.Abs(Mathf.Sin(w * 0.5f));
                d[i] = 0.32f * hum * surge + 0.55f * hpOut * (0.35f + 0.65f * gate * gate) * surge;
            }

            // Snapping crackles (little arcs jumping to the floor), irregular, in bursts.
            float tc = 0.05f;
            while (tc < seconds - 0.2f)
            {
                tc += 0.02f + (float)(rng.NextDouble() * rng.NextDouble()) * 0.22f;
                float a = 0.25f + 0.45f * (float)rng.NextDouble();
                Snap(d, rng, tc, a, 0.004f + 0.006f * (float)rng.NextDouble());
            }

            // Swell in, die out.
            int fin = (int)(0.18f * Rate), fout = (int)(0.4f * Rate);
            for (int i = 0; i < n; i++)
            {
                float k = Mathf.Min(1f, i / (float)fin) * Mathf.Min(1f, (n - i) / (float)fout);
                d[i] *= k;
            }
            return Finish("rc-charge-buzz", d);
        }

        private static AudioClip MakeZap()
        {
            var rng = new System.Random(6061);
            int n = (int)(Rate * 0.75f);
            var d = new float[n];
            Snap(d, rng, 0f, 1.1f, 0.012f);
            Snap(d, rng, 0.004f, 0.7f, 0.02f);
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                ph += 120f / Rate;
                float w = (float)(ph * 2.0 * Mathf.PI);
                float buzz = Mathf.Clamp((Mathf.Sin(w) + 0.6f * Mathf.Sin(3f * w) + 0.4f * Mathf.Sin(5f * w)) * 2f, -1f, 1f);
                d[i] += 0.5f * buzz * Mathf.Exp(-t / 0.22f) * Mathf.Min(1f, t / 0.01f);
            }
            float tc = 0.02f;
            for (int k = 0; k < 12; k++)
            {
                tc += 0.015f + (float)rng.NextDouble() * 0.05f;
                Snap(d, rng, tc, 0.55f * Mathf.Exp(-tc * 3f), 0.005f);
            }
            return Finish("rc-charge-zap", d);
        }

        /// <summary>One electric snap: a white-noise spike with a bright decay.</summary>
        private static void Snap(float[] d, System.Random rng, float at, float amp, float decay)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(decay * 6f * Rate));
            float prev = 0f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate;
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                float hp = x - prev; // crude high-pass: crisp
                prev = x;
                d[i] += amp * 0.6f * hp * Mathf.Exp(-t / decay);
            }
        }

        private static AudioClip Finish(string name, float[] d)
        {
            float peak = 0f;
            foreach (float x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            float g = peak > 0.9f ? 0.9f / peak : 1f;
            for (int i = 0; i < d.Length; i++) d[i] = (d[i] * g) / (1f + 0.15f * Mathf.Abs(d[i] * g));
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
