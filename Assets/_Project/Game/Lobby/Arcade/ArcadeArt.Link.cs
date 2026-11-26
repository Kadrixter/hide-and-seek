using HideAndSeek.Game.DotLink;
using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Art of the KABELKI cabinet (the dot link puzzle, <see cref="DotLinkArcade"/>): a dark terminal-green marquee with
    /// the title between two joined dots, sides with thick coloured lines winding between their dots, the shared
    /// control panel overlay.
    /// </summary>
    public static partial class ArcadeArt
    {
        private static readonly Color32 TermDark = new(10, 18, 14, 255), TermLine = new(40, 70, 52, 255), Bone = new(214, 222, 200, 255);

        public static void ApplyLink(Transform cabinet)
        {
            Set(cabinet, "Marquee", LinkMarquee(), true);
            var side = LinkSide();
            Set(cabinet, "SideL", side, false);
            Set(cabinet, "SideR", side, false);
            Set(cabinet, "PanelArt", Panel(), false);
        }

        private static Color32 Pal(int i) => DotLinkScreen.Palette[i % DotLinkScreen.Palette.Length];

        private static Texture2D LinkMarquee()
        {
            var c = new SnakeCanvas(128, 40);
            c.Clear(TermDark);
            for (int y = 0; y < c.H; y += 4) c.Rect(0, y, c.W, 1, new Color32(14, 24, 18, 255));
            c.Outline(1, 1, c.W - 2, c.H - 2, TermLine);
            c.Outline(3, 3, c.W - 6, c.H - 6, Pal(5));
            const string title = "KABELKI";
            int x0 = (c.W - SnakeCanvas.TextWidth(title, 3)) / 2, y0 = 16;
            c.Text(title, x0 + 1, y0 + 1, 3, new Color32(0, 0, 0, 255), false);
            c.Text(title, x0, y0, 3, Bone, false);
            // A red line under the title joining two dots (canvas y is turned over with the picture below: low y = bottom).
            c.Rect(14, 6, 100, 3, Pal(0));
            c.Rect(10, 4, 7, 7, Pal(0));
            c.Rect(111, 4, 7, 7, Pal(0));
            // The cube's front face maps the texture upside down: turn the picture by 180 degrees.
            System.Array.Reverse(c.Px);
            return ToTex(c, "ArcLinkMarquee");
        }

        private static Texture2D LinkSide()
        {
            var c = new SnakeCanvas(64, 160);
            c.Clear(TermDark);
            for (int x = 6; x < c.W; x += 10) c.Rect(x, 0, 1, c.H, new Color32(20, 34, 26, 255));
            for (int y = 4; y < c.H; y += 10) c.Rect(0, y, c.W, 1, new Color32(20, 34, 26, 255));
            c.Rect(0, 0, 3, c.H, Pal(5)); c.Rect(c.W - 3, 0, 3, c.H, Pal(5));
            // Four lines, each an L / zig-zag of 4 px strokes between two square dots.
            var lines = new[]
            {
                (0, new[] { (12, 12), (12, 60), (40, 60) }),
                (1, new[] { (52, 14), (52, 40), (26, 40) }),
                (3, new[] { (14, 84), (48, 84), (48, 128) }),
                (2, new[] { (24, 148), (24, 104), (36, 104) }),
            };
            foreach (var (col, pts) in lines)
            {
                var k = Pal(col);
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    var (ax, ay) = pts[i];
                    var (bx, by) = pts[i + 1];
                    int x0 = Mathf.Min(ax, bx), y0 = Mathf.Min(ay, by);
                    c.Rect(x0 - 2, y0 - 2, Mathf.Abs(bx - ax) + 4, Mathf.Abs(by - ay) + 4, k);
                }
                foreach (var (dx, dy) in new[] { pts[0], pts[pts.Length - 1] })
                {
                    c.Rect(dx - 5, dy - 5, 11, 11, k);
                    c.Outline(dx - 5, dy - 5, 11, 11, new Color32(0, 0, 0, 255));
                }
            }
            return ToTex(c, "ArcLinkSide");
        }
    }
}
