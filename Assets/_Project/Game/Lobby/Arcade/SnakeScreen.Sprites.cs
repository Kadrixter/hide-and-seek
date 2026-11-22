using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Snake, food and bonus pixel art (one 7x7 cell per grid square).</summary>
    public sealed partial class SnakeScreen
    {
        public const int Cell = 7, GridX = 16, GridY = 10;

        private static readonly Color32 Outline = new(8, 48, 30, 255);
        private static readonly Color32 HeadCol = new(176, 255, 120, 255);
        private static readonly Color32 BodyNear = new(92, 232, 92, 255), BodyFar = new(28, 150, 96, 255);
        private static readonly Color32 Belly = new(18, 110, 64, 255);
        private static readonly Color32 Shine = new(210, 255, 190, 255);

        private static readonly string[] Apple =
        {
            "...23..",
            ".00200.",
            "0010000",
            "0100000",
            "0000000",
            ".00000.",
            "..444..",
        };
        private static readonly string[] Star =
        {
            "...0...",
            "..010..",
            "0011100",
            ".01110.",
            "..111..",
            ".01.10.",
            ".0...0.",
        };

        private static int Px(int cx) => GridX + cx * Cell;
        private static int Py(int cy) => GridY + cy * Cell;

        private void DrawFood(Vector2Int f, float t)
        {
            int bob = (int)(t * 3f) % 2;
            // soft pulse ring
            if ((int)(t * 4f) % 2 == 0)
            {
                var ring = new Color32(90, 20, 36, 255);
                _c.Set(Px(f.x) - 1, Py(f.y) - 1, ring); _c.Set(Px(f.x) + 7, Py(f.y) - 1, ring);
                _c.Set(Px(f.x) - 1, Py(f.y) + 7, ring); _c.Set(Px(f.x) + 7, Py(f.y) + 7, ring);
            }
            _c.Sprite(Apple, Px(f.x), Py(f.y) + bob, new[]
            {
                new Color32(232, 40, 64, 255), new Color32(255, 196, 200, 255), new Color32(120, 72, 30, 255),
                new Color32(90, 225, 80, 255), new Color32(150, 16, 40, 255),
            });
        }

        private void DrawBonus(Vector2Int b, float left, float t)
        {
            if (left < 2f && (int)(t * 10f) % 2 == 0) return;
            float hue = t * 3f;
            var edge = SnakeCanvas.Lerp(new Color32(255, 140, 20, 255), new Color32(255, 60, 30, 255), 0.5f + 0.5f * Mathf.Sin(hue));
            _c.Sprite(Star, Px(b.x), Py(b.y), new[] { edge, new Color32(255, 228, 70, 255) });
            // sparkles turning around the star
            int k = (int)(t * 8f) % 4;
            var sp = new Color32(255, 255, 220, 255);
            int x = Px(b.x), y = Py(b.y);
            if (k == 0) { _c.Set(x - 2, y + 8, sp); _c.Set(x + 8, y - 2, sp); }
            if (k == 1) { _c.Set(x + 3, y + 9, sp); _c.Set(x + 3, y - 3, sp); }
            if (k == 2) { _c.Set(x + 8, y + 8, sp); _c.Set(x - 2, y - 2, sp); }
            if (k == 3) { _c.Set(x - 3, y + 3, sp); _c.Set(x + 9, y + 3, sp); }
        }

        private static bool Adjacent(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;

        /// <summary>Segments as joined blocks: an outline only where the neighbour is not the next/previous segment.</summary>
        private void DrawSnake(List<Vector2Int> body, int dir, bool crashed, float t, float dim)
        {
            int n = body.Count;
            bool flash = crashed && (int)(t * 6f) % 2 == 0;
            for (int i = n - 1; i >= 0; i--)
            {
                var c = body[i];
                int x = Px(c.x), y = Py(c.y);
                Color32 fill = i == 0 ? HeadCol : SnakeCanvas.Lerp(BodyNear, BodyFar, n <= 2 ? 0f : (i - 1f) / (n - 2f));
                if (crashed) fill = flash ? new Color32(255, 70, 60, 255) : new Color32(120, 24, 24, 255);
                fill = Scale(fill, dim);
                var line = Scale(crashed ? new Color32(50, 0, 0, 255) : Outline, dim);
                bool up = Linked(body, i, c + Vector2Int.up), down = Linked(body, i, c + Vector2Int.down);
                bool left = Linked(body, i, c + Vector2Int.left), right = Linked(body, i, c + Vector2Int.right);
                // tail end tapers: shrink the free sides by one pixel
                int inset = i == n - 1 && n > 1 ? 1 : 0;
                int x0 = x + (left ? 0 : inset), x1 = x + 6 - (right ? 0 : inset);
                int y0 = y + (down ? 0 : inset), y1 = y + 6 - (up ? 0 : inset);
                _c.Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1, fill);
                if (!left) _c.Rect(x0, y0 + 1, 1, y1 - y0 - 1, line);
                if (!right) _c.Rect(x1, y0 + 1, 1, y1 - y0 - 1, line);
                if (!down) _c.Rect(x0 + 1, y0, x1 - x0 - 1, 1, line);
                if (!up) _c.Rect(x0 + 1, y1, x1 - x0 - 1, 1, line);
                if (i > 0 && !crashed)
                {
                    // scale pattern: a darker diamond every other segment, a light glint on the others
                    if (i % 2 == 0) { _c.Rect(x + 3, y + 2, 1, 3, Scale(Belly, dim)); _c.Rect(x + 2, y + 3, 3, 1, Scale(Belly, dim)); }
                    else _c.Set(x + 2, y + 4, Scale(Shine, dim));
                }
            }
            if (n > 0) DrawHead(body[0], dir, crashed, t, dim);
        }

        private static bool Linked(List<Vector2Int> body, int i, Vector2Int cell)
            => i > 0 && body[i - 1] == cell && Adjacent(body[i], cell) || i + 1 < body.Count && body[i + 1] == cell && Adjacent(body[i], cell);

        private void DrawHead(Vector2Int h, int dir, bool crashed, float t, float dim)
        {
            int x = Px(h.x), y = Py(h.y);
            // forward / side unit vectors in pixels
            Vector2Int f = dir switch { 0 => Vector2Int.up, 1 => Vector2Int.right, 2 => Vector2Int.down, _ => Vector2Int.left };
            Vector2Int s = new Vector2Int(f.y, -f.x);
            // local (fwd 0..6 from the back, side -3..3) -> pixel
            Vector2Int P(int fw, int sd) => new Vector2Int(x + 3 + (fw - 3) * f.x + sd * s.x, y + 3 + (fw - 3) * f.y + sd * s.y);
            var white = Scale(new Color32(255, 255, 255, 255), dim);
            var black = new Color32(0, 0, 0, 255);
            foreach (int sg in new[] { -1, 1 })
            {
                if (crashed)
                {
                    // X eyes (3x3 cross)
                    foreach (var (a, b) in new[] { (2, 1), (3, 2), (4, 3), (4, 1), (2, 3) })
                    {
                        var px = P(a, b * sg);
                        _c.Set(px.x, px.y, black);
                    }
                    continue;
                }
                for (int a = 3; a <= 4; a++)
                for (int b = 1; b <= 2; b++)
                {
                    var px = P(a, b * sg);
                    _c.Set(px.x, px.y, white);
                }
                var pupil = P(4, 2 * sg); // pupils look ahead
                _c.Set(pupil.x, pupil.y, black);
            }
            // nostrils + flicking tongue
            if (!crashed && (int)(t * 2.5f) % 3 == 0)
            {
                var red = Scale(new Color32(255, 40, 70, 255), dim);
                var a1 = P(7, 0); var a2 = P(8, 0); var a3 = P(9, -1); var a4 = P(9, 1);
                _c.Set(a1.x, a1.y, red); _c.Set(a2.x, a2.y, red); _c.Set(a3.x, a3.y, red); _c.Set(a4.x, a4.y, red);
            }
        }

        private static Color32 Scale(Color32 c, float k) => k >= 0.999f ? c : new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
    }
}
