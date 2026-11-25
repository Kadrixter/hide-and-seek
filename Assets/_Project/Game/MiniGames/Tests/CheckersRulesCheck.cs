using System.Collections.Generic;
using System.Linq;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Rule checks of Polish 8x8 checkers on fixed positions (pure C#, run by the editor test
    /// CheckersRulesTests and usable anywhere): start moves, mandatory capture, maximum capture, a man capturing
    /// backwards, flying king captures (and the landing that keeps capturing), a blocked king, promotion only at the end
    /// of a move, endings (no pieces, no moves, repetition), the text form.
    /// </summary>
    public static class CheckersRulesCheck
    {
        /// <summary>Runs every check; <paramref name="report"/>(name, ok, detail) for each. Returns the number of failures.</summary>
        public static int Run(System.Action<string, bool, string> report)
        {
            int fail = 0;
            void Check(string name, bool ok, string detail = "") { if (!ok) fail++; report(name, ok, detail); }
            string Moves(CheckersBoard b) => string.Join(" ", b.LegalMoves().Select(m => m.ToString()).OrderBy(s => s));

            var start = new CheckersBoard();
            Check("start: 7 white moves", start.LegalMoves().Count == 7, Moves(start));
            Check("start: 12 + 12 men on dark squares", start.Count(1) == 12 && start.Count(-1) == 12 && Enumerable.Range(0, 64).All(s => start.Sq[s] == 0 || CheckersBoard.IsDark(s)));
            var bs = new CheckersBoard(); bs.Side = CheckersBoard.Black;
            Check("start: 7 black moves (downwards)", bs.LegalMoves().Count == 7 && bs.LegalMoves().All(m => ChessBoard.Rank(m.To) == 4), Moves(bs));
            Check("text form round trip", new CheckersBoard().Decode(start.Encode()) && start.Encode() == CheckersBoard.Start, start.Encode());

            // Mandatory capture: c3 must take d4 although g3 could move.
            var b = CheckersBoard.From(1, "c3 g3", "", "d4 h8", "");
            Check("mandatory capture", Moves(b) == "c3e5", Moves(b));
            Check("quiet moves exist besides (g3)", b.QuietMoves(new List<CheckersMove>()).Any(m => m.From == ChessBoard.ParseSq("g3")));

            // Maximum capture: e3xg5xe7 (2) beats a1xc3 (1).
            b = CheckersBoard.From(1, "a1 e3", "", "b2 f4 f6", "");
            var l = b.LegalMoves();
            Check("maximum capture wins", l.Count == 1 && l[0].ToString() == "e3g5e7" && l[0].Captured.Count == 2, Moves(b));
            // Equal counts: every sequence of the maximum length is allowed (a man's second jump may go backwards).
            b = CheckersBoard.From(1, "c3 a3", "", "d4 f6 b4", "");
            Check("all maximum sequences allowed", Moves(b) == "a3c5e3 c3e5g7", Moves(b));

            // A man captures backwards.
            b = CheckersBoard.From(1, "e5", "", "d4 a7", "");
            Check("man captures backwards", Moves(b) == "e5c3", Moves(b));
            // ... but moves only forwards.
            b = CheckersBoard.From(1, "e5", "", "a7", "");
            Check("man moves forwards only", Moves(b) == "e5d6 e5f6", Moves(b));

            // Flying king: from a1 over d4, any free square behind it.
            b = CheckersBoard.From(1, "", "a1", "d4 h2", "");
            Check("flying king capture: 4 landings", Moves(b) == "a1e5 a1f6 a1g7 a1h8", Moves(b));
            // The king must land where it can go on capturing (maximum rule): e5, then over f4 to g3 / h2.
            b = CheckersBoard.From(1, "", "a1", "d4 f4", "");
            Check("flying king: landing that continues", Moves(b) == "a1e5g3 a1e5h2", Moves(b));
            // Two pieces in a row cannot be jumped.
            b = CheckersBoard.From(1, "", "a1", "d4 e5 a7", "");
            Check("king cannot jump two in a row", b.LegalMoves().All(m => !m.IsCapture), Moves(b));
            // A king moves any distance.
            b = CheckersBoard.From(1, "", "a1", "h8", "");
            Check("king long move", b.LegalMoves().Count == 6 && b.LegalMoves().Any(m => m.ToString() == "a1g7"), Moves(b));

            // Promotion only at the end of the move: b6xd8 (last row) then back over e7 to f6 = still a man.
            b = CheckersBoard.From(1, "b6", "", "c7 e7", "");
            l = b.LegalMoves();
            bool through = l.Count == 1 && l[0].ToString() == "b6d8f6" && !l[0].Promotes;
            if (through) b.Make(l[0]);
            Check("passing the last row mid-capture does not promote", through && b.Sq[ChessBoard.ParseSq("f6")] == CheckersBoard.Man, Moves(CheckersBoard.From(1, "b6", "", "c7 e7", "")));
            b = CheckersBoard.From(1, "b6", "", "c7 h8", "");
            l = b.LegalMoves();
            bool ends = l.Count == 1 && l[0].ToString() == "b6d8" && l[0].Promotes;
            if (ends) b.Make(l[0]);
            Check("capture ending on the last row promotes", ends && b.Sq[ChessBoard.ParseSq("d8")] == CheckersBoard.King);
            b = CheckersBoard.From(-1, "", "", "c2", "");
            Check("black man promotes on rank 1", b.LegalMoves().All(m => m.Promotes) && b.LegalMoves().Count == 2, Moves(b));
            // A jumped disc stays until the move is over: the king may not jump it a second time on a loop.
            b = CheckersBoard.From(1, "", "d2", "e5 e3 c3 c5", "");
            l = b.LegalMoves();
            Check("king loop takes 4, each disc once", l.Count > 0 && l.All(m => m.Captured.Count == 4 && m.Captured.Distinct().Count() == 4) && l.Any(m => m.To == m.From), Moves(b));

            // Endings.
            var r = new CheckersReferee();
            r.Reset(CheckersBoard.From(1, "c3", "", "d4", "").Encode());
            bool ok = r.Play(new[] { ChessBoard.ParseSq("c3"), ChessBoard.ParseSq("e5") }, out _, out var end, out int win);
            Check("last piece taken = win (no pieces)", ok && end == GameEnd.NoPieces && win == 1, $"{end} {win}");
            r.Reset(CheckersBoard.From(1, "b4 c3 h2", "", "a5", "").Encode());
            ok = r.Play(new[] { ChessBoard.ParseSq("h2"), ChessBoard.ParseSq("g3") }, out _, out end, out win);
            Check("blocked side loses (no moves)", ok && end == GameEnd.NoMoves && win == 1, $"{end} {win}");
            r.Reset(CheckersBoard.From(1, "a3", "a1", "h6", "h2").Encode());
            int[] a1 = { ChessBoard.ParseSq("a1"), ChessBoard.ParseSq("b2") }, h8 = { ChessBoard.ParseSq("h2"), ChessBoard.ParseSq("g1") };
            end = GameEnd.None;
            for (int i = 0; i < 8 && end == GameEnd.None; i++)
            {
                bool back = (i / 2) % 2 == 1;
                var path = i % 2 == 0 ? (back ? new[] { a1[1], a1[0] } : a1) : (back ? new[] { h8[1], h8[0] } : h8);
                r.Play(path, out _, out end, out win);
            }
            Check("threefold repetition = draw", end == GameEnd.Repetition, $"{end}");
            Check("illegal move refused", !new CheckersReferee().Play(new[] { ChessBoard.ParseSq("c3"), ChessBoard.ParseSq("c4") }, out _, out _, out _));
            return fail;
        }
    }
}
