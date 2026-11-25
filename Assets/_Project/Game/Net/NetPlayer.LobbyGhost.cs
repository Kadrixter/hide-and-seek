using System;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Pose of a dead / escaped player's body in the lobby during a round (<see cref="DeadLobby"/>). Lobby-local
    /// coordinates (the lobby room as built, origin in its middle), so it does not depend on where the copy is loaded.
    /// </summary>
    public struct LobbyGhostState : INetworkSerializable, IEquatable<LobbyGhostState>
    {
        public const byte Off = 0, WalkingIn = 1, InLobby = 2, Watching = 3;

        /// <summary><see cref="Off"/>, <see cref="WalkingIn"/>, <see cref="InLobby"/> or <see cref="Watching"/> (full-screen spectating, the body waits at the TV).</summary>
        public byte Mode;
        /// <summary>Lobby seat (LobbySeat index) the ghost sits on, -1 standing.</summary>
        public sbyte Seat;
        /// <summary>Feet position, lobby-local.</summary>
        public Vector3 Pos;
        public float Yaw, Pitch;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref Seat);
            s.SerializeValue(ref Pos);
            s.SerializeValue(ref Yaw);
            s.SerializeValue(ref Pitch);
        }

        public bool Equals(LobbyGhostState o) => Mode == o.Mode && Seat == o.Seat && Pos == o.Pos && Yaw == o.Yaw && Pitch == o.Pitch;
    }

    /// <summary>The lobby ghost of this player (written by the owner, read by the other dead players' lobby copies).</summary>
    public partial class NetPlayer
    {
        public readonly NetworkVariable<LobbyGhostState> LobbyGhost = new(new LobbyGhostState { Seat = -1 },
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>Out of the round but still in the session (dead, or escaped through the exit gate).</summary>
        public bool IsObserver => Life.Value == HideAndSeek.Core.LifeState.Dead || Escaped.Value;
    }
}
