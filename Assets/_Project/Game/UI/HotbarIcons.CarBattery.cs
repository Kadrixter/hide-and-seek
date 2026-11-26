using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of the car battery: a black box with a grey lid, the red + and black - terminals on top.</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _carBattery;

        public static Sprite CarBattery()
        {
            if (_carBattery != null) return _carBattery;
            var px = new Color32[N * N];
            var body = C(0.16f, 0.16f, 0.18f);
            var bodyLight = C(0.3f, 0.31f, 0.34f);
            var bodyDark = C(0.08f, 0.08f, 0.09f);
            var lid = C(0.42f, 0.43f, 0.46f);
            var red = C(0.85f, 0.14f, 0.1f);
            var post = C(0.75f, 0.76f, 0.78f);
            var label = C(0.95f, 0.75f, 0.15f);
            Rect(px, 4, 4, 27, 20, body);              // case
            Rect(px, 5, 5, 6, 19, bodyLight);          // lit left edge
            Rect(px, 25, 4, 27, 20, bodyDark);         // shaded right edge
            Rect(px, 3, 21, 28, 23, lid);              // lid
            Rect(px, 7, 24, 10, 26, red);              // + terminal (red cap)
            Rect(px, 8, 27, 9, 27, post);
            Rect(px, 21, 24, 24, 26, bodyDark);        // - terminal
            Rect(px, 22, 27, 23, 27, post);
            Rect(px, 9, 10, 22, 14, label);            // yellow label with a black stripe
            Rect(px, 9, 12, 22, 12, bodyDark);
            Rect(px, 11, 17, 13, 17, red);             // "+" mark on the case
            Rect(px, 12, 16, 12, 18, red);
            Rect(px, 18, 17, 20, 17, C(0.85f, 0.85f, 0.85f)); // "-" mark
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            _carBattery = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            return _carBattery;
        }
    }
}
