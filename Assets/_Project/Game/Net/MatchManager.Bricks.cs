using System.Collections.Generic;
using HideAndSeek.Audio;
using HideAndSeek.Characters;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby toy bricks: everybody can build on the baseplate next to the toy box. The bricks live in a network list
    /// on the match manager, so the building stays through every round until the host shuts the server down.
    /// Each player can undo his own bricks (newest first).
    /// </summary>
    public partial class MatchManager
    {
        /// <summary>Packed bricks: x | y &lt;&lt; 8 | z &lt;&lt; 16 | colour &lt;&lt; 24 (see <see cref="LobbyBricks"/>).</summary>
        public NetworkList<int> Bricks;

        public const int MaxBricks = 1200;
        private readonly Dictionary<ulong, List<int>> _brickUndo = new();

        private void InitBricks() => Bricks = new NetworkList<int>();

        private int BrickIndexAt(int cell)
        {
            for (int i = 0; i < Bricks.Count; i++)
                if ((Bricks[i] & 0xFFFFFF) == cell) return i;
            return -1;
        }

        public void ServerPlaceBrick(NetPlayer pl, int packed)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || !InLobbyScene || Bricks.Count >= MaxBricks) return;
            LobbyBricks.Unpack(packed, out int x, out int y, out int z, out int color);
            if (!LobbyBricks.InGrid(x, y, z) || color < 0 || color >= LobbyBricks.Colors.Length) return;
            if (Vector3.Distance(pl.transform.position, LobbyBricks.CellCenter(x, y, z)) > LobbyBricks.ReachServer) return;
            int cell = packed & 0xFFFFFF;
            if (BrickIndexAt(cell) >= 0) return;
            // Like Minecraft: on the plate or touching another brick.
            if (y > 0)
            {
                bool touch = false;
                foreach (var d in LobbyBricks.Neighbours)
                    if (LobbyBricks.InGrid(x + d.x, y + d.y, z + d.z) && BrickIndexAt(LobbyBricks.Pack(x + d.x, y + d.y, z + d.z, 0)) >= 0) { touch = true; break; }
                if (!touch) return;
            }
            Bricks.Add(packed);
            if (!_brickUndo.TryGetValue(pl.NetworkObjectId, out var stack)) _brickUndo[pl.NetworkObjectId] = stack = new List<int>();
            stack.Add(cell);
            if (stack.Count > 400) stack.RemoveAt(0);
            BrickFxRpc(packed, pl.NetworkObjectId, false);
        }

        public void ServerUndoBrick(NetPlayer pl)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby) return;
            if (!_brickUndo.TryGetValue(pl.NetworkObjectId, out var stack)) return;
            while (stack.Count > 0)
            {
                int cell = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                int i = BrickIndexAt(cell);
                if (i < 0) continue; // already gone
                int packed = Bricks[i];
                Bricks.RemoveAt(i);
                BrickFxRpc(packed, pl.NetworkObjectId, true);
                return;
            }
        }

        /// <summary>Click at the brick, the builder's hand reaches out.</summary>
        [Rpc(SendTo.Everyone)]
        private void BrickFxRpc(int packed, ulong who, bool removed)
        {
            LobbyBricks.Unpack(packed, out int x, out int y, out int z, out _);
            Vector3 c = LobbyBricks.CellCenter(x, y, z);
            GameAudio.PlayAt(GameSfx.RcPlace, c, removed ? 0.5f : 0.75f);
            var pl = NetPlayer.Find(who);
            if (pl == null) return;
            (pl.Actions as BlockyCharacterActions)?.PlayPoke(c);
            if (pl.IsOwner) (pl.ViewModel as FirstPersonViewModel)?.PlayPoke();
        }
    }
}
