using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised sounds of the thrown leg (made once at runtime, a few variants): the swish of the throw and the meaty
    /// slap of a leg hitting a head / body (a dull thump of the body, a wet smack and a short crackle). 3D through
    /// <see cref="GameAudio.PlayClipAt"/> (sfx volume, quiet on other floors).
    /// </summary>
    public static class LegSounds
    {
        private const int Rate = 44100;
        private static AudioClip[] _splat, _swish;

        /// <summary>Test counters.</summary>
        public static int Splats, Swishes;

        public static void Splat(Vector3 pos, float volume = 1f)
        {
            Splats++;
            _splat ??= new[] { SplatClip(1, 1f), SplatClip(2, 0.9f), SplatClip(3, 1.1f) };
            GameAudio.PlayClipAt(_splat[Random.Range(0, _splat.Length)], pos, 0.95f * volume, 30f);
        }

        public static void Swish(Vector3 pos, float power)
        {
            Swishes++;
            _swish ??= new[] { SwishClip(11, 0.34f), SwishClip(12, 0.3f) };
            GameAudio.PlayClipAt(_swish[Random.Range(0, _swish.Length)], pos, Mathf.Lerp(0.35f, 0.7f, power), 14f);
        }

        private static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.92f;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Meat on meat: a low body thump, a wet smack of filtered noise, a short sticky crackle.</summary>
        private static AudioClip SplatClip(int seed, float pitch)
        {
            var rnd = new System.Random(seed * 7919);
            int n = (int)(0.42f * Rate);
            var d = new float[n];
            float lp1 = 0f, lp2 = 0f, hp = 0f, prevN = 0f, phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                // Thump: a falling sine, struck hard.
                float f = Mathf.Lerp(95f, 48f, Mathf.Clamp01(t / 0.09f)) * pitch;
                phase += 2f * Mathf.PI * f / Rate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t / 0.07f) * Mathf.Clamp01(t / 0.002f);
                // Smack: noise through two low-pass poles (cut ~1.8 kHz, falling) - the wet slap.
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float a = Mathf.Lerp(0.35f, 0.08f, Mathf.Clamp01(t / 0.08f));
                lp1 += (nz - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                float smack = lp2 * 2.6f * Mathf.Exp(-t / 0.045f) * Mathf.Clamp01(t / 0.0015f);
                // Crackle: sparse sticky clicks after the hit (high-passed).
                hp = 0.82f * (hp + nz - prevN);
                prevN = nz;
                float click = t > 0.012f && t < 0.2f && rnd.NextDouble() < 0.004 ? 1f : 0f;
                float crackle = hp * click * 0.9f * Mathf.Exp(-(t - 0.012f) / 0.06f);
                d[i] = thump * 0.9f + smack + crackle + lp2 * 0.25f * Mathf.Exp(-t / 0.16f);
            }
            return Make("LegSplat" + seed, d);
        }

        /// <summary>A heavy limb swung through the air: band-passed noise swelling past the ear and dying away.</summary>
        private static AudioClip SwishClip(int seed, float length)
        {
            var rnd = new System.Random(seed * 104729);
            int n = (int)(length * Rate);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, x = t / length;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(x, 0.7f));
                env *= env;
                float nz = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float a = Mathf.Lerp(0.06f, 0.22f, env);
                lp += (nz - lp) * a;
                lp2 += (lp - lp2) * 0.02f;
                d[i] = (lp - lp2) * env;
            }
            return Make("LegSwish" + seed, d);
        }
    }
}
