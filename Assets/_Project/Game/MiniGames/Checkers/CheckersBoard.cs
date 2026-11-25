using System.Text;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Position of Polish 8x8 checkers (Brazilian rules): the dark squares of the chess board (a1 is dark), 12 men a
    /// side, white on ranks 1-3 moving up, black on ranks 6-8 moving down, white starts. <see cref="Sq"/> uses the chess
    /// numbering (a1 = 0): +1 white man, +2 white king, -1 black man, -2 black king. Text form (<see cref="Encode"/>):
    /// the 32 dark squares from a1 upwards ('.', w, W, b, B), a space and the side to move ("w" / "b").
    /// Move generation (mandatory maximum capture, flying kings) is in CheckersBoard.Gen.
    /// </summary>
    public sealed partial class CheckersBoard
    {
        public const int Man = 1, King = 2, White = 1, Black = -1;
        public const string Start = "wwwwwwwwwwww........bbbbbbbbbbbb w";

        public readonly sbyte[] Sq = new sbyte[64];
        public int Side = White;

        public CheckersBoard() => Decode(Start);

        public static bool IsDark(int sq) => ((ChessBoard.File(sq) + ChessBoard.Rank(sq)) & 1) == 0;

        /// <summary>Last rank of a colour (where its men become kings).</summary>
        public static int PromotionRank(int colour) => colour > 0 ? 7 : 0;

        public void CopyTo(CheckersBoard b)
        {
            System.Array.Copy(Sq, b.Sq, 64);
            b.Side = Side;
        }

        public string Encode()
        {
            var sb = new StringBuilder(34);
            for (int s = 0; s < 64; s++)
            {
                if (!IsDark(s)) continue;
                sb.Append(Sq[s] switch { 1 => 'w', 2 => 'W', -1 => 'b', -2 => 'B', _ => '.' });
            }
            sb.Append(Side > 0 ? " w" : " b");
            return sb.ToString();
        }

        public bool Decode(string text)
        {
            if (text == null || text.Length < 34) return false;
            var tmp = new sbyte[64];
            int i = 0;
            for (int s = 0; s < 64; s++)
            {
                if (!IsDark(s)) continue;
                char c = text[i++];
                sbyte v = c switch { 'w' => 1, 'W' => 2, 'b' => -1, 'B' => -2, '.' => 0, _ => (sbyte)99 };
                if (v == 99) return false;
                tmp[s] = v;
            }
            if (text[32] != ' ' || (text[33] != 'w' && text[33] != 'b')) return false;
            System.Array.Copy(tmp, Sq, 64);
            Side = text[33] == 'w' ? White : Black;
            return true;
        }

        /// <summary>Plays a legal move: the piece goes to the end square (a man on the far row turns king), the jumped pieces leave.</summary>
        public void Make(CheckersMove m)
        {
            sbyte p = Sq[m.From];
            Sq[m.From] = 0;
            foreach (int c in m.Captured) Sq[c] = 0;
            if (m.Promotes) p = (sbyte)(p > 0 ? King : -King);
            Sq[m.To] = p;
            Side = -Side;
        }

        public int Count(int colour)
        {
            int n = 0;
            for (int s = 0; s < 64; s++) if (Sq[s] != 0 && (Sq[s] > 0) == (colour > 0)) n++;
            return n;
        }

        /// <summary>Builds a position from piece lists (tests): squares like "c3", kings in upper case lists.</summary>
        public static CheckersBoard From(int side, string whiteMen, string whiteKings, string blackMen, string blackKings)
        {
            var b = new CheckersBoard();
            System.Array.Clear(b.Sq, 0, 64);
            b.Side = side;
            void Put(string list, sbyte v)
            {
                if (string.IsNullOrEmpty(list)) return;
                foreach (var t in list.Split(' '))
                {
                    int s = ChessBoard.ParseSq(t);
                    if (s >= 0) b.Sq[s] = v;
                }
            }
            Put(whiteMen, 1); Put(whiteKings, 2); Put(blackMen, -1); Put(blackKings, -2);
            return b;
        }
    }
}
