using System.Text;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// A dot link puzzle (Flow Free rules): a W x H grid with pairs of dots, one pair per colour. Cells are numbered
    /// row by row (cell = y * W + x, y = 0 is the top row of the screen). Plain data, no Unity: used by the generator,
    /// the solver, the server referee and the screens. <see cref="Encode"/> / <see cref="Decode"/> = the text in the
    /// network variable (seed | w | h | dot cells as two digits each, A then B per colour).
    /// </summary>
    public sealed class DotLinkPuzzle
    {
        public int W, H, Seed;
        /// <summary>Dot cells per colour: <see cref="A"/>[c] and <see cref="B"/>[c].</summary>
        public int[] A, B;
        /// <summary>The intended solution (server / generator only, null on clients): cells per colour from A to B.</summary>
        public int[][] Solution;

        public int Colors => A != null ? A.Length : 0;
        public int Cells => W * H;

        int[] _dotColor;

        /// <summary>Colour of the dot on a cell, -1 = no dot.</summary>
        public int DotAt(int cell)
        {
            if (_dotColor == null)
            {
                _dotColor = new int[Cells];
                for (int i = 0; i < _dotColor.Length; i++) _dotColor[i] = -1;
                for (int c = 0; c < Colors; c++)
                {
                    _dotColor[A[c]] = c;
                    _dotColor[B[c]] = c;
                }
            }
            return cell >= 0 && cell < Cells ? _dotColor[cell] : -1;
        }

        /// <summary>The other dot of colour <paramref name="c"/> (given one of its two dots).</summary>
        public int OtherEnd(int c, int cell) => cell == A[c] ? B[c] : A[c];

        public bool Adjacent(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Cells || b >= Cells) return false;
            int ax = a % W, ay = a / W, bx = b % W, by = b / W;
            return (ax == bx && (ay - by == 1 || by - ay == 1)) || (ay == by && (ax - bx == 1 || bx - ax == 1));
        }

        public string Encode()
        {
            var sb = new StringBuilder(16 + Colors * 4);
            sb.Append(Seed).Append('|').Append(W).Append('|').Append(H).Append('|');
            for (int c = 0; c < Colors; c++)
            {
                sb.Append(A[c].ToString("00"));
                sb.Append(B[c].ToString("00"));
            }
            return sb.ToString();
        }

        /// <summary>Puzzle from its network text (null when empty / broken).</summary>
        public static DotLinkPuzzle Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var parts = s.Split('|');
            if (parts.Length != 4 || !int.TryParse(parts[0], out int seed) || !int.TryParse(parts[1], out int w) || !int.TryParse(parts[2], out int h)) return null;
            if (w < 2 || h < 2 || w * h > 100 || parts[3].Length % 4 != 0) return null;
            int n = parts[3].Length / 4;
            var p = new DotLinkPuzzle { W = w, H = h, Seed = seed, A = new int[n], B = new int[n] };
            for (int c = 0; c < n; c++)
            {
                if (!int.TryParse(parts[3].Substring(c * 4, 2), out p.A[c]) || !int.TryParse(parts[3].Substring(c * 4 + 2, 2), out p.B[c])) return null;
                if (p.A[c] >= w * h || p.B[c] >= w * h) return null;
            }
            return p;
        }
    }
}
