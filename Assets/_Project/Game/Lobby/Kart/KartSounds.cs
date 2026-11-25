using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Go-kart sounds synthesised once at runtime (fixed seeds): <see cref="Engine"/> = a seamless one-second loop of a
    /// small single-cylinder engine at 3600 rpm (one bark per revolution: a pulse ringing two exhaust / crankcase
    /// resonances, a puff of noise, slight cycle-to-cycle variation); played faster or slower it follows the revs.
    /// <see cref="Screech"/> = a two-second loop of tyre squeal (a wobbling whistle over band-passed rubber noise).
    /// <see cref="Thud"/> = a bump into a wall: a low knock with a plastic rattle.
    /// </summary>
    public static class KartSounds
    {
        private const int Rate = 44100;
        private static AudioClip _engine, _screech, _thud;

        public static AudioClip Engine => _engine != null ? _engine : _engine = MakeEngine();
        public static AudioClip Screech => _screech != null ? _screech : _screech = MakeScreech();
        public static AudioClip Thud => _thud != null ? _thud : _thud = MakeThud();

        private static AudioClip MakeEngine()
        {
            const int cycles = 60; // 60 Hz firing over exactly one second: loops without a seam
            int n = Rate, per = Rate / cycles;
            var d = new float[n];
            var rng = new System.Random(4242);
            float w1 = 2f * Mathf.PI * 380f / Rate, w2 = 2f * Mathf.PI * 1150f / Rate, w3 = 2f * Mathf.PI * 120f / Rate;
            for (int c = 0; c < cycles; c++)
            {
                float amp = 0.82f + 0.18f * (float)rng.NextDouble();
                int start = c * per + rng.Next(0, 18);
                for (int j = 0; j < per * 2; j++)
                {
                    float t = j / (float)Rate;
                    float s = amp * (0.55f * Mathf.Sin(w1 * j) * Mathf.Exp(-t / 0.0045f)
                                     + 0.22f * Mathf.Sin(w2 * j) * Mathf.Exp(-t / 0.002f)
                                     + 0.35f * Mathf.Sin(w3 * j) * Mathf.Exp(-t / 0.009f));
                    if (j < 260) s += amp * 0.28f * (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - j / 260f);
                    d[(start + j) % n] += s;
                }
            }
            // A low hum at the firing rate under it all (the crank), whole cycles only.
            for (int i = 0; i < n; i++) d[i] += 0.12f * Mathf.Sin(2f * Mathf.PI * cycles * i / Rate);
            return Finish("kart-engine", d);
        }

        private static AudioClip MakeScreech()
        {
            const float seconds = 2f;
            int n = (int)(Rate * seconds), fade = Rate / 20;
            var d = new float[n + fade];
            var rng = new System.Random(777);
            float lp1 = 0f, lp2 = 0f;
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                // Whistle: 1200 / 1800 Hz with a slow periodic wobble (whole cycles in two seconds).
                float wob = 0.035f * Mathf.Sin(2f * Mathf.PI * 3f * t) + 0.02f * Mathf.Sin(2f * Mathf.PI * 5.5f * t + 1f);
                p1 += 1200.0 * (1.0 + wob) / Rate;
                p2 += 1800.0 * (1.0 + wob * 1.2) / Rate;
                p3 += 2650.0 * (1.0 + wob * 0.8) / Rate;
                float tone = 0.5f * Mathf.Sin((float)(p1 * 2.0 * System.Math.PI)) + 0.25f * Mathf.Sin((float)(p2 * 2.0 * System.Math.PI))
                           + 0.12f * Mathf.Sin((float)(p3 * 2.0 * System.Math.PI));
                float am = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                // Rubber noise, band-passed around 1-3 kHz.
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.35f * (x - lp1);
                lp2 += 0.08f * (lp1 - lp2);
                float band = (lp1 - lp2) * 1.6f;
                d[i] = 0.55f * tone * am + 0.45f * band;
            }
            // Crossfade the tail into the start: seamless loop.
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = d[i];
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                o[i] = d[i] * k + d[n + i] * (1f - k);
            }
            return Finish("kart-screech", o);
        }

        private static AudioClip MakeThud()
        {
            int n = (int)(Rate * 0.45f);
            var d = new float[n];
            var rng = new System.Random(99);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float knock = Mathf.Sin(2f * Mathf.PI * (75f - 30f * t) * t) * Mathf.Exp(-t / 0.07f);
                float rattle = Mathf.Sin(2f * Mathf.PI * 620f * t) * Mathf.Exp(-t / 0.05f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 41f * t));
                float click = t < 0.012f ? (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - t / 0.012f) : 0f;
                d[i] = 0.9f * knock + 0.3f * rattle + 0.5f * click;
            }
            return Finish("kart-thud", d);
        }

        private static AudioClip Finish(string name, float[] d)
        {
            float peak = 0f;
            foreach (float x in d) peak = Mathf.Max(peak, Mathf.Abs(x));
            float g = peak > 1e-5f ? 0.9f / peak : 1f;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
