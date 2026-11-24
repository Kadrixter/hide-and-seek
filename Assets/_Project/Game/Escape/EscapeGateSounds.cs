using UnityEngine;

namespace HideAndSeek.Game.Escape
{
    /// <summary>
    /// Synthesised sounds of the opening exit gate (made once, on first use): the shutter motor with the chain
    /// running over the sprocket (seamless loop), the clunk of a chain link / slat, the heavy stop at the top and the
    /// two-tone wailing siren (seamless loop).
    /// </summary>
    public static class EscapeGateSounds
    {
        const int Rate = 44100;
        static AudioClip _motor, _clunk, _stop, _siren;

        public static AudioClip Motor => _motor ??= Make("EscGateMotor", MotorData(2f), true);
        public static AudioClip Clunk => _clunk ??= Make("EscGateClunk", ClunkData(0.32f, 1f), false);
        public static AudioClip Stop => _stop ??= Make("EscGateStop", ClunkData(0.9f, 0.55f), false);
        public static AudioClip Siren => _siren ??= Make("EscGateSiren", SirenData(), true);

        static uint _rng = 0x9E3779B9u;
        static float Rnd()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000 * 2f - 1f;
        }

        static AudioClip Make(string name, float[] d, bool loop)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            float k = 0.9f / peak;
            int fade = loop ? 0 : Mathf.Min(d.Length / 4, Rate / 100);
            for (int i = 0; i < d.Length; i++)
            {
                float g = k;
                int fromEnd = d.Length - 1 - i;
                if (fade > 0 && fromEnd < fade) g *= fromEnd / (float)fade;
                if (!loop && i < 32) g *= i / 32f;
                d[i] *= g;
            }
            var c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        /// <summary>
        /// Electric motor hum (50 Hz mains with harmonics, slow load wobble) plus the chain: short metallic ticks at
        /// 16 per second with a heavier beat every 4th. Every component repeats a whole number of times in the clip,
        /// so the loop has no seam.
        /// </summary>
        static float[] MotorData(float len)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float lp = 0f, bp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float wob = 1f + 0.12f * Mathf.Sin(2f * Mathf.PI * 1.5f * t);
                float hum = 0.5f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.32f * Mathf.Sin(2f * Mathf.PI * 100f * t + 0.4f)
                            + 0.18f * Mathf.Sin(2f * Mathf.PI * 150f * t + 1.1f) + 0.1f * Mathf.Sin(2f * Mathf.PI * 300f * t);
                // gearbox whine
                float whine = 0.07f * Mathf.Sin(2f * Mathf.PI * 820f * t + 2f * Mathf.Sin(2f * Mathf.PI * 3f * t));
                // chain ticks
                float tickT = t * 16f;
                int tick = Mathf.FloorToInt(tickT);
                float tf = (tickT - tick) / 16f;
                float amp = tick % 4 == 0 ? 1f : 0.55f;
                float noise = Rnd();
                bp += (noise - bp) * 0.35f;
                lp += (bp - lp) * 0.08f;
                float metal = (bp - lp) * Mathf.Exp(-tf * 160f) * amp * 1.4f;
                float ring = Mathf.Sin(2f * Mathf.PI * 2350f * t) * Mathf.Exp(-tf * 90f) * amp * 0.12f;
                // rumble of the curtain sliding in the rails
                lp2 += (noise - lp2) * 0.01f;
                d[i] = hum * 0.45f * wob + whine + metal + ring + lp2 * 1.6f;
            }
            return d;
        }

        /// <summary>A heavy steel knock: low thump, inharmonic ringing partials and a noisy rattle tail.</summary>
        static float[] ClunkData(float len, float pitch)
        {
            int n = (int)(Rate * len);
            var d = new float[n];
            float[] f = { 180f, 427f, 1130f, 2370f, 3510f };
            float[] a = { 0.9f, 0.5f, 0.35f, 0.2f, 0.12f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < f.Length; k++) s += a[k] * Mathf.Sin(2f * Mathf.PI * f[k] * pitch * t) * Mathf.Exp(-t * (9f + k * 7f) / Mathf.Max(0.4f, len * 2.5f));
                float noise = Rnd();
                lp += (noise - lp) * 0.25f;
                float rattle = lp * Mathf.Exp(-t * 26f) * (1f + 0.6f * Mathf.Sin(2f * Mathf.PI * 38f * t));
                float thump = Mathf.Sin(2f * Mathf.PI * 62f * pitch * t) * Mathf.Exp(-t * 18f) * 1.2f;
                d[i] = s * 0.6f + rattle * 0.8f + thump;
            }
            return d;
        }

        /// <summary>
        /// Wailing two-tone siren (a slow up-down sweep 560 -> 1180 Hz, square-ish timbre with a fast warble).
        /// Phase is accumulated and the loop length chosen so the sweep and the phase both close.
        /// </summary>
        static float[] SirenData()
        {
            const float sweep = 2.4f; // one rise + fall
            int n = (int)(Rate * sweep);
            var d = new float[n];
            // integral of the frequency over one period, then scale the base so that it is a whole number of cycles
            double cycles = 0;
            for (int i = 0; i < n; i++) cycles += Freq(i / (double)Rate, sweep) / Rate;
            double fix = System.Math.Round(cycles) / cycles;
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                ph += Freq(t, sweep) * fix / Rate;
                float p = (float)(ph * 2.0 * System.Math.PI);
                float s = Mathf.Sin(p) + 0.33f * Mathf.Sin(3f * p) + 0.18f * Mathf.Sin(5f * p) + 0.1f * Mathf.Sin(7f * p);
                float warble = 1f + 0.1f * Mathf.Sin(2f * Mathf.PI * 10f * (float)t); // 24 cycles per loop
                d[i] = s * warble;
            }
            return d;
        }

        static double Freq(double t, float sweep)
        {
            double x = 0.5 - 0.5 * System.Math.Cos(2.0 * System.Math.PI * t / sweep); // 0 -> 1 -> 0
            return 560.0 + 620.0 * x;
        }
    }
}
