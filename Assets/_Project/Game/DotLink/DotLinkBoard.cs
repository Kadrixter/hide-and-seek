using System.Collections.Generic;
using System.Text;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// The drawn lines of a dot link puzzle: one path per colour, a list of cells starting at one of its dots. Rules
    /// (Flow Free): a path starts on its own dot, goes cell by cell in 4 directions, never through a dot of another
    /// colour, never over itself, and stops when it reaches its other dot. Drawing over another colour's line cuts that
    /// line where it is crossed (the part from its own dot up to the crossing stays). Solved = every pair joined and
    /// every cell filled. The server keeps one (the shared, saved progress), the text goes to everybody
    /// (<see cref="Encode"/>: sequence | solved flag | paths as two-digit cells).
    /// </summary>
    public sealed class DotLinkBoard
    {
        public readonly DotLinkPuzzle Puzzle;
        readonly List<int>[] _paths;
        public int Seq;
        public bool SolvedFlag;

        public DotLinkBoard(DotLinkPuzzle p)
        {
            Puzzle = p;
            _paths = new List<int>[p.Colors];
            for (int c = 0; c < _paths.Length; c++) _paths[c] = new List<int>();
        }

        public IReadOnlyList<int> Path(int c) => _paths[c];

        public DotLinkBoard Clone()
        {
            var b = new DotLinkBoard(Puzzle) { Seq = Seq, SolvedFlag = SolvedFlag };
            for (int c = 0; c < _paths.Length; c++) b._paths[c].AddRange(_paths[c]);
            return b;
        }

        public void CopyFrom(DotLinkBoard o)
        {
            Seq = o.Seq;
            SolvedFlag = o.SolvedFlag;
            for (int c = 0; c < _paths.Length; c++)
            {
                _paths[c].Clear();
                _paths[c].AddRange(o._paths[c]);
            }
        }

        /// <summary>Colour whose line (or dot) is on the cell, -1 = empty.</summary>
        public int ColorAt(int cell)
        {
            int d = Puzzle.DotAt(cell);
            if (d >= 0) return d;
            for (int c = 0; c < _paths.Length; c++)
                if (_paths[c].Contains(cell)) return c;
            return -1;
        }

        /// <summary>The line of colour <paramref name="c"/> joins its two dots.</summary>
        public bool Complete(int c)
        {
            var p = _paths[c];
            return p.Count >= 2 && p[p.Count - 1] == Puzzle.OtherEnd(c, p[0]);
        }

        public int CompleteCount
        {
            get
            {
                int n = 0;
                for (int c = 0; c < _paths.Length; c++) if (Complete(c)) n++;
                return n;
            }
        }

        /// <summary>Cells covered by a line or a dot.</summary>
        public int FilledCount
        {
            get
            {
                var seen = new bool[Puzzle.Cells];
                int n = 0;
                for (int c = 0; c < Puzzle.Colors; c++)
                {
                    if (!seen[Puzzle.A[c]]) { seen[Puzzle.A[c]] = true; n++; }
                    if (!seen[Puzzle.B[c]]) { seen[Puzzle.B[c]] = true; n++; }
                    foreach (int cell in _paths[c])
                        if (!seen[cell]) { seen[cell] = true; n++; }
                }
                return n;
            }
        }

        public bool Solved => CompleteCount == _paths.Length && FilledCount == Puzzle.Cells;

        /// <summary>Is <paramref name="cells"/> a legal line of colour <paramref name="c"/> (on its own)?</summary>
        public bool ValidPath(int c, IReadOnlyList<int> cells)
        {
            if (c < 0 || c >= _paths.Length || cells == null) return false;
            if (cells.Count == 0) return true;
            if (cells[0] != Puzzle.A[c] && cells[0] != Puzzle.B[c]) return false;
            int other = Puzzle.OtherEnd(c, cells[0]);
            var seen = new HashSet<int>();
            for (int i = 0; i < cells.Count; i++)
            {
                int cell = cells[i];
                if (cell < 0 || cell >= Puzzle.Cells || !seen.Add(cell)) return false;
                if (i > 0 && !Puzzle.Adjacent(cells[i - 1], cell)) return false;
                int dot = Puzzle.DotAt(cell);
                if (dot >= 0 && dot != c) return false;
                if (cell == other && i != cells.Count - 1) return false;
                if (i > 0 && cell == cells[0]) return false;
            }
            return true;
        }

        /// <summary>
        /// Puts the line of colour <paramref name="c"/> (false = illegal, nothing changes). Other lines crossing it are
        /// cut just before the first shared cell; a cut line that keeps only its dot is removed.
        /// </summary>
        public bool SetPath(int c, IReadOnlyList<int> cells)
        {
            if (!ValidPath(c, cells)) return false;
            _paths[c].Clear();
            if (cells.Count >= 1) _paths[c].AddRange(cells);
            if (cells.Count == 0) return true;
            var mine = new HashSet<int>(cells);
            for (int o = 0; o < _paths.Length; o++)
            {
                if (o == c) continue;
                var p = _paths[o];
                for (int i = 0; i < p.Count; i++)
                {
                    if (!mine.Contains(p[i])) continue;
                    p.RemoveRange(i, p.Count - i);
                    break;
                }
                if (p.Count <= 1) p.Clear();
            }
            return true;
        }

        public void Clear(int c)
        {
            if (c >= 0 && c < _paths.Length) _paths[c].Clear();
        }

        public void ClearAll()
        {
            foreach (var p in _paths) p.Clear();
            SolvedFlag = false;
        }

        // ================================================================ network text

        public static string EncodeCells(IReadOnlyList<int> cells)
        {
            var sb = new StringBuilder(cells.Count * 2);
            foreach (int cell in cells) sb.Append(cell.ToString("00"));
            return sb.ToString();
        }

        public static List<int> DecodeCells(string s)
        {
            var list = new List<int>(s.Length / 2);
            for (int i = 0; i + 1 < s.Length; i += 2)
            {
                int d0 = s[i] - '0', d1 = s[i + 1] - '0';
                if (d0 < 0 || d0 > 9 || d1 < 0 || d1 > 9) return null;
                list.Add(d0 * 10 + d1);
            }
            return list;
        }

        public string Encode()
        {
            var sb = new StringBuilder(260);
            sb.Append(Seq).Append('|').Append(SolvedFlag ? 'S' : '-');
            foreach (var p in _paths) sb.Append('|').Append(EncodeCells(p));
            return sb.ToString();
        }

        /// <summary>Reads the network text into this board (false = broken / other puzzle size).</summary>
        public bool Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var parts = s.Split('|');
            if (parts.Length != _paths.Length + 2 || !int.TryParse(parts[0], out int seq)) return false;
            var lists = new List<int>[_paths.Length];
            for (int c = 0; c < _paths.Length; c++)
            {
                lists[c] = DecodeCells(parts[c + 2]);
                if (lists[c] == null) return false;
            }
            Seq = seq;
            SolvedFlag = parts[1] == "S";
            for (int c = 0; c < _paths.Length; c++)
            {
                _paths[c].Clear();
                _paths[c].AddRange(lists[c]);
            }
            return true;
        }
    }
}
