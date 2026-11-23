using System.Collections.Generic;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>Pseudo-legal move generation and attack tests (<see cref="ChessBoard"/>).</summary>
    public sealed partial class ChessBoard
    {
        static readonly int[] KnightD = { 17, 15, 10, 6, -6, -10, -15, -17 };
        static readonly int[] KnightDf = { 1, -1, 2, -2, 2, -2, 1, -1 };
        static readonly int[] KingD = { 1, -1, 8, -8, 9, 7, -7, -9 };
        static readonly int[] KingDf = { 1, -1, 0, 0, 1, -1, 1, -1 };
        static readonly int[] RookD = { 1, -1, 8, -8 };
        static readonly int[] RookDf = { 1, -1, 0, 0 };
        static readonly int[] BishopD = { 9, 7, -7, -9 };
        static readonly int[] BishopDf = { 1, -1, 1, -1 };

        /// <summary>True when <paramref name="s"/> is attacked by a piece of <paramref name="by"/>.</summary>
        public bool Attacked(int s, int by)
        {
            int f = File(s);
            // Pawns: a white pawn attacks from one rank below.
            int pr = Rank(s) - by;
            if (pr >= 0 && pr <= 7)
            {
                if (f > 0 && Sq[pr * 8 + f - 1] == Pawn * by) return true;
                if (f < 7 && Sq[pr * 8 + f + 1] == Pawn * by) return true;
            }
            for (int i = 0; i < 8; i++)
            {
                int t = s + KnightD[i], tf = f + KnightDf[i];
                if (t >= 0 && t < 64 && tf >= 0 && tf < 8 && Sq[t] == Knight * by) return true;
                t = s + KingD[i]; tf = f + KingDf[i];
                if (t >= 0 && t < 64 && tf >= 0 && tf < 8 && Sq[t] == King * by) return true;
            }
            if (Slides(s, by, RookD, RookDf, Rook)) return true;
            return Slides(s, by, BishopD, BishopDf, Bishop);
        }

        bool Slides(int s, int by, int[] d, int[] df, int type)
        {
            for (int i = 0; i < 4; i++)
            {
                int t = s, tf = File(s);
                while (true)
                {
                    t += d[i]; tf += df[i];
                    if (t < 0 || t > 63 || tf < 0 || tf > 7) break;
                    int p = Sq[t];
                    if (p == 0) continue;
                    if (p == type * by || p == Queen * by) return true;
                    break;
                }
            }
            return false;
        }

        void Pseudo(List<ChessMove> list)
        {
            int side = Side;
            for (int s = 0; s < 64; s++)
            {
                int p = Sq[s];
                if (p == 0 || (p > 0) != (side > 0)) continue;
                int t = p < 0 ? -p : p;
                switch (t)
                {
                    case Pawn: PawnMoves(s, list); break;
                    case Knight: Steps(s, KnightD, KnightDf, list); break;
                    case King: Steps(s, KingD, KingDf, list); CastleMoves(s, list); break;
                    case Rook: Rays(s, RookD, RookDf, list); break;
                    case Bishop: Rays(s, BishopD, BishopDf, list); break;
                    case Queen: Rays(s, RookD, RookDf, list); Rays(s, BishopD, BishopDf, list); break;
                }
            }
        }

        void Add(List<ChessMove> list, int from, int to)
        {
            int target = Sq[to];
            list.Add(new ChessMove(from, to, 0, target != 0 ? ChessMove.Capture : 0));
        }

        bool Enemy(int s) => Sq[s] != 0 && (Sq[s] > 0) != (Side > 0);

        void Steps(int s, int[] d, int[] df, List<ChessMove> list)
        {
            int f = File(s);
            for (int i = 0; i < d.Length; i++)
            {
                int t = s + d[i], tf = f + df[i];
                if (t < 0 || t > 63 || tf < 0 || tf > 7) continue;
                if (Sq[t] == 0 || Enemy(t)) Add(list, s, t);
            }
        }

        void Rays(int s, int[] d, int[] df, List<ChessMove> list)
        {
            for (int i = 0; i < 4; i++)
            {
                int t = s, tf = File(s);
                while (true)
                {
                    t += d[i]; tf += df[i];
                    if (t < 0 || t > 63 || tf < 0 || tf > 7) break;
                    if (Sq[t] == 0) { Add(list, s, t); continue; }
                    if (Enemy(t)) Add(list, s, t);
                    break;
                }
            }
        }

        void PawnMoves(int s, List<ChessMove> list)
        {
            int side = Side, f = File(s), r = Rank(s);
            int fwd = s + 8 * side;
            int lastRank = side > 0 ? 7 : 0, startRank = side > 0 ? 1 : 6;
            if (fwd >= 0 && fwd < 64 && Sq[fwd] == 0)
            {
                PawnAdd(list, s, fwd, 0);
                int two = fwd + 8 * side;
                if (r == startRank && Sq[two] == 0) list.Add(new ChessMove(s, two, 0, ChessMove.DoublePush));
            }
            foreach (int df in new[] { -1, 1 })
            {
                int tf = f + df;
                if (tf < 0 || tf > 7) continue;
                int t = fwd + df;
                if (t < 0 || t > 63) continue;
                if (Enemy(t)) PawnAdd(list, s, t, ChessMove.Capture);
                else if (t == Ep) list.Add(new ChessMove(s, t, 0, ChessMove.EnPassant));
            }

            void PawnAdd(List<ChessMove> l, int from, int to, int flags)
            {
                if (Rank(to) == lastRank)
                    for (int pr = Queen; pr >= Knight; pr--) l.Add(new ChessMove(from, to, pr, flags));
                else l.Add(new ChessMove(from, to, 0, flags));
            }
        }

        void CastleMoves(int s, List<ChessMove> list)
        {
            int side = Side;
            int home = side > 0 ? 4 : 60;
            if (s != home || Attacked(s, -side)) return;
            int kRight = side > 0 ? CastleWK : CastleBK, qRight = side > 0 ? CastleWQ : CastleBQ;
            if ((Castling & kRight) != 0 && Sq[s + 1] == 0 && Sq[s + 2] == 0 && Sq[s + 3] == Rook * side &&
                !Attacked(s + 1, -side) && !Attacked(s + 2, -side))
                list.Add(new ChessMove(s, s + 2, 0, ChessMove.Castle));
            if ((Castling & qRight) != 0 && Sq[s - 1] == 0 && Sq[s - 2] == 0 && Sq[s - 3] == 0 && Sq[s - 4] == Rook * side &&
                !Attacked(s - 1, -side) && !Attacked(s - 2, -side))
                list.Add(new ChessMove(s, s - 2, 0, ChessMove.Castle));
        }
    }
}
