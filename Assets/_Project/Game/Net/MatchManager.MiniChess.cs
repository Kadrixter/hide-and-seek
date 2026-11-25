using HideAndSeek.Game.MiniGames;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Chess table referee (server): moves, clocks, endings, seats (see MatchManager.MiniGames).</summary>
    public partial class MatchManager
    {
        private readonly ChessReferee _chessRef = new();
        private int _chessClockOpt;

        public ChessState ChessServerState => _chess;

        private void PublishChess()
        {
            _chess.Seq++;
            ChessNet.Value = new FixedString512Bytes(_chess.Encode());
        }

        /// <summary>Everything back to an empty table (<paramref name="clearSeats"/>) or a fresh board.</summary>
        private void ResetChess(bool clearSeats)
        {
            if (clearSeats)
            {
                if (_chess.Phase == ChessState.Playing) Debug.Log("[MiniGames] chess game aborted");
                _chess.A = _chess.B = ChessState.Nobody;
                _chess.WhiteIsA = true;
                _chess.Kind = ChessState.KindNone; // the next players pick the game again
            }
            _chessRef.Reset(ChessBoard.StartFen);
            _chess.Phase = ChessState.Waiting;
            _chess.Fen = _chessRef.Board.ToFen();
            _chess.Last = "-";
            _chess.End = GameEnd.None;
            _chess.Winner = 0;
            _chess.Rematch = 0;
            _chess.Ply = 0;
            ApplyClockOption();
            PublishChess();
        }

        private void ApplyClockOption()
        {
            var o = ChessState.ClockOptions[_chessClockOpt % ChessState.ClockOptions.Length];
            _chess.ClockMin = o.min;
            _chess.ClockInc = o.inc;
            _chess.WhiteMs = _chess.BlackMs = o.min * 60000;
        }

        private void StartChess(bool swapColours)
        {
            if (_chess.Kind == ChessState.KindNone) { PublishChess(); return; } // nobody picked the game yet
            if ((_chess.Rematch & ChessState.RematchSwitch) != 0) _chess.Kind = _chess.OtherKind; // agreed to play the other game
            if (_chess.Kind == ChessState.KindCheckers) { StartCheckers(swapColours); return; }
            if (swapColours) _chess.WhiteIsA = !_chess.WhiteIsA;
            _chessRef.Reset(ChessBoard.StartFen);
            _chess.Fen = _chessRef.Board.ToFen();
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
            Debug.Log($"[MiniGames] chess game {_chess.Game} started: white={NickOf(_chess.White)} black={NickOf(_chess.Black)} clock={ChessState.ClockName(_chess.ClockMin, _chess.ClockInc)}");
        }

        /// <summary>Server: a move from a seated player (squares 0..63, promotion piece type, 0 = queen).</summary>
        public void ServerChessMove(NetPlayer pl, byte from, byte to, byte promo)
        {
            if (pl == null || _chess.Phase != ChessState.Playing || _chess.Kind != ChessState.KindChess || from > 63 || to > 63) return;
            int seat = _chess.SeatOf(pl.NetworkObjectId);
            if (seat < 0 || _chess.ColourOfSeat(seat) != _chessRef.Board.Side) return;
            int mover = _chessRef.Board.Side;
            if (_chess.HasClock && _chess.MsLeft(mover, mover, Now) <= 0) { ChessTimeout(mover); return; }
            if (!_chessRef.Play(from, to, promo, out var move, out var end, out int winner))
            {
                Debug.Log($"[MiniGames] illegal chess move from {pl.Nick}: {ChessBoard.SqName(from)}{ChessBoard.SqName(to)}");
                return;
            }
            if (_chess.HasClock)
            {
                int left = _chess.MsLeft(mover, mover, Now) + _chess.ClockInc * 1000;
                if (mover > 0) _chess.WhiteMs = left; else _chess.BlackMs = left;
            }
            _chess.TurnStart = Now;
            _chess.Fen = _chessRef.Board.ToFen();
            _chess.Last = move.ToString();
            _chess.Ply++;
            if (end != GameEnd.None) FinishChess(end, winner);
            else PublishChess();
            Debug.Log($"[MiniGames] chess {_chess.Ply}. {move} {(end != GameEnd.None ? end.ToString() : _chessRef.Board.InCheck(_chessRef.Board.Side) ? "check" : "")}");
        }

        private void FinishChess(GameEnd end, int winner)
        {
            // Freeze the clocks where they stand.
            if (_chess.HasClock && _chess.Phase == ChessState.Playing)
            {
                int side = BoardSide;
                int left = _chess.MsLeft(side, side, Now);
                if (side > 0) _chess.WhiteMs = left; else _chess.BlackMs = left;
            }
            _chess.Phase = ChessState.Over;
            _chess.End = end;
            _chess.Winner = winner;
            _chess.Rematch = 0;
            PublishChess();
            Debug.Log($"[MiniGames] {(_chess.IsCheckers ? "checkers" : "chess")} over: {(_chess.IsCheckers ? CheckersReferee.Describe(end, winner, NickOf(_chess.White), NickOf(_chess.Black)) : ChessReferee.Describe(end, winner, NickOf(_chess.White), NickOf(_chess.Black)))}");
        }

        private void ChessTimeout(int side)
        {
            int winner = _chess.IsCheckers || _chessRef.CanMate(-side) ? -side : 0;
            FinishChess(GameEnd.Timeout, winner);
        }

        /// <summary>A seat emptied (left / disconnected). A running game goes to the other player.</summary>
        private void ChessSeatLeft(int seat, bool disconnected)
        {
            if (seat < 0) return;
            if (_chess.Phase == ChessState.Playing)
            {
                int loser = _chess.ColourOfSeat(seat);
                FinishChess(disconnected ? GameEnd.Left : GameEnd.Resign, -loser);
            }
            if (seat == 0) _chess.A = ChessState.Nobody; else _chess.B = ChessState.Nobody;
            _chess.Rematch = 0;
            if (_chess.A == ChessState.Nobody && _chess.B == ChessState.Nobody) ResetChess(true);
            else PublishChess();
        }

        /// <summary>Server: C at the table while no game runs = next time control.</summary>
        public void ServerChessClock(NetPlayer pl)
        {
            if (pl == null || _chess.Phase == ChessState.Playing || _chess.SeatOf(pl.NetworkObjectId) < 0) return;
            _chessClockOpt = (_chessClockOpt + 1) % ChessState.ClockOptions.Length;
            ApplyClockOption();
            PublishChess();
        }

        private void ChessServerTick()
        {
            for (int seat = 0; seat < 2; seat++)
            {
                ulong id = _chess.Seat(seat);
                if (id != ChessState.Nobody && NetPlayer.Find(id) == null) ChessSeatLeft(seat, true);
            }
            if (_chess.Phase == ChessState.Playing && _chess.HasClock)
            {
                int side = BoardSide;
                if (_chess.MsLeft(side, side, Now) <= 0) ChessTimeout(side);
            }
        }
    }
}
