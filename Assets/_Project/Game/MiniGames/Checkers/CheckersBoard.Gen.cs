using System.Collections.Generic;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Legal moves of Polish 8x8 checkers: capturing is mandatory and the sequence taking the most pieces must be
    /// played; men move one square diagonally forward but capture forwards and backwards; kings fly (any distance,
    /// moving and capturing, landing on any free square behind the jumped piece). Jumped pieces stay on the board until
    /// the move is over (they block and cannot be jumped twice). A man promotes only when the move ENDS on the far row
    /// (passing it during a capture that goes on does not count).
    /// </summary>
    public sealed partial class CheckersBoard
    {
        static readonly int[] DF = { 1, -1, 1, -1 }, DR = { 1, 1, -1, -1 };

        static bool On(int f, int r) => f >= 0 && f < 8 && r >= 0 && r < 8;

        bool Enemy(int sq, int colour) => Sq[sq] != 0 && (Sq[sq] > 0) != (colour > 0);

        /// <summary>All legal moves of the side to move (captures of the maximum length, else simple moves).</summary>
        public List<CheckersMove> LegalMoves(List<CheckersMove> into = null)
        {
            into ??= new List<CheckersMove>();
            into.Clear();
            int best = 0;
            var path = new List<int>(16);
            var caps = new List<int>(16);
            for (int s = 0; s < 64; s++)
            {
                int p = Sq[s];
                if (p == 0 || (p > 0) != (Side > 0)) continue;
                path.Clear(); caps.Clear();
                path.Add(s);
                Capture(s, p, s, 0UL, path, caps, into, ref best);
            }
            if (into.Count > 0) return into;
            return QuietMoves(into);
        }

        /// <summary>Moves without a capture (all the side to move would have if capturing were not mandatory); appended to <paramref name="into"/>.</summary>
        public List<CheckersMove> QuietMoves(List<CheckersMove> into)
        {
            for (int s = 0; s < 64; s++)
            {
                int p = Sq[s];
                if (p == 0 || (p > 0) != (Side > 0)) continue;
                bool king = p == King || p == -King;
                for (int d = 0; d < 4; d++)
                {
                    if (!king && DR[d] != (p > 0 ? 1 : -1)) continue;
                    int f = ChessBoard.File(s) + DF[d], r = ChessBoard.Rank(s) + DR[d];
                    while (On(f, r) && Sq[r * 8 + f] == 0)
                    {
                        var m = new CheckersMove();
                        m.Path.Add(s); m.Path.Add(r * 8 + f);
                        m.Promotes = !king && r == PromotionRank(p);
                        into.Add(m);
                        if (!king) break;
                        f += DF[d]; r += DR[d];
                    }
                }
            }
            return into;
        }

        /// <summary>Depth-first search of capture sequences from <paramref name="at"/> (origin square counts as empty).</summary>
        void Capture(int origin, int piece, int at, ulong taken, List<int> path, List<int> caps, List<CheckersMove> into, ref int best)
        {
            bool king = piece == King || piece == -King;
            bool more = false;
            int f0 = ChessBoard.File(at), r0 = ChessBoard.Rank(at);
            for (int d = 0; d < 4; d++)
            {
                int f = f0 + DF[d], r = r0 + DR[d];
                if (king)
                    while (On(f, r) && (Sq[r * 8 + f] == 0 || r * 8 + f == origin)) { f += DF[d]; r += DR[d]; }
                if (!On(f, r)) continue;
                int over = r * 8 + f;
                if (over == origin || !Enemy(over, piece) || (taken & (1UL << over)) != 0) continue;
                f += DF[d]; r += DR[d];
                while (On(f, r) && (Sq[r * 8 + f] == 0 || r * 8 + f == origin))
                {
                    int land = r * 8 + f;
                    more = true;
                    path.Add(land); caps.Add(over);
                    Capture(origin, piece, land, taken | (1UL << over), path, caps, into, ref best);
                    path.RemoveAt(path.Count - 1); caps.RemoveAt(caps.Count - 1);
                    if (!king) break;
                    f += DF[d]; r += DR[d];
                }
            }
            if (more || caps.Count == 0) return;
            if (caps.Count < best) return;
            if (caps.Count > best) { best = caps.Count; into.Clear(); }
            var m = new CheckersMove();
            m.Path.AddRange(path);
            m.Captured.AddRange(caps);
            m.Promotes = !king && ChessBoard.Rank(at) == PromotionRank(piece);
            into.Add(m);
        }

        /// <summary>The legal move with exactly this path (start, landings, end).</summary>
        public bool FindLegal(IList<int> path, out CheckersMove move)
        {
            move = null;
            foreach (var m in LegalMoves())
                if (m.SamePath(path)) { move = m; return true; }
            return false;
        }

        /// <summary>The side to move must capture.</summary>
        public bool MustCapture()
        {
            var l = LegalMoves();
            return l.Count > 0 && l[0].IsCapture;
        }
    }
}
