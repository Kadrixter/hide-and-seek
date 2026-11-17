using System;
using System.IO;

namespace HideAndSeek.Audio.Editor
{
    /// <summary>Tiny deterministic synth for UI blips (no credits spent on clicks and ticks). 44.1 kHz mono 16-bit.</summary>
    internal static class UiSynth
    {
        public const int Rate = 44100;

        public static float[] Click()
        {
            var b = Buffer(0.045f);
            var rng = new Random(11);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.35f * (n - lp);
                float env = (float)Math.Exp(-t * 180f);
                float tone = (float)Math.Sin(2 * Math.PI * 2300 * t) * (float)Math.Exp(-t * 90f);
                b[i] = 0.55f * (n - lp) * env + 0.45f * tone;
            }
            return Finish(b, 0.8f);
        }

        public static float[] Hover()
        {
            var b = Buffer(0.06f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Math.Min(1f, t / 0.004f) * (float)Math.Exp(-t * 70f);
                b[i] = env * (float)Math.Sin(2 * Math.PI * (1500 + 400 * t / 0.06f) * t);
            }
            return Finish(b, 0.5f);
        }

        public static float[] Error()
        {
            var b = Buffer(0.32f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = t < 0.14f ? 233f : 185f;
                float local = t < 0.14f ? t : t - 0.16f;
                float env = local < 0 ? 0 : Math.Min(1f, local / 0.005f) * (float)Math.Exp(-local * 16f);
                double ph = 2 * Math.PI * f * t;
                float sq = (float)(Math.Sin(ph) + Math.Sin(3 * ph) / 3 + Math.Sin(5 * ph) / 5);
                b[i] = env * sq * 0.6f;
            }
            return Finish(b, 0.8f);
        }

        public static float[] Tick()
        {
            var b = Buffer(0.12f);
            var rng = new Random(5);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float body = (float)(Math.Sin(2 * Math.PI * 1150 * t) + 0.5 * Math.Sin(2 * Math.PI * 2650 * t)) * (float)Math.Exp(-t * 55f);
                float click = (float)(rng.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 900f);
                b[i] = 0.7f * body + 0.4f * click;
            }
            return Finish(b, 0.85f);
        }

        /// <summary>Two-note bell: rising for join, falling for leave.</summary>
        public static float[] TwoTone(bool rising)
        {
            var b = Buffer(0.5f);
            float f1 = rising ? 659.3f : 987.8f, f2 = rising ? 987.8f : 659.3f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float a = Note(t, f1, 0f);
                float c = Note(t, f2, 0.13f);
                b[i] = 0.5f * a + 0.5f * c;
            }
            return Finish(b, 0.7f);
        }

        private static float Note(float t, float f, float start)
        {
            float lt = t - start;
            if (lt < 0) return 0f;
            float env = Math.Min(1f, lt / 0.004f) * (float)Math.Exp(-lt * 9f);
            return env * (float)(Math.Sin(2 * Math.PI * f * lt) + 0.3 * Math.Sin(2 * Math.PI * 2 * f * lt) * Math.Exp(-lt * 20));
        }

        private static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        private static float[] Finish(float[] b, float peak)
        {
            float max = 1e-6f;
            foreach (var s in b) max = Math.Max(max, Math.Abs(s));
            float g = peak / max;
            int fade = Math.Min(b.Length / 4, (int)(0.01f * Rate));
            for (int i = 0; i < b.Length; i++)
            {
                float k = g;
                int fromEnd = b.Length - 1 - i;
                if (fromEnd < fade) k *= fromEnd / (float)fade;
                b[i] *= k;
            }
            return b;
        }

        public static byte[] EncodeWav(float[] samples)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataLen = samples.Length * 2;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(dataLen);
            foreach (var s in samples)
                w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, s)) * 32767f));
            w.Flush();
            return ms.ToArray();
        }
    }
}
