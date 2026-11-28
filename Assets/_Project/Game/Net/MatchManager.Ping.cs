using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Ping of every player, measured by the player's own client (28.11; before that the server asked the transport with
    /// the NGO client id, which is not the transport's id - everybody showed 0 ms). Once a second the local player sends a
    /// stamped message to the host and the host bounces it straight back (<see cref="NetPlayer.PingRpc"/> /
    /// <c>PongRpc</c>): the round trip, the same path every game message takes (network + one tick of batching), is
    /// smoothed and published in <see cref="NetPlayer.PingMs"/> for the lobby list (<c>PlayerListRow</c>). The host has 0.
    /// </summary>
    public partial class MatchManager
    {
        private float _pingAt;

        /// <summary>Every peer, every frame (from UpdateLobbyFun): the local client pings the host once a second.</summary>
        private void UpdatePings()
        {
            if (IsServer || Time.unscaledTime < _pingAt) return;
            var me = NetPlayer.Local;
            if (me == null || !me.IsSpawned) return;
            _pingAt = Time.unscaledTime + 1f;
            me.SendPing();
        }
    }
}
