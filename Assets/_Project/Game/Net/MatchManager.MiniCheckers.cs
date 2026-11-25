using HideAndSeek.Game.MiniGames;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>
    /// The second game of the chess table (server): which game the seated players pick (<see cref="ChessState.Kind"/>)
    /// and the checkers referee. Checkers shares the chess table's state line, seats, clock and rematch (MiniChess):
    /// <see cref="ChessState.Fen"/> holds the checkers position, <see cref="ChessState.Last"/> the path of the last move.
    /// </summary>
    public partial class MatchManager
    {
        private readonly CheckersReferee _checkersRef = new();

        /// <summary>Side to move in the game being played at the chess table (+1 white, -1 black).</summary>
        private int BoardSide => _chess.IsCheckers ? _checkersRef.Board.Side : _chessRef.Board.Side;

        /// <summary>
        /// Server: a seated player picks the game (chess / checkers). Before a game: the game starts as soon as both
        /// seats are taken. After a game: a rematch request in that game (<see cref="ChessState.RematchSwitch"/> when it is
        /// the other one); the other player agrees with R.
        /// </summary>
        public void ServerBoardKind(NetPlayer pl, byte kind)
        {
            if (pl == null || (kind != ChessState.KindChess && kind != ChessState.KindCheckers)) return;
            int seat = _chess.SeatOf(pl.NetworkObjectId);
            if (seat < 0 || _chess.Phase == ChessState.Playing) return;
            Debug.Log($"[MiniGames] {pl.Nick} picked {(kind == ChessState.KindCheckers ? "checkers" : "chess")} at the chess table");
            if (_chess.Phase == ChessState.Over)
            {
                // After a game: "let's play the other game now" = a rematch request in it (the other player confirms with R).
                // The finished game stays on the board until the new one starts.
                _chess.Rematch = kind != _chess.Kind ? (1 << seat) | ChessState.RematchSwitch : 1 << seat;
                PublishChess();
                return;
            }
            _chess.Kind = kind;
            if (_chess.A != ChessState.Nobody && _chess.B != ChessState.Nobody) StartChess(false);
            else PublishChess();
        }

        private void StartCheckers(bool swapColours)
        {
            if (swapColours) _chess.WhiteIsA = !_chess.WhiteIsA;
            _checkersRef.Reset();
            _chess.Fen = _checkersRef.Board.Encode();
            _chess.Last = "-";
            _chess.End = GameEnd.None;
            _chess.Winner = 0;
            _chess.Rematch = 0;
            _chess.Ply = 0;
            _chess.Game++;
            ApplyClockOption();
            _chess.Phase = ChessState.Playing;
            _chess.TurnStart = Now;
            PublishChess();
            Debug.Log($"[MiniGames] checkers game {_chess.Game} started: white={NickOf(_chess.White)} black={NickOf(_chess.Black)} clock={ChessState.ClockName(_chess.ClockMin, _chess.ClockInc)}");
        }

        /// <summary>Server: a checkers move from a seated player (path of square names, "c3e5c7").</summary>
        public void ServerCheckersMove(NetPlayer pl, string pathText)
        {
            if (pl == null || _chess.Phase != ChessState.Playing || !_chess.IsCheckers) return;
            int seat = _chess.SeatOf(pl.NetworkObjectId);
            if (seat < 0 || _chess.ColourOfSeat(seat) != _checkersRef.Board.Side) return;
            int mover = _checkersRef.Board.Side;
            if (_chess.HasClock && _chess.MsLeft(mover, mover, Now) <= 0) { ChessTimeout(mover); return; }
            var path = CheckersMove.ParsePath(pathText);
            if (path == null || !_checkersRef.Play(path, out var move, out var end, out int winner))
            {
                Debug.Log($"[MiniGames] illegal checkers move from {pl.Nick}: {pathText}");
                return;
            }
            if (_chess.HasClock)
            {
                int left = _chess.MsLeft(mover, mover, Now) + _chess.ClockInc * 1000;
                if (mover > 0) _chess.WhiteMs = left; else _chess.BlackMs = left;
            }
            _chess.TurnStart = Now;
            _chess.Fen = _checkersRef.Board.Encode();
            _chess.Last = move.ToString();
            _chess.Ply++;
            if (end != GameEnd.None) FinishChess(end, winner);
            else PublishChess();
            Debug.Log($"[MiniGames] checkers {_chess.Ply}. {move}{(move.IsCapture ? $" x{move.Captured.Count}" : "")}{(move.Promotes ? " =K" : "")} {(end != GameEnd.None ? end.ToString() : "")}");
        }
    }
}
