using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// Chalk strokes of the tic-tac-toe slate. A mark is drawn over a progress 0..1 (X: two strokes, O: one loop
    /// started at the top), so the hand can follow <see cref="PenAt"/> while the texture is filled in step by step.
    /// The winning line is one long stroke through the three cells.
    /// </summary>
    public static class TttChalk
    {
        const int CircleSteps = 48;
        public const float MarkRadius = TttTableModel.Cell * 0.31f;

        /// <summary>Pen point (slate-local) at progress <paramref name="t"/> of a mark; <paramref name="down"/> = touching the slate.</summary>
        public static Vector3 PenAt(int cell, char mark, float t, int seed, out bool down)
        {
            Vector3 c = TttTableModel.CellLocal(cell);
            float r = MarkRadius * (0.95f + 0.1f * Mathf.PerlinNoise(seed, 0.3f));
            down = true;
            if (mark == 'X')
            {
                // Stroke 1: top-left -> bottom-right, lift off the board, stroke 2: top-right -> bottom-left.
                Vector3 a1 = c + new Vector3(-r, r, 0f), b1 = c + new Vector3(r, -r, 0f);
                Vector3 a2 = c + new Vector3(r, r, 0f), b2 = c + new Vector3(-r, -r, 0f);
                if (t < 0.45f) return Vector3.Lerp(a1, b1, t / 0.45f);
                if (t < 0.55f) { down = false; return Vector3.Lerp(b1, a2, (t - 0.45f) / 0.1f) + Vector3.forward * 0.025f * Mathf.Sin((t - 0.45f) / 0.1f * Mathf.PI); }
                return Vector3.Lerp(a2, b2, (t - 0.55f) / 0.45f);
            }
            // O: from the top, anticlockwise, closing with a small overlap.
            float ang = Mathf.PI * 0.5f + t * Mathf.PI * 2.08f;
            float rr = r * (1f + 0.06f * Mathf.Sin(t * 7f + seed));
            return c + new Vector3(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr * 1.05f, 0f);
        }

        /// <summary>Draws the part of a mark between progress <paramref name="from"/> and <paramref name="to"/> (no Apply).</summary>
        public static void DrawMark(TttTableModel m, int cell, char mark, float to, int seed, float from = 0f)
        {
            if (m.SlateTex == null || to <= from) return;
            const float step = 0.008f;
            Vector3 prev = PenAt(cell, mark, from, seed, out bool prevDown);
            // Exactly from..to (the last piece may be shorter than a step), whatever the frame rate.
            for (float t = from; t < to - 1e-5f;)
            {
                float nt = Mathf.Min(to, t + step);
                Vector3 p = PenAt(cell, mark, nt, seed, out bool down);
                if (down && prevDown) m.ChalkLine(prev, p, 1f, seed * 13 + (int)(nt * 100f), 4.6f);
                prev = p;
                prevDown = down;
                t = nt;
            }
        }

        /// <summary>End points of the winning line (slightly past the outer cells).</summary>
        public static void WinLine(int line, out Vector3 a, out Vector3 b)
        {
            var l = TttRules.Lines[line];
            Vector3 p0 = TttTableModel.CellLocal(l[0]), p2 = TttTableModel.CellLocal(l[2]);
            Vector3 d = (p2 - p0).normalized;
            a = p0 - d * TttTableModel.Cell * 0.42f;
            b = p2 + d * TttTableModel.Cell * 0.42f;
        }

        public static Vector3 WinLineEnd(int line)
        {
            WinLine(line, out _, out var b);
            return b;
        }

        public static void DrawWinLine(TttTableModel m, int line, float from, float to)
        {
            if (m.SlateTex == null || line < 0 || to <= from) return;
            WinLine(line, out var a, out var b);
            m.ChalkLine(Vector3.Lerp(a, b, from), Vector3.Lerp(a, b, to), 1f, 900 + line, 6.5f);
        }
    }
}
