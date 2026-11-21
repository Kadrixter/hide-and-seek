using System.Collections.Generic;

namespace HideAndSeek.Characters
{
    /// <summary>
    /// Tiny 5x7 bitmap font for printing on procedural textures (soda can labels, lids).
    /// Each glyph is 7 rows of 5 bits, top row first.
    /// </summary>
    public static class CanFont
    {
        public const int W = 5, H = 7;

        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "01110 10001 10001 11111 10001 10001 10001", ['B'] = "11110 10001 10001 11110 10001 10001 11110",
            ['C'] = "01110 10001 10000 10000 10000 10001 01110", ['D'] = "11100 10010 10001 10001 10001 10010 11100",
            ['E'] = "11111 10000 10000 11110 10000 10000 11111", ['F'] = "11111 10000 10000 11110 10000 10000 10000",
            ['G'] = "01110 10001 10000 10111 10001 10001 01111", ['H'] = "10001 10001 10001 11111 10001 10001 10001",
            ['I'] = "01110 00100 00100 00100 00100 00100 01110", ['J'] = "00111 00010 00010 00010 00010 10010 01100",
            ['K'] = "10001 10010 10100 11000 10100 10010 10001", ['L'] = "10000 10000 10000 10000 10000 10000 11111",
            ['M'] = "10001 11011 10101 10101 10001 10001 10001", ['N'] = "10001 10001 11001 10101 10011 10001 10001",
            ['O'] = "01110 10001 10001 10001 10001 10001 01110", ['P'] = "11110 10001 10001 11110 10000 10000 10000",
            ['Q'] = "01110 10001 10001 10001 10101 10010 01101", ['R'] = "11110 10001 10001 11110 10100 10010 10001",
            ['S'] = "01111 10000 10000 01110 00001 00001 11110", ['T'] = "11111 00100 00100 00100 00100 00100 00100",
            ['U'] = "10001 10001 10001 10001 10001 10001 01110", ['V'] = "10001 10001 10001 10001 10001 01010 00100",
            ['W'] = "10001 10001 10001 10101 10101 10101 01010", ['X'] = "10001 10001 01010 00100 01010 10001 10001",
            ['Y'] = "10001 10001 01010 00100 00100 00100 00100", ['Z'] = "11111 00001 00010 00100 01000 10000 11111",
            ['0'] = "01110 10001 10011 10101 11001 10001 01110", ['1'] = "00100 01100 00100 00100 00100 00100 01110",
            ['2'] = "01110 10001 00001 00010 00100 01000 11111", ['3'] = "11110 00001 00001 01110 00001 00001 11110",
            ['4'] = "00010 00110 01010 10010 11111 00010 00010", ['5'] = "11111 10000 11110 00001 00001 10001 01110",
            ['6'] = "00110 01000 10000 11110 10001 10001 01110", ['7'] = "11111 00001 00010 00100 01000 01000 01000",
            ['8'] = "01110 10001 10001 01110 10001 10001 01110", ['9'] = "01110 10001 10001 01111 00001 00010 01100",
            ['-'] = "00000 00000 00000 11111 00000 00000 00000", ['.'] = "00000 00000 00000 00000 00000 01100 01100",
            ['!'] = "00100 00100 00100 00100 00100 00000 00100", ['%'] = "11001 11010 00010 00100 01000 01011 10011",
            ['+'] = "00000 00100 00100 11111 00100 00100 00000", ['/'] = "00001 00010 00010 00100 01000 01000 10000",
            [':'] = "00000 01100 01100 00000 01100 01100 00000", ['*'] = "00000 10101 01110 11111 01110 10101 00000",
        };

        /// <summary>True when pixel (x, y) of the glyph is set; y = 0 is the top row (may be -2..8 for Polish marks).</summary>
        public static bool On(char c, int x, int y)
        {
            char u = char.ToUpperInvariant(c);
            if (Marks.TryGetValue(u, out var m))
                foreach (var p in m) if (p.x == x && p.y == y) return true;
            if (y < 0 || y >= H || x < 0 || x >= W) return false;
            if (!Glyphs.TryGetValue(Fold(u), out var g)) return false;
            return g[y * 6 + x] == '1';
        }

        /// <summary>Polish letters: base glyph + diacritic pixels (acute / dot above, ogonek below, stroke).</summary>
        static char Fold(char c) => c switch
        {
            'Ą' => 'A', 'Ć' => 'C', 'Ę' => 'E', 'Ł' => 'L', 'Ń' => 'N', 'Ó' => 'O', 'Ś' => 'S', 'Ź' or 'Ż' => 'Z', _ => c,
        };

        static readonly (int x, int y)[] Acute = { (2, -2), (3, -3) };
        static readonly Dictionary<char, (int x, int y)[]> Marks = new Dictionary<char, (int x, int y)[]>
        {
            ['Ą'] = new[] { (4, 7), (3, 8), (4, 8) }, ['Ę'] = new[] { (4, 7), (3, 8), (4, 8) },
            ['Ć'] = Acute, ['Ń'] = Acute, ['Ó'] = Acute, ['Ś'] = Acute, ['Ź'] = Acute,
            ['Ż'] = new[] { (2, -2) }, ['Ł'] = new[] { (1, 3), (2, 2) },
        };

        /// <summary>Rows a glyph may use above (negative) and below the 7-row cell.</summary>
        public const int MarkTop = -3, MarkBottom = 8;

        /// <summary>Width in pixels of <paramref name="text"/> at <paramref name="scale"/> (1 px gap between glyphs).</summary>
        public static int Width(string text, int scale) => text.Length == 0 ? 0 : (text.Length * (W + 1) - 1) * scale;

        /// <summary>
        /// Calls <paramref name="plot"/> for every set pixel of <paramref name="text"/>; (x, y) is the bottom-left corner,
        /// y grows upward (texture space). <paramref name="sx"/> / <paramref name="sy"/> = pixel size.
        /// </summary>
        public static void Draw(string text, int x, int y, int sx, int sy, System.Action<int, int> plot)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int ox = x + i * (W + 1) * sx;
                for (int gy = MarkTop; gy <= MarkBottom; gy++)
                for (int gx = 0; gx < W; gx++)
                {
                    if (!On(c, gx, gy)) continue;
                    int px = ox + gx * sx, py = y + (H - 1 - gy) * sy;
                    for (int a = 0; a < sx; a++)
                    for (int b = 0; b < sy; b++) plot(px + a, py + b);
                }
            }
        }
    }
}
