using System.Collections.Generic;
using System.Text;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>One chess move: squares 0..63 (a1 = 0, h8 = 63), promotion piece type (0 = none) and flags.</summary>
    public struct ChessMove : System.IEquatable<ChessMove>
    {
        public const byte Capture = 1, EnPassant = 2, Castle = 4, DoublePush = 8;
        public byte From, To;
        /// <summary>Piece type the pawn becomes (Knight..Queen), 0 = not a promotion.</summary>
        public byte Promo;
        public byte Flags;

        public ChessMove(int from, int to, int promo = 0, int flags = 0)
        {
            From = (byte)from; To = (byte)to; Promo = (byte)promo; Flags = (byte)flags;
        }

        public bool IsCapture => (Flags & (Capture | EnPassant)) != 0;
        public bool Equals(ChessMove o) => From == o.From && To == o.To && Promo == o.Promo;
        public override bool Equals(object o) => o is ChessMove m && Equals(m);
        public override int GetHashCode() => From | To << 8 | Promo << 16;

        /// <summary>Coordinate notation ("e2e4", "e7e8q").</summary>
        public override string ToString() => ChessBoard.SqName(From) + ChessBoard.SqName(To) + (Promo != 0 ? "" + "  nbrq"[Promo] : "");
    }

    /// <summary>
    /// Chess position with full rules: legal move generation (castling through / out of check forbidden, en passant,
    /// promotion), check, make / unmake, FEN in and out, insufficient material. Pieces are signed bytes: white &gt; 0,
    /// black &lt; 0, type = |value| (1 pawn, 2 knight, 3 bishop, 4 rook, 5 queen, 6 king). Squares: rank * 8 + file.
    /// Pure C#, no Unity: the server referees with it and every client uses it for highlights and animations.
    /// </summary>
    public sealed partial class ChessBoard
    {
        public const int Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6;
        public const int White = 1, Black = -1;
        public const int CastleWK = 1, CastleWQ = 2, CastleBK = 4, CastleBQ = 8;
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public readonly sbyte[] Sq = new sbyte[64];
        /// <summary>Side to move: +1 white, -1 black.</summary>
        public int Side = White;
        public int Castling;
        /// <summary>En passant target square (the one the capturing pawn lands on), -1 = none.</summary>
        public int Ep = -1;
        public int Halfmove;
        public int Fullmove = 1;

        public struct Undo
        {
            public sbyte Captured;
            public int Castling, Ep, Halfmove, Fullmove;
        }

        public static int File(int s) => s & 7;
        public static int Rank(int s) => s >> 3;
        public static string SqName(int s) => s < 0 || s > 63 ? "-" : "" + (char)('a' + File(s)) + (char)('1' + Rank(s));
        public static int ParseSq(string t, int at = 0) =>
            t == null || t.Length < at + 2 || t[at] < 'a' || t[at] > 'h' || t[at + 1] < '1' || t[at + 1] > '8' ? -1 : (t[at + 1] - '1') * 8 + (t[at] - 'a');

        public ChessBoard() => SetFen(StartFen);

        public ChessBoard Clone()
        {
            var b = new ChessBoard();
            CopyTo(b);
            return b;
        }

        public void CopyTo(ChessBoard b)
        {
            System.Array.Copy(Sq, b.Sq, 64);
            b.Side = Side; b.Castling = Castling; b.Ep = Ep; b.Halfmove = Halfmove; b.Fullmove = Fullmove;
        }

        // ================================================================ FEN

        public bool SetFen(string fen)
        {
            if (string.IsNullOrEmpty(fen)) return false;
            var parts = fen.Trim().Split(' ');
            if (parts.Length < 2) return false;
            var sq = new sbyte[64];
            int rank = 7, file = 0;
            foreach (char c in parts[0])
            {
                if (c == '/') { rank--; file = 0; continue; }
                if (c >= '1' && c <= '8') { file += c - '0'; continue; }
                int t = "pnbrqk".IndexOf(char.ToLowerInvariant(c)) + 1;
                if (t == 0 || rank < 0 || file > 7) return false;
                sq[rank * 8 + file] = (sbyte)(char.IsUpper(c) ? t : -t);
                file++;
            }
            System.Array.Copy(sq, Sq, 64);
            Side = parts[1] == "b" ? Black : White;
            Castling = 0;
            if (parts.Length > 2)
                foreach (char c in parts[2])
                    Castling |= c == 'K' ? CastleWK : c == 'Q' ? CastleWQ : c == 'k' ? CastleBK : c == 'q' ? CastleBQ : 0;
            Ep = parts.Length > 3 ? ParseSq(parts[3]) : -1;
            Halfmove = parts.Length > 4 && int.TryParse(parts[4], out int h) ? h : 0;
            Fullmove = parts.Length > 5 && int.TryParse(parts[5], out int f) ? f : 1;
            return true;
        }

        /// <summary>Placement, side, castling and en passant (the part that counts for repetition).</summary>
        public string PositionKey()
        {
            var sb = new StringBuilder(80);
            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int f = 0; f < 8; f++)
                {
                    int p = Sq[r * 8 + f];
                    if (p == 0) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    char c = " pnbrqk"[p < 0 ? -p : p];
                    sb.Append(p > 0 ? char.ToUpperInvariant(c) : c);
                }
                if (empty > 0) sb.Append(empty);
                if (r > 0) sb.Append('/');
            }
            sb.Append(Side == White ? " w " : " b ");
            if (Castling == 0) sb.Append('-');
            else
            {
                if ((Castling & CastleWK) != 0) sb.Append('K');
                if ((Castling & CastleWQ) != 0) sb.Append('Q');
                if ((Castling & CastleBK) != 0) sb.Append('k');
                if ((Castling & CastleBQ) != 0) sb.Append('q');
            }
            // FIDE: the en passant square only matters when a capture is actually possible.
            sb.Append(' ').Append(EpCapturable() ? SqName(Ep) : "-");
            return sb.ToString();
        }

        public string ToFen() => PositionKey() + " " + Halfmove + " " + Fullmove;

        bool EpCapturable()
        {
            if (Ep < 0) return false;
            int from = Ep - 8 * Side;
            foreach (int df in new[] { -1, 1 })
            {
                int f = File(from) + df;
                if (f < 0 || f > 7) continue;
                if (Sq[Rank(from) * 8 + f] == Pawn * Side) return true;
            }
            return false;
        }

        // ================================================================ make / unmake

        public Undo Make(ChessMove m)
        {
            var u = new Undo { Captured = Sq[m.To], Castling = Castling, Ep = Ep, Halfmove = Halfmove, Fullmove = Fullmove };
            sbyte p = Sq[m.From];
            int type = p < 0 ? -p : p;
            Sq[m.To] = m.Promo != 0 ? (sbyte)(m.Promo * Side) : p;
            Sq[m.From] = 0;
            if ((m.Flags & ChessMove.EnPassant) != 0)
            {
                int cap = m.To - 8 * Side;
                u.Captured = Sq[cap];
                Sq[cap] = 0;
            }
            if ((m.Flags & ChessMove.Castle) != 0)
            {
                CastleRook(m, out int rf, out int rt);
                Sq[rt] = Sq[rf];
                Sq[rf] = 0;
            }
            Ep = (m.Flags & ChessMove.DoublePush) != 0 ? (m.From + m.To) / 2 : -1;
            Halfmove = type == Pawn || u.Captured != 0 ? 0 : Halfmove + 1;
            if (Side == Black) Fullmove++;
            // Castling rights go when the king or a rook moves, or a rook is captured on its home square.
            Castling &= RightsMask(m.From) & RightsMask(m.To);
            Side = -Side;
            return u;
        }

        public void Unmake(ChessMove m, Undo u)
        {
            Side = -Side;
            sbyte p = Sq[m.To];
            Sq[m.From] = m.Promo != 0 ? (sbyte)(Pawn * Side) : p;
            Sq[m.To] = 0;
            if ((m.Flags & ChessMove.EnPassant) != 0) Sq[m.To - 8 * Side] = u.Captured;
            else Sq[m.To] = u.Captured;
            if ((m.Flags & ChessMove.Castle) != 0)
            {
                CastleRook(m, out int rf, out int rt);
                Sq[rf] = Sq[rt];
                Sq[rt] = 0;
            }
            Castling = u.Castling; Ep = u.Ep; Halfmove = u.Halfmove; Fullmove = u.Fullmove;
        }

        /// <summary>Rook squares of a castling move (king e1 -> g1: rook h1 -> f1, and so on).</summary>
        public static void CastleRook(ChessMove m, out int rookFrom, out int rookTo)
        {
            bool kingSide = m.To > m.From;
            int baseSq = Rank(m.From) * 8;
            rookFrom = baseSq + (kingSide ? 7 : 0);
            rookTo = baseSq + (kingSide ? 5 : 3);
        }

        static int RightsMask(int s) => s switch
        {
            0 => ~CastleWQ, 7 => ~CastleWK, 4 => ~(CastleWK | CastleWQ),
            56 => ~CastleBQ, 63 => ~CastleBK, 60 => ~(CastleBK | CastleBQ),
            _ => ~0,
        };

        // ================================================================ draws

        /// <summary>No mate possible: K-K, K+minor vs K, K+B vs K+B with bishops on the same colour.</summary>
        public bool InsufficientMaterial()
        {
            int minorsW = 0, minorsB = 0, bishopColours = 0, bishops = 0;
            for (int s = 0; s < 64; s++)
            {
                int p = Sq[s], t = p < 0 ? -p : p;
                if (t == 0 || t == King) continue;
                if (t == Pawn || t == Rook || t == Queen) return false;
                if (p > 0) minorsW++; else minorsB++;
                if (t == Bishop) { bishops++; bishopColours |= 1 << ((File(s) + Rank(s)) & 1); }
            }
            if (minorsW + minorsB <= 1) return true;
            // Only bishops left (any number), all on one square colour.
            return bishops == minorsW + minorsB && bishopColours != 3;
        }

        public int KingSquare(int side)
        {
            sbyte k = (sbyte)(King * side);
            for (int s = 0; s < 64; s++) if (Sq[s] == k) return s;
            return -1;
        }

        public bool InCheck(int side)
        {
            int k = KingSquare(side);
            return k >= 0 && Attacked(k, -side);
        }

        /// <summary>All legal moves for the side to move.</summary>
        public List<ChessMove> LegalMoves(List<ChessMove> into = null)
        {
            into ??= new List<ChessMove>(48);
            into.Clear();
            var pseudo = new List<ChessMove>(64);
            Pseudo(pseudo);
            int side = Side;
            foreach (var m in pseudo)
            {
                var u = Make(m);
                if (!InCheck(side)) into.Add(m);
                Unmake(m, u);
            }
            return into;
        }

        /// <summary>The legal move from -> to (promotion defaults to a queen when <paramref name="promo"/> is 0).</summary>
        public bool FindLegal(int from, int to, int promo, out ChessMove move)
        {
            foreach (var m in LegalMoves())
            {
                if (m.From != from || m.To != to) continue;
                if (m.Promo != 0 && m.Promo != (promo == 0 ? Queen : promo)) continue;
                move = m;
                return true;
            }
            move = default;
            return false;
        }

        public long Perft(int depth)
        {
            if (depth == 0) return 1;
            var moves = LegalMoves(new List<ChessMove>(48));
            if (depth == 1) return moves.Count;
            long n = 0;
            foreach (var m in moves)
            {
                var u = Make(m);
                n += Perft(depth - 1);
                Unmake(m, u);
            }
            return n;
        }
    }
}
