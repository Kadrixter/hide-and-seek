using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Pixel buffer for the arcade screen (y = 0 is the bottom row, like the texture): rectangles, 7x7 sprites,
    /// text in the 5x7 <see cref="CanFont"/> (Polish letters included) with a drop shadow, scanlines.
    /// </summary>
    public sealed class SnakeCanvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public SnakeCanvas(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }

        public void Clear(Color32 c)
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = c;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(H, y + h); yy++)
            for (int xx = Mathf.Max(0, x); xx < Mathf.Min(W, x + w); xx++)
                Px[yy * W + xx] = c;
        }

        public void Outline(int x, int y, int w, int h, Color32 c)
        {
            Rect(x, y, w, 1, c);
            Rect(x, y + h - 1, w, 1, c);
            Rect(x, y, 1, h, c);
            Rect(x + w - 1, y, 1, h, c);
        }

        /// <summary>Multiplies a region (dims what is behind an overlay panel).</summary>
        public void Dim(int x, int y, int w, int h, float k)
        {
            for (int yy = Mathf.Max(0, y); yy < Mathf.Min(H, y + h); yy++)
            for (int xx = Mathf.Max(0, x); xx < Mathf.Min(W, x + w); xx++)
            {
                var c = Px[yy * W + xx];
                Px[yy * W + xx] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
            }
        }

        /// <summary>
        /// 7x7 sprite, rows top first; each char picks a colour from <paramref name="pal"/> ('.' = transparent,
        /// '0'..'9' = palette index).
        /// </summary>
        public void Sprite(string[] rows, int x, int y, Color32[] pal)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                for (int col = 0; col < row.Length; col++)
                {
                    char ch = row[col];
                    if (ch == '.') continue;
                    int i = ch - '0';
                    if (i >= 0 && i < pal.Length) Set(x + col, y + rows.Length - 1 - r, pal[i]);
                }
            }
        }

        public static int TextWidth(string s, int scale) => CanFont.Width(s, scale);

        public void Text(string s, int x, int y, int scale, Color32 c, bool shadow = true)
        {
            if (shadow)
            {
                var sh = new Color32(0, 0, 0, 255);
                CanFont.Draw(s, x + Mathf.Max(1, scale / 2), y - Mathf.Max(1, scale / 2), scale, scale, (px, py) => Set(px, py, sh));
            }
            CanFont.Draw(s, x, y, scale, scale, (px, py) => Set(px, py, c));
        }

        /// <summary>Text centred on <paramref name="cx"/>.</summary>
        public void TextC(string s, int cx, int y, int scale, Color32 c, bool shadow = true) => Text(s, cx - TextWidth(s, scale) / 2, y, scale, c, shadow);

        /// <summary>Right-aligned text ending at <paramref name="rx"/>.</summary>
        public void TextR(string s, int rx, int y, int scale, Color32 c, bool shadow = true) => Text(s, rx - TextWidth(s, scale), y, scale, c, shadow);

        public void Scanlines(float k)
        {
            for (int y = 1; y < H; y += 2)
            for (int x = 0; x < W; x++)
            {
                var c = Px[y * W + x];
                Px[y * W + x] = new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
            }
        }

        public static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));
    }
}
