using System;
using System.Collections.Generic;
using System.Diagnostics;
using HideAndSeek.Game.DotLink;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace HideAndSeek.Game.Editor
{
    /// <summary>
    /// Tests of the dot link puzzle (batchmode: -executeMethod HideAndSeek.Game.Editor.DotLinkTests.RunFromCommandLine,
    /// optional -dotlinkcount N, default 200). Line rules (crossing cuts, illegal lines, network text), then N generated
    /// 10 x 10 puzzles: 10-12 colours, every dot used once, the intended solution solves the board, a second solver run
    /// with a 30x bigger node limit still finds exactly one solution; generation times (avg / max / p95) and attempts.
    /// Every check logs "[DotLinkTest]"; exit code 0 = all passed.
    /// </summary>
    public static class DotLinkTests
    {
        static int _fail, _pass;

        [MenuItem("HideAndSeek/Dot link/Run generator tests")]
        public static void RunMenu() => Run(200);

        public static void RunFromCommandLine()
        {
            bool ok = false;
            int count = 200;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-dotlinkcount");
            if (i >= 0 && i + 1 < args.Length) int.TryParse(args[i + 1], out count);
            try { ok = Run(count); }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static void Check(string name, bool ok, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            if (!ok || detail.Length > 0 || _pass < 40) Debug.Log($"[DotLinkTest] {(ok ? "OK  " : "FAIL")} {name} {detail}");
        }

        public static bool Run(int count)
        {
            _fail = _pass = 0;
            Rules();
            Batch(count);
            Debug.Log($"[DotLinkTest] DONE pass={_pass} fail={_fail}");
            return _fail == 0;
        }

        // ================================================================ rules

        static void Rules()
        {
            // 4 x 3:  A . . B    red  0 -> 3 (top row)
            //         C . . D    blue 4 -> 7
            //         E . . F    green 8 -> 11
            var p = new DotLinkPuzzle { W = 4, H = 3, Seed = 1, A = new[] { 0, 4, 8 }, B = new[] { 3, 7, 11 } };
            var b = new DotLinkBoard(p);
            Check("empty board not solved", !b.Solved);
            Check("diagonal step refused", !b.SetPath(0, new[] { 0, 5 }));
            Check("start off a dot refused", !b.SetPath(0, new[] { 1, 2 }));
            Check("through other dot refused", !b.SetPath(0, new[] { 0, 4 }));
            Check("loop refused", !b.SetPath(0, new[] { 0, 1, 5, 4 }));
            Check("red top row", b.SetPath(0, new[] { 0, 1, 2, 3 }) && b.Complete(0));
            Check("past own end refused", !b.SetPath(1, new[] { 4, 5, 6, 7, 3 }));
            Check("blue middle", b.SetPath(1, new[] { 4, 5, 6, 7 }) && b.Complete(1));
            Check("green from B end", b.SetPath(2, new[] { 11, 10, 9 }) && !b.Complete(2));
            Check("not solved yet", !b.Solved && b.FilledCount == 12, $"filled={b.FilledCount}");
            // Green goes up into blue's line: blue is cut before cell 5 (only its dot stays -> removed).
            Check("crossing cuts", b.SetPath(2, new[] { 11, 10, 9, 5 }) && b.Path(1).Count == 0, $"blue={b.Path(1).Count}");
            Check("cut colour lost", b.ColorAt(6) == -1 && b.ColorAt(5) == 2);
            b.SetPath(1, new[] { 7, 6 });
            Check("blue from B end", b.Path(1).Count == 2 && b.ColorAt(6) == 1);
            b.SetPath(2, new[] { 11, 10, 9, 8 });
            b.SetPath(1, new[] { 4, 5, 6, 7 });
            Check("solved", b.Solved, b.Encode());
            var copy = new DotLinkBoard(p);
            Check("encode/decode", copy.Decode(b.Encode()) && copy.Encode() == b.Encode() && copy.Solved);
            var q = DotLinkPuzzle.Decode(p.Encode());
            Check("puzzle encode/decode", q != null && q.Encode() == p.Encode(), p.Encode());
            b.Clear(0);
            Check("clear colour", !b.Solved && b.Path(0).Count == 0);
            var s = new DotLinkSolver(p);
            Check("solver small", s.Count(2, 100000) == 1 && !s.Aborted, $"nodes={s.Nodes}");
        }

        // ================================================================ generated puzzles

        static void Batch(int count)
        {
            var times = new List<double>(count);
            var colorHist = new Dictionary<int, int>();
            int attemptsTotal = 0, attemptsMax = 0, notUnique = 0;
            var sw = new Stopwatch();
            for (int k = 0; k < count; k++)
            {
                int seed = 1000 + k * 7919;
                sw.Restart();
                var p = DotLinkGenerator.Generate(seed, 10, 10, 10, 12, out var st);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                times.Add(ms);
                attemptsTotal += st.Attempts;
                attemptsMax = Math.Max(attemptsMax, st.Attempts);
                if (p == null) { Check($"#{k} seed {seed} generated", false); continue; }
                if (!st.Unique) notUnique++;
                colorHist[p.Colors] = colorHist.TryGetValue(p.Colors, out int h) ? h + 1 : 1;
                bool okColors = p.Colors >= 10 && p.Colors <= 12;
                var dots = new HashSet<int>();
                for (int c = 0; c < p.Colors; c++) { dots.Add(p.A[c]); dots.Add(p.B[c]); }
                var board = new DotLinkBoard(p);
                bool laid = true;
                for (int c = 0; c < p.Colors; c++) laid &= board.SetPath(c, p.Solution[c]);
                var solver = new DotLinkSolver(p);
                int found = solver.Count(2, DotLinkGenerator.SolverNodes * 30);
                bool sameAsIntended = solver.First != null;
                if (sameAsIntended)
                    for (int c = 0; c < p.Colors; c++)
                        sameAsIntended &= string.Join(",", solver.First[c]) == string.Join(",", p.Solution[c]);
                var back = DotLinkPuzzle.Decode(p.Encode());
                Check($"#{k} seed {seed}",
                    st.Unique && okColors && dots.Count == p.Colors * 2 && laid && board.Solved && found == 1 && !solver.Aborted && sameAsIntended
                    && back != null && back.Encode() == p.Encode(),
                    k % 20 == 0 || !st.Unique || found != 1
                        ? $"colors={p.Colors} attempts={st.Attempts} (rejected {st.Rejected}, aborted {st.Aborted}, stuck {st.Stuck}) nodes={st.Nodes} {ms:0.0} ms; recheck found={found} nodes={solver.Nodes} aborted={solver.Aborted}"
                        : "");
                if (k == 0) Debug.Log("[DotLinkTest] sample\n" + Draw(p));
            }
            times.Sort();
            double sum = 0;
            foreach (double t in times) sum += t;
            var hist = new List<string>();
            foreach (var kv in colorHist) hist.Add($"{kv.Key}:{kv.Value}");
            Debug.Log($"[DotLinkTest] batch {count}: avg {sum / Math.Max(1, count):0.0} ms, p50 {times[count / 2]:0.0} ms, p95 {times[Math.Min(count - 1, count * 95 / 100)]:0.0} ms, " +
                      $"max {times[count - 1]:0.0} ms; attempts avg {attemptsTotal / (float)Math.Max(1, count):0.0} max {attemptsMax}; not unique {notUnique}; colours {string.Join(" ", hist)}");
            Check("max generation time under 1 s", times[count - 1] < 1000.0, $"{times[count - 1]:0} ms");
        }

        static string Draw(DotLinkPuzzle p)
        {
            var g = new char[p.Cells];
            for (int i = 0; i < g.Length; i++) g[i] = '.';
            for (int c = 0; c < p.Colors; c++)
            {
                foreach (int cell in p.Solution[c]) g[cell] = (char)('a' + c);
                g[p.A[c]] = g[p.B[c]] = (char)('A' + c);
            }
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < p.H; y++) sb.Append(new string(g, y * p.W, p.W)).Append('\n');
            return sb.ToString();
        }
    }
}
