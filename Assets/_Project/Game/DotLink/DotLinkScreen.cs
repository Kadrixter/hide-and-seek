using UnityEngine;

namespace HideAndSeek.Game.DotLink
{
    /// <summary>
    /// Paints the dot link screen into a texture (CPU, only when something changes): an old service terminal look -
    /// near-black green-grey background with faint scan lines, thin dim grid, matte colours (no glow). Filled cells get
    /// a faint wash of their colour, lines are flat strokes with round joints, dots are flat discs with a darker rim;
    /// a joined pair gets a thin ring around its dots. <c>lit</c> = the solved flash (brighter lines and washes).
    /// </summary>
    public sealed class DotLinkScreen
    {
        public const int Size = 512, Off = 26, Cell = 46;
        public readonly Texture2D Tex;
        readonly Color32[] _px = new Color32[Size * Size];
        static Color32[] _bg;

        /// <summary>Matte, easy to tell apart: red, blue, green, mustard, orange, teal, pink, violet, brown, bone, lime, sky.</summary>
        public static readonly Color32[] Palette =
        {
            new Color32(196, 70, 58, 255), new Color32(62, 108, 190, 255), new Color32(76, 152, 72, 255), new Color32(214, 182, 62, 255),
            new Color32(216, 122, 46, 255), new Color32(48, 158, 150, 255), new Color32(206, 102, 162, 255), new Color32(130, 92, 186, 255),
            new Color32(130, 84, 50, 255), new Color32(204, 200, 184, 255), new Color32(150, 186, 58, 255), new Color32(122, 178, 224, 255),
        };

        static readonly Color32 GridLine = new Color32(36, 42, 38, 255), Border = new Color32(58, 66, 60, 255), Hover = new Color32(110, 122, 112, 255);

        public DotLinkScreen()
        {
            Tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "DotLinkScreen", filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            DrawOff();
        }

        static Color32[] Background
        {
            get
            {
                if (_bg != null) return _bg;
                _bg = new Color32[Size * Size];
                for (int y = 0; y < Size; y++)
                {
                    float scan = y % 4 == 0 ? 0.8f : 1f;
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = (x - Size * 0.5f) / Size, dy = (y - Size * 0.5f) / Size;
                        float k = scan * (1f - 0.55f * (dx * dx + dy * dy));
                        _bg[y * Size + x] = new Color32((byte)(13 * k), (byte)(17 * k), (byte)(15 * k), 255);
                    }
                }
                return _bg;
            }
        }

        /// <summary>Panel dead / starting: background only, with a block cursor when <paramref name="cursor"/>.</summary>
        public void DrawOff(bool cursor = false)
        {
            System.Array.Copy(Background, _px, _px.Length);
            if (cursor) Rect(Off + 6, Size - Off - 30, Off + 22, Size - Off - 8, new Color32(70, 110, 80, 255), 1f);
            Apply();
        }

        static int Cx(int cell, int w) => Off + (cell % w) * Cell + Cell / 2;
        static int Cy(int cell, int w) => Size - 1 - (Off + (cell / w) * Cell + Cell / 2);

        public void Draw(DotLinkPuzzle p, DotLinkBoard b, int hover, bool lit)
        {
            System.Array.Copy(Background, _px, _px.Length);
            int w = p.W, gw = p.W * Cell, gh = p.H * Cell;
            int top = Size - 1 - Off;
            // Washes of the filled cells.
            for (int c = 0; c < p.Colors; c++)
            {
                var path = b.Path(c);
                bool done = b.Complete(c);
                float wash = lit ? 0.34f : done ? 0.22f : 0.14f;
                var col = Palette[c % Palette.Length];
                for (int i = 0; i < path.Count; i++) WashCell(path[i], w, col, wash);
                if (path.Count == 0) { WashCell(p.A[c], w, col, 0.08f); WashCell(p.B[c], w, col, 0.08f); }
            }
            // Grid.
            for (int i = 0; i <= p.W; i++) Rect(Off + i * Cell, top - gh, Off + i * Cell + 1, top + 1, GridLine, 1f);
            for (int j = 0; j <= p.H; j++) Rect(Off, top - j * Cell - 1, Off + gw + 1, top - j * Cell, GridLine, 1f);
            Frame(Off - 3, top - gh - 3, Off + gw + 3, top + 3, Border);
            if (hover >= 0 && hover < p.Cells) Frame(Off + (hover % w) * Cell + 2, top - (hover / w + 1) * Cell + 2, Off + (hover % w + 1) * Cell - 2, top - (hover / w) * Cell - 2, Hover);
            // Lines.
            for (int c = 0; c < p.Colors; c++)
            {
                var path = b.Path(c);
                bool done = b.Complete(c);
                var col = Shade(Palette[c % Palette.Length], lit ? 1.12f : done ? 1f : 0.86f);
                const int half = 7;
                for (int i = 0; i < path.Count; i++)
                {
                    int x0 = Cx(path[i], w), y0 = Cy(path[i], w);
                    Disc(x0, y0, half + 0.5f, col);
                    if (i == 0) continue;
                    int x1 = Cx(path[i - 1], w), y1 = Cy(path[i - 1], w);
                    Rect(Mathf.Min(x0, x1) - half, Mathf.Min(y0, y1) - half, Mathf.Max(x0, x1) + half + 1, Mathf.Max(y0, y1) + half + 1, col, 1f);
                }
            }
            // Dots on top.
            for (int c = 0; c < p.Colors; c++)
            {
                var col = Palette[c % Palette.Length];
                bool done = b.Complete(c);
                foreach (int cell in new[] { p.A[c], p.B[c] })
                {
                    int x = Cx(cell, w), y = Cy(cell, w);
                    if (done || lit) Ring(x, y, 19.5f, 1.6f, Shade(col, lit ? 1.15f : 0.95f));
                    Disc(x, y, 15.5f, Shade(col, 0.55f));
                    Disc(x, y, 13.5f, lit ? Shade(col, 1.1f) : col);
                }
            }
            Apply();
        }

        void Apply()
        {
            Tex.SetPixels32(_px);
            Tex.Apply(true);
        }

        static Color32 Shade(Color32 c, float k) =>
            new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), 255);

        void WashCell(int cell, int w, Color32 col, float a)
        {
            int x0 = Off + (cell % w) * Cell + 1, y1 = Size - 1 - Off - (cell / w) * Cell;
            Rect(x0, y1 - Cell + 1, x0 + Cell - 1, y1, col, a);
        }

        /// <summary>Blends <paramref name="c"/> over [x0, x1) x [y0, y1) with opacity <paramref name="a"/>.</summary>
        void Rect(int x0, int y0, int x1, int y1, Color32 c, float a)
        {
            x0 = Mathf.Max(0, x0); y0 = Mathf.Max(0, y0); x1 = Mathf.Min(Size, x1); y1 = Mathf.Min(Size, y1);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) Blend(x, y, c, a);
        }

        void Frame(int x0, int y0, int x1, int y1, Color32 c)
        {
            Rect(x0, y0, x1, y0 + 2, c, 1f);
            Rect(x0, y1 - 2, x1, y1, c, 1f);
            Rect(x0, y0, x0 + 2, y1, c, 1f);
            Rect(x1 - 2, y0, x1, y1, c, 1f);
        }

        void Disc(int cx, int cy, float r, Color32 c)
        {
            int ri = Mathf.CeilToInt(r) + 1;
            for (int y = cy - ri; y <= cy + ri; y++)
                for (int x = cx - ri; x <= cx + ri; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0f) Blend(x, y, c, a);
                }
        }

        void Ring(int cx, int cy, float r, float width, Color32 c)
        {
            int ri = Mathf.CeilToInt(r + width) + 1;
            for (int y = cy - ri; y <= cy + ri; y++)
                for (int x = cx - ri; x <= cx + ri; x++)
                {
                    float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);
                    float a = Mathf.Clamp01(width * 0.5f - d + 0.5f);
                    if (a > 0f) Blend(x, y, c, a);
                }
        }

        void Blend(int x, int y, Color32 c, float a)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            int i = y * Size + x;
            var o = _px[i];
            _px[i] = new Color32((byte)(o.r + (c.r - o.r) * a), (byte)(o.g + (c.g - o.g) * a), (byte)(o.b + (c.b - o.b) * a), 255);
        }
    }
}
