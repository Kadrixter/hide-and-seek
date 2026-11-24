using System.Collections.Generic;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// The lines of a generated dot link puzzle (<see cref="DotLinkGenerator"/>): a random Hamiltonian path of the
    /// grid, a uniformly random cut of it into pieces that do not touch themselves (dynamic programming over the cut
    /// points), then random joins of touching piece ends down to the wanted number of colours.
    /// </summary>
    public static class DotLinkCut
    {
        [System.ThreadStatic] static List<(double, int)> _keysT;
        static List<(double, int)> Keys => _keysT ??= new List<(double, int)>(256);

        /// <summary>Random split of the grid into <paramref name="colors"/> lines (null = got stuck).</summary>
        public static List<List<int>> Partition(System.Random rng, int w, int h, int colors)
        {
            int n = w * h;
            var path = Hamiltonian(rng, w, h, out var pos);
            int maxLen = n * 2 / colors + 2;
            // Cut the path into a few more pieces than wanted (dominoes allowed), then join random touching ends.
            List<List<int>> lines = null;
            for (int extra = rng.Next(0, 9); extra >= 0 && lines == null; extra -= 3)
                lines = Cut(rng, path, pos, w, h, colors + extra, extra > 0 ? 2 : 3, maxLen);
            if (lines == null) lines = Cut(rng, path, pos, w, h, colors, 3, maxLen);
            if (lines == null) return null;
            var lineOf = new int[n];
            for (int k = 0; k < lines.Count; k++)
                foreach (int cell in lines[k]) lineOf[cell] = k;
            int alive = lines.Count;
            var cand = new List<(int, int)>(n * 2);
            var keys = Keys;
            while (alive > colors)
            {
                cand.Clear();
                foreach (var l in lines)
                {
                    if (l == null) continue;
                    AddEnds(cand, l[0], lineOf, lines, w, h);
                    AddEnds(cand, l[l.Count - 1], lineOf, lines, w, h);
                }
                // Short lines first (a leftover domino cannot be joined later), with some randomness.
                keys.Clear();
                for (int k = 0; k < cand.Count; k++)
                {
                    var (u, v) = cand[k];
                    keys.Add((System.Math.Min(lines[lineOf[u]].Count, lines[lineOf[v]].Count) + rng.NextDouble() * 4.0, k));
                }
                keys.Sort((x, y) => x.Item1.CompareTo(y.Item1));
                bool merged = false;
                foreach (var (_, k) in keys)
                {
                    var (u, v) = cand[k];
                    int lu = lineOf[u], lv = lineOf[v];
                    if (lu == lv || lines[lu] == null || lines[lv] == null) continue;
                    if (lines[lu].Count + lines[lv].Count > maxLen) continue;
                    var joined = Join(lines[lu], u, lines[lv], v);
                    if (TouchesItself(joined, w)) continue;
                    lines[lu] = joined;
                    foreach (int cell in lines[lv]) lineOf[cell] = lu;
                    lines[lv] = null;
                    alive--;
                    merged = true;
                    break;
                }
                if (!merged) return null;
            }
            var result = new List<List<int>>(colors);
            foreach (var l in lines)
            {
                if (l == null) continue;
                if (l.Count < 3) return null;
                result.Add(l);
            }
            return result;
        }

        /// <summary>A random path through every cell (backbite moves from a snake).</summary>
        static int[] Hamiltonian(System.Random rng, int w, int h, out int[] pos)
        {
            int n = w * h;
            var path = new int[n];
            pos = new int[n];
            for (int y = 0, k = 0; y < h; y++)
                for (int i = 0; i < w; i++, k++)
                    path[k] = y * w + (y % 2 == 0 ? i : w - 1 - i);
            for (int k = 0; k < n; k++) pos[path[k]] = k;
            var nb = new int[4];
            for (int step = 0; step < n * 20; step++)
            {
                if (rng.Next(2) == 0) Reverse(path, pos, 0, n - 1);
                int end = path[0], x = end % w, y = end / w, c = 0;
                if (y > 0) nb[c++] = end - w;
                if (y < h - 1) nb[c++] = end + w;
                if (x > 0) nb[c++] = end - 1;
                if (x < w - 1) nb[c++] = end + 1;
                int i = pos[nb[rng.Next(c)]];
                if (i <= 1) continue;
                Reverse(path, pos, 0, i - 1); // new edge end - path[i]; the edge path[i-1] - path[i] goes
            }
            return path;
        }

        static void Reverse(int[] path, int[] pos, int a, int b)
        {
            for (; a < b; a++, b--)
            {
                int t = path[a];
                path[a] = path[b];
                path[b] = t;
                pos[path[a]] = a;
                pos[path[b]] = b;
            }
        }

        /// <summary>
        /// Random cut of the path into exactly <paramref name="count"/> pieces of <paramref name="minLen"/>..<paramref name="maxLen"/>
        /// cells that do not touch themselves (every such cut equally likely; null = none exists).
        /// </summary>
        static List<List<int>> Cut(System.Random rng, int[] path, int[] pos, int w, int h, int count, int minLen, int maxLen)
        {
            int n = path.Length;
            var maxEnd = new int[n];
            for (int i = 0; i < n; i++)
            {
                int j = i;
                for (; j < n; j++)
                {
                    int cell = path[j], x = cell % w, y = cell / w;
                    if ((y > 0 && Inside(pos[cell - w], i, j)) || (y < h - 1 && Inside(pos[cell + w], i, j)) ||
                        (x > 0 && Inside(pos[cell - 1], i, j)) || (x < w - 1 && Inside(pos[cell + 1], i, j))) break;
                }
                maxEnd[i] = j - 1;
            }
            var ways = new double[n + 1, count + 1];
            ways[n, 0] = 1;
            for (int i = n - 1; i >= 0; i--)
                for (int s = 1; s <= count; s++)
                {
                    double sum = 0;
                    int last = System.Math.Min(maxEnd[i], i + maxLen - 1);
                    for (int e = i + minLen - 1; e <= last; e++) sum += ways[e + 1, s - 1];
                    ways[i, s] = sum;
                }
            if (ways[0, count] <= 0) return null;
            var lines = new List<List<int>>(count);
            for (int i = 0, s = count; s > 0; s--)
            {
                int last = System.Math.Min(maxEnd[i], i + maxLen - 1);
                double r = rng.NextDouble() * ways[i, s];
                int pick = -1;
                for (int e = i + minLen - 1; e <= last; e++)
                {
                    if (ways[e + 1, s - 1] <= 0) continue;
                    pick = e;
                    r -= ways[e + 1, s - 1];
                    if (r < 0) break;
                }
                if (pick < 0) return null;
                var l = new List<int>(pick - i + 1);
                for (int k = i; k <= pick; k++) l.Add(path[k]);
                lines.Add(l);
                i = pick + 1;
            }
            return lines;
        }

        /// <summary>Path index <paramref name="p"/> lies in the piece i..j-2 (a touch, not the previous cell).</summary>
        static bool Inside(int p, int i, int j) => p >= i && p <= j - 2;

        static void AddEnds(List<(int, int)> cand, int end, int[] lineOf, List<List<int>> lines, int w, int h)
        {
            int x = end % w, y = end / w;
            if (y > 0) TryEnd(cand, end, end - w, lineOf, lines);
            if (x < w - 1) TryEnd(cand, end, end + 1, lineOf, lines);
            if (y < h - 1) TryEnd(cand, end, end + w, lineOf, lines);
            if (x > 0) TryEnd(cand, end, end - 1, lineOf, lines);
        }

        static void TryEnd(List<(int, int)> cand, int end, int other, int[] lineOf, List<List<int>> lines)
        {
            if (end > other) return; // each pair once
            var l = lines[lineOf[other]];
            if (lineOf[other] == lineOf[end] || l == null) return;
            if (l[0] == other || l[l.Count - 1] == other) cand.Add((end, other));
        }

        /// <summary>Line a ... u then v ... b (reversed as needed so u and v meet).</summary>
        static List<int> Join(List<int> a, int u, List<int> b, int v)
        {
            var r = new List<int>(a.Count + b.Count);
            if (a[a.Count - 1] == u) r.AddRange(a);
            else for (int i = a.Count - 1; i >= 0; i--) r.Add(a[i]);
            if (b[0] == v) r.AddRange(b);
            else for (int i = b.Count - 1; i >= 0; i--) r.Add(b[i]);
            return r;
        }

        /// <summary>Two cells of the line are grid neighbours without being one after the other in the line.</summary>
        static bool TouchesItself(List<int> line, int w)
        {
            var index = new Dictionary<int, int>(line.Count);
            for (int i = 0; i < line.Count; i++) index[line[i]] = i;
            for (int i = 0; i < line.Count; i++)
            {
                int cell = line[i], x = cell % w;
                if (Check(index, cell + w, i) || Check(index, cell - w, i)) return true;
                if (x < w - 1 && Check(index, cell + 1, i)) return true;
                if (x > 0 && Check(index, cell - 1, i)) return true;
            }
            return false;
        }

        static bool Check(Dictionary<int, int> index, int cell, int i) =>
            index.TryGetValue(cell, out int j) && j != i - 1 && j != i + 1;
    }
}
