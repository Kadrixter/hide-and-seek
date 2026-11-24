using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Synthesised sounds of the dot link panel (made once): a dry relay-like tick per cell drawn (higher when a line
    /// grows, lower when it shrinks), a soft two-tone beep when a pair is joined, a low buzz for a blocked move, the
    /// power-on (relay clunk + rising mains hum) when the panel comes alive, and the solved chime (clunk + four soft
    /// rising tones).
    /// </summary>
    public static class DotLinkSounds
    {
        const int Rate = 44100;
        static AudioClip _tick, _join, _deny, _power, _solved;

        static AudioClip Clip(string name, float seconds, System.Func<float, int, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = f(i / (float)Rate, rnd.Next());
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.0001f) for (int i = 0; i < n; i++) data[i] *= 0.9f / peak;
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float Noise(int r) => (r & 0xFFFF) / 32768f - 1f;
        static float Sin(float t, float f) => Mathf.Sin(t * 2f * Mathf.PI * f);

        /// <summary>Soft tone with a quick attack (a little second harmonic so it is not a pure beep).</summary>
        static float Tone(float t, float f, float decay) =>
            t < 0f ? 0f : (Sin(t, f) + 0.25f * Sin(t, f * 2f)) * Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 300f);

        static float Clunk(float t) =>
            Noise((int)(t * 977331)) * Mathf.Exp(-t * 260f) * 0.6f + Sin(t, 95f) * Mathf.Exp(-t * 32f) * 0.7f + Sin(t, 410f) * Mathf.Exp(-t * 70f) * 0.3f;

        public static AudioClip Tick => _tick != null ? _tick : (_tick = Clip("DotLinkTick", 0.05f, (t, r) =>
            Noise(r) * Mathf.Exp(-t * 1800f) * 0.45f + Sin(t, 2600f) * Mathf.Exp(-t * 190f) * 0.35f + Sin(t, 900f) * Mathf.Exp(-t * 120f) * 0.25f));

        public static AudioClip Join => _join != null ? _join : (_join = Clip("DotLinkJoin", 0.42f, (t, r) =>
            Tone(t, 784f, 11f) * 0.5f + Tone(t - 0.085f, 1175f, 9f) * 0.45f));

        public static AudioClip Deny => _deny != null ? _deny : (_deny = Clip("DotLinkDeny", 0.16f, (t, r) =>
            (Mathf.Sign(Sin(t, 110f)) * 0.3f + Sin(t, 220f) * 0.4f) * Mathf.Exp(-t * 18f) * Mathf.Clamp01(t * 400f)));

        public static AudioClip Power => _power != null ? _power : (_power = Clip("DotLinkPower", 1.3f, (t, r) =>
        {
            float hum = (Sin(t, 50f) * 0.5f + Sin(t, 100f) * 0.35f + Sin(t, 150f) * 0.15f) * Mathf.Clamp01((t - 0.05f) * 2.5f) * Mathf.Clamp01((1.3f - t) * 3f);
            float whine = Sin(t, 3000f + 2500f * Mathf.Clamp01(t)) * 0.05f * Mathf.Clamp01(t * 2f) * Mathf.Clamp01((1.3f - t) * 3f);
            return Clunk(t) + hum * 0.45f + whine;
        }));

        public static AudioClip Solved => _solved != null ? _solved : (_solved = Clip("DotLinkSolved", 1.8f, (t, r) =>
            Clunk(t) * 0.8f + Tone(t - 0.12f, 523f, 3.2f) * 0.4f + Tone(t - 0.26f, 659f, 3.2f) * 0.4f
            + Tone(t - 0.40f, 784f, 3.0f) * 0.4f + Tone(t - 0.56f, 1047f, 2.4f) * 0.5f));
    }
}
