using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Lobby boombox state (server-written). Position of the current track = Now - StartTime while playing.</summary>
    public struct RadioState : INetworkSerializable, IEquatable<RadioState>
    {
        public int Index;        // current queue entry (-1 = none)
        public int Seq;          // bumps on every track change / restart
        public bool Playing;
        public double StartTime; // server time at which the track position was 0
        public float PausedAt;   // position while paused
        public byte Volume;      // 0..100
        public byte Bass;        // 0..100

        public static RadioState Default => new RadioState { Index = -1, Volume = 60, Bass = 30 };

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Index);
            s.SerializeValue(ref Seq);
            s.SerializeValue(ref Playing);
            s.SerializeValue(ref StartTime);
            s.SerializeValue(ref PausedAt);
            s.SerializeValue(ref Volume);
            s.SerializeValue(ref Bass);
        }

        public bool Equals(RadioState o) => Index == o.Index && Seq == o.Seq && Playing == o.Playing && StartTime == o.StartTime &&
                                           PausedAt == o.PausedAt && Volume == o.Volume && Bass == o.Bass;
    }

    public enum RadioOp : byte { Add = 0, Toggle = 1, Next = 2, VolUp = 3, VolDown = 4, BassUp = 5, BassDown = 6, Prev = 7 }

    /// <summary>
    /// Lobby boombox: a shared queue of YouTube links. Every peer streams the current track itself (yt-dlp + ffmpeg)
    /// at the position given by the server clock, so everybody hears the same music in sync.
    /// </summary>
    public partial class MatchManager
    {
        public NetworkList<FixedString128Bytes> RadioQueue;
        public readonly NetworkVariable<RadioState> Radio = new(RadioState.Default);
        public const int RadioMaxQueue = 30;

        private void InitRadio() => RadioQueue = new NetworkList<FixedString128Bytes>();

        /// <summary>Accepts youtube.com / youtu.be / music.youtube.com links only (they are passed to yt-dlp as data, never as options).</summary>
        public static bool IsYoutubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > 120) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u)) return false;
            if (u.Scheme != "https" && u.Scheme != "http") return false;
            string h = u.Host.ToLowerInvariant();
            return h == "youtu.be" || h == "youtube.com" || h.EndsWith(".youtube.com");
        }

        private float RadioPosition(in RadioState r) => r.Playing ? (float)(Now - r.StartTime) : r.PausedAt;

        public void ServerRadio(NetPlayer pl, RadioOp op, string url)
        {
            if (pl == null) return;
            Debug.Log($"[Radio] {pl.Nick}: {op} {url}");
            var r = Radio.Value;
            switch (op)
            {
                case RadioOp.Add:
                    if (!IsYoutubeUrl(url) || RadioQueue.Count >= RadioMaxQueue) return;
                    RadioQueue.Add(new FixedString128Bytes(url.Trim()));
                    if (!r.Playing && (r.Index < 0 || r.Index >= RadioQueue.Count - 1 && r.PausedAt <= 0f))
                    {
                        r.Index = RadioQueue.Count - 1;
                        r.Seq++;
                        r.Playing = Phase.Value == MatchPhase.Lobby;
                        r.PausedAt = 0f;
                        r.StartTime = Now;
                    }
                    pl.ToastRpc("Dodano do kolejki radia.", 2f);
                    break;
                case RadioOp.Toggle:
                    if (r.Index < 0 || r.Index >= RadioQueue.Count) return;
                    if (r.Playing)
                    {
                        r.PausedAt = Mathf.Max(0f, RadioPosition(r));
                        r.Playing = false;
                    }
                    else if (Phase.Value == MatchPhase.Lobby)
                    {
                        r.StartTime = Now - r.PausedAt;
                        r.Playing = true;
                    }
                    break;
                case RadioOp.Next:
                case RadioOp.Prev:
                {
                    int to = r.Index + (op == RadioOp.Next ? 1 : -1);
                    if (to < 0 || to >= RadioQueue.Count) { if (op == RadioOp.Next) { r.Playing = false; r.PausedAt = 0f; r.Index = RadioQueue.Count; r.Seq++; } break; }
                    r.Index = to;
                    r.Seq++;
                    r.PausedAt = 0f;
                    r.StartTime = Now;
                    r.Playing = Phase.Value == MatchPhase.Lobby;
                    break;
                }
                case RadioOp.VolUp: r.Volume = (byte)Mathf.Min(100, r.Volume + 10); break;
                case RadioOp.VolDown: r.Volume = (byte)Mathf.Max(0, r.Volume - 10); break;
                case RadioOp.BassUp: r.Bass = (byte)Mathf.Min(100, r.Bass + 10); break;
                case RadioOp.BassDown: r.Bass = (byte)Mathf.Max(0, r.Bass - 10); break;
            }
            Radio.Value = r;
        }

        /// <summary>The host's own player finished (or failed) the track with <paramref name="seq"/>: next one.</summary>
        public void ServerRadioEnded(int seq)
        {
            if (!IsServer) return;
            var r = Radio.Value;
            if (r.Seq != seq || !r.Playing) return;
            r.Index++;
            r.Seq++;
            r.PausedAt = 0f;
            r.StartTime = Now;
            r.Playing = r.Index < RadioQueue.Count;
            Radio.Value = r;
        }

        /// <summary>Music only in the lobby: pause when a round starts.</summary>
        private void UpdateRadioServer()
        {
            var r = Radio.Value;
            if (r.Playing && Phase.Value != MatchPhase.Lobby)
            {
                r.PausedAt = Mathf.Max(0f, RadioPosition(r));
                r.Playing = false;
                Radio.Value = r;
            }
        }
    }
}
