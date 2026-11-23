using HideAndSeek.Game.Net;
using UnityEngine;

namespace HideAndSeek.Game.Voice
{
    /// <summary>
    /// Host option of the voice chat (round settings, <see cref="NetSettings.VoiceMode"/> / <see cref="NetSettings.VoiceDelayMs"/>):
    /// <list type="bullet">
    /// <item>classic (default): the pre-22.11 playback - streamed 16 kHz AudioClip, 40 ms frames; the delay setting is
    /// the start of the jitter buffer (it grows by 60 ms after a dropout and comes back down to setting - 60 ms);</item>
    /// <item>low latency: the same voice path (gain, soft clip, fades at 16 kHz, linear 16 kHz -> output like Unity's
    /// resampler) read straight on the audio thread instead of through Unity's ~0.5 s stream read-ahead, 20 ms frames;
    /// the delay setting is the buffer target (+20 ms after a real dropout, up to setting + 120 ms, back down when steady).</item>
    /// </list>
    /// Everybody plays and sends by the same setting; a change resets every voice stream (fade, swap, restart).
    /// </summary>
    public static class VoiceMode
    {
        public const byte Classic = 0, LowLatency = 1;
        public const int ClassicDefaultMs = 160, LowDefaultMs = 100;
        /// <summary>Delay steps offered in the round settings (ms).</summary>
        public static readonly int[] Steps = { 40, 60, 80, 100, 130, 160, 200, 250, 300, 400 };

        /// <summary>Tests: -voicelow [ms] on every copy = low latency regardless of the host setting (phone / PA tests).</summary>
        public static byte? Forced = ForcedFromArgs(out ForcedDelayMs);
        public static int ForcedDelayMs;

        private static byte? ForcedFromArgs(out int ms)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, "-voicelow");
            ms = 0;
            if (i < 0) return null;
            if (i + 1 < a.Length && int.TryParse(a[i + 1], out int v)) ms = v;
            return LowLatency;
        }

        private static NetSettings Current => MatchManager.Instance != null ? MatchManager.Instance.Settings.Value : NetSettings.Default;

        /// <summary>Mode of this session (classic when not connected).</summary>
        public static byte Mode => Forced ?? (Current.VoiceMode == LowLatency ? LowLatency : Classic);

        public static bool IsLow => Mode == LowLatency;

        /// <summary>Buffer delay of this session in ms (0 in the settings = the mode's default).</summary>
        public static int DelayMs
        {
            get
            {
                int ms = Forced.HasValue ? ForcedDelayMs : Current.VoiceDelayMs;
                return ClampDelay(ms, Mode);
            }
        }

        public static int ClampDelay(int ms, byte mode) => ms <= 0 ? DefaultFor(mode) : Mathf.Clamp(ms, Steps[0], Steps[Steps.Length - 1]);

        public static int DefaultFor(byte mode) => mode == LowLatency ? LowDefaultMs : ClassicDefaultMs;

        /// <summary>Samples per sent frame: 40 ms classic (as before 22.11), 20 ms low latency.</summary>
        public static int FrameSamples => IsLow ? VoiceCodec.FrameSamples / 2 : VoiceCodec.FrameSamples;

        /// <summary>Index of the step nearest to <paramref name="ms"/>.</summary>
        public static int StepIndex(int ms)
        {
            int best = 0;
            for (int i = 1; i < Steps.Length; i++)
                if (Mathf.Abs(Steps[i] - ms) < Mathf.Abs(Steps[best] - ms)) best = i;
            return best;
        }

        public static string ModeName(byte mode) => mode == LowLatency ? "niskie opóźnienie" : "klasyczny";

        /// <summary>What the delay means in the given mode (settings line).</summary>
        public static string DelayMeaning(byte mode) => mode == LowLatency ? "cel bufora" : "start bufora";

        /// <summary>Short line for the lobby board / logs, e.g. "klasyczny, bufor 160 ms".</summary>
        public static string Describe(byte mode, int ms) => $"{ModeName(mode)}, bufor {ClampDelay(ms, mode)} ms";
    }
}
