using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Common start of a boombox track. The server marks a new track Loading; every copy buffers it from 0:00 and sends
    /// "ready" (a named message, no change to NetPlayer). The server starts it when all connected copies are ready, or
    /// <see cref="RadioWaitAfterHost"/> s after the host itself was ready (slow copies then join at the current position),
    /// at most <see cref="RadioWaitMax"/> s after the track was chosen. The start is set <see cref="RadioLead"/> s ahead
    /// so the new state reaches everybody before the first sample.
    /// </summary>
    public partial class MatchManager
    {
        public const string RadioReadyMessage = "hns.radio.ready";
        public const float RadioLead = 0.45f, RadioWaitAfterHost = 6f, RadioWaitMax = 25f;

        private readonly HashSet<ulong> _radioReady = new();
        private float _radioLoadSince, _radioHostReadyAt = -1f;
        private NetworkManager _radioMsgOn;

        private void BeginRadioLoad(ref RadioState r)
        {
            r.Seq++;
            r.Playing = Phase.Value == MatchPhase.Lobby;
            r.Loading = true;
            r.PausedAt = 0f;
            r.StartTime = 0d;
            r.Ready = 0;
            r.Peers = (byte)Mathf.Min(255, NetworkManager.ConnectedClientsIds.Count);
            _radioReady.Clear();
            _radioLoadSince = Time.unscaledTime;
            _radioHostReadyAt = -1f;
        }

        /// <summary>This copy has buffered the start of track <paramref name="seq"/> (any copy, main thread).</summary>
        public void ReportRadioReady(int seq)
        {
            var nm = NetworkManager;
            if (nm == null || !nm.IsListening) return;
            if (IsServer) { ServerRadioReady(NetworkManager.ServerClientId, seq); return; }
            using var w = new FastBufferWriter(8, Allocator.Temp);
            w.WriteValueSafe(seq);
            nm.CustomMessagingManager.SendNamedMessage(RadioReadyMessage, NetworkManager.ServerClientId, w, NetworkDelivery.Reliable);
        }

        private void OnRadioReadyMessage(ulong sender, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int seq);
            ServerRadioReady(sender, seq);
        }

        private void ServerRadioReady(ulong client, int seq)
        {
            var r = Radio.Value;
            if (!IsServer || !r.Loading || r.Seq != seq) return;
            _radioReady.Add(client);
            if (client == NetworkManager.ServerClientId && _radioHostReadyAt < 0f) _radioHostReadyAt = Time.unscaledTime;
            Debug.Log($"[Radio] ready: client {client} for seq {seq} ({_radioReady.Count}/{NetworkManager.ConnectedClientsIds.Count})");
        }

        private void UpdateRadioLoading()
        {
            var nm = NetworkManager;
            if (nm == null) return;
            if (_radioMsgOn != nm && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(RadioReadyMessage, OnRadioReadyMessage);
                _radioMsgOn = nm;
            }
            var r = Radio.Value;
            if (!r.Loading) return;
            int peers = 0, ready = 0;
            foreach (var id in nm.ConnectedClientsIds)
            {
                peers++;
                if (_radioReady.Contains(id)) ready++;
            }
            if (r.Ready != ready || r.Peers != peers)
            {
                r.Ready = (byte)Mathf.Min(255, ready);
                r.Peers = (byte)Mathf.Min(255, peers);
                Radio.Value = r;
            }
            if (!r.Playing) return;
            float now = Time.unscaledTime;
            bool hostReady = _radioHostReadyAt >= 0f;
            bool go = hostReady && (ready >= peers || now - _radioHostReadyAt > RadioWaitAfterHost) || now - _radioLoadSince > RadioWaitMax;
            if (!go) return;
            r.Loading = false;
            r.StartTime = Now + RadioLead;
            Radio.Value = r;
            Debug.Log($"[Radio] start seq {r.Seq} together: {ready}/{peers} ready after {now - _radioLoadSince:0.00}s");
        }
    }
}
