using HideAndSeek.Characters;
using UnityEngine;

namespace HideAndSeek.Game.MiniGames
{
    /// <summary>
    /// The chess clock's backlit LCD (256 x 64): seat B's time on the left half, seat A's on the right half (as seen
    /// from the board), a small arrow over the running clock, "--:--" without a time control.
    /// </summary>
    public static class ChessClockFace
    {
        static readonly Color Back = new(0.78f, 0.86f, 0.62f), Ink = new(0.08f, 0.1f, 0.07f), Ghost = new(0.7f, 0.78f, 0.56f);
        static Color32[] _px;

        public static string Format(int ms)
        {
            int s = Mathf.CeilToInt(ms / 1000f);
            if (ms < 10000 && ms > 0) return $"{ms / 1000}.{ms / 100 % 10}";
            return $"{s / 60}:{s % 60:00}";
        }

        /// <summary>Draws both clocks. <paramref name="aRunning"/> = whose clock runs (ignored when nothing runs).</summary>
        public static void Draw(Texture2D tex, int msA, int msB, bool aRunning, bool running, bool clockOn = true)
        {
            if (tex == null) return;
            int w = tex.width, h = tex.height;
            _px ??= new Color32[w * h];
            Color32 back = Back;
            for (int i = 0; i < _px.Length; i++) _px[i] = back;
            // Dark frame line round each window.
            for (int x = 0; x < w; x++) { Set(x, 0, Ink, w, h); Set(x, h - 1, Ink, w, h); }
            for (int y = 0; y < h; y++) { Set(0, y, Ink, w, h); Set(w - 1, y, Ink, w, h); Set(w / 2, y, Ink, w, h); Set(w / 2 - 1, y, Ink, w, h); }
            Text(clockOn ? Format(msB) : "--:--", w / 4, h / 2 - 3, w, h);
            Text(clockOn ? Format(msA) : "--:--", w * 3 / 4, h / 2 - 3, w, h);
            if (running && clockOn)
            {
                int cx = aRunning ? w * 3 / 4 : w / 4;
                for (int y = 0; y < 6; y++)
                    for (int x = -y; x <= y; x++) Set(cx + x, h - 4 - (5 - y), Ink, w, h);
            }
            tex.SetPixels32(_px);
            tex.Apply(false);
        }

        static void Set(int x, int y, Color c, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            _px[y * w + x] = c;
        }

        static void Text(string s, int cx, int cy, int w, int h)
        {
            const int sc = 4;
            int width = CanFont.Width(s, sc);
            int x0 = cx - width / 2, y0 = cy - CanFont.H * sc / 2;
            // Faint "88:88" segments behind, like a real LCD.
            CanFont.Draw(s.Replace('0', '8').Replace('1', '8').Replace('2', '8').Replace('3', '8').Replace('4', '8')
                .Replace('5', '8').Replace('6', '8').Replace('7', '8').Replace('9', '8'), x0, y0, sc, sc, (x, y) => Plot(x, y, Ghost, w, h));
            CanFont.Draw(s, x0, y0, sc, sc, (x, y) => Plot(x, y, Ink, w, h));
        }

        static void Plot(int x, int y, Color c, int w, int h) => Set(x, y, c, w, h);
    }
}
