using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised toy slime blaster sounds (made once at runtime, a few variants): the squishy "thwop" of a shot
    /// (air pump + wet plop sweeping down), the wet slap of the goo landing (heavier on a body) and the hollow plastic
    /// clatter of the empty gun. Played in 3D through <see cref="GameAudio.PlayClipAt"/>.
    /// </summary>
    public static class ToySounds
    {
        private const int Rate = 44100;
        private static AudioClip[] _shot, _splat, _clatter;

        public static void Shot(Vector3 pos)
        {
            _shot ??= new[] { ShotClip(1, 1f), ShotClip(2, 1.08f), ShotClip(3, 0.93f) };
            GameAudio.PlayClipAt(_shot[Random.Range(0, _shot.Length)], pos, 0.85f, 30f);
        }

        public static void Splat(Vector3 pos, bool body)
        {
            _splat ??= new[] { SplatClip(11, 1f), SplatClip(12, 0.9f), SplatClip(13, 1.12f) };
            GameAudio.PlayClipAt(_splat[Random.Range(0, _splat.Length)], pos, body ? 1f : 0.8f, body ? 30f : 24f);
        }

        public static void Clatter(Vector3 pos)
        {
            _clatter ??= new[] { ClatterClip(21), ClatterClip(22) };
            GameAudio.PlayClipAt(_clatter[Random.Range(0, _clatter.Length)], pos, 0.55f, 16f);
        }

        private static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.9f;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Pump hiss + plop: a resonant "bwop" sweeping 520 -> 110 Hz with bubbly wobble.</summary>
        private static AudioClip ShotClip(int seed, float pitch)
        {
            var rnd = new System.Random(seed * 97);
            int n = (int)(0.42f * Rate);
            var d = new float[n];
            float phase = 0f, lp = 0f, bp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.18f;
                bp += (lp - bp) * 0.5f;
                float hiss = (lp - bp) * Mathf.Exp(-t / 0.05f) * 1.4f;
                float f = (110f + 410f * Mathf.Exp(-t / 0.045f)) * pitch * (1f + 0.06f * Mathf.Sin(t * 2f * Mathf.PI * 38f));
                phase += 2f * Mathf.PI * f / Rate;
                float plop = Mathf.Sin(phase) * Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t / 0.11f);
                float bubble = Mathf.Sin(2f * Mathf.PI * 900f * pitch * t * (1f + 2.5f * t)) * Mathf.Exp(-Mathf.Pow((t - 0.12f) / 0.025f, 2f)) * 0.35f;
                d[i] = hiss * 0.6f + plop + bubble;
            }
            return Make("ToyShot" + seed, d);
        }

        /// <summary>Wet slap: short low thump, a burst of damp noise and a squelchy tail.</summary>
        private static AudioClip SplatClip(int seed, float pitch)
        {
            var rnd = new System.Random(seed * 131);
            int n = (int)(0.55f * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.32f;
                lp2 += (lp - lp2) * 0.25f;
                float slap = lp * Mathf.Exp(-t / 0.035f) * 1.1f;
                float thump = Mathf.Sin(2f * Mathf.PI * 75f * pitch * t) * Mathf.Exp(-t / 0.06f) * 0.9f;
                float sf = (260f + 140f * Mathf.Sin(t * 2f * Mathf.PI * 9f)) * pitch;
                phase += 2f * Mathf.PI * sf / Rate;
                float squelch = Mathf.Sin(phase) * lp2 * 3f * Mathf.Clamp01((t - 0.05f) / 0.04f) * Mathf.Exp(-(t - 0.05f) / 0.16f);
                d[i] = slap + thump + squelch * 0.8f;
            }
            return Make("ToySplat" + seed, d);
        }

        /// <summary>Light hollow plastic: a few clicks with short ringing modes.</summary>
        private static AudioClip ClatterClip(int seed)
        {
            var rnd = new System.Random(seed);
            int n = (int)(0.3f * Rate);
            var d = new float[n];
            float[] hits = { 0f, 0.06f + (float)rnd.NextDouble() * 0.04f, 0.15f + (float)rnd.NextDouble() * 0.05f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int h = 0; h < hits.Length; h++)
                {
                    float u = t - hits[h];
                    if (u < 0f) continue;
                    float a = 1f / (1 + h);
                    v += a * (Mathf.Sin(2f * Mathf.PI * 1250f * u) * 0.5f + Mathf.Sin(2f * Mathf.PI * 2730f * u) * 0.3f + Mathf.Sin(2f * Mathf.PI * 610f * u) * 0.4f) * Mathf.Exp(-u / 0.018f);
                }
                d[i] = v;
            }
            return Make("ToyClatter" + seed, d);
        }
    }
}
