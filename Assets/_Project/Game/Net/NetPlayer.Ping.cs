using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Ping measurement (owner side, see MatchManager.Ping): a stamp goes to the host, which sends it straight back to
    /// the owner; the round trip is smoothed and written to <see cref="PingMs"/> (owner-writable, everybody reads it).
    /// </summary>
    public partial class NetPlayer
    {
        private float _pingSmooth = -1f;

        /// <summary>Owner: one ping to the host.</summary>
        public void SendPing()
        {
            if (!IsOwner || !IsSpawned || IsServer) return;
            PingRpc(Time.realtimeSinceStartupAsDouble);
        }

        [Rpc(SendTo.Server)]
        private void PingRpc(double sentAt) => PongRpc(sentAt);

        [Rpc(SendTo.Owner)]
        private void PongRpc(double sentAt)
        {
            if (!IsOwner) return;
            float ms = (float)((Time.realtimeSinceStartupAsDouble - sentAt) * 1000.0);
            if (ms < 0f || ms > 20000f) return;
            // Smoothed, but a big change shows at once (no slow crawl after a lag spike / a new connection).
            _pingSmooth = _pingSmooth < 0f || Mathf.Abs(ms - _pingSmooth) > 80f ? ms : Mathf.Lerp(_pingSmooth, ms, 0.35f);
            ushort v = (ushort)Mathf.Clamp(Mathf.RoundToInt(_pingSmooth), 1, 9999);
            if (PingMs.Value != v) PingMs.Value = v;
        }
    }
}
