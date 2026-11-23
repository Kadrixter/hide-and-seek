using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// One boombox's shared music: a queue of YouTube links and the playback state (both server-written fields of
    /// <see cref="MatchManager"/>), plus the server logic that used to live in <c>MatchManager.Radio.cs</c> /
    /// <c>RadioSync.cs</c> (queue / transport ops, the common start of a track after every copy buffered it, pausing
    /// when music is not allowed). The lobby boombox is one channel (<see cref="MatchManager.LobbyRadio"/>), every radio
    /// found on the parking is another (<c>MatchManager.MapRadio.cs</c>). Every copy plays a channel through a
    /// <see cref="Boombox"/> (yt-dlp + ffmpeg stream, position from the server clock).
    /// <para>Start of a track: the server marks it Loading; every copy buffers it from 0:00 and sends "ready" (a named
    /// message, <see cref="ReportReady"/>). The server starts it when all connected copies are ready, or
    /// <see cref="MatchManager.RadioWaitAfterHost"/> s after the host itself was ready (slow copies then join at the
    /// current position), at most <see cref="MatchManager.RadioWaitMax"/> s after the track was chosen. The start is set
    /// <see cref="MatchManager.RadioLead"/> s ahead so the new state reaches everybody before the first sample.</para>
    /// </summary>
    public sealed class RadioChannel
    {
        public readonly string Name;
        public readonly NetworkVariable<RadioState> State;
        public readonly NetworkList<FixedString128Bytes> Queue;

        private readonly MatchManager _mm;
        private readonly string _readyMessage;
        private readonly Func<bool> _canPlay;
        private readonly Func<NetPlayer, bool> _mayUse;
        private readonly Action<NetPlayer, RadioOp, FixedString128Bytes> _send;

        private readonly HashSet<ulong> _ready = new();
        private float _loadSince, _hostReadyAt = -1f;
        private NetworkManager _msgOn;

        /// <param name="canPlay">Server: music may play now (else it pauses / a new track only loads).</param>
        /// <param name="mayUse">Server: this player may change the channel (null = anybody).</param>
        /// <param name="send">Client: sends an op to the server (an RPC on the local player).</param>
        public RadioChannel(MatchManager mm, string name, NetworkVariable<RadioState> state, NetworkList<FixedString128Bytes> queue,
            string readyMessage, Func<bool> canPlay, Func<NetPlayer, bool> mayUse, Action<NetPlayer, RadioOp, FixedString128Bytes> send)
        {
            _mm = mm;
            Name = name;
            State = state;
            Queue = queue;
            _readyMessage = readyMessage;
            _canPlay = canPlay;
            _mayUse = mayUse;
            _send = send;
        }

        /// <summary>Log prefix: "[Radio]" for the lobby boombox (as before), "[Radio:name]" for the others.</summary>
        public string Tag => string.IsNullOrEmpty(Name) ? "[Radio]" : $"[Radio:{Name}]";

        public double Now => _mm != null ? _mm.Now : 0d;
        public int Count => Queue != null ? Queue.Count : 0;
        public string At(int i) => Queue[i].ToString();

        /// <summary>Link of the current entry (null = none).</summary>
        public string Current
        {
            get
            {
                var r = State.Value;
                return r.Index >= 0 && r.Index < Count ? At(r.Index) : null;
            }
        }

        public float Position(in RadioState r) => r.Playing && !r.Loading ? (float)(Now - r.StartTime) : r.PausedAt;

        /// <summary>Client: asks the server for <paramref name="op"/> (through the local player).</summary>
        public void Request(NetPlayer me, RadioOp op, FixedString128Bytes url)
        {
            if (me != null) _send?.Invoke(me, op, url);
        }

        // ================================================================ server: ops

        public void ServerOp(NetPlayer pl, RadioOp op, string url)
        {
            if (pl == null || (_mayUse != null && !_mayUse(pl))) return;
            Debug.Log($"{Tag} {pl.Nick}: {op} {url}");
            var r = State.Value;
            switch (op)
            {
                case RadioOp.Add:
                    if (!MatchManager.IsYoutubeUrl(url) || Queue.Count >= MatchManager.RadioMaxQueue) return;
                    Queue.Add(new FixedString128Bytes(url.Trim()));
                    if (!r.Playing && (r.Index < 0 || r.Index >= Queue.Count - 1 && r.PausedAt <= 0f))
                    {
                        r.Index = Queue.Count - 1;
                        BeginLoad(ref r);
                    }
                    pl.ToastRpc("Dodano do kolejki radia.", 2f);
                    break;
                case RadioOp.Toggle:
                    if (r.Index < 0 || r.Index >= Queue.Count) return;
                    if (r.Playing)
                    {
                        r.PausedAt = Mathf.Max(0f, Position(r));
                        r.Playing = false;
                    }
                    else if (_canPlay())
                    {
                        r.Playing = true;
                        if (r.Loading) _loadSince = Time.unscaledTime;
                        else r.StartTime = Now + MatchManager.RadioLead - r.PausedAt; // everybody resumes on the same sample
                    }
                    break;
                case RadioOp.Next:
                case RadioOp.Prev:
                {
                    int to = r.Index + (op == RadioOp.Next ? 1 : -1);
                    if (to < 0 || to >= Queue.Count)
                    {
                        if (op == RadioOp.Next) { r.Playing = false; r.Loading = false; r.PausedAt = 0f; r.Index = Queue.Count; r.Seq++; }
                        break;
                    }
                    r.Index = to;
                    BeginLoad(ref r);
                    break;
                }
                case RadioOp.VolUp: r.Volume = (byte)Mathf.Min(100, r.Volume + 10); break;
                case RadioOp.VolDown: r.Volume = (byte)Mathf.Max(0, r.Volume - 10); break;
                case RadioOp.BassUp: r.Bass = (byte)Mathf.Min(100, r.Bass + 10); break;
                case RadioOp.BassDown: r.Bass = (byte)Mathf.Max(0, r.Bass - 10); break;
            }
            State.Value = r;
        }

        /// <summary>The host's own player finished (or failed) the track with <paramref name="seq"/>: next one.</summary>
        public void ServerEnded(int seq)
        {
            if (_mm == null || !_mm.IsServer) return;
            var r = State.Value;
            if (r.Seq != seq || !r.Playing) return;
            r.Index++;
            if (r.Index < Queue.Count) BeginLoad(ref r);
            else
            {
                r.Seq++;
                r.Playing = r.Loading = false;
                r.PausedAt = 0f;
            }
            State.Value = r;
        }

        /// <summary>Server: empty queue, default state (a new radio on the map).</summary>
        public void ServerReset()
        {
            if (Queue.Count > 0) Queue.Clear();
            var r = RadioState.Default;
            r.Seq = State.Value.Seq + 1; // every copy drops its old stream
            State.Value = r;
            _ready.Clear();
            _hostReadyAt = -1f;
        }

        /// <summary>Server, every frame: pause when music is not allowed; start a loaded track together.</summary>
        public void ServerUpdate()
        {
            var r = State.Value;
            if (r.Playing && !_canPlay())
            {
                r.PausedAt = Mathf.Max(0f, Position(r));
                r.Playing = false;
                State.Value = r;
            }
            UpdateLoading();
        }

        // ================================================================ common start

        private void BeginLoad(ref RadioState r)
        {
            r.Seq++;
            r.Playing = _canPlay();
            r.Loading = true;
            r.PausedAt = 0f;
            r.StartTime = 0d;
            r.Ready = 0;
            r.Peers = (byte)Mathf.Min(255, _mm.NetworkManager.ConnectedClientsIds.Count);
            _ready.Clear();
            _loadSince = Time.unscaledTime;
            _hostReadyAt = -1f;
        }

        /// <summary>This copy has buffered the start of track <paramref name="seq"/> (any copy, main thread).</summary>
        public void ReportReady(int seq)
        {
            var nm = _mm != null ? _mm.NetworkManager : null;
            if (nm == null || !nm.IsListening) return;
            if (_mm.IsServer) { ServerReady(NetworkManager.ServerClientId, seq); return; }
            using var w = new FastBufferWriter(8, Allocator.Temp);
            w.WriteValueSafe(seq);
            nm.CustomMessagingManager.SendNamedMessage(_readyMessage, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
        }

        private void OnReadyMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int seq);
            ServerReady(sender, seq);
        }

        private void ServerReady(ulong client, int seq)
        {
            var r = State.Value;
            if (!_mm.IsServer || !r.Loading || r.Seq != seq) return;
            _ready.Add(client);
            if (client == NetworkManager.ServerClientId && _hostReadyAt < 0f) _hostReadyAt = Time.unscaledTime;
            Debug.Log($"{Tag} ready: client {client} for seq {seq} ({_ready.Count}/{_mm.NetworkManager.ConnectedClientsIds.Count})");
        }

        private void UpdateLoading()
        {
            var nm = _mm.NetworkManager;
            if (nm == null) return;
            if (_msgOn != nm && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(_readyMessage, OnReadyMessage);
                _msgOn = nm;
            }
            var r = State.Value;
            if (!r.Loading) return;
            int peers = 0, ready = 0;
            foreach (var id in nm.ConnectedClientsIds)
            {
                peers++;
                if (_ready.Contains(id)) ready++;
            }
            if (r.Ready != ready || r.Peers != peers)
            {
                r.Ready = (byte)Mathf.Min(255, ready);
                r.Peers = (byte)Mathf.Min(255, peers);
                State.Value = r;
            }
            if (!r.Playing) return;
            float now = Time.unscaledTime;
            bool hostReady = _hostReadyAt >= 0f;
            bool go = hostReady && (ready >= peers || now - _hostReadyAt > MatchManager.RadioWaitAfterHost) || now - _loadSince > MatchManager.RadioWaitMax;
            if (!go) return;
            r.Loading = false;
            r.StartTime = Now + MatchManager.RadioLead;
            State.Value = r;
            Debug.Log($"{Tag} start seq {r.Seq} together: {ready}/{peers} ready after {now - _loadSince:0.00}s");
        }
    }
}
