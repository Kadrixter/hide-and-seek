using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>
    /// Pixel art printed on the SNAKE cabinet, drawn once at runtime (point filtered): the backlit marquee with the
    /// title, the side panels (a big snake winding up between apples and stars) and the control panel overlay
    /// (joystick ring with arrows, button rings, 1P / 2P START stickers, stripes).
    /// </summary>
    public static partial class ArcadeArt
    {
        private static readonly Color32 Green = new(110, 255, 120, 255), DarkGreen = new(20, 110, 50, 255), Yellow = new(255, 222, 70, 255);
        private static readonly Color32 Magenta = new(235, 60, 170, 255), Cyan = new(60, 200, 255, 255), Red = new(232, 40, 64, 255);

        public static void Apply(Transform cabinet)
        {
            Set(cabinet, "Marquee", Marquee(), true);
            var side = Side();
            Set(cabinet, "SideL", side, false);
            Set(cabinet, "SideR", side, false);
            Set(cabinet, "PanelArt", Panel(), false);
        }

        private static void Set(Transform cabinet, string name, Texture2D tex, bool glow)
        {
            var t = cabinet.Find(name);
            var r = t != null ? t.GetComponent<Renderer>() : null;
            if (r == null) return;
            var m = r.material;
            m.mainTexture = tex;
            m.color = Color.white;
            if (glow && m.HasProperty("_EmissionMap"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", Color.white * 1.25f);
            }
        }

        private static Texture2D ToTex(SnakeCanvas c, string name)
        {
            var t = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            t.SetPixels32(c.Px);
            t.Apply(false, true);
            return t;
        }

        // ================================================================ marquee

        private static Texture2D Marquee()
        {
            var c = new SnakeCanvas(128, 40);
            for (int y = 0; y < c.H; y++)
                c.Rect(0, y, c.W, 1, SnakeCanvas.Lerp(new Color32(10, 6, 40, 255), new Color32(40, 10, 70, 255), y / (float)c.H));
            var rnd = new System.Random(3);
            for (int i = 0; i < 40; i++) c.Set(rnd.Next(c.W), rnd.Next(c.H), new Color32(200, 210, 255, 255));
            // neon border
            c.Outline(1, 1, c.W - 2, c.H - 2, Magenta);
            c.Outline(3, 3, c.W - 6, c.H - 6, Cyan);
            // title with a dark outline and a yellow top highlight
            const string title = "SNAKE";
            int x0 = (c.W - SnakeCanvas.TextWidth(title, 4)) / 2, y0 = 6;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                c.Text(title, x0 + dx, y0 + dy, 4, DarkGreen, false);
            c.Text(title, x0, y0, 4, Green, false);
            for (int x = 0; x < c.W; x++)
            for (int y = y0 + 20; y < y0 + 28; y++)
            {
                var p = c.Px[y * c.W + x];
                if (p.g == Green.g && p.r == Green.r) c.Set(x, y, Yellow);
            }
            // The cube's front face maps the texture upside down: turn the picture by 180 degrees.
            System.Array.Reverse(c.Px);
            return ToTex(c, "ArcadeMarquee");
        }

        // ================================================================ sides

        private static Texture2D Side()
        {
            var c = new SnakeCanvas(64, 160);
            c.Clear(new Color32(26, 14, 44, 255));
            for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
                if (((x + y) / 6) % 9 == 0) c.Set(x, y, new Color32(36, 20, 60, 255));
            // neon stripes on both long edges
            c.Rect(0, 0, 3, c.H, Magenta); c.Rect(4, 0, 2, c.H, Cyan);
            c.Rect(c.W - 3, 0, 3, c.H, Magenta); c.Rect(c.W - 6, 0, 2, c.H, Cyan);
            var rnd = new System.Random(11);
            for (int i = 0; i < 50; i++) c.Set(8 + rnd.Next(c.W - 16), rnd.Next(c.H), new Color32(180, 190, 255, 255));
            // apples
            foreach (var (ax, ay) in new[] { (16, 30), (46, 70), (18, 112) })
            {
                c.Rect(ax - 2, ay - 2, 5, 5, Red);
                c.Set(ax - 1, ay + 1, new Color32(255, 200, 200, 255));
                c.Set(ax, ay + 3, new Color32(90, 225, 80, 255));
            }
            // big snake winding upwards, head with eyes near the top
            int prevX = -1;
            for (int y = 6; y < 140; y++)
            {
                int cx = 32 + Mathf.RoundToInt(13f * Mathf.Sin(y * 0.075f));
                var body = ((y / 5) & 1) == 0 ? new Color32(92, 232, 92, 255) : new Color32(60, 190, 90, 255);
                c.Rect(cx - 4, y, 9, 1, body);
                c.Set(cx - 5, y, DarkGreen); c.Set(cx + 5, y, DarkGreen);
                if (y % 10 == 0) c.Rect(cx - 1, y, 3, 2, DarkGreen);
                prevX = cx;
            }
            int hx = prevX;
            c.Rect(hx - 6, 140, 13, 11, new Color32(176, 255, 120, 255));
            c.Outline(hx - 7, 139, 15, 13, DarkGreen);
            c.Rect(hx - 4, 146, 3, 3, Color.white); c.Rect(hx + 2, 146, 3, 3, Color.white);
            c.Set(hx - 3, 147, Color.black); c.Set(hx + 3, 147, Color.black);
            c.Rect(hx, 152, 1, 4, Red); c.Set(hx - 1, 156, Red); c.Set(hx + 1, 156, Red);
            return ToTex(c, "ArcadeSide");
        }

        // ================================================================ control panel overlay

        /// <summary>Panel art: 132 x 56 px over 0.66 x 0.28 m (5 mm per pixel), v grows towards the screen.</summary>
        private static Texture2D Panel()
        {
            var c = new SnakeCanvas(132, 56);
            c.Clear(new Color32(22, 22, 30, 255));
            // diagonal stripes on the right
            for (int y = 0; y < c.H; y++)
            for (int x = 100; x < c.W; x++)
            {
                int k = (x + y) % 14;
                if (k < 2) c.Set(x, y, Magenta);
                else if (k < 4) c.Set(x, y, Cyan);
            }
            c.Outline(0, 0, c.W, c.H, new Color32(70, 70, 90, 255));
            Vector2Int P(float x, float z) => new(Mathf.RoundToInt((x + 0.33f) * 200f), Mathf.RoundToInt((z + 0.14f) * 200f));
            // joystick ring with direction arrows
            var s = P(-0.17f, -0.01f);
            Ring(c, s.x, s.y, 15, Yellow);
            Ring(c, s.x, s.y, 13, new Color32(120, 100, 30, 255));
            foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
            {
                int ax = s.x + dx * 19, ay = s.y + dy * 19;
                c.Set(ax, ay, Yellow);
                c.Set(ax - dy, ay - dx, Yellow); c.Set(ax + dy, ay + dx, Yellow);
                c.Set(ax - dx, ay - dy, Yellow);
            }
            // rings under the buttons
            var cols = new[] { Red, new Color32(50, 128, 255, 255), Yellow, new Color32(50, 230, 80, 255) };
            var spots = new[] { (0.03f, -0.045f), (0.10f, -0.035f), (0.05f, 0.035f), (0.12f, 0.045f) };
            for (int i = 0; i < 4; i++)
            {
                var b = P(spots[i].Item1, spots[i].Item2);
                Ring(c, b.x, b.y, 7, cols[i]);
            }
            var s1 = P(0.235f, 0.085f);
            var s2 = P(0.285f, 0.085f);
            c.TextC("1P", s1.x, s1.y - 13, 1, Color.white, false);
            c.TextC("2P", s2.x, s2.y - 13, 1, Color.white, false);
            c.TextC("START", (s1.x + s2.x) / 2, s1.y + 9, 1, new Color32(255, 170, 60, 255), false);
            c.Text("SNAKE", 4, 4, 1, Green, false);
            return ToTex(c, "ArcadePanelArt");
        }

        private static void Ring(SnakeCanvas c, int cx, int cy, int r, Color32 col)
        {
            for (int a = 0; a < 96; a++)
            {
                float t = a / 96f * Mathf.PI * 2f;
                c.Set(cx + Mathf.RoundToInt(Mathf.Cos(t) * r), cy + Mathf.RoundToInt(Mathf.Sin(t) * r), col);
            }
        }
    }
}
