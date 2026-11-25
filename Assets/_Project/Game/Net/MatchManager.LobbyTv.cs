using Unity.Collections;
using Unity.Netcode;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby TV: a YouTube video everybody in the lobby watches together. The shared state is a boombox channel
    /// (<see cref="RadioChannel"/>: link, common start after every copy loaded it, pause, seek, volume) with the TV ops
    /// <see cref="RadioOp.Replace"/> / <see cref="RadioOp.Clear"/>; every copy downloads and plays the video itself
    /// (<c>LobbyTvVideo</c>). Only in the lobby: when a round starts the TV stops (back to its CCTV picture).
    /// </summary>
    public partial class MatchManager
    {
        public const string TvReadyMessage = "hns.tv.ready";

        public NetworkList<FixedString128Bytes> TvQueue;
        public readonly NetworkVariable<RadioState> TvVideo = new(RadioState.Default);

        /// <summary>The lobby TV's channel (<see cref="TvVideo"/> + <see cref="TvQueue"/>).</summary>
        public RadioChannel LobbyTv { get; private set; }

        private void InitLobbyTv()
        {
            TvQueue = new NetworkList<FixedString128Bytes>();
            LobbyTv = new RadioChannel(this, "tv", TvVideo, TvQueue, TvReadyMessage, () => Phase.Value == MatchPhase.Lobby, null,
                (me, op, url) => me.RequestTvRpc(op, url));
        }

        public void ServerTv(NetPlayer pl, RadioOp op, string url) => LobbyTv.ServerOp(pl, op, url);

        /// <summary>Tests: starts the round as the lever would (skips the "everybody ready" check).</summary>
        public void TestStartRound() => StartMatch();

        /// <summary>Server, every frame: common start of a loaded video; outside the lobby the TV is switched off.</summary>
        private void UpdateLobbyTvServer()
        {
            if (Phase.Value != MatchPhase.Lobby && LobbyTv.HasCurrent) LobbyTv.ServerStop();
            LobbyTv.ServerUpdate();
        }
    }
}
