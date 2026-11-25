using System.Collections.Generic;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// One game of checkers as the server referees it. A side without pieces or without a legal move loses. Draws:
    /// the same position (with the same side to move) for the third time, or <see cref="KingMovesDraw"/> half-moves in
    /// a row made only by kings without a capture (15 moves each).
    /// </summary>
    public sealed class CheckersReferee
    {
        public const int KingMovesDraw = 30;

        public readonly CheckersBoard Board = new();
        readonly List<string> _history = new();
        readonly List<CheckersMove> _legal = new();
        /// <summary>Half-moves in a row by kings without capturing.</summary>
        public int KingMoves { get; private set; }

        public CheckersReferee() => Reset();

        public void Reset(string position = CheckersBoard.Start)
        {
            Board.Decode(position);
            KingMoves = 0;
            _history.Clear();
            _history.Add(Board.Encode());
        }

        /// <summary>Plays the move with this path; false = illegal. Afterwards <paramref name="end"/> / <paramref name="winner"/> (+1 white, -1 black, 0 draw).</summary>
        public bool Play(IList<int> path, out CheckersMove move, out GameEnd end, out int winner)
        {
            end = GameEnd.None;
            winner = 0;
            if (!Board.FindLegal(path, out move)) return false;
            int mover = Board.Side;
            int p = Board.Sq[move.From];
            bool king = p == CheckersBoard.King || p == -CheckersBoard.King;
            Board.Make(move);
            if (king && !move.IsCapture) KingMoves++;
            else KingMoves = 0;
            if (move.IsCapture || !king) _history.Clear(); // a man move or a capture can never be undone
            _history.Add(Board.Encode());
            end = Status(out winner, mover);
            return true;
        }

        public GameEnd Status(out int winner, int lastMover)
        {
            winner = 0;
            if (Board.Count(Board.Side) == 0) { winner = lastMover; return GameEnd.NoPieces; }
            Board.LegalMoves(_legal);
            if (_legal.Count == 0) { winner = lastMover; return GameEnd.NoMoves; }
            int reps = 0;
            string key = Board.Encode();
            foreach (var k in _history) if (k == key) reps++;
            if (reps >= 3) return GameEnd.Repetition;
            if (KingMoves >= KingMovesDraw) return GameEnd.KingMoves;
            return GameEnd.None;
        }

        /// <summary>Polish end-of-game line of a checkers game.</summary>
        public static string Describe(GameEnd end, int winner, string whiteName, string blackName)
        {
            string w = winner > 0 ? whiteName : blackName;
            string who = string.IsNullOrEmpty(w) ? (winner > 0 ? "ją białe" : "ją czarne") : " " + w;
            return end switch
            {
                GameEnd.NoPieces => $"Wszystkie pionki zbite - wygrywa{who}",
                GameEnd.NoMoves => $"Brak ruchu - wygrywa{who}",
                GameEnd.Repetition => "Remis - trzykrotne powtórzenie pozycji",
                GameEnd.KingMoves => "Remis - 15 ruchów damkami bez bicia",
                GameEnd.Resign => $"Poddanie - wygrywa{who}",
                GameEnd.Timeout => $"Koniec czasu - wygrywa{who}",
                GameEnd.Left => $"Przeciwnik wyszedł - wygrywa{who}",
                GameEnd.Aborted => "Partia przerwana",
                _ => "",
            };
        }
    }
}
