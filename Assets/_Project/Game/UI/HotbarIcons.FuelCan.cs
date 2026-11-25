using UnityEngine;

namespace HideAndSeek.Game
{
    /// <summary>Pixel icon of the chainsaw fuel canister: a red jerry can with the embossed X, the top handle and a dark spout.</summary>
    public static partial class HotbarIcons
    {
        private static Sprite _fuelCan;

        public static Sprite FuelCan()
        {
            if (_fuelCan != null) return _fuelCan;
            var px = new Color32[N * N];
            var red = C(0.82f, 0.12f, 0.08f);
            var redLight = C(0.95f, 0.32f, 0.24f);
            var redDark = C(0.52f, 0.05f, 0.04f);
            var spout = C(0.16f, 0.16f, 0.18f);
            var spoutLight = C(0.42f, 0.43f, 0.46f);
            Rect(px, 6, 3, 25, 23, red);                 // body
            Rect(px, 7, 4, 8, 22, redLight);             // lit left edge
            Rect(px, 23, 3, 25, 23, redDark);            // shaded right edge
            Rect(px, 6, 3, 25, 3, redDark);
            Line(px, 9f, 6f, 22f, 20f, 0.9f, redDark);   // embossed X
            Line(px, 9f, 20f, 22f, 6f, 0.9f, redDark);
            Rect(px, 9, 24, 10, 26, red);                // three handle posts and the grip bar
            Rect(px, 14, 24, 15, 26, red);
            Rect(px, 19, 24, 20, 26, red);
            Rect(px, 8, 27, 21, 28, redLight);
            Line(px, 23f, 24f, 27f, 29f, 1.6f, spout);   // spout with its cap
            Rect(px, 26, 28, 28, 30, spoutLight);
            Outline(px);
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            _fuelCan = Sprite.Create(tex, new UnityEngine.Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 32f);
            return _fuelCan;
        }
    }
}
