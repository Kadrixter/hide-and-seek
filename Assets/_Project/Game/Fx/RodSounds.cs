using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised peg-leg sounds (made once at runtime, several variants): the rod planting on concrete (dull rubber
    /// foot + ringing square tube), the tip scraping along the floor, shoving the rod onto the stump and tearing duct
    /// tape. Played in 3D through <see cref="GameAudio.PlayClipAt"/> (sfx volume, silent on other floors).
    /// </summary>
    public static class RodSounds
    {
        private const int Rate = 44100;
        private static AudioClip[] _clank, _scrape, _tape;
        private static AudioClip _insert;

        /// <summary>How many rod steps / scrapes were played on this instance (tests).</summary>
        public static int Steps, Scrapes;

        public static void Step(Vector3 pos, float volume = 1f)
        {
            Steps++;
            _clank ??= new[] { Clank(1, 1f), Clank(2, 1.06f), Clank(3, 0.94f), Clank(4, 1.12f) };
            GameAudio.PlayClipAt(_clank[Random.Range(0, _clank.Length)], pos, 0.62f * volume, 22f);
        }

        public static void Scrape(Vector3 pos, float volume = 1f)
        {
            Scrapes++;
            _scrape ??= new[] { ScrapeClip(11, 0.22f), ScrapeClip(12, 0.3f), ScrapeClip(13, 0.26f) };
            GameAudio.PlayClipAt(_scrape[Random.Range(0, _scrape.Length)], pos, 0.42f * volume, 16f);
        }

        public static void Insert(Vector3 pos)
        {
            _insert ??= InsertClip();
            GameAudio.PlayClipAt(_insert, pos, 0.9f, 18f);
        }

        public static void Tape(Vector3 pos)
        {
            _tape ??= new[] { TapeClip(21, 0.34f), TapeClip(22, 0.42f) };
            GameAudio.PlayClipAt(_tape[Random.Range(0, _tape.Length)], pos, 0.6f, 12f);
        }

        // ---------------------------------------------------------------- synthesis

        private static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * 0.92f;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        /// <summary>Rod foot on concrete: rubber thud, short click and the inharmonic ring of a hollow steel tube.</summary>
        private static AudioClip Clank(int seed, float pitch)
        {
            var rnd = new System.Random(seed * 7919);
            int n = (int)(0.38f * Rate);
            var d = new float[n];
            float[] f = { 640f, 1745f, 3330f, 5180f };
            float[] a = { 0.42f, 0.3f, 0.18f, 0.1f };
            float[] tau = { 0.11f, 0.07f, 0.045f, 0.03f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.35f;
                float click = lp * Mathf.Exp(-t / 0.004f) * 0.9f;
                float thud = Mathf.Sin(2f * Mathf.PI * (95f * pitch) * t) * Mathf.Exp(-t / 0.035f) * 0.85f;
                float ring = 0f;
                for (int k = 0; k < f.Length; k++)
                    ring += a[k] * Mathf.Sin(2f * Mathf.PI * f[k] * pitch * t + k) * Mathf.Exp(-t / tau[k]);
                d[i] = click + thud + ring * 0.75f;
            }
            return Make("RodClank" + seed, d);
        }

        /// <summary>Tip dragged over rough concrete: grainy band-passed noise.</summary>
        private static AudioClip ScrapeClip(int seed, float length)
        {
            var rnd = new System.Random(seed);
            int n = (int)(length * Rate);
            var d = new float[n];
            float lp1 = 0f, lp2 = 0f, grain = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp1 += (w - lp1) * 0.45f;
                lp2 += (lp1 - lp2) * 0.12f;
                float band = lp1 - lp2;                           // ~1.5-5 kHz
                if (rnd.NextDouble() < 0.004) grain = 1f;         // catching on grit
                grain *= 0.996f;
                float env = Mathf.Clamp01(t / 0.025f) * Mathf.Clamp01((length - t) / (length * 0.6f));
                d[i] = band * env * (0.55f + 0.45f * grain + 0.2f * Mathf.Sin(2f * Mathf.PI * 37f * t));
            }
            return Make("RodScrape" + seed, d);
        }

        /// <summary>Rod shoved onto the stump: wet squelch and a dull knock.</summary>
        private static AudioClip InsertClip()
        {
            var rnd = new System.Random(31);
            int n = (int)(0.45f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * 0.06f;
                float squish = lp * 3f * Mathf.Exp(-t / 0.12f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (18f + 30f * t) * t));
                float knock = Mathf.Sin(2f * Mathf.PI * 150f * t) * Mathf.Exp(-t / 0.05f) + 0.35f * Mathf.Sin(2f * Mathf.PI * 820f * t) * Mathf.Exp(-t / 0.03f);
                d[i] = squish + knock * 0.6f;
            }
            return Make("RodInsert", d);
        }

        /// <summary>Duct tape pulled off the roll: crackling high noise.</summary>
        private static AudioClip TapeClip(int seed, float length)
        {
            var rnd = new System.Random(seed);
            int n = (int)(length * Rate);
            var d = new float[n];
            float prev = 0f, hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = (float)(rnd.NextDouble() * 2.0 - 1.0);
                hp = 0.85f * (hp + w - prev);
                prev = w;
                float crackle = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (42f + 25f * (float)rnd.NextDouble()) * t));
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((length - t) / 0.08f);
                d[i] = hp * crackle * env;
            }
            return Make("RodTape" + seed, d);
        }
    }
}
