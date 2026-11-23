using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Common start of a boombox track (constants; the logic is in <see cref="RadioChannel"/>). The server marks a new
    /// track Loading; every copy buffers it from 0:00 and sends "ready" (a named message, no change to NetPlayer). The
    /// server starts it when all connected copies are ready, or <see cref="RadioWaitAfterHost"/> s after the host itself
    /// was ready (slow copies then join at the current position), at most <see cref="RadioWaitMax"/> s after the track
    /// was chosen. The start is set <see cref="RadioLead"/> s ahead so the new state reaches everybody before the first sample.
    /// </summary>
    public partial class MatchManager
    {
        public const string RadioReadyMessage = "hns.radio.ready";
        public const float RadioLead = 0.45f, RadioWaitAfterHost = 6f, RadioWaitMax = 25f;

        /// <summary>This copy has buffered the start of lobby track <paramref name="seq"/> (any copy, main thread).</summary>
        public void ReportRadioReady(int seq) => LobbyRadio.ReportReady(seq);
    }
}
