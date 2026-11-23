using HideAndSeek.Core;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby mini interactions (server): light / fan switch, fireplace, record player (toggles in <see cref="PropBits"/>),
    /// feeding the fish, spinning the globe, darts. A use is checked (lobby, alive, not busy / seated, close enough,
    /// short cooldown per object) and broadcast with an argument (globe speed, dart hit) so every peer shows the same.
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>See <see cref="LobbyPropBits"/>.</summary>
        public readonly NetworkVariable<int> PropBits = new(0);

        private readonly float[] _propNext = new float[8];
        private readonly System.Random _propRnd = new();

        [Rpc(SendTo.Server)]
        public void UsePropRpc(byte kind, RpcParams p = default) => ServerUseProp(NetPlayer.FindByClient(p.Receive.SenderClientId), kind);

        /// <summary>Server: player <paramref name="pl"/> uses the prop (also called directly by the host tests).</summary>
        public void ServerUseProp(NetPlayer pl, byte kind)
        {
            if (Phase.Value != MatchPhase.Lobby || kind > (byte)LobbyPropKind.Darts) return;
            var k = (LobbyPropKind)kind;
            var prop = LobbyProp.Of(k);
            if (pl == null || prop == null || pl.Busy.Value || pl.Seat.Value >= 0 || pl.Life.Value != LifeState.Alive) return;
            if (ArcadeCabOf(pl.NetworkObjectId) >= 0 || ForkHolder.Value == pl.NetworkObjectId) return;
            if (Vector3.Distance(pl.EyePosition, prop.Aim) > prop.range + 0.8f) return;
            if (Time.time < _propNext[kind]) return;
            int arg = 0;
            switch (k)
            {
                case LobbyPropKind.Globe:
                    arg = _propRnd.Next(380, 900) * (_propRnd.Next(2) == 0 ? 1 : -1); // degrees / s at the start
                    _propNext[kind] = Time.time + 0.9f;
                    break;
                case LobbyPropKind.Aquarium:
                    arg = _propRnd.Next(1 << 20);
                    _propNext[kind] = Time.time + 2.2f;
                    break;
                case LobbyPropKind.Darts:
                    if (!LobbyDarts.CanThrowFrom(pl.transform.position)) return;
                    pl.TeleportRpc(LobbyDarts.ThrowSpot(pl.transform.position), LobbyDarts.ThrowYaw);
                    arg = LobbyDarts.RollHit(_propRnd);
                    _propNext[kind] = Time.time + 1.15f;
                    break;
                default:
                    PropBits.Value ^= 1 << kind;
                    _propNext[kind] = Time.time + 0.7f;
                    break;
            }
            PropUsedRpc(kind, pl.NetworkObjectId, arg);
        }

        [Rpc(SendTo.Everyone)]
        private void PropUsedRpc(byte kind, ulong user, int arg) => LobbyPropDriver.OnUsed((LobbyPropKind)kind, user, arg);
    }
}
