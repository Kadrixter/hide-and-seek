using System.Collections.Generic;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Backtracking solver of a dot link puzzle that counts solutions (up to a limit, usually 2 = "is it unique?").
    /// Every colour grows from its dot A towards dot B; each step takes the unfinished colour with the fewest moves
    /// (forced moves first). Every solution of the rules (all pairs joined, all cells filled) is one branch, so the
    /// count is exact. Pruning after each step: an empty cell needs two usable neighbours, an unfinished dot B needs a
    /// way in, every unfinished colour needs an empty region (or a direct step) joining its head and its dot B, and
    /// every empty region must be reachable by some colour that can pass through it. A node limit keeps the time
    /// bounded (aborted = unknown, the generator throws such a puzzle away).
    /// </summary>
    public sealed class DotLinkSolver
    {
        readonly DotLinkPuzzle _p;
        readonly int _n, _colors;
        readonly int[] _nb;          // 4 neighbours per cell, -1 = wall
        readonly int[] _grid;        // colour per cell, -1 = empty
        readonly int[] _head, _target;
        readonly bool[] _done;
        readonly List<int>[] _path;
        readonly int[] _comp, _queue;
        int _empty, _limit;
        long _maxNodes;

        public long Nodes { get; private set; }
        public bool Aborted { get; private set; }
        public int Found { get; private set; }
        /// <summary>Cells per colour (from A to B) of the first solution found.</summary>
        public int[][] First { get; private set; }

        public DotLinkSolver(DotLinkPuzzle p)
        {
            _p = p;
            _n = p.Cells;
            _colors = p.Colors;
            _nb = new int[_n * 4];
            for (int i = 0; i < _n; i++)
            {
                int x = i % p.W, y = i / p.W;
                _nb[i * 4 + 0] = y > 0 ? i - p.W : -1;
                _nb[i * 4 + 1] = x < p.W - 1 ? i + 1 : -1;
                _nb[i * 4 + 2] = y < p.H - 1 ? i + p.W : -1;
                _nb[i * 4 + 3] = x > 0 ? i - 1 : -1;
            }
            _grid = new int[_n];
            _head = new int[_colors];
            _target = new int[_colors];
            _done = new bool[_colors];
            _path = new List<int>[_colors];
            _comp = new int[_n];
            _queue = new int[_n];
        }

        /// <summary>Counts solutions up to <paramref name="limit"/> within <paramref name="maxNodes"/> search nodes.</summary>
        public int Count(int limit, long maxNodes)
        {
            _limit = limit;
            _maxNodes = maxNodes;
            Nodes = 0;
            Found = 0;
            Aborted = false;
            First = null;
            for (int i = 0; i < _n; i++) _grid[i] = -1;
            _empty = _n;
            for (int c = 0; c < _colors; c++)
            {
                _grid[_p.A[c]] = c;
                _grid[_p.B[c]] = c;
                _head[c] = _p.A[c];
                _target[c] = _p.B[c];
                _done[c] = false;
                _path[c] = new List<int> { _p.A[c] };
            }
            _empty -= _colors * 2;
            Search();
            return Found;
        }

        bool Stop => Found >= _limit || Aborted;

        void Search()
        {
            if (++Nodes > _maxNodes) { Aborted = true; return; }
            int best = -1, bestMoves = 5;
            for (int c = 0; c < _colors; c++)
            {
                if (_done[c]) continue;
                int m = Moves(c);
                if (m == 0) return;
                if (m < bestMoves) { bestMoves = m; best = c; }
            }
            if (best < 0)
            {
                if (_empty == 0) Record();
                return;
            }
            int h = _head[best];
            for (int k = 0; k < 4 && !Stop; k++)
            {
                int n = _nb[h * 4 + k];
                if (n < 0) continue;
                if (n == _target[best])
                {
                    _done[best] = true;
                    _path[best].Add(n);
                    if (Feasible()) Search();
                    _path[best].RemoveAt(_path[best].Count - 1);
                    _done[best] = false;
                }
                else if (_grid[n] < 0)
                {
                    _grid[n] = best;
                    _head[best] = n;
                    _empty--;
                    _path[best].Add(n);
                    if (Feasible()) Search();
                    _path[best].RemoveAt(_path[best].Count - 1);
                    _empty++;
                    _head[best] = h;
                    _grid[n] = -1;
                }
            }
        }

        int Moves(int c)
        {
            int h = _head[c], m = 0;
            for (int k = 0; k < 4; k++)
            {
                int n = _nb[h * 4 + k];
                if (n >= 0 && (_grid[n] < 0 || n == _target[c])) m++;
            }
            return m;
        }

        void Record()
        {
            Found++;
            if (First != null) return;
            First = new int[_colors][];
            for (int c = 0; c < _colors; c++) First[c] = _path[c].ToArray();
        }

        /// <summary>Usable neighbour of an empty cell: empty, or the head / dot B of an unfinished colour.</summary>
        bool Usable(int n)
        {
            int g = _grid[n];
            if (g < 0) return true;
            return !_done[g] && (_head[g] == n || _target[g] == n);
        }

        bool Feasible()
        {
            // Dead ends: an empty cell needs a way in and a way out.
            for (int i = 0; i < _n; i++)
            {
                if (_grid[i] >= 0) continue;
                int u = 0;
                for (int k = 0; k < 4; k++)
                {
                    int n = _nb[i * 4 + k];
                    if (n >= 0 && Usable(n)) u++;
                }
                if (u < 2) return false;
            }
            // Empty regions.
            for (int i = 0; i < _n; i++) _comp[i] = -1;
            int comps = 0;
            for (int i = 0; i < _n; i++)
            {
                if (_grid[i] >= 0 || _comp[i] >= 0) continue;
                int qh = 0, qt = 0;
                _queue[qt++] = i;
                _comp[i] = comps;
                while (qh < qt)
                {
                    int cell = _queue[qh++];
                    for (int k = 0; k < 4; k++)
                    {
                        int n = _nb[cell * 4 + k];
                        if (n < 0 || _grid[n] >= 0 || _comp[n] >= 0) continue;
                        _comp[n] = comps;
                        _queue[qt++] = n;
                    }
                }
                comps++;
            }
            ulong served = 0; // regions some colour can pass through (head and dot B both touch it); up to 64 regions
            for (int c = 0; c < _colors; c++)
            {
                if (_done[c]) continue;
                int h = _head[c], t = _target[c];
                bool direct = false, ok = false;
                for (int a = 0; a < 4; a++)
                {
                    int na = _nb[h * 4 + a];
                    if (na < 0) continue;
                    if (na == t) { direct = true; continue; }
                    if (_grid[na] >= 0) continue;
                    int ca = _comp[na];
                    for (int b = 0; b < 4; b++)
                    {
                        int nb = _nb[t * 4 + b];
                        if (nb >= 0 && _grid[nb] < 0 && _comp[nb] == ca)
                        {
                            ok = true;
                            if (ca < 64) served |= 1UL << ca;
                        }
                    }
                }
                if (!direct && !ok) return false;
            }
            for (int r = 0; r < comps && r < 64; r++)
                if ((served & (1UL << r)) == 0) return false;
            return true;
        }
    }
}
