using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby go-kart on the player: the driver writes the kart's pose / controls here (owner-authoritative, like the RC
    /// cars' <see cref="RcDrive"/>), everybody else reads it (<see cref="LobbyKart"/>). Throttled to ~30 sends a second.
    /// </summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<KartPack> KartDrive = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float _nextKartSend;

        /// <summary>Owner: publishes the kart pose (at most ~30 times a second, only when it changed).</summary>
        public void SendKartPack(KartPack pack)
        {
            if (!IsOwner || !IsSpawned || Time.unscaledTime < _nextKartSend) return;
            _nextKartSend = Time.unscaledTime + 1f / 30f;
            if (!pack.Equals(KartDrive.Value)) KartDrive.Value = pack;
        }

        /// <summary>Another driver's kart hit this player's kart (<paramref name="kart"/>): the shove, on his client.</summary>
        [Rpc(SendTo.Owner)]
        public void KartPushRpc(byte kart, Vector3 dv) => LobbyKart.Get(kart)?.ExternalPush(dv);
    }
}
