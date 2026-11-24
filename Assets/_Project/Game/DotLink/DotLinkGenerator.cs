using System.Collections.Generic;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Makes a dot link puzzle with exactly one solution from a seed (deterministic: limits are counted in search
    /// nodes, not in time). One attempt: a random path through every cell (backbite moves), cut at random into a few
    /// more pieces than wanted, then random joins of touching piece ends (short pieces first) until the wanted number
    /// of colours is left; a line may never run next to itself (no shortcuts), every line is at least 3 cells long.
    /// The line ends become the dots and the solver checks that the dots allow only this one filling.
    /// Up to <see cref="MaxAttempts"/> attempts (the lines and the dot search live in <see cref="DotLinkCut"/>).
    /// </summary>
    public static class DotLinkGenerator
    {
        public const int MaxAttempts = 400;
        public const long SolverNodes = 60000;

        public struct Stats
        {
            public int Attempts, Rejected, Aborted, Stuck;
            public long Nodes;
            public bool Unique;
        }

        /// <summary>Puzzle of <paramref name="w"/> x <paramref name="h"/> with <paramref name="minColors"/>..<paramref name="maxColors"/> colours.</summary>
        public static DotLinkPuzzle Generate(int seed, int w, int h, int minColors, int maxColors, out Stats st)
        {
            st = default;
            var rng = new System.Random(seed);
            DotLinkPuzzle fallback = null;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                st.Attempts++;
                int colors = rng.Next(minColors, maxColors + 1);
                var lines = DotLinkCut.Partition(rng, w, h, colors);
                if (lines == null) { st.Stuck++; continue; }
                var p = FromLines(seed, w, h, lines);
                var solver = new DotLinkSolver(p);
                int found = solver.Count(2, SolverNodes);
                st.Nodes += solver.Nodes;
                if (solver.Aborted) { st.Aborted++; fallback ??= p; continue; }
                if (found != 1) { st.Rejected++; fallback ??= p; continue; }
                st.Unique = true;
                return p;
            }
            return fallback;
        }

        static DotLinkPuzzle FromLines(int seed, int w, int h, List<List<int>> lines)
        {
            // Stable colour order: by the first dot in reading order.
            lines.Sort((a, b) => System.Math.Min(a[0], a[a.Count - 1]).CompareTo(System.Math.Min(b[0], b[b.Count - 1])));
            var p = new DotLinkPuzzle { W = w, H = h, Seed = seed, A = new int[lines.Count], B = new int[lines.Count], Solution = new int[lines.Count][] };
            for (int c = 0; c < lines.Count; c++)
            {
                var l = lines[c];
                if (l[0] > l[l.Count - 1]) l.Reverse();
                p.A[c] = l[0];
                p.B[c] = l[l.Count - 1];
                p.Solution[c] = l.ToArray();
            }
            return p;
        }
    }
}
