using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>G.711 mu-law: 16-bit PCM <-> 8-bit, good enough for speech at 16 kHz (16 kB/s per talker).</summary>
    public static class VoiceCodec
    {
        public const int SampleRate = 16000;
        public const int FrameSamples = 640; // 40 ms (as before 22.11)

        private const int Bias = 0x84, Clip = 32635;
        private static readonly float[] DecodeTable = BuildDecode();

        public static byte Encode(float sample)
        {
            int pcm = Mathf.Clamp((int)(sample * 32767f), -32768, 32767);
            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = -pcm;
            if (pcm > Clip) pcm = Clip;
            pcm += Bias;
            int exponent = 7;
            for (int mask = 0x4000; (pcm & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        public static float Decode(byte b) => DecodeTable[b];

        private static float[] BuildDecode()
        {
            var t = new float[256];
            for (int i = 0; i < 256; i++)
            {
                int u = ~i & 0xFF;
                int sign = u & 0x80, exponent = (u >> 4) & 0x07, mantissa = u & 0x0F;
                int pcm = ((mantissa << 3) + Bias) << exponent;
                pcm -= Bias;
                t[i] = (sign != 0 ? -pcm : pcm) / 32768f;
            }
            return t;
        }
    }
}
