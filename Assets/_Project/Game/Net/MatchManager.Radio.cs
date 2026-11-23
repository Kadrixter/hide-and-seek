using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby boombox state (server-written). Position of the current track = Now - StartTime while playing.
    /// Loading: a new track is being buffered by every copy; StartTime is set once they are ready.
    /// </summary>
    public struct RadioState : INetworkSerializable, IEquatable<RadioState>
    {
        public int Index;        // current queue entry (-1 = none)
        public int Seq;          // bumps on every track change / restart
        public bool Playing;
        public bool Loading;     // waiting for the copies to buffer the start of the track
        public double StartTime; // server time at which the track position was 0
        public float PausedAt;   // position while paused
        public byte Volume;      // 0..100
        public byte Bass;        // 0..100
        public byte Ready, Peers; // loading: copies that reported ready / copies waited for

        public static RadioState Default => new RadioState { Index = -1, Volume = 60, Bass = 30 };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Index);
            s.SerializeValue(ref Seq);
            s.SerializeValue(ref Playing);
            s.SerializeValue(ref Loading);
            s.SerializeValue(ref StartTime);
            s.SerializeValue(ref PausedAt);
            s.SerializeValue(ref Volume);
            s.SerializeValue(ref Bass);
            s.SerializeValue(ref Ready);
            s.SerializeValue(ref Peers);
        }

        public bool Equals(RadioState o) => Index == o.Index && Seq == o.Seq && Playing == o.Playing && Loading == o.Loading &&
                                           StartTime == o.StartTime && PausedAt == o.PausedAt && Volume == o.Volume && Bass == o.Bass &&
                                           Ready == o.Ready && Peers == o.Peers;
    }

    public enum RadioOp : byte { Add = 0, Toggle = 1, Next = 2, VolUp = 3, VolDown = 4, BassUp = 5, BassDown = 6, Prev = 7 }

    /// <summary>
    /// Lobby boombox: a shared queue of YouTube links. Every copy streams the current track itself (yt-dlp + ffmpeg).
    /// The logic (queue ops, common start after every copy buffered the track, pausing outside the lobby) is in
    /// <see cref="RadioChannel"/>, shared with the radios found on the parking (<c>MatchManager.MapRadio.cs</c>);
    /// these members stay as the lobby boombox's API.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<FixedString128Bytes> RadioQueue;
        public readonly NetworkVariable<RadioState> Radio = new(RadioState.Default);
        public const int RadioMaxQueue = 30;

        /// <summary>The lobby boombox's channel (<see cref="Radio"/> + <see cref="RadioQueue"/>).</summary>
        public RadioChannel LobbyRadio { get; private set; }

        private void InitRadio()
        {
            RadioQueue = new NetworkList<FixedString128Bytes>();
            LobbyRadio = new RadioChannel(this, "", Radio, RadioQueue, RadioReadyMessage, () => Phase.Value == MatchPhase.Lobby, null,
                (me, op, url) => me.RequestRadioRpc(op, url));
            InitMapRadio();
        }

        /// <summary>Accepts youtube.com / youtu.be / music.youtube.com links only (they are passed to yt-dlp as data, never as options).</summary>
        public static bool IsYoutubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > 120) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u)) return false;
            if (u.Scheme != "https" && u.Scheme != "http") return false;
            string h = u.Host.ToLowerInvariant();
            return h == "youtu.be" || h == "youtube.com" || h.EndsWith(".youtube.com");
        }

        public void ServerRadio(NetPlayer pl, RadioOp op, string url) => LobbyRadio.ServerOp(pl, op, url);

        /// <summary>The host's own player finished (or failed) the track with <paramref name="seq"/>: next one.</summary>
        public void ServerRadioEnded(int seq) => LobbyRadio.ServerEnded(seq);

        /// <summary>Music only in the lobby: pause when a round starts; start a loaded track together. Also the map radios.</summary>
        private void UpdateRadioServer()
        {
            LobbyRadio.ServerUpdate();
            UpdateMapRadioServer();
        }
    }
}
