using System.Collections.Generic;
using UnityEngine;

namespace HideAndSeek.Map.Editor
{
    /// <summary>
    /// Tiny hand-made 5x7 pixel font rendered into a glyph atlas (16 columns x 8 rows of 32x32 cells,
    /// ASCII 32..127). Special glyphs: '^' up arrow, '|' down arrow, '<' left arrow, '>' right arrow.
    /// </summary>
    internal static class PixelFont
    {
        public const int AtlasWidth = 512;
        public const int AtlasHeight = 256;
        public const int Cell = 32;
        public const int Columns = AtlasWidth / Cell;
        private const int Scale = 4;
        private const int GlyphOffsetX = 6;
        private const int GlyphOffsetY = 2;

        /// <summary>Width/height ratio of one character quad.</summary>
        public const float Aspect = 24f / 32f;

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['0'] = new[] { " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### " },
            ['1'] = new[] { "  #  ", " ##  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            ['2'] = new[] { " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####" },
            ['3'] = new[] { "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### " },
            ['4'] = new[] { "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # " },
            ['5'] = new[] { "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### " },
            ['6'] = new[] { "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### " },
            ['7'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   " },
            ['8'] = new[] { " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### " },
            ['9'] = new[] { " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  " },
            ['A'] = new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['B'] = new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " },
            ['C'] = new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " },
            ['D'] = new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " },
            ['E'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" },
            ['F'] = new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " },
            ['G'] = new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####" },
            ['H'] = new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" },
            ['I'] = new[] { " ### ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", " ### " },
            ['J'] = new[] { "  ###", "   # ", "   # ", "   # ", "   # ", "#  # ", " ##  " },
            ['K'] = new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" },
            ['L'] = new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" },
            ['M'] = new[] { "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #" },
            ['N'] = new[] { "#   #", "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #" },
            ['O'] = new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['P'] = new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " },
            ['Q'] = new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #" },
            ['R'] = new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" },
            ['S'] = new[] { " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### " },
            ['T'] = new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['U'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " },
            ['V'] = new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " },
            ['W'] = new[] { "#   #", "#   #", "#   #", "# # #", "# # #", "# # #", " # # " },
            ['X'] = new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" },
            ['Y'] = new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " },
            ['Z'] = new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" },
            ['-'] = new[] { "     ", "     ", "     ", "#####", "     ", "     ", "     " },
            ['.'] = new[] { "     ", "     ", "     ", "     ", "     ", " ##  ", " ##  " },
            ['/'] = new[] { "    #", "    #", "   # ", "  #  ", " #   ", "#    ", "#    " },
            [':'] = new[] { "     ", " ##  ", " ##  ", "     ", " ##  ", " ##  ", "     " },
            ['!'] = new[] { "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  " },
            ['+'] = new[] { "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     " },
            ['^'] = new[] { "  #  ", " ### ", "# # #", "  #  ", "  #  ", "  #  ", "  #  " },
            ['|'] = new[] { "  #  ", "  #  ", "  #  ", "  #  ", "# # #", " ### ", "  #  " },
            ['<'] = new[] { "     ", "  #  ", " #   ", "#####", " #   ", "  #  ", "     " },
            ['>'] = new[] { "     ", "  #  ", "   # ", "#####", "   # ", "  #  ", "     " },
        };

        /// <summary>Returns coverage (0/1) for every pixel of the atlas, row 0 = bottom.</summary>
        public static float[] RenderAtlas()
        {
            var a = new float[AtlasWidth * AtlasHeight];
            foreach (var kv in Glyphs)
            {
                int idx = kv.Key - 32;
                int cx = (idx % Columns) * Cell;
                int cy = (idx / Columns) * Cell;
                string[] rows = kv.Value;
                for (int r = 0; r < 7; r++)
                for (int c = 0; c < 5; c++)
                {
                    if (rows[r][c] == ' ') continue;
                    int px0 = cx + GlyphOffsetX + c * Scale;
                    int py0 = cy + GlyphOffsetY + (6 - r) * Scale;
                    for (int y = 0; y < Scale; y++)
                    for (int x = 0; x < Scale; x++)
                        a[(py0 + y) * AtlasWidth + px0 + x] = 1f;
                }
            }
            return a;
        }

        public static bool Has(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c));

        /// <summary>UV rectangle (min, max) of the glyph quad for a character.</summary>
        public static void GlyphUV(char c, out Vector2 min, out Vector2 max)
        {
            c = char.ToUpperInvariant(c);
            int idx = c - 32;
            if (idx < 0 || idx >= 96) idx = 0;
            float cx = (idx % Columns) * Cell;
            float cy = (idx / Columns) * Cell;
            min = new Vector2((cx + 4f) / AtlasWidth, (cy + 0.5f) / AtlasHeight);
            max = new Vector2((cx + 28f) / AtlasWidth, (cy + Cell - 0.5f) / AtlasHeight);
        }
    }
}
