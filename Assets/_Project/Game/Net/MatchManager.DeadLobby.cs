using HideAndSeek.Characters;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Server side of the dead players' lobby (<see cref="DeadLobby"/>): their switches and toys during a round. Checked
    /// like the living's uses (cooldown per object), but the user must be out of the round (dead / escaped) with the
    /// lobby body active. The toggles go into <see cref="PropBits"/>, so the real lobby after the round shows them too.
    /// The broadcast plays the prop and the ghost's arm only (the corpse in the garage never moves).
    /// </summary>
    public partial class MatchManager
    {
        [Rpc(SendTo.Server)]
        public void GhostPropRpc(byte kind, RpcParams p = default)
        {
            var pl = NetPlayer.FindByClient(p.Receive.SenderClientId);
            var ph = Phase.Value;
            if (pl == null || !pl.IsObserver || (ph != MatchPhase.Hiding && ph != MatchPhase.Seeking)) return;
            if (pl.LobbyGhost.Value.Mode != LobbyGhostState.InLobby || pl.LobbyGhost.Value.Seat >= 0) return;
            if (kind >= (byte)LobbyPropKind.Darts || Time.time < _propNext[kind]) return; // no darts in the copy (shelved)
            int arg = 0;
            switch ((LobbyPropKind)kind)
            {
                case LobbyPropKind.Globe:
                    arg = _propRnd.Next(380, 900) * (_propRnd.Next(2) == 0 ? 1 : -1);
                    _propNext[kind] = Time.time + 0.9f;
                    break;
                case LobbyPropKind.Aquarium:
                    arg = _propRnd.Next(1 << 20);
                    _propNext[kind] = Time.time + 2.2f;
                    break;
                default:
                    PropBits.Value ^= 1 << kind;
                    _propNext[kind] = Time.time + 0.7f;
                    break;
            }
            Debug.Log($"[DeadLobby] server: {pl.Nick} (lobby ghost) used {(LobbyPropKind)kind}");
            GhostPropUsedRpc(kind, pl.NetworkObjectId, arg);
        }

        [Rpc(SendTo.Everyone)]
        private void GhostPropUsedRpc(byte kind, ulong user, int arg)
        {
            // No NetPlayer as the user: its body is the corpse in the garage. The ghost's own arm reaches instead.
            LobbyPropDriver.OnUsed((LobbyPropKind)kind, ulong.MaxValue, arg);
            var prop = LobbyProp.Of((LobbyPropKind)kind);
            if (prop != null) DeadLobbyAvatars.Reach(user, prop.Aim, prop.Style);
        }

        /// <summary>Test: starts the next round straight from the lobby (everybody ready, like the autostart).</summary>
        public void TestStartNextRound()
        {
            if (!IsServer || Phase.Value != MatchPhase.Lobby) return;
            foreach (var pl in NetPlayer.All) if (pl != null) pl.Ready.Value = true;
            RequestStartRpc();
        }
    }
}
