using HideAndSeek.Game.MiniGames;
using Unity.Collections;
using UnityEngine;

namespace HideAndSeek.Game.Net
{
    /// <summary>Tic-tac-toe referee (server): seat A = X, seat B = O, starter alternates every round, score kept per pair.</summary>
    public partial class MatchManager
    {
        public TttState TttServerState => _ttt;

        private void PublishTtt()
        {
            _ttt.Seq++;
            TttNet.Value = new FixedString512Bytes(_ttt.Encode());
        }

        private void ResetTtt(bool clearSeats)
        {
            if (clearSeats)
            {
                _ttt.A = _ttt.B = TttState.Nobody;
                _ttt.ScoreA = _ttt.ScoreB = _ttt.Draws = 0;
                _ttt.Starter = 0;
            }
            _ttt.Phase = TttState.Waiting;
            _ttt.Cells = ".........";
            _ttt.End = GameEnd.None;
            _ttt.Winner = -1;
            _ttt.Line = -1;
            _ttt.Rematch = 0;
            _ttt.LastCell = -1;
            PublishTtt();
        }

        private void StartTtt(bool nextRound)
        {
            if (nextRound) _ttt.Starter = 1 - _ttt.Starter;
            _ttt.Cells = ".........";
            _ttt.Turn = _ttt.Starter;
            _ttt.End = GameEnd.None;
            _ttt.Winner = -1;
            _ttt.Line = -1;
            _ttt.Rematch = 0;
            _ttt.LastCell = -1;
            _ttt.Round++;
            _ttt.Phase = TttState.Playing;
            PublishTtt();
            Debug.Log($"[MiniGames] ttt round {_ttt.Round}: X={NickOf(_ttt.A)} O={NickOf(_ttt.B)}, starts {TttState.Mark(_ttt.Starter)}");
        }

        public void ServerTttMove(NetPlayer pl, byte cell)
        {
            if (pl == null || _ttt.Phase != TttState.Playing || cell > 8) return;
            int seat = _ttt.SeatOf(pl.NetworkObjectId);
            if (seat < 0 || seat != _ttt.Turn || _ttt.Cells[cell] != '.') return;
            var c = _ttt.Cells.ToCharArray();
            c[cell] = TttState.Mark(seat);
            _ttt.Cells = new string(c);
            _ttt.LastCell = cell;
            int line = TttRules.WinLine(_ttt.Cells);
            if (line >= 0)
            {
                _ttt.Phase = TttState.Over;
                _ttt.End = GameEnd.Line;
                _ttt.Winner = seat;
                _ttt.Line = line;
                if (seat == 0) _ttt.ScoreA++; else _ttt.ScoreB++;
            }
            else if (TttRules.Full(_ttt.Cells))
            {
                _ttt.Phase = TttState.Over;
                _ttt.End = GameEnd.Full;
                _ttt.Winner = -1;
                _ttt.Draws++;
            }
            else _ttt.Turn = 1 - seat;
            PublishTtt();
            Debug.Log($"[MiniGames] ttt {TttState.Mark(seat)} -> {cell} [{_ttt.Cells}] {(_ttt.Phase == TttState.Over ? _ttt.End.ToString() : "")}");
        }

        private void TttSeatLeft(int seat, bool disconnected)
        {
            if (seat < 0) return;
            if (_ttt.Phase == TttState.Playing)
            {
                _ttt.Phase = TttState.Over;
                _ttt.End = GameEnd.Left;
                _ttt.Winner = 1 - seat;
            }
            if (seat == 0) _ttt.A = TttState.Nobody; else _ttt.B = TttState.Nobody;
            _ttt.ScoreA = _ttt.ScoreB = _ttt.Draws = 0;
            if (_ttt.A == TttState.Nobody && _ttt.B == TttState.Nobody) ResetTtt(true);
            else PublishTtt();
            _ = disconnected;
        }

        private void TttServerTick()
        {
            for (int seat = 0; seat < 2; seat++)
            {
                ulong id = _ttt.Seat(seat);
                if (id != TttState.Nobody && NetPlayer.Find(id) == null) TttSeatLeft(seat, true);
            }
        }
    }
}
