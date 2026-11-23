using HideAndSeek.Game.MiniGames;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// Lobby board games (chess, tic-tac-toe), refereed by the server. Each table's whole state is one text network
    /// variable (<see cref="ChessNet"/>, <see cref="TttNet"/>): late joiners get the current board at once, every
    /// change is one update, and the clients animate the difference. Players sit down with E (seat A / B), a game
    /// starts when both seats are taken; leaving, disconnecting or the round starting ends it cleanly.
    /// </summary>
    public partial class MatchManager
    {
        public const byte GameChess = 0, GameTtt = 1;

        public readonly NetworkVariable<FixedString512Bytes> ChessNet = new();
        public readonly NetworkVariable<FixedString512Bytes> TttNet = new();

        private System.Action _stepMiniGames;
        private readonly ChessState _chess = new();
        private readonly TttState _ttt = new();
        private bool _miniInit;

        /// <summary>Per frame, all peers (hooked into Update): tables in the lobby, server referee ticks.</summary>
        private void UpdateMiniGames()
        {
            bool lobby = Phase.Value == MatchPhase.Lobby && InLobbyScene;
            if (lobby) MiniGameTables.Ensure();
            if (!IsServer) return;
            if (!_miniInit)
            {
                _miniInit = true;
                ResetChess(true);
                ResetTtt(true);
            }
            if (!lobby)
            {
                // Round starting / loading: every game ends, every seat is freed.
                if (_chess.A != ChessState.Nobody || _chess.B != ChessState.Nobody || _chess.Phase != ChessState.Waiting) ResetChess(true);
                if (_ttt.A != TttState.Nobody || _ttt.B != TttState.Nobody || _ttt.Phase != TttState.Waiting) ResetTtt(true);
                return;
            }
            ChessServerTick();
            TttServerTick();
        }

        /// <summary>Which table and seat a player sits at (game, seat) or (-1, -1).</summary>
        public (int game, int seat) MiniSeatOf(ulong id)
        {
            int s = _chess.SeatOf(id);
            if (s >= 0) return (GameChess, s);
            s = _ttt.SeatOf(id);
            if (s >= 0) return (GameTtt, s);
            return (-1, -1);
        }

        /// <summary>Server: E at a table = sit down on seat A / B.</summary>
        public void ServerMiniSit(NetPlayer pl, byte game, byte seat)
        {
            if (pl == null || Phase.Value != MatchPhase.Lobby || seat > 1 || game > 1) return;
            if (pl.Busy.Value || pl.Seat.Value >= 0 || ArcadePlayer.Value == pl.NetworkObjectId || ForkHolder.Value == pl.NetworkObjectId) return;
            if (MiniSeatOf(pl.NetworkObjectId).game >= 0) return;
            var table = MiniGameTables.Get(game);
            if (table == null || Vector3.Distance(pl.transform.position, table.SeatStand(seat)) > 2.6f) return;
            if (game == GameChess)
            {
                if (_chess.Seat(seat) != ChessState.Nobody) return;
                if (seat == 0) _chess.A = pl.NetworkObjectId; else _chess.B = pl.NetworkObjectId;
                _chess.Rematch = 0;
                if (_chess.A != ChessState.Nobody && _chess.B != ChessState.Nobody) StartChess(false);
                else PublishChess();
            }
            else
            {
                if (_ttt.Seat(seat) != TttState.Nobody) return;
                if (seat == 0) _ttt.A = pl.NetworkObjectId; else _ttt.B = pl.NetworkObjectId;
                _ttt.Rematch = 0;
                if (_ttt.A != TttState.Nobody && _ttt.B != TttState.Nobody)
                {
                    _ttt.ScoreA = _ttt.ScoreB = _ttt.Draws = 0; // new pair of players
                    StartTtt(false);
                }
                else PublishTtt();
            }
            pl.TeleportRpc(table.SeatStand(seat), table.SeatYaw(seat));
            Debug.Log($"[MiniGames] {pl.Nick} sat at {(game == GameChess ? "chess" : "ttt")} seat {(seat == 0 ? 'A' : 'B')}");
        }

        /// <summary>Server: the player gets up (Esc). A running game is resigned / abandoned.</summary>
        public void ServerMiniLeave(NetPlayer pl)
        {
            if (pl == null) return;
            var (game, seat) = MiniSeatOf(pl.NetworkObjectId);
            if (game == GameChess) ChessSeatLeft(seat, false);
            else if (game == GameTtt) TttSeatLeft(seat, false);
            var table = game >= 0 ? MiniGameTables.Get(game) : null;
            if (table != null) pl.TeleportRpc(table.LeavePoint(seat), table.SeatYaw(seat));
        }

        /// <summary>Server: R after a game. Chess: both players must want it (colours swap). Tic-tac-toe: next round at once, the other player starts.</summary>
        public void ServerMiniRematch(NetPlayer pl)
        {
            if (pl == null) return;
            var (game, seat) = MiniSeatOf(pl.NetworkObjectId);
            if (game == GameChess && _chess.Phase == ChessState.Over)
            {
                _chess.Rematch |= 1 << seat;
                if (_chess.Rematch == 3 && _chess.A != ChessState.Nobody && _chess.B != ChessState.Nobody) StartChess(true);
                else PublishChess();
            }
            else if (game == GameTtt && _ttt.Phase == TttState.Over)
            {
                // Tic-tac-toe rounds are short: one R from either player starts the next one (the other starts it).
                if (_ttt.A != TttState.Nobody && _ttt.B != TttState.Nobody) StartTtt(true);
            }
        }

        private static string NickOf(ulong id)
        {
            var p = id == ulong.MaxValue ? null : NetPlayer.Find(id);
            return p != null ? p.Nick : "";
        }
    }
}
