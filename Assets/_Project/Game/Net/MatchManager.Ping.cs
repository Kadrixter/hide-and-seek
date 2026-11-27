using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Ping of every player (server side): once a second the transport's round trip to each client
    /// (<see cref="NetworkTransport.GetCurrentRtt"/>, UDP and Steam alike) goes into <see cref="NetPlayer.PingMs"/>, so
    /// every copy can show it next to the nick in the lobby list (<c>PlayerListRow</c>). The host's own player has 0.
    /// </summary>
    public partial class MatchManager
    {
        private float _pingAt;

        /// <summary>Server, every frame (from UpdateLobbyFun).</summary>
        private void UpdatePings()
        {
            if (!IsServer || Time.unscaledTime < _pingAt) return;
            _pingAt = Time.unscaledTime + 1f;
            var nm = NetworkManager;
            var tr = nm != null ? nm.NetworkConfig.NetworkTransport : null;
            if (tr == null) return;
            foreach (var p in NetPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                ulong rtt = 0;
                if (p.OwnerClientId != NetworkManager.ServerClientId)
                {
                    try { rtt = tr.GetCurrentRtt(p.OwnerClientId); }
                    catch (System.Exception) { rtt = 0; }
                }
                ushort ms = (ushort)Mathf.Min(9999, (long)rtt);
                if (p.PingMs.Value != ms) p.PingMs.Value = ms;
            }
        }
    }
}
