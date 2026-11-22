using HideAndSeek.Audio;
using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Synthesised sounds of the big start lever (made once at runtime, deterministic): hands slapping on the bar,
    /// the creak of the weight going on, the clank when it unsticks, the bearings grinding while it travels, the
    /// heavy slam into the jaws with an electric arc, the locked rattle, the spring back, and the countdown ticks and
    /// the start hit (2D, heard by everybody in the lobby).
    /// </summary>
    public static class LeverSounds
    {
        const int Rate = 44100;
        static AudioClip _grab, _strain, _unstick, _slide, _latch, _jam, _release, _tick, _go;

        public static void Grab(Vector3 p) => GameAudio.PlayClipAt(_grab ??= Make("LeverGrab", GrabData()), p, 0.75f, 14f);
        public static void Strain(Vector3 p) => GameAudio.PlayClipAt(_strain ??= Make("LeverStrain", StrainData()), p, 0.55f, 14f);
        public static void Unstick(Vector3 p) => GameAudio.PlayClipAt(_unstick ??= Make("LeverUnstick", ClankData(1, 1f, 0.7f)), p, 0.85f, 22f);
        public static void Slide(Vector3 p) => GameAudio.PlayClipAt(_slide ??= Make("LeverSlide", SlideData()), p, 0.6f, 18f);
        public static void Latch(Vector3 p) => GameAudio.PlayClipAt(_latch ??= Make("LeverLatch", LatchData()), p, 1f, 40f);
        public static void Jam(Vector3 p) => GameAudio.PlayClipAt(_jam ??= Make("LeverJam", JamData()), p, 0.8f, 16f);
        public static void Release(Vector3 p) => GameAudio.PlayClipAt(_release ??= Make("LeverRelease", ReleaseData()), p, 0.8f, 20f);
        public static void Tick() => GameAudio.PlayClip2D(_tick ??= Make("LeverTick", TickData()), 0.55f, GameSfx.CountdownTick);
        public static void Go() => GameAudio.PlayClip2D(_go ??= Make("LeverGo", GoData()), 0.95f, GameSfx.RoundStart);

        /// <summary>
        /// Tests: mono mix (44.1 kHz) of the pull and countdown sounds as heard by the puller, starting
        /// <paramref name="lead"/> s before the pull, <paramref name="length"/> s long (for the recorded video).
        /// </summary>
        public static float[] MixPull(float lead, float length)
        {
            var mix = new float[(int)(length * Rate)];
            void Add(AudioClip c, float at, float vol)
            {
                var d = new float[c.samples];
                c.GetData(d, 0);
                int o = (int)((at + lead) * Rate);
                for (int i = 0; i < d.Length && o + i < mix.Length; i++) if (o + i >= 0) mix[o + i] += d[i] * vol;
            }
            Add(_grab ??= Make("LeverGrab", GrabData()), LeverTimeline.Grip, 0.75f);
            Add(_strain ??= Make("LeverStrain", StrainData()), LeverTimeline.Grip + 0.1f, 0.55f);
            Add(_unstick ??= Make("LeverUnstick", ClankData(1, 1f, 0.7f)), LeverTimeline.Unstick, 0.85f);
            Add(_slide ??= Make("LeverSlide", SlideData()), LeverTimeline.Unstick + 0.02f, 0.6f);
            Add(_latch ??= Make("LeverLatch", LatchData()), LeverTimeline.Latch, 1f);
            for (int k = 0; k < LeverTimeline.CountdownFrom; k++) Add(_tick ??= Make("LeverTick", TickData()), LeverTimeline.CountdownAt + k, 0.55f);
            Add(_go ??= Make("LeverGo", GoData()), LeverTimeline.GoAt, 0.95f);
            float peak = 0.001f;
            foreach (float v in mix) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.95f) for (int i = 0; i < mix.Length; i++) mix[i] *= 0.95f / peak;
            return mix;
        }

        /// <summary>16-bit mono WAV bytes.</summary>
        public static byte[] Wav(float[] samples)
        {
            var ms = new System.IO.MemoryStream();
            var w = new System.IO.BinaryWriter(ms);
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + samples.Length * 2);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(samples.Length * 2);
            foreach (float v in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
            w.Flush();
            return ms.ToArray();
        }

        // ---------------------------------------------------------------- helpers

        static AudioClip Make(string name, float[] d)
        {
            float peak = 0.001f;
            foreach (float v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
            int fade = Mathf.Min(d.Length / 4, Rate / 100);
            for (int i = 0; i < d.Length; i++)
            {
                float k = 0.92f / peak;
                int fromEnd = d.Length - 1 - i;
                if (fromEnd < fade) k *= fromEnd / (float)fade;
                d[i] *= k;
            }
            var clip = AudioClip.Create(name, d.Length, 1, Rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        static float[] Buf(float s) => new float[(int)(s * Rate)];
        static float Sin(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Env(float t, float start, float decay) => t < start ? 0f : Mathf.Exp(-(t - start) * decay);

        /// <summary>Inharmonic metal ring (partials with their own decay) starting at <paramref name="start"/>.</summary>
        static float Ring(float t, float start, float f0, float decay, float[] ratios)
        {
            if (t < start) return 0f;
            float lt = t - start, s = 0f;
            for (int k = 0; k < ratios.Length; k++)
                s += Sin(f0 * ratios[k], lt) * Mathf.Exp(-lt * decay * (1f + k * 0.6f)) / (1f + k * 0.5f);
            return s;
        }

        static readonly float[] Steel = { 1f, 2.76f, 5.4f, 8.93f };
        static readonly float[] Plate = { 1f, 1.59f, 2.14f, 2.3f, 3.6f };

        /// <summary>Two-pole resonator over a signal (band-pass like), in place.</summary>
        static void Resonate(float[] x, float f, float r)
        {
            float c = 2f * r * Mathf.Cos(2f * Mathf.PI * f / Rate), r2 = r * r, y1 = 0f, y2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float y = x[i] + c * y1 - r2 * y2;
                y2 = y1; y1 = y;
                x[i] = y;
            }
        }

        // ---------------------------------------------------------------- sounds

        static float[] GrabData()
        {
            var b = Buf(0.35f);
            var rng = new System.Random(3);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                b[i] = 0.9f * Sin(150f, t) * Env(t, 0f, 45f) + 0.5f * n * Env(t, 0f, 160f)
                     + 0.25f * Ring(t, 0f, 980f, 22f, Steel) + 0.2f * Ring(t, 0.035f, 1130f, 28f, Steel);
            }
            return b;
        }

        static float[] StrainData()
        {
            // Stick-slip creak: irregular impulses through a resonance, swelling as the weight goes on.
            var b = Buf(0.5f);
            var rng = new System.Random(7);
            float next = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                if (t >= next)
                {
                    b[i] = 1f;
                    next = t + 1f / (38f + 30f * t + (float)rng.NextDouble() * 12f);
                }
            }
            var low = (float[])b.Clone();
            Resonate(b, 720f, 0.995f);
            Resonate(low, 260f, 0.993f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.Clamp01((t - 0.42f) / 0.08f));
                b[i] = (b[i] * 0.6f + low[i] * 0.4f) * env;
            }
            return b;
        }

        static float[] ClankData(int seed, float pitch, float length)
        {
            var b = Buf(length);
            var rng = new System.Random(seed);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                b[i] = 0.8f * Sin(85f * pitch, t) * Env(t, 0f, 18f) + 0.7f * n * Env(t, 0f, 90f)
                     + 0.6f * Ring(t, 0.002f, 420f * pitch, 9f, Plate) + 0.3f * Ring(t, 0.006f, 1130f * pitch, 14f, Steel);
            }
            return b;
        }

        static float[] SlideData()
        {
            // Bearings and blades grinding: filtered noise with a rumble that speeds up, a rising whirr.
            var b = Buf(0.9f);
            var rng = new System.Random(19);
            float lp = 0f, ph = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.08f * (n - lp);
                float speed = t / 0.85f;
                ph += (25f + 45f * speed) / Rate;
                float rumble = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * ph);
                float whirr = Sin(140f + 160f * speed * speed, t);
                float env = Mathf.Clamp01(t / 0.12f) * (0.5f + 0.5f * speed) * (t > 0.84f ? Mathf.Exp(-(t - 0.84f) * 60f) : 1f);
                b[i] = env * (1.4f * lp * rumble + 0.18f * whirr + 0.08f * n);
            }
            return b;
        }

        static float[] LatchData()
        {
            // Slam into the jaws: deep hit, big metal ring, then the arc crackles and the 100 Hz buzz dies away.
            var b = Buf(1.5f);
            var rng = new System.Random(29);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.25f * (n - lp);
                float hit = 1.2f * Sin(52f, t) * Env(t, 0f, 7f) + 0.9f * n * Env(t, 0f, 55f);
                float ring = 0.7f * Ring(t, 0.003f, 310f, 4.5f, Plate) + 0.35f * Ring(t, 0.004f, 760f, 6f, Steel);
                float crackle = (rng.NextDouble() < 0.06 * Env(t, 0.01f, 9f) ? (float)(rng.NextDouble() * 2 - 1) : 0f) * 1.3f;
                float buzzPh = Mathf.Repeat(t * 100f, 1f);
                float buzz = (buzzPh < 0.5f ? 1f : -1f) * 0.18f * Env(t, 0.02f, 5f) * (0.6f + 0.4f * lp);
                b[i] = hit + ring + crackle + buzz + 0.25f * lp * Env(t, 0.01f, 12f);
            }
            return b;
        }

        static float[] JamData()
        {
            // Locked: the bar bangs against the bolt a few times.
            var b = Buf(0.6f);
            var rng = new System.Random(41);
            float[] hits = { 0f, 0.075f, 0.14f, 0.22f, 0.31f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1), s = 0f;
                for (int h = 0; h < hits.Length; h++)
                {
                    float g = 1f - h * 0.17f;
                    s += g * (0.6f * n * Env(t, hits[h], 160f) + 0.5f * Ring(t, hits[h], 1550f + h * 40f, 35f, Steel) + 0.35f * Sin(120f, t - hits[h]) * Env(t, hits[h], 40f));
                }
                b[i] = s;
            }
            return b;
        }

        static float[] ReleaseData()
        {
            // Springs back up: swish, then a lighter clank at the top.
            var b = Buf(1f);
            var clank = ClankData(51, 1.25f, 0.55f);
            var rng = new System.Random(53);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += 0.05f * ((float)(rng.NextDouble() * 2 - 1) - lp);
                b[i] = 1.6f * lp * Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                int c = i - (int)(0.42f * Rate);
                if (c >= 0 && c < clank.Length) b[i] += 0.8f * clank[c];
            }
            return b;
        }

        static float[] TickData()
        {
            // Relay click + short beep.
            var b = Buf(0.22f);
            var rng = new System.Random(61);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                float beep = (Sin(880f, t) + 0.3f * Sin(1760f, t)) * Mathf.Clamp01(t / 0.004f) * Env(t, 0f, 18f);
                b[i] = 0.7f * n * Env(t, 0f, 700f) + 0.6f * beep;
            }
            return b;
        }

        static float[] GoData()
        {
            // Start: deep boom, a bright buzzer chord and a metallic clang.
            var b = Buf(1.2f);
            var rng = new System.Random(71);
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                float chord = 0f;
                foreach (float f in new[] { 440f, 554.4f, 659.3f, 880f })
                {
                    float ph = Mathf.Repeat(t * f, 1f);
                    chord += (ph < 0.5f ? 1f : -1f) * 0.12f + Sin(f, t) * 0.12f;
                }
                float chordEnv = Mathf.Clamp01(t / 0.01f) * (t < 0.55f ? 1f : Mathf.Exp(-(t - 0.55f) * 9f));
                b[i] = 1.1f * Sin(48f, t) * Env(t, 0f, 4f) + 0.6f * n * Env(t, 0f, 40f) + chord * chordEnv * 0.9f + 0.35f * Ring(t, 0f, 620f, 5f, Plate);
            }
            return b;
        }
    }
}
