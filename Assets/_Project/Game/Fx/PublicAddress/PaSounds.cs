using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised click of the microphone's talk bar: a hard plastic tick with a short body resonance; the press is a
    /// little lower and fuller, the release shorter and brighter. (The crackle in the speakers is made by
    /// <see cref="Voice.PaFilter"/> on the voice itself.)
    /// </summary>
    public static class PaSounds
    {
        private static AudioClip _down, _up;

        public static AudioClip ClickDown => _down != null ? _down : _down = Make("pa-ptt-down", 2300f, 0.010f, 0.9f, 11);
        public static AudioClip ClickUp => _up != null ? _up : _up = Make("pa-ptt-up", 3100f, 0.007f, 0.7f, 23);

        private static AudioClip Make(string name, float ring, float decay, float amp, int seed)
        {
            const int rate = 44100;
            int n = rate / 20;
            var d = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t / decay);
                // impulse + a little filtered noise (the plastic snap) + the shell ringing
                float imp = i < 3 ? (i == 0 ? 1f : -0.6f) : 0f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.45f;
                float body = Mathf.Sin(2f * Mathf.PI * ring * t) * env * 0.55f + Mathf.Sin(2f * Mathf.PI * ring * 2.7f * t) * env * env * 0.25f;
                float snap = lp * Mathf.Exp(-t / (decay * 0.35f)) * 0.7f;
                d[i] = Mathf.Clamp((imp * 0.8f + body + snap) * amp, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
