using System;
using System.Collections.Generic;
using System.Diagnostics;
using HideAndSeek.Game.MiniGames;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Rules tests for the lobby board games (batchmode: -executeMethod HideAndSeek.Game.Editor.MiniGameRulesTests.RunFromCommandLine).
    /// Perft node counts from well-known positions (start position to depth 4: 20 / 400 / 8902 / 197281), plus game
    /// endings: scholar's mate, stalemate, threefold repetition, 50 moves, insufficient material, castling rules,
    /// en passant, promotion, tic-tac-toe lines. Every check logs "[ChessTest]" / "[TttTest]"; exit code 0 = all passed.
    /// </summary>
    public static class MiniGameRulesTests
    {
        static int _fail, _pass;

        [MenuItem("HideAndSeek/Mini games/Run rules tests")]
        public static void RunMenu() => Run();

        public static void RunFromCommandLine()
        {
            bool ok = false;
            try { ok = Run(); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static void Check(string tag, string name, bool ok, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            Debug.Log($"[{tag}] {(ok ? "OK  " : "FAIL")} {name} {detail}");
        }

        public static bool Run()
        {
            _fail = _pass = 0;
            Perft("start", ChessBoard.StartFen, new long[] { 20, 400, 8902, 197281 });
            Perft("kiwipete", "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", new long[] { 48, 2039, 97862 });
            Perft("pos3", "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", new long[] { 14, 191, 2812, 43238 });
            Perft("pos4", "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", new long[] { 6, 264, 9467 });
            Perft("pos5", "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", new long[] { 44, 1486, 62379 });
            Endings();
            Ttt();
            Debug.Log($"[ChessTest] SUMMARY passed={_pass} failed={_fail}");
            return _fail == 0;
        }

        static void Perft(string name, string fen, long[] expect)
        {
            var b = new ChessBoard();
            b.SetFen(fen);
            for (int d = 1; d <= expect.Length; d++)
            {
                var sw = Stopwatch.StartNew();
                long n = b.Perft(d);
                Check("ChessTest", $"perft {name} d{d}", n == expect[d - 1], $"= {n} (expected {expect[d - 1]}, {sw.ElapsedMilliseconds} ms)");
            }
            Check("ChessTest", $"perft {name} board restored", b.ToFen() == new ChessBoardFen(fen).Normalized, b.ToFen());
        }

        /// <summary>FEN as ToFen writes it (en passant only when capturable).</summary>
        readonly struct ChessBoardFen
        {
            public readonly string Normalized;
            public ChessBoardFen(string fen) { var b = new ChessBoard(); b.SetFen(fen); Normalized = b.ToFen(); }
        }

        static bool Moves(ChessReferee r, string moves, out GameEnd end, out int winner)
        {
            end = GameEnd.None; winner = 0;
            foreach (var m in moves.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int promo = m.Length > 4 ? " pnbrqk".IndexOf(m[4]) : 0;
                if (!r.Play(ChessBoard.ParseSq(m, 0), ChessBoard.ParseSq(m, 2), promo, out _, out end, out winner))
                {
                    Debug.Log($"[ChessTest] illegal move {m} in {r.Board.ToFen()}");
                    return false;
                }
            }
            return true;
        }

        static void Endings()
        {
            var r = new ChessReferee();
            bool ok = Moves(r, "e2e4 e7e5 d1h5 b8c6 f1c4 g8f6 h5f7", out var end, out int win);
            Check("ChessTest", "scholar's mate", ok && end == GameEnd.Checkmate && win == 1, $"{end} winner={win}");

            r.Reset("7k/8/6QK/8/8/8/8/8 w - - 0 1");
            ok = Moves(r, "g6f7", out end, out _);
            Check("ChessTest", "stalemate", ok && end == GameEnd.Stalemate, $"{end} {r.Board.ToFen()}");

            r.Reset(ChessBoard.StartFen);
            ok = Moves(r, "g1f3 g8f6 f3g1 f6g8 g1f3 g8f6 f3g1 f6g8", out end, out _);
            Check("ChessTest", "threefold repetition", ok && end == GameEnd.Repetition, $"{end}");

            r.Reset("4k3/8/8/8/8/8/R7/4K3 w - - 98 80");
            ok = Moves(r, "a2a3", out end, out _);
            Check("ChessTest", "50 moves at 99 half-moves = not yet", ok && end == GameEnd.None, $"{end} hm={r.Board.Halfmove}");
            ok = Moves(r, "e8d8", out end, out _);
            Check("ChessTest", "50 moves", ok && end == GameEnd.FiftyMoves, $"{end} hm={r.Board.Halfmove}");

            r.Reset("4k3/8/8/8/8/8/3n4/4K3 w - - 0 1");
            ok = Moves(r, "e1d2", out end, out _);
            Check("ChessTest", "insufficient material (KxN)", ok && end == GameEnd.Material, $"{end}");
            r.Reset("4k3/8/3b4/8/8/8/8/2B1K3 w - - 0 1");
            Check("ChessTest", "same-colour bishops = material draw", r.Board.InsufficientMaterial());
            r.Reset("4k3/8/2b5/8/8/8/8/2B1K3 w - - 0 1");
            Check("ChessTest", "opposite bishops = playable", !r.Board.InsufficientMaterial());

            // Castling: both sides, then through check refused.
            r.Reset("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            ok = Moves(r, "e1g1 e8c8", out end, out _);
            Check("ChessTest", "castling O-O / O-O-O", ok && r.Board.Sq[6] == ChessBoard.King && r.Board.Sq[5] == ChessBoard.Rook &&
                r.Board.Sq[58] == -ChessBoard.King && r.Board.Sq[59] == -ChessBoard.Rook, r.Board.ToFen());
            r.Reset("r3k2r/8/8/8/8/8/5r2/R3K2R w KQkq - 0 1"); // f2 rook: e1 is in check? no - f-file attacks f1
            Check("ChessTest", "no castling through an attacked square", !r.Board.FindLegal(4, 6, 0, out _));
            r.Reset("r3k2r/8/8/8/8/8/8/R3K1NR w KQkq - 0 1");
            Check("ChessTest", "no castling through a piece", !r.Board.FindLegal(4, 6, 0, out _));
            r.Reset("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            ok = Moves(r, "h1h2 a8a7 h2h1 a7a8", out end, out _);
            Check("ChessTest", "rights lost after a rook move", ok && !r.Board.FindLegal(4, 6, 0, out _) && r.Board.FindLegal(4, 2, 0, out _));

            // En passant: only right after the double step.
            r.Reset("4k3/3p4/8/4P3/8/8/8/4K3 b - - 0 1");
            ok = Moves(r, "d7d5 e5d6", out end, out _);
            Check("ChessTest", "en passant", ok && r.Board.Sq[ChessBoard.ParseSq("d5")] == 0 && r.Board.Sq[ChessBoard.ParseSq("d6")] == ChessBoard.Pawn, r.Board.ToFen());
            r.Reset("4k3/3p4/8/4P3/8/8/8/4K3 b - - 0 1");
            ok = Moves(r, "d7d5 e1d1 e8d8", out end, out _);
            Check("ChessTest", "en passant expires", ok && !r.Board.FindLegal(ChessBoard.ParseSq("e5"), ChessBoard.ParseSq("d6"), 0, out _));

            // Promotion to a knight with check.
            r.Reset("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1");
            ok = Moves(r, "b7b8n", out end, out _);
            Check("ChessTest", "promotion (knight)", ok && r.Board.Sq[ChessBoard.ParseSq("b8")] == ChessBoard.Knight, r.Board.ToFen());
            r.Reset("4k3/1P6/8/8/8/8/8/4K3 w - - 0 1");
            ok = Moves(r, "b7b8", out end, out _);
            Check("ChessTest", "promotion default queen + check", ok && r.Board.Sq[ChessBoard.ParseSq("b8")] == ChessBoard.Queen && r.Board.InCheck(-1));

            // Pinned piece cannot move; illegal move rejected.
            r.Reset("4k3/4r3/8/8/8/8/4B3/4K3 w - - 0 1");
            Check("ChessTest", "pinned bishop", !r.Board.FindLegal(ChessBoard.ParseSq("e2"), ChessBoard.ParseSq("d3"), 0, out _));
            Check("ChessTest", "illegal move rejected", !new ChessReferee().Play(ChessBoard.ParseSq("e2"), ChessBoard.ParseSq("e5"), 0, out _, out _, out _));

            // State text round trip.
            var s = new ChessState { Seq = 7, A = 3, B = ChessState.Nobody, Fen = r.Board.ToFen(), Last = "e2e4", TurnStart = 123.456, WhiteMs = 300000 };
            var d = new ChessState();
            Check("ChessTest", "state encode/decode", ChessState.TryDecode(s.Encode(), d) && d.Encode() == s.Encode() && d.Encode().Length < 400, s.Encode());
        }

        static void Ttt()
        {
            Check("TttTest", "row", TttRules.WinLine("XXX.O.O..") == 0);
            Check("TttTest", "diagonal", TttRules.WinLine("O.X.OX..O") == 6);
            Check("TttTest", "anti-diagonal", TttRules.WinLine("..X.X.X..") == 7);
            Check("TttTest", "none", TttRules.WinLine("XOXXOOOXX") == -1 && TttRules.Full("XOXXOOOXX"));
            var s = new TttState { Seq = 3, A = 1, B = 2, Cells = "X.O......", Line = -1 };
            var d = new TttState();
            Check("TttTest", "state encode/decode", TttState.TryDecode(s.Encode(), d) && d.Encode() == s.Encode());
        }
    }
}
