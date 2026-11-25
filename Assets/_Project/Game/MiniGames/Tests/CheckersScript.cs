using System.Collections.Generic;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// A whole checkers game for the two-instance test, found by simulation (pure rules, the same on host and client):
    /// seeded random games with a slight preference for promotions are played until one ends by a win within
    /// <see cref="MaxPlies"/> half-moves and shows everything the test wants to see on the real table - a multi-capture,
    /// a promotion, a king's long move (3+ squares), a king's flying capture, and a position where capturing is
    /// mandatory while another disc could have made a quiet move.
    /// </summary>
    public static class CheckersScript
    {
        public const int MaxPlies = 90;

        public sealed class Result
        {
            public int Seed;
            public readonly List<string> Moves = new();
            public GameEnd End;
            public int Winner;
            /// <summary>Ply of the first multi-capture / promotion / long king move / flying king capture / "must capture although a quiet move exists".</summary>
            public int MultiAt = -1, PromoteAt = -1, KingLongAt = -1, KingCaptureAt = -1, MandatoryAt = -1;
            public int MaxCaptured;

            public override string ToString() =>
                $"seed={Seed} plies={Moves.Count} end={End} winner={Winner} multi@{MultiAt} (max {MaxCaptured}) promote@{PromoteAt} kingLong@{KingLongAt} kingCapture@{KingCaptureAt} mandatory@{MandatoryAt}";
        }

        /// <summary>First seed (from <paramref name="firstSeed"/>) whose game has everything; null when none in <paramref name="tries"/>.</summary>
        public static Result Find(int firstSeed = 1, int tries = 20000)
        {
            for (int seed = firstSeed; seed < firstSeed + tries; seed++)
            {
                var r = Play(seed);
                if (r != null && r.MultiAt >= 0 && r.PromoteAt >= 0 && r.KingLongAt >= 0 && r.KingCaptureAt >= 0 && r.MandatoryAt >= 0) return r;
            }
            return null;
        }

        /// <summary>One simulated game; null when it does not end with a win in time.</summary>
        public static Result Play(int seed)
        {
            var rnd = new System.Random(seed);
            var referee = new CheckersReferee();
            var b = referee.Board;
            var r = new Result { Seed = seed };
            var legal = new List<CheckersMove>();
            var quiet = new List<CheckersMove>();
            for (int ply = 0; ply < MaxPlies; ply++)
            {
                b.LegalMoves(legal);
                if (legal.Count == 0) return null;
                if (legal[0].IsCapture && r.MandatoryAt < 0)
                {
                    quiet.Clear();
                    b.QuietMoves(quiet);
                    bool other = false;
                    foreach (var q in quiet)
                    {
                        bool capturer = false;
                        foreach (var m in legal) if (m.From == q.From) { capturer = true; break; }
                        if (!capturer) { other = true; break; }
                    }
                    if (other) r.MandatoryAt = ply;
                }
                CheckersMove pick = null;
                foreach (var m in legal) if (m.Promotes && rnd.NextDouble() < 0.8) { pick = m; break; }
                pick ??= legal[rnd.Next(legal.Count)];
                int p = b.Sq[pick.From];
                bool king = p == CheckersBoard.King || p == -CheckersBoard.King;
                if (pick.Captured.Count >= 2 && r.MultiAt < 0) r.MultiAt = ply;
                if (pick.Captured.Count > r.MaxCaptured) r.MaxCaptured = pick.Captured.Count;
                if (pick.Promotes && r.PromoteAt < 0) r.PromoteAt = ply;
                if (king && !pick.IsCapture && System.Math.Abs(ChessBoard.File(pick.To) - ChessBoard.File(pick.From)) >= 3 && r.KingLongAt < 0) r.KingLongAt = ply;
                if (king && pick.IsCapture && r.KingCaptureAt < 0 && FlyingCapture(pick)) r.KingCaptureAt = ply;
                string text = pick.ToString();
                referee.Play(pick.Path, out _, out var end, out int winner);
                r.Moves.Add(text);
                if (end != GameEnd.None)
                {
                    r.End = end;
                    r.Winner = winner;
                    return end == GameEnd.NoPieces || end == GameEnd.NoMoves ? r : null;
                }
            }
            return null;
        }

        /// <summary>A king's capture with a jump longer than two squares (it flew to or past the jumped disc).</summary>
        static bool FlyingCapture(CheckersMove m)
        {
            for (int i = 0; i + 1 < m.Path.Count; i++)
                if (System.Math.Abs(ChessBoard.File(m.Path[i + 1]) - ChessBoard.File(m.Path[i])) > 2) return true;
            return false;
        }
    }
}
