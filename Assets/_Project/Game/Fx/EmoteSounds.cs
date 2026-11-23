using HideAndSeek.Audio;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Short synthesised contact sounds of the emotes (no assets): a clap (palms: bright noise snap with a hollow body),
    /// a high-five slap (louder, wetter), a fist bump (dull knock), a pat on the back (soft thump through cloth), a grip
    /// (hands grabbing, rustle), the robot's servo stop, a bottom landing on the floor. Each kind has 3 variants
    /// (fixed seeds) picked at random so a row of claps does not machine-gun. Played 3D at the contact.
    /// </summary>
    public static class EmoteSounds
    {
        private const int Rate = 44100;
        private static AudioClip[][] _clips;

        public static void Play(EmoteBeat kind, Vector3 at, bool own)
        {
            _clips ??= new AudioClip[8][];
            int k = (int)kind;
            if (k < 0 || k >= _clips.Length) return;
            _clips[k] ??= new[] { Make(kind, 11 + k * 7), Make(kind, 12 + k * 7), Make(kind, 13 + k * 7) };
            var clip = _clips[k][Random.Range(0, 3)];
            float vol = kind switch
            {
                EmoteBeat.Slap => 0.95f,
                EmoteBeat.Clap => 0.7f,
                EmoteBeat.Snap => 0.35f,
                EmoteBeat.Pat => 0.6f,
                _ => 0.55f,
            };
            GameAudio.PlayClipAt(clip, at, vol * (own ? 0.85f : 1f), kind == EmoteBeat.Slap || kind == EmoteBeat.Clap ? 26f : 14f);
        }

        private static AudioClip Make(EmoteBeat kind, int seed)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            var d = new float[(int)(Rate * 0.35f)];
            switch (kind)
            {
                case EmoteBeat.Clap:
                    Noise(d, rng, 0f, 0.09f, 0.9f, 700f, 6500f, 0.012f + 0.004f * R());
                    Tone(d, 0f, 900f + 300f * R(), 0.05f, 0.2f, 0.008f);
                    Noise(d, rng, 0.003f, 0.12f, 0.18f, 300f, 2500f, 0.03f);
                    break;
                case EmoteBeat.Slap:
                    Noise(d, rng, 0f, 0.12f, 1f, 500f, 7500f, 0.016f + 0.004f * R());
                    Tone(d, 0f, 600f + 150f * R(), 0.08f, 0.32f, 0.012f);
                    Noise(d, rng, 0.004f, 0.25f, 0.22f, 250f, 3000f, 0.06f);
                    break;
                case EmoteBeat.Bump:
                    Tone(d, 0f, 170f + 30f * R(), 0.12f, 0.7f, 0.025f);
                    Noise(d, rng, 0f, 0.05f, 0.35f, 300f, 2200f, 0.008f);
                    break;
                case EmoteBeat.Pat:
                    Tone(d, 0f, 120f + 25f * R(), 0.12f, 0.6f, 0.03f);
                    Noise(d, rng, 0f, 0.1f, 0.35f, 200f, 1600f, 0.02f);
                    break;
                case EmoteBeat.Grip:
                    Noise(d, rng, 0f, 0.12f, 0.45f, 400f, 3500f, 0.03f);
                    Tone(d, 0f, 230f, 0.06f, 0.25f, 0.015f);
                    break;
                case EmoteBeat.Snap:
                    Tone(d, 0f, 1800f + 400f * R(), 0.05f, 0.25f, 0.006f);
                    Noise(d, rng, 0f, 0.04f, 0.25f, 2500f, 9000f, 0.004f);
                    break;
                case EmoteBeat.Sit:
                case EmoteBeat.Stomp:
                    Tone(d, 0f, 90f, 0.2f, 0.8f, 0.05f);
                    Noise(d, rng, 0f, 0.15f, 0.4f, 150f, 1500f, 0.04f);
                    break;
            }
            return Finish("emote-" + kind + "-" + seed, d);
        }

        private static void Tone(float[] d, float at, float freq, float len, float amp, float decay)
        {
            int s = (int)(at * Rate), e = Mathf.Min(d.Length, s + (int)(len * Rate));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)Rate;
                d[i] += amp * Mathf.Exp(-t / decay) * Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Min(1f, t / 0.001f);
            }
        }

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
            float g = peak > 0.9f ? 0.9f / peak : 1f;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }
    }
}
