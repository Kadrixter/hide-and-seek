using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HideAndSeek.Audio.Editor
{
    /// <summary>
    /// Synthesised replacements for two generated ambient loops that did not fit an underground garage:
    /// <list type="bullet">
    /// <item>GarageBed: was a gusty wind rumble, then a steady pink-noise rumble below ~1 kHz that still sounded like
    /// wind. Now only what is left without that rumble: a faint far-away fan hum and a breath of high air, at the same
    /// level as in the old mix (the rumble is still generated, only to keep the normalisation gain, and then dropped).
    /// Stereo, 20 s, seamless loop. Air noise of the garage comes from the 3D Ventilation emitters.</item>
    /// <item>LampBuzz: was a 6-20 kHz crackle that sounded like crickets / worn brake pads. Now a real fluorescent
    /// ballast hum: 100 Hz (50 Hz mains, twice per cycle) with a few soft harmonics and a hint of buzz. Mono, 8 s.</item>
    /// </list>
    /// Batch: -executeMethod HideAndSeek.Audio.Editor.AmbientSynth.RegenerateFromCommandLine (writes the WAVs, keeps their
    /// .meta / GUID, then rebuilds the game audio library).
    /// </summary>
    public static class AmbientSynth
    {
        private const int Rate = 44100;

        [MenuItem("HideAndSeek/Audio/Regenerate Garage Room Tone + Lamp Hum")]
        public static void Regenerate()
        {
            WriteStereo(GameAudioBuilder.AmbientRoot + "/GarageBed.wav", RoomTone(20f));
            WriteMono(GameAudioBuilder.AmbientRoot + "/LampBuzz.wav", LampHum(8f));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GameAudioBuilder.Build();
        }

        public static void RegenerateFromCommandLine()
        {
            try
            {
                Regenerate();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[AmbientSynth] FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        // ================================================================ garage room tone

        private static float[][] RoomTone(float seconds)
        {
            int fade = 2 * Rate;
            int n = (int)(seconds * Rate) + fade; // generated past the end, folded back by Loop
            var rumble = new[] { new float[n], new float[n] }; // the old wind-like body: only sets the level, not written
            var ch = new[] { new float[n], new float[n] };
            var rng = new System.Random(3401);
            for (int c = 0; c < 2; c++)
            {
                var pink = new Pink();
                var lp1 = Lp(950f); var lp2 = Lp(950f); var hp = Hp(55f);
                var air = Hp(2500f); var airLp = Lp(6000f);
                for (int i = 0; i < n; i++)
                {
                    float w = (float)(rng.NextDouble() * 2 - 1);
                    rumble[c][i] = 0.9f * hp.Tick(lp2.Tick(lp1.Tick(pink.Tick(w))));
                    ch[c][i] = 0.04f * airLp.Tick(air.Tick(w));
                }
            }
            // far fan hum, the same in both ears, slowly breathing in level
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                float wob = 1f + 0.25f * (float)Math.Sin(2 * Math.PI * 0.1 * t) + 0.1f * (float)Math.Sin(2 * Math.PI * 0.37 * t + 1.3);
                float hum = (float)(Math.Sin(2 * Math.PI * 50 * t) * 0.5 + Math.Sin(2 * Math.PI * 100 * t + 0.4) * 0.35 + Math.Sin(2 * Math.PI * 150 * t + 1.1) * 0.12);
                float slow = 1f + 0.08f * (float)Math.Sin(2 * Math.PI * 0.05 * t); // +-0.7 dB swell, never gusty
                for (int c = 0; c < 2; c++)
                {
                    ch[c][i] = (ch[c][i] + 0.035f * wob * hum) * slow;
                    rumble[c][i] *= slow;
                }
            }
            for (int c = 0; c < 2; c++)
            {
                ch[c] = Loop(ch[c], fade, true);
                rumble[c] = Loop(rumble[c], fade, true);
            }
            // gain of the old full mix (rumble + air + hum at -27 dB RMS), so hum and air keep their old level
            var full = new float[2][];
            for (int c = 0; c < 2; c++)
            {
                full[c] = new float[ch[c].Length];
                for (int i = 0; i < full[c].Length; i++) full[c][i] = ch[c][i] + rumble[c][i];
            }
            float g = NormalizeGain(full, -27f);
            foreach (var b in ch) for (int i = 0; i < b.Length; i++) b[i] = Mathf.Clamp(b[i] * g, -0.98f, 0.98f);
            return ch;
        }

        // ================================================================ fluorescent lamp hum

        private static float[] LampHum(float seconds)
        {
            int fade = Rate / 2;
            int n = (int)(seconds * Rate) + fade;
            var b = new float[n];
            var rng = new System.Random(77);
            var buzzBp = Bp(1200f, 1.2f);
            float[] amp = { 1f, 0.42f, 0.3f, 0.16f, 0.1f, 0.06f, 0.035f };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)Rate;
                double ph = 2 * Math.PI * 100 * t;
                float x = 0f;
                for (int h = 0; h < amp.Length; h++) x += amp[h] * (float)Math.Sin((h + 1) * ph + h * 0.7);
                // magnetostriction buzz: a little noise gated by the 100 Hz cycle
                float gate = (float)Math.Pow(Math.Abs(Math.Sin(ph * 0.5)), 6);
                float buzz = buzzBp.Tick((float)(rng.NextDouble() * 2 - 1)) * gate;
                float wob = 1f + 0.06f * (float)Math.Sin(2 * Math.PI * 0.25 * t); // 8 s = 2 whole periods
                b[i] = (0.8f * x + 0.5f * buzz) * wob;
            }
            b = Loop(b, fade, false);
            Normalize(new[] { b }, -28f);
            return b;
        }

        // ================================================================ helpers

        /// <summary>
        /// Seamless loop: <paramref name="b"/> was generated <paramref name="fade"/> samples past the wanted end; that
        /// overhang is crossfaded into the head, so the last sample flows straight into the first.
        /// Equal-power for noise, linear for in-phase tones.
        /// </summary>
        private static float[] Loop(float[] b, int fade, bool equalPower)
        {
            int n = b.Length - fade;
            var r = new float[n];
            Array.Copy(b, r, n);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                float a = equalPower ? Mathf.Sqrt(k) : k, c = equalPower ? Mathf.Sqrt(1f - k) : 1f - k;
                r[i] = b[i] * a + b[n + i] * c;
            }
            return r;
        }

        private static void Normalize(float[][] chans, float rmsDb)
        {
            float g = NormalizeGain(chans, rmsDb);
            foreach (var b in chans) for (int i = 0; i < b.Length; i++) b[i] = Mathf.Clamp(b[i] * g, -0.98f, 0.98f);
        }

        /// <summary>Gain that brings <paramref name="chans"/> to <paramref name="rmsDb"/> dBFS RMS.</summary>
        private static float NormalizeGain(float[][] chans, float rmsDb)
        {
            double sum = 0; long count = 0;
            foreach (var b in chans) foreach (var s in b) { sum += s * s; count++; }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, count));
            return Mathf.Pow(10f, rmsDb / 20f) / Mathf.Max(1e-6f, rms);
        }

        private static void WriteMono(string assetPath, float[] s) => WriteStereo(assetPath, new[] { s });

        private static void WriteStereo(string assetPath, float[][] chans)
        {
            int channels = chans.Length, n = chans[0].Length;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            int dataLen = n * channels * 2;
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            w.Write(36 + dataLen);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(Rate); w.Write(Rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            w.Write(dataLen);
            for (int i = 0; i < n; i++)
                for (int c = 0; c < channels; c++)
                    w.Write((short)Math.Round(Mathf.Clamp(chans[c][i], -1f, 1f) * 32767f));
            w.Flush();
            File.WriteAllBytes(Path.GetFullPath(assetPath), ms.ToArray());
            Debug.Log($"[AmbientSynth] wrote {assetPath} ({n / (float)Rate:0.0} s, {channels} ch)");
        }

        /// <summary>Paul Kellet's pink noise filter.</summary>
        private sealed class Pink
        {
            private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;
            public float Tick(float w)
            {
                _b0 = 0.99886f * _b0 + w * 0.0555179f; _b1 = 0.99332f * _b1 + w * 0.0750759f;
                _b2 = 0.969f * _b2 + w * 0.153852f; _b3 = 0.8665f * _b3 + w * 0.3104856f;
                _b4 = 0.55f * _b4 + w * 0.5329522f; _b5 = -0.7616f * _b5 - w * 0.016898f;
                float y = _b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + w * 0.5362f;
                _b6 = w * 0.115926f;
                return y * 0.11f;
            }
        }

        private sealed class Bq
        {
            private float _b0, _b1, _b2, _a1, _a2, _z1, _z2;
            public Bq(double b0, double b1, double b2, double a0, double a1, double a2)
            { _b0 = (float)(b0 / a0); _b1 = (float)(b1 / a0); _b2 = (float)(b2 / a0); _a1 = (float)(a1 / a0); _a2 = (float)(a2 / a0); }
            public float Tick(float x)
            {
                float y = _b0 * x + _z1;
                _z1 = _b1 * x - _a1 * y + _z2;
                _z2 = _b2 * x - _a2 * y;
                return y;
            }
        }

        private static void Prep(float f, float q, out double c, out double a)
        {
            double w0 = 2 * Math.PI * f / Rate;
            c = Math.Cos(w0);
            a = Math.Sin(w0) / (2 * q);
        }

        private static Bq Lp(float f, float q = 0.7071f) { Prep(f, q, out var c, out var a); return new Bq((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a); }
        private static Bq Hp(float f, float q = 0.7071f) { Prep(f, q, out var c, out var a); return new Bq((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a); }
        private static Bq Bp(float f, float q) { Prep(f, q, out var c, out var a); return new Bq(a, 0, -a, 1 + a, -2 * c, 1 - a); }
    }
}
