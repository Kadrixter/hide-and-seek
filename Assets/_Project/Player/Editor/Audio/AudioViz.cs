using System;
using System.IO;
using UnityEngine;

namespace HideAndSeek.Player.Editor.Audio
{
    /// <summary>WAV I/O (16-bit PCM mono).</summary>
    internal static class Wav
    {
        public static byte[] Encode(float[] samples, int sampleRate = Dsp.SampleRate)
        {
            int dataBytes = samples.Length * 2;
            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(sampleRate);
                w.Write(sampleRate * 2); // byte rate
                w.Write((short)2);       // block align
                w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767));
                w.Flush();
                return ms.ToArray();
            }
        }
    }

    /// <summary>Waveform + spectrogram rendering for preview PNGs (with a tiny pixel font for labels).</summary>
    internal static class AudioViz
    {
        public static void FFT(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len;
                float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1f, ci = 0f;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = i + k + len / 2;
                        float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        float ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        private static Color Magma(float v)
        {
            v = Mathf.Clamp01(v);
            Color[] stops =
            {
                new Color(0.0f, 0.0f, 0.02f), new Color(0.23f, 0.06f, 0.44f), new Color(0.55f, 0.16f, 0.5f),
                new Color(0.87f, 0.32f, 0.36f), new Color(0.99f, 0.65f, 0.36f), new Color(0.99f, 0.99f, 0.75f),
            };
            float p = v * (stops.Length - 1);
            int i = Mathf.Min((int)p, stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], p - i);
        }

        /// <summary>Draws waveform (top) + log-frequency spectrogram (bottom) of <paramref name="s"/> into a rect.</summary>
        public static void Draw(Color32[] px, int texW, int x0, int y0, int w, int h, float[] s, float seconds, float waveFrac = 0.3f)
        {
            int waveH = Mathf.RoundToInt(h * waveFrac);
            int specH = h - waveH - 1;
            int total = Mathf.Max(1, Dsp.Samples(seconds));
            // background
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(px, texW, x0 + x, y0 + y, y >= specH + 1 ? new Color(0.07f, 0.08f, 0.1f) : Color.black);

            // waveform: min/max per column (top area, y grows upward)
            float mid = y0 + specH + 1 + waveH * 0.5f;
            for (int x = 0; x < w; x++)
            {
                int a = (int)((long)x * total / w), b = (int)((long)(x + 1) * total / w);
                float mn = 0f, mx = 0f;
                for (int i = a; i < b && i < s.Length; i++) { mn = Mathf.Min(mn, s[i]); mx = Mathf.Max(mx, s[i]); }
                int ya = Mathf.RoundToInt(mid + mn * waveH * 0.48f), yb = Mathf.RoundToInt(mid + mx * waveH * 0.48f);
                for (int y = ya; y <= yb; y++) Set(px, texW, x0 + x, y, new Color(0.45f, 0.85f, 1f));
            }

            // spectrogram
            const int N = 1024;
            var re = new float[N];
            var im = new float[N];
            var win = new float[N];
            for (int i = 0; i < N; i++) win[i] = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * i / (N - 1));
            float fMin = 50f, fMax = 18000f;
            for (int x = 0; x < w; x++)
            {
                int center = (int)((long)(x + 0.5f) * total / w);
                for (int i = 0; i < N; i++)
                {
                    int idx = center - N / 2 + i;
                    re[i] = idx >= 0 && idx < s.Length ? s[idx] * win[i] : 0f;
                    im[i] = 0f;
                }
                FFT(re, im);
                for (int y = 0; y < specH; y++)
                {
                    float f = fMin * Mathf.Pow(fMax / fMin, (float)y / (specH - 1));
                    float fNext = fMin * Mathf.Pow(fMax / fMin, (float)(y + 1) / (specH - 1));
                    int k0 = Mathf.Clamp(Mathf.FloorToInt(f / Dsp.Fs * N), 1, N / 2 - 1);
                    int k1 = Mathf.Clamp(Mathf.CeilToInt(fNext / Dsp.Fs * N), k0, N / 2 - 1);
                    float m = 0f;
                    for (int k = k0; k <= k1; k++) m = Mathf.Max(m, re[k] * re[k] + im[k] * im[k]);
                    float db = 10f * Mathf.Log10(m + 1e-12f) - 20f * Mathf.Log10(N * 0.25f);
                    Set(px, texW, x0 + x, y0 + y, Magma((db + 90f) / 75f));
                }
            }
        }

        public static void Set(Color32[] px, int w, int x, int y, Color c)
        {
            int h = px.Length / w;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        // 3x5 pixel font, rows top->bottom, '1' = pixel.
        private static readonly string Glyphs =
            "A:010101111101101|B:110101110101110|C:011100100100011|D:110101101101110|E:111100110100111|F:111100110100100|" +
            "G:011100101101011|H:101101111101101|I:111010010010111|J:001001001101010|K:101101110101101|L:100100100100111|" +
            "M:101111111101101|N:110101101101101|O:010101101101010|P:110101110100100|Q:010101101110011|R:110101110101101|" +
            "S:011100010001110|T:111010010010010|U:101101101101111|V:101101101101010|W:101101111111101|X:101101010101101|" +
            "Y:101101010010010|Z:111001010100111|0:111101101101111|1:010110010010111|2:110001010100111|3:110001010001110|" +
            "4:101101111001001|5:111100110001110|6:011100111101111|7:111001010010010|8:111101111101111|9:111101111001110|" +
            "-:000000111000000|.:000000000000010|/:001001010100100|(:010100100100010|):010001001001010|+:000010111010000|" +
            " :000000000000000";

        public static void Text(Color32[] px, int texW, int x, int yTop, string text, Color c, int scale = 2)
        {
            foreach (char ch0 in text.ToUpperInvariant())
            {
                int i = Glyphs.IndexOf(ch0 + ":", StringComparison.Ordinal);
                if (i >= 0)
                {
                    string bits = Glyphs.Substring(i + 2, 15);
                    for (int r = 0; r < 5; r++)
                    for (int col = 0; col < 3; col++)
                        if (bits[r * 3 + col] == '1')
                            for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                Set(px, texW, x + col * scale + sx, yTop - r * scale - sy, c);
                }
                x += 4 * scale;
            }
        }
    }
}
