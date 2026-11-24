using HideAndSeek.Audio;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised sounds of the fuses and fuse boxes (made once at runtime): a fuse sliding into its socket and the
    /// click when it seats, the tugs and the pop when one is pulled out, the crackle of sparks, the hum of a working box
    /// (loop), the big contactor thump with the mains hum and the tubes striking when a level's lights come on, and the
    /// thump with the hum dying when they go off. 3D through <see cref="GameAudio.PlayClipAt"/>.
    /// </summary>
    public static class FuseSounds
    {
        private const int Rate = 44100;
        private static AudioClip _slide, _click, _tug, _pop, _spark, _hum, _on, _off;

        /// <summary>Played on this peer (tests).</summary>
        public static int Played;

        public static void Slide(Vector3 p) => Play(_slide ??= SlideClip(), p, 0.5f, 10f);
        public static void Click(Vector3 p) => Play(_click ??= ClickClip(), p, 0.9f, 16f);
        public static void Tug(Vector3 p) => Play(_tug ??= TugClip(), p, 0.45f, 10f);
        public static void Pop(Vector3 p) => Play(_pop ??= PopClip(), p, 0.8f, 14f);
        public static void Spark(Vector3 p) => Play(_spark ??= SparkClip(), p, 0.9f, 22f);
        public static void PowerOn(Vector3 p, float volume, float range) => Play(_on ??= OnClip(), p, volume, range);
        public static void PowerOff(Vector3 p, float volume, float range) => Play(_off ??= OffClip(), p, volume, range);
        public static AudioClip Hum => _hum ??= HumClip();
        public static AudioClip PowerOnClip => _on ??= OnClip();

        private static void Play(AudioClip c, Vector3 p, float vol, float range)
        {
            Played++;
            GameAudio.PlayClipAt(c, p, vol, range);
        }

        public static AudioClip[] AllClips() => new[] { _slide ??= SlideClip(), _click ??= ClickClip(), _tug ??= TugClip(), _pop ??= PopClip(), _spark ??= SparkClip(), Hum, _on ??= OnClip(), _off ??= OffClip() };

        // ---------------------------------------------------------------- synthesis

        private static AudioClip Make(string name, float[] d, float level = 0.9f)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < d.Length; i++) d[i] = d[i] / peak * level;
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        private static float Noise(System.Random r) => (float)r.NextDouble() * 2f - 1f;

        /// <summary>Short hard knock: a few damped partials (bakelite / steel).</summary>
        private static void Knock(float[] d, int at, float f, float amp, float tau)
        {
            float[] ratio = { 1f, 2.3f, 3.9f, 6.1f };
            for (int i = at; i < d.Length && i < at + (int)(tau * 6f * Rate); i++)
            {
                float t = (i - at) / (float)Rate;
                float s = 0f;
                for (int k = 0; k < ratio.Length; k++) s += Mathf.Sin(2f * Mathf.PI * f * ratio[k] * t + k) * Mathf.Exp(-t / (tau / (1f + k * 0.7f))) / (1f + k);
                d[i] += amp * s;
            }
        }

        private static AudioClip SlideClip()
        {
            var r = new System.Random(11);
            int n = (int)(0.28f * Rate);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (Noise(r) - lp) * 0.25f;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.28f) * Mathf.PI) * (0.7f + 0.3f * Mathf.Sin(t * 180f));
                d[i] = lp * env * 0.6f;
            }
            return Make("FuseSlide", d, 0.6f);
        }

        private static AudioClip ClickClip()
        {
            var r = new System.Random(12);
            int n = (int)(0.25f * Rate);
            var d = new float[n];
            Knock(d, 0, 1900f, 1f, 0.006f);
            Knock(d, (int)(0.012f * Rate), 3400f, 0.6f, 0.004f);
            Knock(d, (int)(0.03f * Rate), 420f, 0.5f, 0.02f); // the whole box answers
            for (int i = 0; i < (int)(0.01f * Rate); i++) d[i] += Noise(r) * (1f - i / (0.01f * Rate)) * 0.8f;
            return Make("FuseClick", d);
        }

        private static AudioClip TugClip()
        {
            var r = new System.Random(13);
            int n = (int)(0.22f * Rate);
            var d = new float[n];
            for (int k = 0; k < 5; k++) Knock(d, (int)((0.02f + k * 0.035f + (float)r.NextDouble() * 0.01f) * Rate), 1200f + (float)r.NextDouble() * 900f, 0.4f, 0.004f);
            float lp = 0f;
            for (int i = 0; i < n; i++) { lp += (Noise(r) - lp) * 0.1f; d[i] += lp * 0.25f * Mathf.Sin(i / (float)n * Mathf.PI); }
            return Make("FuseTug", d, 0.6f);
        }

        private static AudioClip PopClip()
        {
            var r = new System.Random(14);
            int n = (int)(0.3f * Rate);
            var d = new float[n];
            for (int i = 0; i < (int)(0.02f * Rate); i++) d[i] += Noise(r) * Mathf.Exp(-i / (0.004f * Rate));
            Knock(d, 0, 900f, 0.9f, 0.01f);
            Knock(d, (int)(0.004f * Rate), 2600f, 0.5f, 0.006f);
            return Make("FusePop", d);
        }

        private static AudioClip SparkClip()
        {
            var r = new System.Random(15);
            int n = (int)(0.6f * Rate);
            var d = new float[n];
            for (int k = 0; k < 60; k++)
            {
                int at = (int)(Mathf.Pow((float)r.NextDouble(), 2.2f) * 0.5f * Rate);
                float a = 0.3f + (float)r.NextDouble() * 0.7f;
                int len = (int)((0.001f + (float)r.NextDouble() * 0.004f) * Rate);
                for (int i = at; i < Mathf.Min(n, at + len); i++) d[i] += Noise(r) * a * (1f - (i - at) / (float)len);
            }
            for (int i = 0; i < n; i++) d[i] += Mathf.Sin(2f * Mathf.PI * 100f * i / Rate) * 0.25f * Mathf.Exp(-i / (0.15f * Rate)); // the arc's buzz
            return Make("FuseSpark", d);
        }

        /// <summary>Mains hum of a working box (loops: whole cycles of 50 Hz).</summary>
        private static AudioClip HumClip()
        {
            var r = new System.Random(16);
            int n = Rate * 2;
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float s = 0.5f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * 150f * t) + 0.1f * Mathf.Sin(2f * Mathf.PI * 300f * t);
                s += Noise(r) * 0.03f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 100f * t));
                d[i] = s;
            }
            return Make("FuseHum", d, 0.7f);
        }

        /// <summary>Lights on: contactor thump, the mains hum rising, tubes striking one after another (ticks and pings).</summary>
        private static AudioClip OnClip()
        {
            var r = new System.Random(17);
            int n = (int)(2.6f * Rate);
            var d = new float[n];
            Knock(d, 0, 95f, 1.6f, 0.09f);
            Knock(d, (int)(0.01f * Rate), 640f, 0.8f, 0.03f);
            for (int i = 0; i < (int)(0.05f * Rate); i++) d[i] += Noise(r) * 0.9f * Mathf.Exp(-i / (0.012f * Rate));
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01((t - 0.05f) / 0.5f) * Mathf.Clamp01((2.6f - t) / 0.8f);
                d[i] += env * 0.35f * (Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 200f * t + 1f));
            }
            for (int k = 0; k < 22; k++)
            {
                int at = (int)((0.25f + k * 0.07f + (float)r.NextDouble() * 0.05f) * Rate);
                Knock(d, at, 2200f + (float)r.NextDouble() * 1500f, 0.35f, 0.012f);
            }
            return Make("FusePowerOn", d);
        }

        private static AudioClip OffClip()
        {
            int n = (int)(1.4f * Rate);
            var d = new float[n];
            Knock(d, 0, 85f, 1.4f, 0.08f);
            Knock(d, (int)(0.006f * Rate), 540f, 0.6f, 0.025f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(100f, 40f, t / 1.4f);
                d[i] += 0.3f * Mathf.Exp(-t / 0.45f) * Mathf.Sin(2f * Mathf.PI * f * t);
            }
            return Make("FusePowerOff", d);
        }
    }
}
