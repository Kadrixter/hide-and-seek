using System.Collections.Generic;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// One game of chess as the server referees it: the board plus the positions seen since the last pawn move or
    /// capture (threefold repetition). <see cref="Play"/> checks legality and returns how the game stands afterwards.
    /// Draws are automatic: stalemate, 50 moves without a pawn move or capture (100 half-moves), the same position for
    /// the third time, or no mating material left.
    /// </summary>
    public sealed class ChessReferee
    {
        public readonly ChessBoard Board = new();
        readonly List<string> _history = new();
        readonly List<ChessMove> _legal = new();

        public ChessReferee() => Reset(ChessBoard.StartFen);

        public void Reset(string fen)
        {
            Board.SetFen(fen);
            _history.Clear();
            _history.Add(Board.PositionKey());
        }

        public int Repetitions(string key)
        {
            int n = 0;
            foreach (var k in _history) if (k == key) n++;
            return n;
        }

        /// <summary>
        /// Plays from -> to (promotion piece type, 0 = queen when needed). False = illegal (nothing changes). On success
        /// <paramref name="end"/> / <paramref name="winner"/> say whether the game is over (+1 white, -1 black, 0 draw).
        /// </summary>
        public bool Play(int from, int to, int promo, out ChessMove move, out GameEnd end, out int winner)
        {
            end = GameEnd.None;
            winner = 0;
            if (!Board.FindLegal(from, to, promo, out move)) return false;
            int mover = Board.Side;
            Board.Make(move);
            if (Board.Halfmove == 0) _history.Clear();
            string key = Board.PositionKey();
            _history.Add(key);
            end = Status(out winner, mover);
            return true;
        }

        /// <summary>State of the position after <paramref name="lastMover"/> moved.</summary>
        public GameEnd Status(out int winner, int lastMover)
        {
            winner = 0;
            Board.LegalMoves(_legal);
            if (_legal.Count == 0)
            {
                if (Board.InCheck(Board.Side)) { winner = lastMover; return GameEnd.Checkmate; }
                return GameEnd.Stalemate;
            }
            if (Board.InsufficientMaterial()) return GameEnd.Material;
            if (Repetitions(Board.PositionKey()) >= 3) return GameEnd.Repetition;
            if (Board.Halfmove >= 100) return GameEnd.FiftyMoves;
            return GameEnd.None;
        }

        /// <summary>Can <paramref name="colour"/> still mate at all (used when the opponent's flag falls)?</summary>
        public bool CanMate(int colour)
        {
            int minors = 0;
            for (int s = 0; s < 64; s++)
            {
                int p = Board.Sq[s];
                if (p == 0 || (p > 0) != (colour > 0)) continue;
                int t = p < 0 ? -p : p;
                if (t == ChessBoard.Pawn || t == ChessBoard.Rook || t == ChessBoard.Queen) return true;
                if (t == ChessBoard.Knight || t == ChessBoard.Bishop) minors++;
            }
            return minors >= 2;
        }

        /// <summary>Polish end-of-game line ("Mat - wygrywają białe", "Pat - remis").</summary>
        public static string Describe(GameEnd end, int winner, string whiteName, string blackName)
        {
            string w = winner > 0 ? whiteName : blackName;
            // "wygrywa Kuba" / "wygrywają białe"
            string who = string.IsNullOrEmpty(w) ? (winner > 0 ? "ją białe" : "ją czarne") : " " + w;
            return end switch
            {
                GameEnd.Checkmate => $"Mat - wygrywa{who}",
                GameEnd.Stalemate => "Pat - remis",
                GameEnd.FiftyMoves => "Remis - 50 ruchów bez bicia i ruchu pionem",
                GameEnd.Repetition => "Remis - trzykrotne powtórzenie pozycji",
                GameEnd.Material => "Remis - brak materiału do mata",
                GameEnd.Resign => $"Poddanie - wygrywa{who}",
                GameEnd.Timeout => winner == 0 ? "Czas minął - remis (brak materiału)" : $"Koniec czasu - wygrywa{who}",
                GameEnd.Left => $"Przeciwnik wyszedł - wygrywa{who}",
                GameEnd.Aborted => "Partia przerwana",
                _ => "",
            };
        }
    }
}
